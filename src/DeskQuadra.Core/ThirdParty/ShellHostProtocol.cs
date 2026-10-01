using System.Text.Json;

namespace DeskQuadra.Core.ThirdParty;

// T5 isolamento: protocolo UI ↔ DeskQuadra.ShellHost. Definição ÚNICA (as duas
// pontas usam estes DTOs/helpers — nada de struct espelhada em dois projetos).
//
// DECISÃO DE IPC (documentada aqui, fonte da verdade): stdin/stdout em JSON,
// UMA linha por pedido/resposta ("JSON-lines"), processo ONE-SHOT por operação
// (o cliente spawna o host, escreve 1 linha, lê 1 linha, o host sai).
// Por que não pipe nomeado persistente: pipe exige dono do lifetime,
// reconnect, framing e STA afinada nos dois lados — complexidade que o plano
// proíbe ("o mais simples robusto"). One-shot dá de graça o que T5 exige:
//   - "inicia sob demanda": sem daemon, sem pré-spawn, sem timer;
//   - "timeout por operação": WaitForExit(ms) do lado cliente (molde do Join
//     da STA em T2/T3 — espera limitada, sem timer novo na UI);
//   - "kill + relança se travar": Kill() no timeout; a próxima operação spawna
//     um host fresco (sem estado = sem "relançamento" explícito);
//   - "host morto = placeholder silencioso": sem stdout válido → lista vazia.
// Síncrono do ponto de vista do chamador: o cliente bloqueia no máximo
// QueryTimeoutMs (query ~3s, intacto de T2) / InvokeTimeoutMs (invoke ~30s,
// intacto de T3 — o handler pode abrir diálogo modal).
// Deadlock de pipe CHEIO evitado no cliente: ele drena o stdout em background
// (ReadToEndAsync) ANTES do WaitForExit — resposta grande (menu do 7-Zip cheio
// passa de 4KB) nunca trava o host. O host só escreve UMA linha no stdout;
// diagnóstico livre vai p/ stderr (ignorado pelo cliente, visível no manual).
// UTF-8 nas duas pontas (rótulo com acento atravessa intacto).
// Sem dependência nova: System.Text.Json é in-box no .NET 8.
public static class ShellHostProtocol
{
    public const string ExeName = "DeskQuadra.ShellHost.exe";

    public const string QueryOp = "query";
    public const string InvokeOp = "invoke";

    // FIX folhas sem verbo estável (Paint, Fireworks, destinos do SendTo):
    // folhas com GCS_VERBW vazio caíam no fallback por offset, mas handlers
    // dinâmicos reordenam os offsets entre duas QueryContextMenu — o invoke
    // fazia NOVA query onde os offsets mudaram → VALIDATEW reprovava (ou, pior,
    // clicava o vizinho) → false silencioso. Novo pedido "invoke-by-label":
    // (path, extended, lista de rótulos do caminho — ex. ["Abrir com","Paint"],
    // hwnd, ponto). O host, NUMA query só, enumera (com o lazy-populate
    // existente), caminha pelos rótulos e invoca o offset achado NA MESMA
    // interface (sem re-query, sem VALIDATEW — a garantia é a sessão única).
    // RÓTULO = O QUE O USUÁRIO VIU: match exato (Ordinal) após a mesma
    // normalização de exibição usada na listagem (CleanLabelForDisplay — sem
    // '&', sem sufixo '\t', trim; case-sensitive como o Header do WPF).
    // O caminho-verbo (HasStableVerb) continua intacto — este pedido só é
    // usado para folhas SEM verbo estável.
    public const string InvokeByLabelOp = "invoke-by-label";

    // Orçamentos T5 (§ plano: query ~3s / invoke ~30s). Fonte única: o cliente
    // espera isso no processo e o engine usa o mesmo valor na STA interna.
    public const int QueryTimeoutMs = 3000;
    public const int InvokeTimeoutMs = 30000;

    // Faixa válida de offset (id - idCmdFirst) p/ o HMENU fantasma (movido do
    // engine T3 p/ cá: guarda pura, usada pelo cliente ANTES de spawnar e pelo
    // host — sem Vanara, sem COM, testável sem Shell).
    private const uint IdCmdFirst = 1;
    private const uint IdCmdLast = 0x7FFF;

    public static bool IsOffsetInRange(uint commandOffset)
        => commandOffset <= (IdCmdLast - IdCmdFirst);

    // Verbo canônico estável? Não-vazio = caminho-verbo (preferido, imune a
    // reordenação de offsets). Vazio/nulo = fallback por offset (raro).
    // (Movido do engine T3 p/ cá, mesmo motivo do IsOffsetInRange.)
    public static bool HasStableVerb(string? verb)
        => !string.IsNullOrEmpty(verb);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string SerializeQueryRequest(string path, bool extended)
        => JsonSerializer.Serialize(new ShellHostQueryRequest(QueryOp, path, extended), JsonOptions);

    public static string SerializeInvokeRequest(
        string path,
        string verb,
        uint offset,
        bool extended,
        long hwnd,
        int? x,
        int? y)
        => JsonSerializer.Serialize(
            new ShellHostInvokeRequest(InvokeOp, path, verb, offset, extended, hwnd, x, y),
            JsonOptions);

    public static string SerializeInvokeByLabelRequest(
        string path,
        IReadOnlyList<string> labels,
        bool extended,
        long hwnd,
        int? x,
        int? y)
        => JsonSerializer.Serialize(
            new ShellHostInvokeByLabelRequest(
                InvokeByLabelOp,
                path,
                extended,
                labels is List<string> list ? list : new List<string>(labels ?? Array.Empty<string>()),
                hwnd,
                x,
                y),
            JsonOptions);

    // Lê o "op" sem desserializar tudo (o host despacha por ele).
    public static bool TryReadOp(string? line, out string op)
    {
        op = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty("Op", out var opProp))
            {
                return false;
            }

            string? value = opProp.GetString();
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            op = value;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static ShellHostQueryRequest? ParseQueryRequest(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            var req = JsonSerializer.Deserialize<ShellHostQueryRequest>(line, JsonOptions);
            if (req is null || req.Op != QueryOp || string.IsNullOrWhiteSpace(req.Path))
            {
                return null;
            }

            return req;
        }
        catch
        {
            return null;
        }
    }

    public static ShellHostInvokeRequest? ParseInvokeRequest(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            var req = JsonSerializer.Deserialize<ShellHostInvokeRequest>(line, JsonOptions);
            if (req is null || req.Op != InvokeOp || string.IsNullOrWhiteSpace(req.Path))
            {
                return null;
            }

            if (!HasStableVerb(req.Verb) && !IsOffsetInRange(req.Offset))
            {
                return null;
            }

            return req;
        }
        catch
        {
            return null;
        }
    }

    public static ShellHostInvokeByLabelRequest? ParseInvokeByLabelRequest(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            var req = JsonSerializer.Deserialize<ShellHostInvokeByLabelRequest>(line, JsonOptions);
            if (req is null || req.Op != InvokeByLabelOp || string.IsNullOrWhiteSpace(req.Path))
            {
                return null;
            }

            if (req.Labels is null || req.Labels.Count == 0)
            {
                return null;
            }

            foreach (string label in req.Labels)
            {
                if (string.IsNullOrWhiteSpace(label))
                {
                    return null;
                }
            }

            return req;
        }
        catch
        {
            return null;
        }
    }

    public static ShellHostQueryResponse? ParseQueryResponse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ShellHostQueryResponse>(line, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static ShellHostInvokeResponse? ParseInvokeResponse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ShellHostInvokeResponse>(line, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static string SerializeQueryResponse(IReadOnlyList<ThirdPartyMenuEntry> entries, string? error = null)
    {
        var items = new List<ShellHostMenuItem>(entries.Count);
        foreach (var entry in entries)
        {
            items.Add(FromEntry(entry));
        }

        return JsonSerializer.Serialize(new ShellHostQueryResponse(items, error), JsonOptions);
    }

    public static string SerializeInvokeResponse(bool ok, string? error = null)
        => JsonSerializer.Serialize(new ShellHostInvokeResponse(ok, error), JsonOptions);

    // Resposta → entradas da UI (recursivo; tolera null de host velho/quebrado).
    public static IReadOnlyList<ThirdPartyMenuEntry> ToEntries(ShellHostQueryResponse? response)
    {
        var result = new List<ThirdPartyMenuEntry>();
        if (response?.Entries is null)
        {
            return result;
        }

        foreach (var item in response.Entries)
        {
            result.Add(ToEntry(item));
        }

        return result;
    }

    private static ShellHostMenuItem FromEntry(ThirdPartyMenuEntry entry)
    {
        var children = new List<ShellHostMenuItem>(entry.Children.Count);
        foreach (var child in entry.Children)
        {
            children.Add(FromEntry(child));
        }

        return new ShellHostMenuItem(entry.Label, entry.Verb, entry.CommandOffset, children);
    }

    private static ThirdPartyMenuEntry ToEntry(ShellHostMenuItem item)
    {
        var children = new List<ThirdPartyMenuEntry>(item.Children?.Count ?? 0);
        if (item.Children is not null)
        {
            foreach (var child in item.Children)
            {
                children.Add(ToEntry(child));
            }
        }

        return new ThirdPartyMenuEntry(
            item.Label ?? string.Empty,
            item.Verb ?? string.Empty,
            item.Offset,
            children);
    }
}

// Pedidos (1 linha no stdin do host). HWND atravessa como long (handle é
// válido entre processos); ponto do cursor como X/Y anuláveis (sem ponto =
// invoke sem PTINVOKE, best-effort, mesmo molde T3).
public sealed record ShellHostQueryRequest(string Op, string Path, bool Extended);

public sealed record ShellHostInvokeRequest(
    string Op,
    string Path,
    string Verb,
    uint Offset,
    bool Extended,
    long Hwnd,
    int? X,
    int? Y);

// FIX invoke-by-label (folhas sem verbo estável): Labels = rótulos do caminho
// como o usuário VIU (já CleanLabelForDisplay na listagem — ex.
// ["Abrir com","Paint"]). O host limpa de novo os dois lados com
// CleanLabelForDisplay e compara exato (Ordinal) — sem contains, sem idioma.
public sealed record ShellHostInvokeByLabelRequest(
    string Op,
    string Path,
    bool Extended,
    List<string> Labels,
    long Hwnd,
    int? X,
    int? Y);

// Respostas (1 linha no stdout do host). Query SEMPRE devolve Entries
// (vazia em falha — a UI mantém o placeholder T1); Error é diagnóstico p/
// o shell-menu.log, nunca texto visível (sem resx novo em T5).
public sealed record ShellHostMenuItem(string Label, string Verb, uint Offset, List<ShellHostMenuItem> Children);

public sealed record ShellHostQueryResponse(List<ShellHostMenuItem>? Entries, string? Error);

public sealed record ShellHostInvokeResponse(bool Ok, string? Error);
