using System.IO;
using DeskQuadra.Core.FileSystem;

namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

// F3 e F4 drop-em-container (docs/PLANO_DROP_CONTAINER.md):
// Validação pura inicial (container + fontes existentes, sem auto-drop).
// Execução Shell-First via ShellHostClient (F3).
// Se o Shell não possuir DropHandler (ex: .7z, .tar, .rar no Win11),
// aciona o fallback em camadas via IArchiveFallbackHandler (F4: .NET Zip, 7-Zip, WinRAR).
public sealed class ArchiveDropService : IArchiveDropService
{
    private readonly ShellHostClient _host;
    private readonly IArchiveFallbackHandler _fallback;

    public event EventHandler<ArchiveToolMissingEventArgs>? ToolMissing;

    public ArchiveDropService()
        : this(new ShellHostClient(), new ArchiveFallbackHandler())
    {
    }

    internal ArchiveDropService(ShellHostClient host, IArchiveFallbackHandler? fallback = null)
    {
        _host = host;
        _fallback = fallback ?? new ArchiveFallbackHandler();
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

            // 1. Tenta Shell-First (F3: IShellDropTarget via ShellHost isolado)
            bool hostOk = _host.DropOntoContainer(containerPath!, existing);
            if (hostOk)
            {
                return true;
            }

            // 2. Fallback (F4: .NET Zip nativo ou ferramentas CLI instaladas: 7-Zip / WinRAR)
            bool fallbackOk = _fallback.TryAddToArchive(containerPath!, existing, out string? missingTool);
            if (fallbackOk)
            {
                return true;
            }

            // Se falhou por falta de ferramenta externa compatível, dispara evento para notificação amigável
            if (!string.IsNullOrEmpty(missingTool))
            {
                string ext = Path.GetExtension(containerPath!) ?? string.Empty;
                ToolMissing?.Invoke(this, new ArchiveToolMissingEventArgs(containerPath!, ext, missingTool));
            }

            return false;
        }
        catch
        {
            // Silencioso, padrão do projeto: drop falhou = item continua
            // na Quadra, sem exceção na cara do usuário.
            return false;
        }
    }
}
