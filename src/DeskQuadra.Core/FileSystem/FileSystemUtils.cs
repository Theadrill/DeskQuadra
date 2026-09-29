namespace DeskQuadra.Core.FileSystem;

/// <summary>
/// Verificação de existência de caminhos no disco (extraída de FileLauncherService.Launch
/// e IconExtractorService.ExtractIconDirect).
/// Lógica pura via <see cref="System.IO"/> (sem Win32/UI): existe como arquivo ou diretório. Coberta por xUnit.
/// </summary>
public static class FileSystemUtils
{
    /// <summary>
    /// Indica se <paramref name="path"/> existe como arquivo ou diretório.
    /// Caminhos nulos, vazios ou em branco retornam falso.
    /// </summary>
    public static bool PathExists(string? path) =>
        !string.IsNullOrWhiteSpace(path) && (File.Exists(path) || Directory.Exists(path));
}
