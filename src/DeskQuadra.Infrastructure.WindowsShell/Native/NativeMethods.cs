using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("DeskQuadra.UI.Wpf")]
// D12: libera o helper puro/testável ao projeto de teste sem referência nova
// (UI.Wpf.Tests já enxerga WindowsShell por transitividade via UI.Wpf).
[assembly: InternalsVisibleTo("DeskQuadra.UI.Wpf.Tests")]

namespace DeskQuadra.Infrastructure.WindowsShell.Native;

/// <summary>
/// Declarações P/Invoke nativas do Windows rigorosamente tipadas conforme as diretrizes do dotnet-pinvoke.
/// </summary>
internal static class NativeMethods
{
    public const int GWL_EXSTYLE = -20;
    public const int GWL_STYLE = -16;
    public const int GWL_HWNDPARENT = -8;

    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    public const int WS_CHILD = 0x40000000;
    public const int WS_POPUP = unchecked((int)0x80000000);

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_HIDEWINDOW = 0x0080;

    public const int WM_WINDOWPOSCHANGING = 0x0046;
    public const int WM_MOVING = 0x0216;
    public const int WM_SYSCOMMAND = 0x0112;
    public const int SC_MINIMIZE = 0xF020;

    public enum INPUT_MESSAGE_DEVICE_TYPE
    {
        IMDT_UNAVAILABLE = 0x00000000,
        IMDT_KEYBOARD = 0x00000001,
        IMDT_MOUSE = 0x00000002,
        IMDT_TOUCH = 0x00000004,
        IMDT_PEN = 0x00000008,
        IMDT_TOUCHPAD = 0x00000010,
        IMDT_HID = 0x00000020
    }

    public enum INPUT_MESSAGE_ORIGIN_ID
    {
        IMO_UNAVAILABLE = 0x00000000,
        IMO_HARDWARE = 0x00000001,
        IMO_INJECTED = 0x00000002,
        IMO_SYSTEM = 0x00000004
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT_MESSAGE_SOURCE
    {
        public INPUT_MESSAGE_DEVICE_TYPE deviceType;
        public INPUT_MESSAGE_ORIGIN_ID originId;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCurrentInputMessageSource(out INPUT_MESSAGE_SOURCE inputMessageSource);

    [DllImport("user32.dll")]
    public static extern IntPtr GetMessageExtraInfo();

    public static bool IsCurrentMessageFromTouch()
    {
        // Prioridade máxima: verifica a assinatura Win32 padrão de mensagens sintetizadas a partir de toque
        // Bit 7 (0x80) diferencia tela de toque física capacitiva (0xFF515780) de caneta/touchpad (0xFF515700)
        long extra = GetMessageExtraInfo().ToInt64();
        if ((extra & 0xFFFFFF80L) == 0xFF515780L)
        {
            return true;
        }

        // Se a mensagem for diretamente WM_POINTER/WM_TOUCH pura
        if (GetCurrentInputMessageSource(out var source))
        {
            if (source.deviceType == INPUT_MESSAGE_DEVICE_TYPE.IMDT_TOUCH)
            {
                return true;
            }
        }

        return false;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    public static readonly IntPtr HWND_BOTTOM = new(1);

    public const uint WM_SPAWN_WORKER = 0x052C;
    public const uint SMTO_NORMAL = 0x0000;

    public const int WM_NCLBUTTONDOWN = 0x00A1;
    public const int HTCAPTION = 0x0002;

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string? className, string? windowTitle);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        UIntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out UIntPtr lpdwResult);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8)
        {
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        }
        return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    /// <summary>
    /// Aplica click-through + sem ativação + toolwindow (invisível a mouse/toque/foco,
    /// fora da Taskbar/Alt+Tab) e força refresh do frame. Flags idênticas ao bloco
    /// original de <c>DragPreviewWindow</c>.
    /// </summary>
    public static void ApplyClickThroughNoActivate(IntPtr hWnd)
    {
        int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        SetWindowLongPtr(
            hWnd,
            GWL_EXSTYLE,
            new IntPtr(exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));
        SetWindowPos(
            hWnd,
            IntPtr.Zero,
            0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED | SWP_NOACTIVATE);
    }

    /// <summary>
    /// Aplica apenas <c>WS_EX_TOOLWINDOW</c> (fora da Taskbar/Alt+Tab) e força refresh
    /// do frame. Não toca em parent nem Z-order (NOZORDER).
    /// </summary>
    public static void ApplyToolWindow(IntPtr hWnd)
    {
        int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        SetWindowLongPtr(
            hWnd,
            GWL_EXSTYLE,
            new IntPtr(exStyle | WS_EX_TOOLWINDOW));
        SetWindowPos(
            hWnd,
            IntPtr.Zero,
            0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED | SWP_NOACTIVATE);
    }

    // D12: comparação pura de PID, sem Win32 — testável via xUnit.
    public static bool IsOwnProcess(uint pid, uint currentPid) => pid == currentPid;

    // D12: hwnd pertence ao processo atual? Zero → false sem P/Invoke;
    // senão resolve o PID via GetWindowThreadProcessId e compara com o PID atual.
    public static bool IsOwnProcessWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(hwnd, out uint pid);
        return IsOwnProcess(pid, (uint)Environment.ProcessId);
    }

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    // ChordDiag TEMPORÁRIO (remover se ficar sem uso após deletar Services/ChordDiagLog.cs):
    // mínimo para o instantâneo foco/captura do diagnóstico do chord. GetClassName já existia.
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern IntPtr GetCapture();

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        IntPtr hToken,
        out IntPtr ppszPath);

    public const uint SHGFI_ICON = 0x000000100;
    public const uint SHGFI_LARGEICON = 0x000000000;
    public const uint SHGFI_SMALLICON = 0x000000001;
    public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    public const int WH_MOUSE_LL = 14;
    public const int WM_MOUSEMOVE = 0x0200;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP = 0x0205;
    public const int VK_RBUTTON = 0x02;
    public const uint GA_ROOT = 2;

    [DllImport("user32.dll")]
    public static extern short GetKeyState(int nVirtKey);

    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT Point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    // Par-gesto R (fix clique morto): eventos republicados via SendInput voltam
    // pelo hook marcados como injetados — o hook repassa sem processar.
    public const uint LLMHF_INJECTED = 0x00000001;

    // Marca própria no dwExtraInfo da republicação (cinto + suspensório junto
    // ao LLMHF_INJECTED, que o sistema já marca em tudo que sai do SendInput).
    public static readonly IntPtr MouseChordRepublishTag = new(unchecked((nint)(int)0xD35C4AD1));

    // Decisão pura do par-gesto (testável via xUnit, sem hook): injetado se o
    // sistema marcou (LLMHF_INJECTED) ou se carrega a nossa marca de republicação.
    public static bool IsInjectedMouseEvent(uint flags, IntPtr dwExtraInfo)
        => (flags & LLMHF_INJECTED) != 0 || dwExtraInfo == MouseChordRepublishTag;

    // Limiar puro do gesto (testável via xUnit): mesma distância de 15px que o
    // hook já usava para confirmar o arrasto.
    public const double DragThresholdPx = 15;
    public static bool ShouldStartDrag(double distance) => distance >= DragThresholdPx;

    public const uint INPUT_MOUSE = 0;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    // Detecção Auto de densidade (Fatia 2, mínimo): só GetSystemMetrics, sem hook/timer.
    // SM_DIGITIZER=94 (NID_INTEGRATED/EXTERNAL_TOUCH), SM_MAXIMUMTOUCHES=95.
    public const int SM_DIGITIZER = 94;
    public const int SM_MAXIMUMTOUCHES = 95;

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    /// <summary>
    /// Leitura sob demanda do hardware touch. Best-effort: falha =&gt; false (Normal).
    /// Decisão pura vive em <c>DeskQuadra.Core.DensityResolver</c> (testável).
    /// </summary>
    public static bool IsTouchHardwarePresent()
    {
        try
        {
            int digitizer = GetSystemMetrics(SM_DIGITIZER);
            int maxTouches = GetSystemMetrics(SM_MAXIMUMTOUCHES);
            return DeskQuadra.Core.DensityResolver.HasTouchHardware(digitizer, maxTouches);
        }
        catch
        {
            return false;
        }
    }

    // Menu clássico do fundo do desktop (fallback do Cancelar): mínimo IContextMenu (1).
    // IContextMenu2/3 só seriam necessários para repassar WM_INITMENUPOPUP/draw de
    // extensões via HandleMenuMsg — sem defeito observado no menu do fundo, adiado.
    public static readonly Guid IID_IContextMenu = new("000214e4-0000-0000-c000-000000000046");

    public const uint CMF_NORMAL = 0x00000000;
    public const uint TPM_LEFTALIGN = 0x0000;
    public const uint TPM_TOPALIGN = 0x0000;
    public const uint TPM_LEFTBUTTON = 0x0000;
    public const uint TPM_RETURNCMD = 0x0100;

    // Receita MSDN p/ menus com TPM_RETURNCMD (ESC funcionar): SetForegroundWindow
    // no dono antes do TrackPopupMenuEx + PostMessage(WM_NULL) depois do retorno.
    public const int WM_NULL = 0x0000;

    public const uint ContextMenuIdFirst = 1; // Nunca 0: TrackPopupMenuEx devolve 0 em cancelar/erro.
    public const uint ContextMenuIdLast = 0x7FFF;

    public const int SW_SHOWNORMAL = 1;

    [StructLayout(LayoutKind.Sequential)]
    public struct CMINVOKECOMMANDINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
    }

    // Vtable na ordem nativa até GetUIObjectOf (8º método); o chamado é o
    // CreateViewObject (6º) — menu DO FUNDO da pasta. GetUIObjectOf (8º) com
    // cidl=0 devolve o menu DA PASTA (Abrir, Fixar...), não o do fundo.
    [ComImport]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellFolder
    {
        [PreserveSig]
        int ParseDisplayName(
            IntPtr hwnd,
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName,
            ref uint pchEaten,
            out IntPtr ppidl,
            ref uint pdwAttributes);

        [PreserveSig]
        int EnumObjects(IntPtr hwnd, int grfFlags, out IntPtr ppenumIDList);

        [PreserveSig]
        int BindToObject(
            IntPtr pidl,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out object ppv);

        [PreserveSig]
        int BindToStorage(
            IntPtr pidl,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out object ppv);

        [PreserveSig]
        int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);

        [PreserveSig]
        int CreateViewObject(
            IntPtr hwndOwner,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IContextMenu ppv);

        [PreserveSig]
        int GetAttributesOf(
            uint cidl,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] apidl,
            ref uint rgfInOut);

        // Mantido só por completude da vtable (8º slot); NÃO usar com cidl=0
        // para o menu do fundo — isso devolve o menu DA PASTA, não o do fundo.
        [PreserveSig]
        int GetUIObjectOf(
            IntPtr hwndOwner,
            uint cidl,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[]? apidl,
            ref Guid riid,
            IntPtr rgfReserved,
            [MarshalAs(UnmanagedType.Interface)] out IContextMenu ppv);
    }

    [ComImport]
    [Guid("000214e4-0000-0000-c000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

        [PreserveSig]
        int InvokeCommand(ref CMINVOKECOMMANDINFO pici);

        [PreserveSig]
        int GetCommandString(IntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
    }

    [DllImport("shell32.dll")]
    public static extern int SHGetDesktopFolder([MarshalAs(UnmanagedType.Interface)] out IShellFolder ppshf);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CreatePopupMenu();

    // Com TPM_RETURNCMD devolve o ID do comando (0 = cancelado/erro); sem ela seria BOOL.
    [DllImport("user32.dll", SetLastError = true)]
    public static extern int TrackPopupMenuEx(IntPtr hmenu, uint uFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyMenu(IntPtr hMenu);

    // Receita MSDN p/ menus com TPM_RETURNCMD: dono em foreground antes do
    // TrackPopupMenuEx (sem isso o ESC é ignorado) e WM_NULL depois do retorno
    // para destravar o estado modal do menu.
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    // Comparação pura: só ID != 0 vira InvokeCommand (testável via xUnit, sem HWND).
    public static bool IsContextMenuCommand(int commandId) => commandId != 0;

    // Parsing puro: offset relativo a idCmdFirst para lpVerb (MAKEINTRESOURCE).
    public static int ToContextMenuVerbOffset(int commandId, uint idCmdFirst) => commandId - (int)idCmdFirst;
}
