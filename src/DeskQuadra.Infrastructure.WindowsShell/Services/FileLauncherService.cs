using System.Diagnostics;
using System.IO;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.FileSystem;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Serviço de execução segura de arquivos, atalhos e diretórios via Shell nativo do Windows.
/// </summary>
public sealed class FileLauncherService : IFileLauncherService
{
    public bool Launch(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        if (!FileSystemUtils.PathExists(filePath))
        {
            return false;
        }

        try
        {
            string? workingDirectory = Directory.Exists(filePath)
                ? filePath
                : Path.GetDirectoryName(filePath);

            var psi = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true,
                WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? string.Empty : workingDirectory
            };

            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
