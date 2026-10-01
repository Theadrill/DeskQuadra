using System.Collections.Concurrent;
using System.IO;
using DeskQuadra.Infrastructure.WindowsShell.Native;
using Vanara.PInvoke;

namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

// T3 terceiros: query via ShellThirdPartyQuery (STA dedicada) + filtro §1 +
// cache por extensão (positivo E negativo; sem expiração em T2, sem timers) +
// invoke por VERBO via ShellThirdPartyInvoke (STA dedicada).
// CMF_EXTENDEDVERBS só com Shift pressionado (GetKeyState no momento da abertura).
// Query + alça de invoke resolvidos JUNTOS: GetForPath registra o Shift usado
// por caminho; CreateHandle captura (path, verb, offset, extended) e TryInvoke
// reexecuta o MESMO pipeline na MESMA interface raiz (verbo estável, imune a
// reordenação de offsets; VALIDATEW+offset só no fallback de verbo vazio).
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
            string key = GetCacheKey(path, isDirectory) + (extended ? "|ext" : string.Empty);
            return _cache.GetOrAdd(key, _ => QueryUncached(path, extended));
        }
        catch
        {
            // Silencioso, padrão do projeto.
            return Empty;
        }
    }

    public ThirdPartyInvokeHandle? CreateHandle(string? path, string? verb, uint commandOffset)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            // Caminho-verbo não exige offset válido (seleção é pelo verbo
            // estável); offset só é guardado como fallback p/ verbo vazio.
            string stableVerb = verb ?? string.Empty;
            if (!ShellThirdPartyInvoke.HasStableVerb(stableVerb)
                && !ShellThirdPartyInvoke.IsOffsetInRange(commandOffset))
            {
                return null;
            }

            bool extended = _extendedByPath.TryGetValue(path, out bool tracked)
                ? tracked
                : NativeMethods.IsShiftPressed();
            return new ThirdPartyInvokeHandle(path, stableVerb, commandOffset, extended);
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

            return ShellThirdPartyInvoke.TryInvoke(
                handle.Path,
                handle.Verb,
                handle.CommandOffset,
                handle.IncludeExtendedVerbs,
                hwnd,
                invokePoint);
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

    private static IReadOnlyList<ThirdPartyMenuEntry> QueryUncached(string path, bool extended)
    {
        try
        {
            var raw = ShellThirdPartyQuery.QueryForPath(path, extended);
            return ThirdPartyTreeBuilder.Build(raw);
        }
        catch
        {
            return Empty;
        }
    }

    // Cache por extensão (§1 T2): ".ZIP"→".zip", sem extensão→"", pasta→"<folder>".
    // .lnk cai em ".lnk" (query sobre o próprio link, sem resolver alvo).
    internal static string GetCacheKey(string path, bool isDirectory)
    {
        if (isDirectory)
        {
            return "<folder>";
        }

        return Path.GetExtension(path).ToLowerInvariant();
    }
}
