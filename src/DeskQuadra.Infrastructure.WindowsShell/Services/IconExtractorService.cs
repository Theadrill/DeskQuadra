using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskQuadra.Core.FileSystem;
using DeskQuadra.Infrastructure.WindowsShell.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Serviço de extração de ícones nativos de alta fidelidade do Shell do Windows.
/// Utiliza SHGetFileInfo com conversão em BitmapSource congelado (Freeze) e cache inteligente em memória.
/// </summary>
public sealed class IconExtractorService : IIconExtractorService
{
    private readonly ConcurrentDictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);

    public void ClearCache() => _cache.Clear();

    public ImageSource GetIcon(string filePath, bool large = true)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return GetDefaultFileIcon();
        }

        string cacheKey = GetCacheKey(filePath, large);

        return _cache.GetOrAdd(cacheKey, _ => ExtractIconDirect(filePath, large));
    }

    private static string GetCacheKey(string filePath, bool large)
    {
        string prefix = large ? "L:" : "S:";

        if (Directory.Exists(filePath))
        {
            return prefix + "DIR";
        }

        string ext = Path.GetExtension(filePath);

        // Extensões com ícones específicos por binário exigem cache por caminho completo
        if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".ico", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".url", StringComparison.OrdinalIgnoreCase))
        {
            return prefix + filePath;
        }

        // Demais tipos (ex: .pdf, .docx, .png) compartilham o ícone do tipo de arquivo para poupar memória
        return prefix + (string.IsNullOrEmpty(ext) ? filePath : ext);
    }

    private static ImageSource ExtractIconDirect(string filePath, bool large)
    {
        var shinfo = new NativeMethods.SHFILEINFO();
        uint flags = NativeMethods.SHGFI_ICON | (large ? NativeMethods.SHGFI_LARGEICON : NativeMethods.SHGFI_SMALLICON);

        // Se o arquivo não existir fisicamente, tenta obter o ícone genérico baseado na extensão
        if (!FileSystemUtils.PathExists(filePath))
        {
            flags |= NativeMethods.SHGFI_USEFILEATTRIBUTES;
        }

        IntPtr result = NativeMethods.SHGetFileInfo(
            filePath,
            0,
            ref shinfo,
            (uint)Marshal.SizeOf<NativeMethods.SHFILEINFO>(),
            flags);

        if (result != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
        {
            try
            {
                var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                    shinfo.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                // Congela a imagem para permitir compartilhamento livre entre threads e otimizar renderização
                bitmapSource.Freeze();
                return bitmapSource;
            }
            finally
            {
                NativeMethods.DestroyIcon(shinfo.hIcon);
            }
        }

        return GetDefaultFileIcon();
    }

    private static ImageSource GetDefaultFileIcon()
    {
        // Cria um bitmap vazio suave de 32x32 caso nenhum ícone seja retornado pelo sistema
        var emptyBitmap = BitmapSource.Create(
            32, 32,
            96, 96,
            PixelFormats.Pbgra32,
            null,
            new byte[32 * 32 * 4],
            32 * 4);

        emptyBitmap.Freeze();
        return emptyBitmap;
    }
}
