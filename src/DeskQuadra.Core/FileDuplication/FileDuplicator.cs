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
    /// Resolve o diretório de fallback (Desktop do usuário): usa o diretório
    /// injetado pela costura de teste quando presente, senão o Desktop real.
    /// Extrai o `userDesktopDir ?? GetFolderPath` antes duplicado nos dois ramos.
    /// </summary>
    private static string ResolveFallbackDir(string? userDesktopDir) =>
        userDesktopDir ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    /// <summary>
    /// Núcleo comum da duplicação: tenta copiar no diretório primário e, em falha,
    /// tenta de novo no Desktop (fallback), preservando a continuação do
    /// <c>copyIndex</c> entre as tentativas. Parametriza só o que difere entre
    /// arquivo e diretório (construção do nome, existência e cópia); sufixos,
    /// ordem de tentativas e semântica de erro (falha total propaga para o
    /// chamador retornar o original) ficam inalterados.
    /// </summary>
    private static string DuplicateCore(
        string sourcePath,
        string primaryDir,
        Func<int, string> buildName,
        Func<string, bool> exists,
        Action<string, string> copy,
        string? userDesktopDir)
    {
        int copyIndex = 1;
        string targetPath = GetUniquePath(primaryDir, buildName, exists, ref copyIndex);

        try
        {
            copy(sourcePath, targetPath);
            return targetPath;
        }
        catch
        {
            // Fallback para o Desktop do usuário se o diretório for protegido (ex: Public Desktop)
            string fallbackDir = ResolveFallbackDir(userDesktopDir);
            string fallbackTarget = GetUniquePath(fallbackDir, buildName, exists, ref copyIndex);

            copy(sourcePath, fallbackTarget);
            return fallbackTarget;
        }
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
            string cleanPath = path?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cleanPath))
            {
                return path ?? string.Empty;
            }

            if (File.Exists(cleanPath))
            {
                string dir = Path.GetDirectoryName(cleanPath) ?? string.Empty;
                string nameWithoutExt = Path.GetFileNameWithoutExtension(cleanPath);
                string ext = Path.GetExtension(cleanPath);
                string BuildFileName(int i) => i == 1
                    ? $"{nameWithoutExt}{copySuffix}{ext}"
                    : $"{nameWithoutExt}{string.Format(copySuffixIndexedFormat, i)}{ext}";

                Action<string, string> doCopyFile = copyFile ?? ((s, d) => File.Copy(s, d));
                return DuplicateCore(cleanPath, dir, BuildFileName, File.Exists, doCopyFile, userDesktopDir);
            }

            if (Directory.Exists(cleanPath))
            {
                string parent = Directory.GetParent(cleanPath)?.FullName ?? string.Empty;
                string dirName = Path.GetFileName(cleanPath);
                string BuildDirName(int i) => i == 1
                    ? $"{dirName}{copySuffix}"
                    : $"{dirName}{string.Format(copySuffixIndexedFormat, i)}";

                Action<string, string> doCopyDir = copyDirectory ?? CopyDirectoryRecursively;
                return DuplicateCore(cleanPath, parent, BuildDirName, Directory.Exists, doCopyDir, userDesktopDir);
            }
        }
        catch
        {
            // Em caso de falha de I/O, usa o item original sem interromper a interface
        }

        return path ?? string.Empty;
    }

    public static void CopyDirectoryRecursively(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: true);
        }

        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectoryRecursively(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
        }
    }
}
