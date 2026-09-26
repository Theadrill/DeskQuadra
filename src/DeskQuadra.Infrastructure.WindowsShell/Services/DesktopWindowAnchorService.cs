using System.Runtime.InteropServices;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Serviço de infraestrutura Win32 responsável por ancorar janelas de Quadras diretamente
/// na camada de papel de parede do Windows (WorkerW/Progman), tornando-as imunes ao Win + D.
/// </summary>
public sealed class DesktopWindowAnchorService : IWindowAnchorService
{
    private IntPtr _desktopContainerHandle = IntPtr.Zero;

    public bool AnchorToDesktop(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        IntPtr desktopContainer = GetDesktopContainerHandle();
        if (desktopContainer == IntPtr.Zero)
        {
            return false;
        }

        // 1. Aplicar estilo estendido WS_EX_TOOLWINDOW para não poluir a barra de tarefas nem o Alt+Tab
        int exStyle = NativeMethods.GetWindowLong(windowHandle, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongPtr(
            windowHandle,
            NativeMethods.GWL_EXSTYLE,
            new IntPtr(exStyle | NativeMethods.WS_EX_TOOLWINDOW));

        // 2. Ancorar como janela filha da camada de fundo do desktop
        NativeMethods.SetParent(windowHandle, desktopContainer);
        return true;
    }

    public bool UnanchorFromDesktop(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        NativeMethods.SetParent(windowHandle, IntPtr.Zero);
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
            return IntPtr.Zero;
        }

        // Dispara mensagem 0x052C ao Explorer para que ele desdobre a árvore de renderização do desktop
        NativeMethods.SendMessageTimeout(
            progman,
            NativeMethods.WM_SPAWN_WORKER,
            new UIntPtr(0x0000000D),
            IntPtr.Zero,
            NativeMethods.SMTO_NORMAL,
            1000,
            out _);

        IntPtr targetWorkerW = IntPtr.Zero;

        // Enumera as janelas de topo para localizar o WorkerW logo atrás de SHELLDLL_DefView
        NativeMethods.EnumWindows((topHandle, _) =>
        {
            IntPtr defView = NativeMethods.FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView != IntPtr.Zero)
            {
                targetWorkerW = NativeMethods.FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
            }
            return true;
        }, IntPtr.Zero);

        // Fallback: se nenhum WorkerW irmão foi encontrado, buscar WorkerW avulso ou usar o próprio Progman
        if (targetWorkerW == IntPtr.Zero)
        {
            targetWorkerW = NativeMethods.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "WorkerW", null);
        }

        _desktopContainerHandle = targetWorkerW != IntPtr.Zero ? targetWorkerW : progman;
        return _desktopContainerHandle;
    }
}
