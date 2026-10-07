using System.IO;
using DeskQuadra.Core;
using DeskQuadra.Infrastructure.WindowsShell.Services;

namespace DeskQuadra.UI.Wpf;

/// <summary>
/// Ponto de entrada explícito de alta velocidade (BRAINSTORMING Seção 22: Zero-Flicker Startup).
/// Executa a checagem de instância única em &lt; 1ms e a ocultação nativa dos ícones em &lt; 2ms
/// antes de inicializar o pipeline do WPF e os recursos XAML.
/// </summary>
public static class Program
{
    private static Mutex? _singleInstanceMutex;

    [STAThread]
    public static void Main(string[] args)
    {
        // 1. Verificação ultrarrápida de instância única (< 1ms).
        try
        {
            _singleInstanceMutex = new Mutex(true, @"Local\DeskQuadra.UI.Wpf", out bool createdNew);
            if (!createdNew)
            {
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                return;
            }
        }
        catch
        {
            _singleInstanceMutex = null;
        }

        // 2. Parâmetro de restauração emergencial (--restore-icons)
        if (args.Any(a => string.Equals(a, "--restore-icons", StringComparison.OrdinalIgnoreCase)))
        {
            using var iconService = new NativeDesktopIconService();
            iconService.ShowDesktopIcons();
            return;
        }

        // 3. Ocultação Win32 Ultra-Precoce (BRAINSTORMING Seção 22: Zero-Flicker Startup)
        try
        {
            NativeDesktopIconService.QuickHideDesktopIcons();
        }
        catch
        {
            // Best-effort: se a chamada falhar, App.OnStartup tentará novamente via DI.
        }

        // 4. Transfere a guarda do Mutex e inicializa o runtime do WPF
        App.SingleInstanceMutex = _singleInstanceMutex;

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
