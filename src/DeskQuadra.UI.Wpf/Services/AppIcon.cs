using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskQuadra.UI.Wpf.Services;

/// <summary>
/// Ícone próprio do DeskQuadra (Fase 5): fonte única em Assets/DeskQuadra.ico
/// (Resource embutido + ApplicationIcon do exe no .csproj).
/// Best-effort silencioso (padrão do projeto): falha retorna null e o chamador
/// usa o fallback (SystemIcons.Application no tray, sem ícone nas janelas).
/// Deve ser chamado na thread da UI, como os demais acessos a Resources.
/// </summary>
internal static class AppIcon
{
    private const string PackUri = "pack://application:,,,/Assets/DeskQuadra.ico";

    // Tray usa a variante de crop justo (a arte cheia some na taskbar escura em 16px).
    private const string TrayPackUri = "pack://application:,,,/Assets/DeskQuadra.Tray.ico";

    private static ImageSource? _wpfIcon;

    // Tray residente: o Icon mantém referência no stream de origem, por isso o
    // MemoryStream fica enraizado em campo até o fim do processo (nunca liberado),
    // mesmo molde da trava de instância única em App.xaml.cs.
    private static System.Drawing.Icon? _trayIcon;
    private static MemoryStream? _trayStream;

    /// <summary>WPF (Window.Icon via code-behind): compartilhado, carregado uma vez.</summary>
    internal static ImageSource? Wpf
    {
        get
        {
            if (_wpfIcon != null)
            {
                return _wpfIcon;
            }

            try
            {
                var streamInfo = System.Windows.Application.GetResourceStream(new Uri(PackUri, UriKind.Absolute));
                if (streamInfo?.Stream is null)
                {
                    return null;
                }

                using (streamInfo.Stream)
                {
                    var decoder = new IconBitmapDecoder(streamInfo.Stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count == 0)
                    {
                        return null;
                    }

                    _wpfIcon = decoder.Frames[0];
                }

                return _wpfIcon;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>WinForms (NotifyIcon.Icon): compartilhado, lifetime = processo.</summary>
    internal static System.Drawing.Icon? Tray
    {
        get
        {
            if (_trayIcon != null)
            {
                return _trayIcon;
            }

            try
            {
                var streamInfo = System.Windows.Application.GetResourceStream(new Uri(TrayPackUri, UriKind.Absolute));
                if (streamInfo?.Stream is null)
                {
                    return null;
                }

                using (streamInfo.Stream)
                {
                    _trayStream = new MemoryStream();
                    streamInfo.Stream.CopyTo(_trayStream);
                    _trayStream.Position = 0;
                    _trayIcon = new System.Drawing.Icon(_trayStream);
                }

                return _trayIcon;
            }
            catch
            {
                return null;
            }
        }
    }
}
