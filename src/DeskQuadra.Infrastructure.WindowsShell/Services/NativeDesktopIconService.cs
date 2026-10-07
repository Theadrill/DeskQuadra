using System.Runtime.InteropServices;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Serviço de controle de visibilidade da camada nativa de ícones do Windows Desktop (Progman / WorkerW / SysListView32).
/// Garante restauração automática dos ícones caso o aplicativo seja encerrado ou descartado.
/// </summary>
public sealed class NativeDesktopIconService : INativeDesktopIconService
{
    private IntPtr _cachedListViewHandle = IntPtr.Zero;
    private IntPtr _cachedShellViewHandle = IntPtr.Zero;
    private bool _areIconsHidden;
    private bool _isDisposed;

    public bool AreIconsHidden => _areIconsHidden;

    public bool HideDesktopIcons()
    {
        IntPtr hListView = GetDesktopListViewHandle();
        if (hListView == IntPtr.Zero)
        {
            return false;
        }

        bool result = NativeMethods.ShowWindow(hListView, NativeMethods.SW_HIDE);
        _areIconsHidden = true;
        return true;
    }

    public bool ShowDesktopIcons()
    {
        IntPtr hListView = GetDesktopListViewHandle();
        if (hListView == IntPtr.Zero && _cachedShellViewHandle == IntPtr.Zero)
        {
            return false;
        }

        if (_cachedShellViewHandle != IntPtr.Zero)
        {
            NativeMethods.ShowWindow(_cachedShellViewHandle, NativeMethods.SW_SHOW);
        }

        if (hListView != IntPtr.Zero)
        {
            NativeMethods.ShowWindow(hListView, NativeMethods.SW_SHOW);
        }

        _areIconsHidden = false;
        return true;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // Regra Pétrea: Nunca deixar o desktop do usuário invisível ao encerrar
        if (_areIconsHidden)
        {
            ShowDesktopIcons();
        }
    }

    /// <summary>
    /// Ocultação Win32 ultra-precoce (< 2ms) para chamada no ponto de entrada Main()
    /// antes do pipeline WPF/XAML iniciar (BRAINSTORMING Seção 22: Zero-Flicker Startup).
    /// </summary>
    public static bool QuickHideDesktopIcons()
    {
        IntPtr hListView = FindDesktopListViewHandle(out _);
        if (hListView == IntPtr.Zero)
        {
            return false;
        }

        return NativeMethods.ShowWindow(hListView, NativeMethods.SW_HIDE);
    }

    public static IntPtr FindDesktopListViewHandle() => FindDesktopListViewHandle(out _);

    public static IntPtr FindDesktopListViewHandle(out IntPtr shellViewHandle)
    {
        shellViewHandle = IntPtr.Zero;
        IntPtr shellView = IntPtr.Zero;

        // 1. Tenta localizar SHELLDLL_DefView no Progman
        IntPtr progman = NativeMethods.GetProgmanHandle();

        if (progman != IntPtr.Zero)
        {
            shellView = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        }

        // 2. Se não estiver no Progman (Windows 11 com wallpaper ativo), busca nas instâncias de WorkerW
        if (shellView == IntPtr.Zero)
        {
            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                IntPtr sv = NativeMethods.FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (sv != IntPtr.Zero)
                {
                    shellView = sv;
                    return false; // Interrompe enumeração
                }
                return true;
            }, IntPtr.Zero);
        }

        if (shellView == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        shellViewHandle = shellView;

        // 3. Dentro do SHELLDLL_DefView, busca SysListView32 (ícones do desktop) ou DirectUIHWND
        IntPtr listView = NativeMethods.FindWindowEx(shellView, IntPtr.Zero, "SysListView32", null);
        if (listView == IntPtr.Zero)
        {
            listView = NativeMethods.FindWindowEx(shellView, IntPtr.Zero, "DirectUIHWND", null);
        }

        return listView != IntPtr.Zero ? listView : shellView;
    }

    private IntPtr GetDesktopListViewHandle()
    {
        if (_cachedListViewHandle != IntPtr.Zero && NativeMethods.IsWindowVisible(_cachedListViewHandle))
        {
            return _cachedListViewHandle;
        }

        IntPtr listView = FindDesktopListViewHandle(out IntPtr shellView);
        _cachedShellViewHandle = shellView;
        _cachedListViewHandle = listView;
        return _cachedListViewHandle;
    }
}
