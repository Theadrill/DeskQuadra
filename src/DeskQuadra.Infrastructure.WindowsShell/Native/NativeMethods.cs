using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;

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
    public const int SC_MASK = 0xFFF0;
    public const int WM_DISPLAYCHANGE = 0x007E;

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
    public const int WM_NCHITTEST = 0x0084;
    public const int HTCAPTION = 0x0002;
    public const int HTTRANSPARENT = -1;

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

    // E1-infra: fonte canônica do handle Progman/Shell — FindWindow("Progman", null),
    // se Zero → GetShellWindow. Pode retornar Zero — chamadores já tratam.
    public static IntPtr GetProgmanHandle() => ResolveProgmanHandle(() => FindWindow("Progman", null), GetShellWindow);

    // E1-infra: decisão pura/testável com short-circuit idêntico ao original —
    // se o FindWindow resolveu, retorna sem chamar o fallback.
    public static IntPtr ResolveProgmanHandle(Func<IntPtr> findProgman, Func<IntPtr> getShellWindow)
    {
        IntPtr progman = findProgman();
        return progman != IntPtr.Zero ? progman : getShellWindow();
    }

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
    public const int VK_CONTROL = 0x11;
    public const int VK_SHIFT = 0x10;
    public const uint GA_ROOT = 2;

    [DllImport("user32.dll")]
    public static extern short GetKeyState(int nVirtKey);

    // T2 terceiros: Shift pressionado no momento da chamada (verbos estendidos
    // CMF_EXTENDEDVERBS só com Shift, §1). Best-effort: exceção = false.
    public static bool IsShiftPressed()
    {
        try
        {
            return (GetKeyState(VK_SHIFT) & 0x8000) != 0;
        }
        catch
        {
            return false;
        }
    }

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

    // ESC fecha o menu dual: hotkey com escopo estrito ao popup (registra ao
    // abrir, desregistra em todos os fechamentos). Dono é o HWND da janela de
    // seleção; o hook de WM_HOTKEY vive no HwndSource dela (App.xaml.cs).
    // DllImport no padrão do repo (Guardian usa a mesma assinatura); SetLastError
    // para diagnóstico, sem exceção (tudo best-effort no chamador).
    public const int WM_HOTKEY = 0x0312;
    public const uint VK_ESCAPE = 0x1B;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Republicação do ESC (padrão par-gesto): o ESC reservado pelo hotkey seria
    // engolido; reenviar via SendInput entrega ao destino original do foco.
    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBOARD_INPUT
    {
        public uint type;
        public KEYBDINPUT ki;
    }

    // Mesmo export SendInput do mouse, só que com payload de teclado
    // (EntryPoint explícito, padrão do repo quando o nome gerenciado difere).
    [DllImport("user32.dll", EntryPoint = "SendInput", SetLastError = true)]
    public static extern uint SendKeyboardInput(uint nInputs, [In] KEYBOARD_INPUT[] pInputs, int cbSize);

    // Par ESC down+up, best-effort e silencioso (nunca lança no hook do WPF).
    public static void RepublishEscapeKey()
    {
        try
        {
            var inputs = new KEYBOARD_INPUT[2];
            inputs[0] = new KEYBOARD_INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT { wVk = (ushort)VK_ESCAPE },
            };
            inputs[1] = new KEYBOARD_INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT { wVk = (ushort)VK_ESCAPE, dwFlags = KEYEVENTF_KEYUP },
            };
            SendKeyboardInput((uint)inputs.Length, inputs, Marshal.SizeOf<KEYBOARD_INPUT>());
        }
        catch
        {
            // Silencioso, padrão do projeto: sem o par, o ESC só fecha o popup.
        }
    }

    // E12-UI: atalho .lnk — destino via IShellLinkW::GetPath (COM, thread STA da UI).
    // Dono da memória: chamador aloca o StringBuilder, o shell preenche; COM liberado no finally.
    internal const uint SLGP_UNCPRIORITY = 0x0002;
    internal const uint STGM_READ = 0;
    internal const int MaxPathChars = 260;

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellLinkW
    {
        // GetPath é o primeiro slot após IUnknown — declaração mínima basta para este uso.
        // Marshalling explícito Unicode (LPWStr), sem CharSet.Auto.
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, nint pfd, uint fFlags);
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLink
    {
    }

    [ComImport]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
    }

    // Excluir-via-Shell: SHFileOperationW (FO_DELETE) com flags mínimas.
    // Lixeira = FOF_ALLOWUNDO (com undo); permanente = sem o flag (sem undo).
    // FOF_NOCONFIRMATION + FOF_NOERRORUI + FOF_SILENT: sem 2º prompt do Explorer
    // (o app já confirma no DarkDialog) e sem UI de erro/progresso.
    internal const uint FO_DELETE = 0x0003;
    internal const ushort FOF_ALLOWUNDO = 0x0040;
    internal const ushort FOF_NOCONFIRMATION = 0x0010;
    internal const ushort FOF_NOERRORUI = 0x0400;
    internal const ushort FOF_SILENT = 0x0004;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    // E12-UI: resolve o destino do .lnk ou retorna null (best-effort, sem exceção).
    // Nulo = mantém o comportamento atual no chamador (seleciona o próprio .lnk).
    internal static string? TryResolveShortcutTarget(string? lnkPath)
    {
        // Só atalho com extensão .lnk; resto não resolve.
        if (string.IsNullOrWhiteSpace(lnkPath))
        {
            return null;
        }

        if (!lnkPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            // Atalho inexistente = quebrado, volta ao comportamento atual.
            if (!File.Exists(lnkPath))
            {
                return null;
            }

            object? comObj = null;
            try
            {
                comObj = new ShellLink();
                var persist = (IPersistFile)comObj;
                persist.Load(lnkPath, STGM_READ);
                var link = (IShellLinkW)comObj;
                var sb = new StringBuilder(MaxPathChars);
                link.GetPath(sb, sb.Capacity, IntPtr.Zero, SLGP_UNCPRIORITY);
                string target = sb.ToString();
                if (string.IsNullOrWhiteSpace(target))
                {
                    return null;
                }

                // Destino quebrado = null para o chamador selecionar o próprio .lnk.
                if (!File.Exists(target) && !Directory.Exists(target))
                {
                    return null;
                }

                return target;
            }
            finally
            {
                // Libera o RCW do COM; sem isso vaza referência do ShellLink.
                if (comObj is not null && Marshal.IsComObject(comObj))
                {
                    Marshal.ReleaseComObject(comObj);
                }
            }
        }
        catch
        {
            // Silencioso, padrão do projeto: falha de COM/IO nunca quebra o "abrir local".
            return null;
        }
    }
}
