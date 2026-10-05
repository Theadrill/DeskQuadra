namespace DeskQuadra.Core.FileSystem;

/// <summary>
/// Detecção pura de arquivo container para drop (F1 do `docs/PLANO_DROP_CONTAINER.md`).
/// Lógica pura em C#, testável sem WPF/Win32: extensão conhecida + existe como arquivo (nunca diretório).
/// Pastas e `.lnk` nunca são container. Comparação de extensão case-insensitive.
/// </summary>
public static class ArchiveFormatDetector
{
    private static readonly HashSet<string> ContainerExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip",
        ".7z",
        ".rar",
        ".tar",
        ".gz",
        ".tgz",
        ".bz2",
        ".xz",
    };

    /// <summary>
    /// Indica se <paramref name="path"/> é um arquivo container válido como alvo de drop.
    /// </summary>
    public static bool IsContainer(
        string? path,
        Func<string, bool>? fileExists = null,
        Func<string, bool>? directoryExists = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        fileExists ??= File.Exists;
        directoryExists ??= Directory.Exists;

        // Diretório nunca é container (drop em pasta segue o caminho de pasta existente).
        if (directoryExists(path))
        {
            return false;
        }

        if (!fileExists(path))
        {
            return false;
        }

        string extension;
        try
        {
            extension = Path.GetExtension(path);
        }
        catch
        {
            return false;
        }

        return !string.IsNullOrEmpty(extension) && ContainerExtensions.Contains(extension);
    }
}
