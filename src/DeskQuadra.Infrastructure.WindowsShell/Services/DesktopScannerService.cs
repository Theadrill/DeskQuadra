using System.IO;
using System.Runtime.InteropServices;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Serviço de varredura das fontes físicas do desktop (Desktop do Usuário, OneDrive e Desktop Público).
/// Aplica filtros inteligentes contra arquivos de sistema (desktop.ini, temporários ~$*.*) e deduplicação.
/// </summary>
public sealed class DesktopScannerService : IDesktopScannerService
{
    private static readonly Guid FolderIdDesktop = new("B4BFCC3A-DB2C-424C-B029-7FE99A87C641");
    private static readonly Guid FolderIdPublicDesktop = new("C4AA340D-F20F-4863-AFEF-F87EF2E65A39");

    public IReadOnlyList<DesktopItem> ScanDesktopItems()
    {
        var items = new List<DesktopItem>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var pathsToScan = GetDesktopDirectories();

        int orderIndex = 0;

        foreach (var dir in pathsToScan)
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }

            try
            {
                var directoryInfo = new DirectoryInfo(dir);

                // 1. Varrer Pastas na Área de Trabalho
                foreach (var subDir in directoryInfo.EnumerateDirectories())
                {
                    if (ShouldIgnoreFile(subDir.Name, subDir.Attributes))
                    {
                        continue;
                    }

                    if (seenNames.Add(subDir.Name))
                    {
                        items.Add(new DesktopItem(
                            name: subDir.Name,
                            filePath: subDir.FullName,
                            targetPath: subDir.FullName,
                            isDirectory: true,
                            orderIndex: orderIndex++));
                    }
                }

                // 2. Varrer Arquivos e Atalhos na Área de Trabalho
                foreach (var file in directoryInfo.EnumerateFiles())
                {
                    if (ShouldIgnoreFile(file.Name, file.Attributes))
                    {
                        continue;
                    }

                    string displayName = file.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                        ? Path.GetFileNameWithoutExtension(file.Name)
                        : file.Name;

                    if (seenNames.Add(displayName))
                    {
                        items.Add(new DesktopItem(
                            name: displayName,
                            filePath: file.FullName,
                            targetPath: file.FullName,
                            isDirectory: false,
                            orderIndex: orderIndex++));
                    }
                }
            }
            catch
            {
                // Resiliente a restrições de permissão em pastas específicas
            }
        }

        return items;
    }

    private static List<string> GetDesktopDirectories()
    {
        var paths = new List<string>();

        // 1. Desktop do Usuário via SHGetKnownFolderPath (resolve OneDrive e redirecionamentos)
        string? userDesktop = GetKnownFolderPath(FolderIdDesktop);
        if (!string.IsNullOrWhiteSpace(userDesktop) && !paths.Contains(userDesktop, StringComparer.OrdinalIgnoreCase))
        {
            paths.Add(userDesktop);
        }

        // 2. Desktop Público (All Users / Instaladores)
        string? publicDesktop = GetKnownFolderPath(FolderIdPublicDesktop);
        if (!string.IsNullOrWhiteSpace(publicDesktop) && !paths.Contains(publicDesktop, StringComparer.OrdinalIgnoreCase))
        {
            paths.Add(publicDesktop);
        }

        // 3. Fallback: Environment.SpecialFolder.Desktop
        string localDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (!string.IsNullOrWhiteSpace(localDesktop) && !paths.Contains(localDesktop, StringComparer.OrdinalIgnoreCase))
        {
            paths.Add(localDesktop);
        }

        // 4. Fallback: CommonDesktopDirectory
        string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        if (!string.IsNullOrWhiteSpace(commonDesktop) && !paths.Contains(commonDesktop, StringComparer.OrdinalIgnoreCase))
        {
            paths.Add(commonDesktop);
        }

        return paths;
    }

    private static string? GetKnownFolderPath(Guid knownFolderId)
    {
        int hr = NativeMethods.SHGetKnownFolderPath(knownFolderId, 0, IntPtr.Zero, out IntPtr pPath);
        if (hr == 0 && pPath != IntPtr.Zero)
        {
            try
            {
                return Marshal.PtrToStringUni(pPath);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pPath);
            }
        }
        return null;
    }

    private static bool ShouldIgnoreFile(string name, FileAttributes attributes)
    {
        // Ignora desktop.ini em qualquer combinação de caixa
        if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Ignora arquivos temporários de edição do Microsoft Office (~$*.docx, etc)
        if (name.StartsWith("~$", StringComparison.Ordinal))
        {
            return true;
        }

        // Ignora arquivos ocultos de sistema que não sejam atalhos comuns
        if (attributes.HasFlag(FileAttributes.Hidden) && attributes.HasFlag(FileAttributes.System))
        {
            return true;
        }

        return false;
    }
}
