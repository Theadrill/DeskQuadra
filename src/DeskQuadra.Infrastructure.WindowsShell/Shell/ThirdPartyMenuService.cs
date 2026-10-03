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
// com expiração assimétrica T8b — negativo 60s, positivo 300s — sem timers)
// + registro do Shift/EXTENDEDVERBS por caminho +
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

    private readonly ConcurrentDictionary<string, CacheEntry> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    // T8b TTL assimétrico: instalar app novo aparece sem restart. Entrada =
    // (entries, timestampUtc); negativo (lista vazia, inclui falha que resulte
    // em vazio) expira rápido, positivo dura mais. Sem timers (expiração
    // preguiçosa: só re-consulta no próximo GetForPath após o TTL).
    internal const int NegativeTtlSeconds = 60;
    internal const int PositiveTtlSeconds = 300;

    private readonly record struct CacheEntry(
        IReadOnlyList<ThirdPartyMenuEntry> Entries, DateTime TimestampUtc);

    // Shift usado na query, por caminho (o Shift do clique pode já ter sido
    // solto — o invoke precisa das MESMAS flags da listagem). Limitado: é
    // best-effort, o fallback é o Shift atual; limpeza quando estoura.
    private readonly ConcurrentDictionary<string, bool> _extendedByPath =
        new(StringComparer.OrdinalIgnoreCase);
    private const int MaxTrackedPaths = 1024;

    private readonly ShellHostClient _host;
    private readonly Func<DateTime> _utcNow;

    public ThirdPartyMenuService()
        : this(new ShellHostClient(), static () => DateTime.UtcNow)
    {
    }

    // Injeção p/ xUnit (fake launcher + relógio controlável). Não muda o
    // comportamento público além do ClearCache (T8c refresh manual do tray).
    internal ThirdPartyMenuService(IShellHostLauncher launcher, Func<DateTime>? utcNow = null)
        : this(new ShellHostClient(launcher), utcNow)
    {
    }

    internal ThirdPartyMenuService(ShellHostClient host, Func<DateTime>? utcNow = null)
    {
        _host = host;
        _utcNow = utcNow ?? (static () => DateTime.UtcNow);
    }

    public IReadOnlyList<ThirdPartyMenuEntry> GetForPath(string? path, bool background = false)
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
            // não poluem o cache normal e vice-versa). Fundo tem bucket e
            // registro próprios (nunca mistura com item de pasta).
            bool extended = NativeMethods.IsShiftPressed();
            TrackExtendedFlag(path, extended, background);
            string key = GetCacheKey(path, isDirectory, extended, background);

            // Hit fresco → devolve; hit expirado ou miss → QueryUncached de
            // novo e regrava com DateTime.UtcNow (via _utcNow, mockável).
            if (_cache.TryGetValue(key, out CacheEntry cached))
            {
                int ttlSeconds = cached.Entries.Count == 0 ? NegativeTtlSeconds : PositiveTtlSeconds;
                if (_utcNow() - cached.TimestampUtc < TimeSpan.FromSeconds(ttlSeconds))
                {
                    return cached.Entries;
                }
            }

            IReadOnlyList<ThirdPartyMenuEntry> fresh = QueryUncached(path, extended, background);
            _cache[key] = new CacheEntry(fresh, _utcNow());
            return fresh;
        }
        catch
        {
            // Silencioso, padrão do projeto.
            return Empty;
        }
    }

    public ThirdPartyInvokeHandle? CreateHandle(string? path, string? verb, uint commandOffset, IReadOnlyList<string>? labelPath = null, bool background = false)
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

            bool extended = _extendedByPath.TryGetValue(ExtendedKey(path, background), out bool tracked)
                ? tracked
                : NativeMethods.IsShiftPressed();
            return new ThirdPartyInvokeHandle(path, stableVerb, commandOffset, extended, keptLabels, background);
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
                    invokePoint?.Y,
                    handle.Background);
            }

            return _host.InvokeMenu(
                handle.Path,
                handle.Verb,
                handle.CommandOffset,
                handle.IncludeExtendedVerbs,
                hwnd.ToInt64(),
                invokePoint?.X,
                invokePoint?.Y,
                handle.Background);
        }
        catch
        {
            // Silencioso, padrão do projeto.
            return false;
        }
    }

    // T8c refresh manual do tray: esvazia _cache (buckets item/fundo/extended)
    // e _extendedByPath; próxima abertura refaz query. Limpar o extended é
    // seguro: o invoke-by-label resolve rótulos na hora e o CreateHandle
    // recai no Shift atual quando não há flag registrada (fallback intacto).
    public void ClearCache()
    {
        try
        {
            _cache.Clear();
            _extendedByPath.Clear();
        }
        catch
        {
            // Best-effort silencioso, padrão do projeto.
        }
    }

    private void TrackExtendedFlag(string path, bool extended, bool background)
    {
        try
        {
            if (_extendedByPath.Count > MaxTrackedPaths)
            {
                _extendedByPath.Clear();
            }

            _extendedByPath[ExtendedKey(path, background)] = extended;
        }
        catch
        {
            // Best-effort: sem o registro, o CreateHandle usa o Shift atual.
        }
    }

    // Registro de Shift separado por fundo/item: o Shift do clique pode já ter
    // sido solto no invoke, e fundo nunca pode herdar o flag do item (e
    // vice-versa). '\u001F' é ilegal em caminho Windows — sem colisão.
    private static string ExtendedKey(string path, bool background)
        => background ? path + "|\u001Fbg" : path;

    private IReadOnlyList<ThirdPartyMenuEntry> QueryUncached(string path, bool extended, bool background)
    {
        try
        {
            // Host morto/timeout/resposta inválida = null = placeholder T1
            // (silencioso, padrão do projeto). O host já filtra (§1); a UI só
            // espelha. Cache negativo continua valendo (não respawna o host à
            // toa a cada abertura do menu).
            return _host.QueryMenu(path, extended, background) ?? Empty;
        }
        catch
        {
            return Empty;
        }
    }

    // Cache por extensão (§1 T2): ".ZIP"→".zip", sem extensão→"", pasta→"<folder>".
    // .lnk cai em ".lnk" (query sobre o próprio link, sem resolver alvo).
    // Fundo (espaço vazio/barra) tem bucket próprio "<background>": o menu de
    // fundo da pasta NUNCA compartilha com o de item de pasta (era o bug —
    // "<folder>" servia verbos de item no vazio). Item intacto.
    // T6 Shift: consulta com Shift (CMF_EXTENDEDVERBS) usa bucket separado
    // ("|ext") — verbos estendidos nunca poluem o cache normal e vice-versa
    // (sonda T6 provou: .txt extended lista Extrair/Testar do 7-Zip, normal
    // não; .zip/pasta extended trazem opennewprocess, pasta traz Powershell).
    internal static string GetCacheKey(string path, bool isDirectory)
        => GetCacheKey(path, isDirectory, extended: false);

    internal static string GetCacheKey(string path, bool isDirectory, bool extended)
        => GetCacheKey(path, isDirectory, extended, background: false);

    internal static string GetCacheKey(string path, bool isDirectory, bool extended, bool background)
    {
        string baseKey;
        if (background)
        {
            baseKey = "<background>";
        }
        else if (isDirectory)
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
