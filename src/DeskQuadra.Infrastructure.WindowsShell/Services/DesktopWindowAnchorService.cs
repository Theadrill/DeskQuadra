using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Serviço de infraestrutura Win32 responsável por ancorar janelas de Quadras diretamente
/// na camada de fundo do desktop do Windows 11 (WorkerW/Progman/DefView Host),
/// tornando-as imunes ao Win + D e persistentes.
/// </summary>
public sealed class DesktopWindowAnchorService : IWindowAnchorService
{
    private IntPtr _desktopContainerHandle = IntPtr.Zero;

    public bool AnchorToDesktop(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            Log("AnchorToDesktop chamado com windowHandle nulo.");
            return false;
        }

        IntPtr desktopContainer = GetDesktopContainerHandle();
        if (desktopContainer == IntPtr.Zero)
        {
            Log("Falha: nenhum container de desktop (WorkerW/Progman) foi localizado.");
            return false;
        }

        Log($"Iniciando ancoragem da janela 0x{windowHandle:X} no container 0x{desktopContainer:X}...");

        // 1. Aplicar estilo estendido WS_EX_TOOLWINDOW para não poluir Taskbar nem Alt+Tab
        int exStyle = NativeMethods.GetWindowLong(windowHandle, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongPtr(
            windowHandle,
            NativeMethods.GWL_EXSTYLE,
            new IntPtr(exStyle | NativeMethods.WS_EX_TOOLWINDOW));

        // 2. Ajustar estilo padrão da janela: remover WS_POPUP e adicionar WS_CHILD.
        // Essencial: o Windows Shell só minimiza janelas Top-Level/Popup no Win + D. Janelas com WS_CHILD são ignoradas.
        int style = NativeMethods.GetWindowLong(windowHandle, NativeMethods.GWL_STYLE);
        int newStyle = (style & ~NativeMethods.WS_POPUP) | NativeMethods.WS_CHILD;
        NativeMethods.SetWindowLongPtr(
            windowHandle,
            NativeMethods.GWL_STYLE,
            new IntPtr(newStyle));

        // 3. Acoplar como janela filha direta do container de desktop
        IntPtr previousParent = NativeMethods.SetParent(windowHandle, desktopContainer);
        int lastError = Marshal.GetLastWin32Error();

        // 4. Notificar a pilha de janelas que o frame mudou
        NativeMethods.SetWindowPos(
            windowHandle,
            IntPtr.Zero,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);

        Log($"Ancoragem concluída. Parent anterior: 0x{previousParent:X}, Win32Error: {lastError}.");
        return true;
    }

    public bool UnanchorFromDesktop(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        NativeMethods.SetParent(windowHandle, IntPtr.Zero);
        Log($"Janela 0x{windowHandle:X} desancorada com sucesso.");
        return true;
    }

    private IntPtr GetDesktopContainerHandle()
    {
        if (_desktopContainerHandle != IntPtr.Zero)
        {
            return _desktopContainerHandle;
        }

        IntPtr progman = NativeMethods.FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            progman = NativeMethods.GetShellWindow();
            Log($"FindWindow(Progman) nulo; obtido via GetShellWindow: 0x{progman:X}");
        }
        else
        {
            Log($"Progman localizado: 0x{progman:X}");
        }

        if (progman != IntPtr.Zero)
        {
            // Dispara 0x052C com wParam=0 e wParam=0xD para suportar todas as compilações do Windows 10 e 11
            NativeMethods.SendMessageTimeout(
                progman,
                NativeMethods.WM_SPAWN_WORKER,
                new UIntPtr(0x0000000D),
                IntPtr.Zero,
                NativeMethods.SMTO_NORMAL,
                1000,
                out _);

            NativeMethods.SendMessageTimeout(
                progman,
                NativeMethods.WM_SPAWN_WORKER,
                UIntPtr.Zero,
                IntPtr.Zero,
                NativeMethods.SMTO_NORMAL,
                1000,
                out _);
        }

        IntPtr wallpaperWorkerW = IntPtr.Zero;
        IntPtr defViewParentWindow = IntPtr.Zero;

        // Enumera todas as janelas de topo para descobrir a topologia atual do Explorer
        NativeMethods.EnumWindows((topHandle, _) =>
        {
            IntPtr defView = NativeMethods.FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView != IntPtr.Zero)
            {
                defViewParentWindow = topHandle;
                // No Windows padrão, o WorkerW do papel de parede fica imediatamente atrás da janela do DefView
                wallpaperWorkerW = NativeMethods.FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
            }
            return true;
        }, IntPtr.Zero);

        // No Windows 11 24H2+, o WorkerW pode ser um filho direto do Progman
        if (wallpaperWorkerW == IntPtr.Zero && progman != IntPtr.Zero)
        {
            wallpaperWorkerW = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
            if (wallpaperWorkerW != IntPtr.Zero)
            {
                Log($"Windows 11 24H2 detectado: WorkerW filho do Progman: 0x{wallpaperWorkerW:X}");
            }
        }

        // Se o WorkerW de papel de parede não foi encontrado, utiliza a janela que hospeda os ícones (DefView Host)
        if (wallpaperWorkerW == IntPtr.Zero && defViewParentWindow != IntPtr.Zero)
        {
            wallpaperWorkerW = defViewParentWindow;
            Log($"Usando janela host de SHELLDLL_DefView como container: 0x{wallpaperWorkerW:X}");
        }

        // Fallback final: o próprio Progman
        _desktopContainerHandle = wallpaperWorkerW != IntPtr.Zero ? wallpaperWorkerW : progman;
        Log($"Container de desktop final selecionado: 0x{_desktopContainerHandle:X}");
        return _desktopContainerHandle;
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
