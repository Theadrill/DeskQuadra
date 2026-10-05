using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.UI.Wpf.Services;

/// <summary>
/// Work area (rcWork, sem barra de tarefas) do monitor da janela, em DIPs.
/// Extração da 2ª repetição do molde (arraste da Quadra + SettingsWindow):
/// MonitorFromWindow + GetMonitorInfo + MapPhysicalToDip, com fallback
/// para <see cref="SystemParameters.WorkArea"/> (primário).
/// Deve ser chamado na thread da UI, como o molde original.
/// </summary>
internal static class MonitorWorkArea
{
    public static Rect2D GetFor(Window window)
    {
        var helper = new WindowInteropHelper(window);
        IntPtr hMonitor = NativeMethods.MonitorFromWindow(helper.Handle, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new NativeMethods.MONITORINFO();
        monitorInfo.cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>();

        if (hMonitor != IntPtr.Zero && NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            var (waLeft, waTop, waWidth, waHeight) = DpiHelper.MapPhysicalToDip(
                window,
                monitorInfo.rcWork.Left, monitorInfo.rcWork.Top,
                monitorInfo.rcWork.Right - monitorInfo.rcWork.Left,
                monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top);
            return new Rect2D(waLeft, waTop, waWidth, waHeight);
        }

        return new Rect2D(
            SystemParameters.WorkArea.Left,
            SystemParameters.WorkArea.Top,
            SystemParameters.WorkArea.Width,
            SystemParameters.WorkArea.Height);
    }
}
