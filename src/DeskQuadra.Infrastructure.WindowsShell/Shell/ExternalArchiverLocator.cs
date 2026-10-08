using System.IO;
using Microsoft.Win32;

namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

/// <summary>
/// Localizador padrão de ferramentas externas de compactação (7-Zip e WinRAR).
/// Consulta diretórios padrão de instalação, chaves de registro do Windows e variável PATH.
/// Estritamente de leitura (não altera nenhuma configuração do sistema).
/// </summary>
public sealed class ExternalArchiverLocator : IExternalArchiverLocator
{
    public string? FindSevenZip()
    {
        try
        {
            // 1. Program Files padrão (64-bit e 32-bit)
            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            string[] directCandidates =
            {
                Path.Combine(progFiles, "7-Zip", "7zG.exe"),
                Path.Combine(progFiles, "7-Zip", "7z.exe"),
                Path.Combine(progFilesX86, "7-Zip", "7zG.exe"),
                Path.Combine(progFilesX86, "7-Zip", "7z.exe")
            };

            foreach (var path in directCandidates)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // 2. Chaves de registro de instalação do 7-Zip
            string? fromRegistry = ReadRegistryPath(@"SOFTWARE\7-Zip", "Path");
            if (!string.IsNullOrEmpty(fromRegistry))
            {
                string guiCandidate = Path.Combine(fromRegistry, "7zG.exe");
                if (File.Exists(guiCandidate))
                {
                    return guiCandidate;
                }

                string cliCandidate = Path.Combine(fromRegistry, "7z.exe");
                if (File.Exists(cliCandidate))
                {
                    return cliCandidate;
                }
            }

            // 3. Procura no PATH do sistema
            string? fromPath = SearchInSystemPath("7zG.exe") ?? SearchInSystemPath("7z.exe");
            if (!string.IsNullOrEmpty(fromPath))
            {
                return fromPath;
            }
        }
        catch
        {
            // Best-effort: falhas de I/O nunca sobem exceção.
        }

        return null;
    }

    public string? FindWinRar()
    {
        try
        {
            // 1. Program Files padrão
            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            string[] directCandidates =
            {
                Path.Combine(progFiles, "WinRAR", "WinRAR.exe"),
                Path.Combine(progFilesX86, "WinRAR", "WinRAR.exe")
            };

            foreach (var path in directCandidates)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // 2. App Paths do Windows
            string? fromAppPaths = ReadRegistryAppPath(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\WinRAR.exe");
            if (!string.IsNullOrEmpty(fromAppPaths) && File.Exists(fromAppPaths))
            {
                return fromAppPaths;
            }

            // 3. PATH do sistema
            string? fromPath = SearchInSystemPath("WinRAR.exe");
            if (!string.IsNullOrEmpty(fromPath))
            {
                return fromPath;
            }
        }
        catch
        {
            // Best-effort
        }

        return null;
    }

    private static string? ReadRegistryPath(string subKey, string valueName)
    {
        try
        {
            using var hklmKey = Registry.LocalMachine.OpenSubKey(subKey);
            object? val = hklmKey?.GetValue(valueName);
            if (val is string str && !string.IsNullOrWhiteSpace(str))
            {
                return str.Trim();
            }

            using var hkcuKey = Registry.CurrentUser.OpenSubKey(subKey);
            val = hkcuKey?.GetValue(valueName);
            if (val is string strCu && !string.IsNullOrWhiteSpace(strCu))
            {
                return strCu.Trim();
            }
        }
        catch
        {
            // Best-effort
        }

        return null;
    }

    private static string? ReadRegistryAppPath(string appPathSubKey)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(appPathSubKey);
            object? val = key?.GetValue(null); // Valor padrão
            if (val is string str && !string.IsNullOrWhiteSpace(str))
            {
                return str.Trim('"', ' ');
            }
        }
        catch
        {
            // Best-effort
        }

        return null;
    }

    private static string? SearchInSystemPath(string exeName)
    {
        try
        {
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathEnv))
            {
                return null;
            }

            var entries = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var dir in entries)
            {
                try
                {
                    string candidate = Path.Combine(dir, exeName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                    // Ignora diretórios inválidos no PATH
                }
            }
        }
        catch
        {
            // Best-effort
        }

        return null;
    }
}
