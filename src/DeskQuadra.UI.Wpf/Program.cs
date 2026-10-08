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

    private static void Log(string message)
    {
        try
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskQuadra");
            Directory.CreateDirectory(appData);
            File.AppendAllText(Path.Combine(appData, "startup-diag.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [PID {Environment.ProcessId}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    [STAThread]
    public static void Main(string[] args)
    {
        Log("Program.Main enter. Args: " + string.Join(" ", args));

        // 1. Verificação ultrarrápida de instância única (< 1ms).
        try
        {
            _singleInstanceMutex = new Mutex(true, @"Local\DeskQuadra.UI.Wpf", out bool createdNew);
            Log($"Mutex check: createdNew={createdNew}");
            if (!createdNew)
            {
                Log("Instância secundária detectada. Encerrando.");
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                return;
            }
        }
        catch (Exception ex)
        {
            Log($"Mutex exception: {ex.Message}");
            _singleInstanceMutex = null;
        }

        // 2. Parâmetro de restauração emergencial (--restore-icons)
        if (args.Any(a => string.Equals(a, "--restore-icons", StringComparison.OrdinalIgnoreCase)))
        {
            Log("Argumento --restore-icons detectado.");
            using var iconService = new NativeDesktopIconService();
            iconService.ShowDesktopIcons();
            return;
        }

        // 3. Ocultação Win32 Ultra-Precoce (BRAINSTORMING Seção 22: Zero-Flicker Startup)
        try
        {
            NativeDesktopIconService.QuickHideDesktopIcons();
        }
        catch (Exception ex)
        {
            Log($"QuickHideDesktopIcons exception: {ex.Message}");
        }

        // 4. Transfere a guarda do Mutex e inicializa o runtime do WPF
        App.SingleInstanceMutex = _singleInstanceMutex;

        try
        {
            Log("Instanciando App e iniciando app.Run()...");
            var app = new App();
            app.InitializeComponent();
            app.Run();
            Log("app.Run() finalizou normalmente.");
        }
        catch (Exception ex)
        {
            Log($"Exceção não tratada em Program.Main: {ex}");
        }
    }
}
