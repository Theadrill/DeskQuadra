using System.IO;
using System.Runtime.InteropServices;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Serviço de infraestrutura Win32 responsável por ancorar janelas de Quadras diretamente
/// na camada de fundo do desktop (Progman / Shell Desktop), tornando-as integradas ao ambiente
/// de trabalho sem interferir com a renderização de alto desempenho do WPF.
/// </summary>
public sealed class DesktopWindowAnchorService : IWindowAnchorService
{
    private IntPtr _shellDesktopHandle = IntPtr.Zero;

    public bool AnchorToDesktop(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            Log("AnchorToDesktop chamado com windowHandle nulo.");
            return false;
        }

        IntPtr progman = GetShellDesktopHandle();
        if (progman == IntPtr.Zero)
        {
            Log("Falha: janela do Progman/Shell não foi localizada.");
            return false;
        }

        Log($"Iniciando ancoragem da janela 0x{windowHandle:X} como janela associada ao Progman 0x{progman:X}...");

        // 1. Aplicar estilo estendido WS_EX_TOOLWINDOW para não poluir Taskbar nem Alt+Tab
        NativeMethods.ApplyToolWindow(windowHandle);

        // 2. Definir o Progman como Owner (proprietário) da janela via GWL_HWNDPARENT (-8)
        // Isso vincula a janela permanentemente à camada do Desktop no gerenciador de janelas do Windows.
        NativeMethods.SetWindowLongPtr(
            windowHandle,
            NativeMethods.GWL_HWNDPARENT,
            progman);

        // 3. Posicionar a janela na base da Z-order (HWND_BOTTOM)
        NativeMethods.SetWindowPos(
            windowHandle,
            NativeMethods.HWND_BOTTOM,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);

        Log($"Ancoragem concluída com sucesso para 0x{windowHandle:X}.");
        return true;
    }

    public bool UnanchorFromDesktop(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        NativeMethods.SetWindowLongPtr(windowHandle, NativeMethods.GWL_HWNDPARENT, IntPtr.Zero);
        Log($"Janela 0x{windowHandle:X} desancorada com sucesso.");
        return true;
    }

    private IntPtr GetShellDesktopHandle()
    {
        if (_shellDesktopHandle != IntPtr.Zero)
        {
            return _shellDesktopHandle;
        }

        IntPtr progman = NativeMethods.FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            progman = NativeMethods.GetShellWindow();
            Log($"Progman obtido via GetShellWindow: 0x{progman:X}");
        }
        else
        {
            Log($"Progman localizado via FindWindow: 0x{progman:X}");
        }

        _shellDesktopHandle = progman;
        return _shellDesktopHandle;
    }

    private static void Log(string message)
    {
        try
        {
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DeskQuadra");
            Directory.CreateDirectory(appData);
            string logFile = Path.Combine(appData, "anchor.log");
            File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Silencioso em caso de restrição de I/O
        }
    }
}
