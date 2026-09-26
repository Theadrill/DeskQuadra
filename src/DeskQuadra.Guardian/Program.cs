using System.Diagnostics;
using System.Runtime.InteropServices;
using DeskQuadra.Infrastructure.WindowsShell.Services;

namespace DeskQuadra.Guardian;

internal static class Program
{
    private const uint SYNCHRONIZE = 0x00100000;
    private const uint INFINITE = 0xFFFFFFFF;
    private const uint GENERIC_ALL = 0x10000000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern IntPtr OpenWindowStation(string lpszWinSta, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll")]
    private static extern bool SetProcessWindowStation(IntPtr hWinSta);

    [DllImport("user32.dll")]
    private static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll")]
    private static extern bool SetThreadDesktop(IntPtr hDesktop);

    static void Main(string[] args)
    {
        int parentPid = 0;
        string fullArgs = string.Join(" ", args);
        foreach (var part in fullArgs.Split([' ', '='], StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part, out int parsedPid) && parsedPid > 0)
            {
                parentPid = parsedPid;
                break;
            }
        }

        if (parentPid == 0)
        {
            var active = Process.GetProcessesByName("DeskQuadra.UI.Wpf");
            if (active.Length > 0)
            {
                parentPid = active[0].Id;
            }
        }

        if (parentPid > 0)
        {
            IntPtr hProcess = OpenProcess(SYNCHRONIZE, false, parentPid);

            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    WaitForSingleObject(hProcess, INFINITE);
                }
                finally
                {
                    CloseHandle(hProcess);
                }
            }
            else
            {
                while (true)
                {
                    Thread.Sleep(500);
                    var active = Process.GetProcessesByName("DeskQuadra.UI.Wpf");
                    if (active.Length == 0)
                    {
                        break;
                    }
                }
            }
        }

        Thread.Sleep(150);

        try
        {
            var activeInstances = Process.GetProcessesByName("DeskQuadra.UI.Wpf");
            if (activeInstances.Length == 0)
            {
                var iconService = new NativeDesktopIconService();
                iconService.ShowDesktopIcons();
            }
        }
        catch
        {
            var iconService = new NativeDesktopIconService();
            iconService.ShowDesktopIcons();
        }
    }
}
