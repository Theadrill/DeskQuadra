using System.IO;
using DeskQuadra.Core.FileSystem;

namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

// F3 drop-em-container: validação pura aqui (container + fontes existentes,
// sem auto-drop), execução no ShellHost via ShellHostClient (one-shot +
// timeout/kill, molde do InvokeMenu). Injeção do cliente p/ xUnit, mesmo
// molde do ThirdPartyMenuService.
public sealed class ArchiveDropService : IArchiveDropService
{
    private readonly ShellHostClient _host;

    public ArchiveDropService()
        : this(new ShellHostClient())
    {
    }

    internal ArchiveDropService(ShellHostClient host)
    {
        _host = host;
    }

    public bool TryAddToContainer(string? containerPath, IReadOnlyList<string>? sourcePaths)
    {
        try
        {
            if (!ArchiveFormatDetector.IsContainer(containerPath))
            {
                return false;
            }

            if (sourcePaths is null || sourcePaths.Count == 0)
            {
                return false;
            }

            var existing = sourcePaths
                .Where(f => !string.IsNullOrWhiteSpace(f) && (File.Exists(f) || Directory.Exists(f)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (existing.Count == 0)
            {
                return false;
            }

            // Soltar o container nele mesmo nunca é válido (a UI já barra,
            // aqui é segunda barreira — mesma regra do highlight F2).
            string fullContainer = Path.GetFullPath(containerPath!).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            existing = existing.Where(f =>
            {
                try
                {
                    string full = Path.GetFullPath(f).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    return !string.Equals(full, fullContainer, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            }).ToList();
            if (existing.Count == 0)
            {
                return false;
            }

            return _host.DropOntoContainer(containerPath!, existing);
        }
        catch
        {
            // Silencioso, padrão do projeto: drop falhou = item continua
            // na Quadra, sem erro na cara do usuário (a F4 assume).
            return false;
        }
    }
}
