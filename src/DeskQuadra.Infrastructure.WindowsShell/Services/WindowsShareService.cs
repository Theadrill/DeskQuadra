using System.Diagnostics;
using System.IO;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.FileSystem;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Serviço de invocação do fluxo de compartilhamento nativo do Windows Shell.
/// Tenta acionar os verbos "windows.share" e "share" nativos com fallback gracioso para a área de transferência.
/// </summary>
public sealed class WindowsShareService : IShareService
{
    private readonly IClipboardService? _clipboardService;

    public WindowsShareService(IClipboardService? clipboardService = null)
    {
        _clipboardService = clipboardService;
    }

    public bool ShareFile(IntPtr windowHandle, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !FileSystemUtils.PathExists(filePath))
        {
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = filePath,
                Verb = "windows.share",
                UseShellExecute = true
            };

            Process.Start(psi);
            return true;
        }
        catch
        {
            try
            {
                var psiShare = new ProcessStartInfo
                {
                    FileName = filePath,
                    Verb = "share",
                    UseShellExecute = true
                };

                Process.Start(psiShare);
                return true;
            }
            catch
            {
                // Fallback gracioso: copia o arquivo para a área de transferência
                _clipboardService?.SetFileDropList(new[] { filePath }, isCut: false);
                return true;
            }
        }
    }
}
