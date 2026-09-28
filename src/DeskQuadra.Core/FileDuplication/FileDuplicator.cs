namespace DeskQuadra.Core.FileDuplication;

/// <summary>
/// Duplicação física de arquivos/diretórios no disco (extraída de QuadraWindow.DuplicateFileOnDisk).
/// Lógica pura de I/O via <see cref="System.IO"/> (sem WPF/timers/textos): nome único + fallback
/// para o Desktop do usuário quando a cópia no diretório de origem falha. Coberta por xUnit.
/// Os sufixos (" - Cópia" / " - Cópia ({0})") vêm do chamador (resx da UI), para não acoplar o Core a recursos WPF.
/// Falhas de I/O são best-effort: retorna o caminho original sem interromper a interface.
/// </summary>
public static class FileDuplicator
{
    /// <summary>
    /// Unifica os 4 loops de nome único do código original (arquivo primário/fallback,
    /// diretório primário/fallback). Começa em <paramref name="copyIndex"/> (1 = sufixo simples,
    /// 2+ = formato indexado) e avança enquanto <paramref name="exists"/> for verdadeiro,
    /// preservando a continuação do índice entre a tentativa primária e o fallback.
    /// </summary>
    public static string GetUniquePath(
        string directory,
        Func<int, string> buildFileName,
        Func<string, bool> exists,
        ref int copyIndex)
    {
        string targetName = buildFileName(copyIndex);
        string targetPath = Path.Combine(directory, targetName);
        while (exists(targetPath))
        {
            copyIndex++;
            targetName = buildFileName(copyIndex);
            targetPath = Path.Combine(directory, targetName);
        }

        return targetPath;
    }

    /// <summary>
    /// Duplica <paramref name="path"/> (arquivo ou diretório) com o mesmo comportamento do
    /// código original: nomes " - Cópia"/" - Cópia (N)", fallback para o Desktop em caso de
    /// falha na cópia primária, retorno do caminho original em falha total.
    /// <paramref name="copyFile"/>/<paramref name="copyDirectory"/> existem como costura de
    /// teste (simular diretório protegido sem tocar no disco real); o padrão usa System.IO.
    /// </summary>
    public static string Duplicate(
        string path,
        string copySuffix,
        string copySuffixIndexedFormat,
        string? userDesktopDir = null,
        Action<string, string>? copyFile = null,
        Action<string, string>? copyDirectory = null)
    {
        try
        {
            if (File.Exists(path))
            {
                string dir = Path.GetDirectoryName(path) ?? string.Empty;
                string nameWithoutExt = Path.GetFileNameWithoutExtension(path);
                string ext = Path.GetExtension(path);
                string BuildFileName(int i) => i == 1
                    ? $"{nameWithoutExt}{copySuffix}{ext}"
                    : $"{nameWithoutExt}{string.Format(copySuffixIndexedFormat, i)}{ext}";

                int copyIndex = 1;
                string targetPath = GetUniquePath(dir, BuildFileName, File.Exists, ref copyIndex);

                Action<string, string> doCopyFile = copyFile ?? ((s, d) => File.Copy(s, d));
                try
                {
                    doCopyFile(path, targetPath);
                    return targetPath;
                }
                catch
                {
                    // Fallback para o Desktop do usuário se o diretório for protegido (ex: Public Desktop)
                    string userDesktop = userDesktopDir ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string fallbackTarget = GetUniquePath(userDesktop, BuildFileName, File.Exists, ref copyIndex);

                    doCopyFile(path, fallbackTarget);
                    return fallbackTarget;
                }
            }

            if (Directory.Exists(path))
            {
                string parent = Directory.GetParent(path)?.FullName ?? string.Empty;
                string dirName = Path.GetFileName(path);
                string BuildDirName(int i) => i == 1
                    ? $"{dirName}{copySuffix}"
                    : $"{dirName}{string.Format(copySuffixIndexedFormat, i)}";

                int copyIndex = 1;
                string targetPath = GetUniquePath(parent, BuildDirName, Directory.Exists, ref copyIndex);

                Action<string, string> doCopyDir = copyDirectory ?? CopyDirectoryRecursively;
                try
                {
                    doCopyDir(path, targetPath);
                    return targetPath;
                }
                catch
                {
                    string userDesktop = userDesktopDir ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string fallbackTarget = GetUniquePath(userDesktop, BuildDirName, Directory.Exists, ref copyIndex);

                    doCopyDir(path, fallbackTarget);
                    return fallbackTarget;
                }
            }
        }
        catch
        {
            // Em caso de falha de I/O, usa o item original sem interromper a interface
        }

        return path;
    }

    public static void CopyDirectoryRecursively(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)));
        }

        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectoryRecursively(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
        }
    }
}
