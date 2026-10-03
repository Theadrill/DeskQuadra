using DeskQuadra.Core.FileDuplication;

namespace DeskQuadra.Core.FileSystem;

/// <summary>
/// Operações de validação, movimentação e cópia de itens (arquivos/pastas)
/// para um diretório de destino (ex: arrastar e soltar sobre pasta na Quadra).
/// Lógica pura em C#, testável sem dependências de WPF ou Win32.
/// </summary>
public static class DesktopItemMover
{
    /// <summary>
    /// Valida se <paramref name="sourcePath"/> pode ser movido ou copiado para dentro de <paramref name="targetDirectoryPath"/>.
    /// Impede:
    /// - Caminhos nulos ou vazios
    /// - Diretório de destino inexistente
    /// - Arquivo/diretório de origem inexistente
    /// - Mover uma pasta para dentro de si mesma
    /// - Mover uma pasta para uma de suas subpastas
    /// - Mover um item para o mesmo diretório onde ele já reside
    /// </summary>
    public static bool CanMoveInto(
        string? sourcePath,
        string? targetDirectoryPath,
        Func<string, bool>? fileExists = null,
        Func<string, bool>? directoryExists = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(targetDirectoryPath))
        {
            return false;
        }

        fileExists ??= File.Exists;
        directoryExists ??= Directory.Exists;

        if (!directoryExists(targetDirectoryPath))
        {
            return false;
        }

        bool isFile = fileExists(sourcePath);
        bool isDir = directoryExists(sourcePath);

        if (!isFile && !isDir)
        {
            return false;
        }

        try
        {
            string fullSource = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullTarget = Path.GetFullPath(targetDirectoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Não pode mover para si mesma
            if (string.Equals(fullSource, fullTarget, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Não pode mover um diretório pai para dentro de um de seus subdiretórios
            if (isDir)
            {
                string targetWithSep = fullTarget + Path.DirectorySeparatorChar;
                string sourceWithSep = fullSource + Path.DirectorySeparatorChar;
                if (targetWithSep.StartsWith(sourceWithSep, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Não pode mover para a mesma pasta onde já está
            string? parent = Path.GetDirectoryName(fullSource)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(parent, fullTarget, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Calcula o caminho de destino livre de colisões usando FileDuplicator.GetUniquePath.
    /// Se não houver colisão, retorna o nome original. Se houver, adiciona " (2)", " (3)", etc.
    /// </summary>
    public static string GetDestinationPath(
        string sourcePath,
        string targetDirectoryPath,
        Func<string, bool>? pathExists = null,
        bool? isSourceDirectory = null)
    {
        pathExists ??= p => File.Exists(p) || Directory.Exists(p);

        bool isDir = isSourceDirectory ?? Directory.Exists(sourcePath);
        int copyIndex = 1;

        if (isDir)
        {
            string dirName = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return FileDuplicator.GetUniquePath(
                targetDirectoryPath,
                i => i == 1 ? dirName : $"{dirName} ({i})",
                pathExists,
                ref copyIndex);
        }
        else
        {
            string nameWithoutExt = Path.GetFileNameWithoutExtension(sourcePath);
            string ext = Path.GetExtension(sourcePath);
            return FileDuplicator.GetUniquePath(
                targetDirectoryPath,
                i => i == 1 ? $"{nameWithoutExt}{ext}" : $"{nameWithoutExt} ({i}){ext}",
                pathExists,
                ref copyIndex);
        }
    }

    /// <summary>
    /// Move fisicamente o item (arquivo ou diretório) para o diretório de destino,
    /// tratando colisões com sufixo indexado ("nome (2).ext").
    /// </summary>
    public static string Move(
        string sourcePath,
        string targetDirectoryPath,
        Action<string, string>? moveFile = null,
        Action<string, string>? moveDirectory = null)
    {
        string destinationPath = GetDestinationPath(sourcePath, targetDirectoryPath);

        if (Directory.Exists(sourcePath))
        {
            var doMove = moveDirectory ?? Directory.Move;
            doMove(sourcePath, destinationPath);
        }
        else
        {
            var doMove = moveFile ?? File.Move;
            doMove(sourcePath, destinationPath);
        }

        return destinationPath;
    }

    /// <summary>
    /// Copia fisicamente o item (arquivo ou diretório) para o diretório de destino,
    /// tratando colisões com sufixo indexado ("nome (2).ext").
    /// </summary>
    public static string Copy(
        string sourcePath,
        string targetDirectoryPath,
        Action<string, string>? copyFile = null,
        Action<string, string>? copyDirectory = null)
    {
        string destinationPath = GetDestinationPath(sourcePath, targetDirectoryPath);

        if (Directory.Exists(sourcePath))
        {
            var doCopy = copyDirectory ?? FileDuplicator.CopyDirectoryRecursively;
            doCopy(sourcePath, destinationPath);
        }
        else
        {
            var doCopy = copyFile ?? File.Copy;
            doCopy(sourcePath, destinationPath);
        }

        return destinationPath;
    }
}
