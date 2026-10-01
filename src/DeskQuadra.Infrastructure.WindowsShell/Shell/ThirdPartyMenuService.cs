using System.Collections.Concurrent;
using System.IO;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

// T2 terceiros: query via ShellThirdPartyQuery (STA dedicada) + filtro §1 +
// cache por extensão (positivo E negativo; sem expiração em T2, sem timers).
// CMF_EXTENDEDVERBS só com Shift pressionado (GetKeyState no momento da abertura).
// Tudo best-effort e silencioso: qualquer falha devolve lista vazia.
public sealed class ThirdPartyMenuService : IThirdPartyMenuService
{
    private static readonly IReadOnlyList<ThirdPartyMenuEntry> Empty =
        Array.Empty<ThirdPartyMenuEntry>();

    private readonly ConcurrentDictionary<string, IReadOnlyList<ThirdPartyMenuEntry>> _cache =
        new(StringComparer.OrdinalIgnoreCase);

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
            string key = GetCacheKey(path, isDirectory) + (extended ? "|ext" : string.Empty);
            return _cache.GetOrAdd(key, _ => QueryUncached(path, extended));
        }
        catch
        {
            // Silencioso, padrão do projeto.
            return Empty;
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
