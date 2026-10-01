using System.Collections.Concurrent;
using System.IO;
using DeskQuadra.Core.ThirdParty;
using DeskQuadra.Infrastructure.WindowsShell.Native;
using Vanara.PInvoke;

namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

// T5 terceiros: MESMA interface de T3, motor trocado — query/invoke agora
// rodam no DeskQuadra.ShellHost (outro processo) via ShellHostClient
// (one-shot stdin/stdout JSON, timeout/kill, host morto = vazio silencioso).
// Mantidos aqui (UI-side, sem COM): cache por extensão (positivo E negativo;
// sem expiração, sem timers) + registro do Shift/EXTENDEDVERBS por caminho +
// guardas de alça. Visual e chamada intactos (zero XAML/resx em T5).
// CMF_EXTENDEDVERBS só com Shift pressionado (GetKeyState no momento da abertura).
// Query + alça de invoke resolvidos JUNTOS: GetForPath registra o Shift usado
// por caminho; CreateHandle captura (path, verb, offset, extended, labelPath)
// e TryInvoke reenvia os MESMOS parâmetros ao host (verbo estável, imune a
// reordenação de offsets, quando há; invoke-by-label NUMA query só quando NÃO
// há verbo estável; VALIDATEW+offset só no fallback legado sem rótulos,
// dentro do host). O caminho-verbo (7-Zip etc.) segue intacto — este fix só
// muda o destino das folhas SEM verbo estável (Paint, Fireworks, SendTo).
// Tudo best-effort e silencioso: qualquer falha devolve lista vazia / false.
public sealed class ThirdPartyMenuService : IThirdPartyMenuService
{
    private static readonly IReadOnlyList<ThirdPartyMenuEntry> Empty =
        Array.Empty<ThirdPartyMenuEntry>();

    private readonly ConcurrentDictionary<string, IReadOnlyList<ThirdPartyMenuEntry>> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    // Shift usado na query, por caminho (o Shift do clique pode já ter sido
    // solto — o invoke precisa das MESMAS flags da listagem). Limitado: é
    // best-effort, o fallback é o Shift atual; limpeza quando estoura.
    private readonly ConcurrentDictionary<string, bool> _extendedByPath =
        new(StringComparer.OrdinalIgnoreCase);
    private const int MaxTrackedPaths = 1024;

    private readonly ShellHostClient _host = new();

    public IReadOnlyList<ThirdPartyMenuEntry> GetForPath(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Empty;
            }

            bool isDirectory = Directory.Exists(path);
            if (!isDirectory && !File.Exists(path))
            {
                return Empty;
            }

            // Shift no momento da abertura: consulta separada (verbos estendidos
            // não poluem o cache normal e vice-versa).
            bool extended = NativeMethods.IsShiftPressed();
            TrackExtendedFlag(path, extended);
            string key = GetCacheKey(path, isDirectory, extended);
            return _cache.GetOrAdd(key, _ => QueryUncached(path, extended));
        }
        catch
        {
            // Silencioso, padrão do projeto.
            return Empty;
        }
    }

    public ThirdPartyInvokeHandle? CreateHandle(string? path, string? verb, uint commandOffset, IReadOnlyList<string>? labelPath = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            // Caminho-verbo não exige offset válido (seleção é pelo verbo
            // estável); offset só é guardado como fallback p/ verbo vazio.
            // Folha sem verbo estável continua DESABILITADA quando não há como
            // invocar: sem rótulos (chamador antigo) exige offset válido como
            // antes; COM rótulos (UI nova) também exige offset válido — o
            // offset aqui é só sinal de "folha real" (o sintético 0xFFFF do
            // complemento-vazio Transmitir segue nulo = desabilitado) e NÃO é
            // usado no invoke-by-label (o host resolve o offset na mesma query).
            string stableVerb = verb ?? string.Empty;
            if (!ShellHostProtocol.HasStableVerb(stableVerb)
                && !ShellHostProtocol.IsOffsetInRange(commandOffset))
            {
                return null;
            }

            IReadOnlyList<string>? keptLabels = null;
            if (!ShellHostProtocol.HasStableVerb(stableVerb) && labelPath is not null && labelPath.Count > 0)
            {
                var cleaned = new List<string>(labelPath.Count);
                foreach (string raw in labelPath)
                {
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        return null;
                    }

                    cleaned.Add(raw);
                }

                keptLabels = cleaned;
            }

            bool extended = _extendedByPath.TryGetValue(path, out bool tracked)
                ? tracked
                : NativeMethods.IsShiftPressed();
            return new ThirdPartyInvokeHandle(path, stableVerb, commandOffset, extended, keptLabels);
        }
        catch
        {
            return null;
        }
    }

    public bool TryInvoke(ThirdPartyInvokeHandle? handle, IntPtr hwnd, POINT? invokePoint)
    {
        try
        {
            if (handle is null || string.IsNullOrWhiteSpace(handle.Path))
            {
                return false;
            }

            // Guarda barata antes de spawnar (mesma do engine T3): fallback por
            // offset exige faixa válida quando não há verbo estável. Com
            // rótulos, o CreateHandle já barrou o sintético 0xFFFF
            // (complemento-vazio = desabilitado); aqui só re-checa o legado
            // sem rótulos.
            if (!ShellHostProtocol.HasStableVerb(handle.Verb)
                && (handle.LabelPath is null || handle.LabelPath.Count == 0)
                && !ShellHostProtocol.IsOffsetInRange(handle.CommandOffset))
            {
                return false;
            }

            // HWND/ponto atravessam o IPC como números (handle é válido entre
            // processos; sem ponto = invoke sem PTINVOKE, best-effort).
            // Folha SEM verbo estável + com rótulos = invoke-by-label (NUMA
            // query só no host); verbo estável = caminho-verbo intacto.
            if (!ShellHostProtocol.HasStableVerb(handle.Verb)
                && handle.LabelPath is not null && handle.LabelPath.Count > 0)
            {
                return _host.InvokeMenuByLabel(
                    handle.Path,
                    handle.LabelPath,
                    handle.IncludeExtendedVerbs,
                    hwnd.ToInt64(),
                    invokePoint?.X,
                    invokePoint?.Y);
            }

            return _host.InvokeMenu(
                handle.Path,
                handle.Verb,
                handle.CommandOffset,
                handle.IncludeExtendedVerbs,
                hwnd.ToInt64(),
                invokePoint?.X,
                invokePoint?.Y);
        }
        catch
        {
            // Silencioso, padrão do projeto.
            return false;
        }
    }

    private void TrackExtendedFlag(string path, bool extended)
    {
        try
        {
            if (_extendedByPath.Count > MaxTrackedPaths)
            {
                _extendedByPath.Clear();
            }

            _extendedByPath[path] = extended;
        }
        catch
        {
            // Best-effort: sem o registro, o CreateHandle usa o Shift atual.
        }
    }

    private IReadOnlyList<ThirdPartyMenuEntry> QueryUncached(string path, bool extended)
    {
        try
        {
            // Host morto/timeout/resposta inválida = null = placeholder T1
            // (silencioso, padrão do projeto). O host já filtra (§1); a UI só
            // espelha. Cache negativo continua valendo (não respawna o host à
            // toa a cada abertura do menu).
            return _host.QueryMenu(path, extended) ?? Empty;
        }
        catch
        {
            return Empty;
        }
    }

    // Cache por extensão (§1 T2): ".ZIP"→".zip", sem extensão→"", pasta→"<folder>".
    // .lnk cai em ".lnk" (query sobre o próprio link, sem resolver alvo).
    // T6 Shift: consulta com Shift (CMF_EXTENDEDVERBS) usa bucket separado
    // ("|ext") — verbos estendidos nunca poluem o cache normal e vice-versa
    // (sonda T6 provou: .txt extended lista Extrair/Testar do 7-Zip, normal
    // não; .zip/pasta extended trazem opennewprocess, pasta traz Powershell).
    internal static string GetCacheKey(string path, bool isDirectory)
        => GetCacheKey(path, isDirectory, extended: false);

    internal static string GetCacheKey(string path, bool isDirectory, bool extended)
    {
        string baseKey;
        if (isDirectory)
        {
            baseKey = "<folder>";
        }
        else
        {
            baseKey = Path.GetExtension(path).ToLowerInvariant();
        }

        return extended ? baseKey + "|ext" : baseKey;
    }
}
