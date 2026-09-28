using System.Runtime.InteropServices;
using System.Text;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Serviço de captura de gestos de desenho com botão direito na Área de Trabalho do Windows (WH_MOUSE_LL).
/// Garante que cliques simples (<15px) sejam repassados integralmente ao Shell para abrir o menu do Windows,
/// enquanto arrastos maiores que 15px desenham o retângulo de criação da Quadra e neutralizam o menu nativo.
/// </summary>
public sealed class DesktopDrawingService : IDesktopDrawingService
{
    private readonly NativeMethods.LowLevelMouseProc _hookProc;
    private IntPtr _hookHandle = IntPtr.Zero;
    private bool _isDownOnDesktop;
    private bool _isDragging;
    private NativeMethods.POINT _startPt;
    private bool _isDisposed;

    public event EventHandler<Rect2D>? DrawingProgress;
    public event EventHandler<Rect2D>? DrawingCompleted;
    public event EventHandler? DrawingCancelled;
    public event EventHandler? GlobalLeftClick;

    public bool IsDrawingActive => _isDownOnDesktop && _isDragging;

    public DesktopDrawingService()
    {
        // Mantém a referência do delegado viva no heap para evitar coleta pelo Garbage Collector
        _hookProc = HookCallback;
    }

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        IntPtr hModule = NativeMethods.GetModuleHandle(null);
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _hookProc, hModule, 0);
    }

    public void Stop()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        _isDownOnDesktop = false;
        _isDragging = false;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();

            if (msg == NativeMethods.WM_RBUTTONDOWN)
            {
                GlobalLeftClick?.Invoke(this, EventArgs.Empty);
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                IntPtr targetWindow = NativeMethods.WindowFromPoint(hookStruct.pt);

                if (IsDesktopWindow(targetWindow))
                {
                    _isDownOnDesktop = true;
                    _isDragging = false;
                    _startPt = hookStruct.pt;
                    ChordDiagLog.Log($"RDown onDesktop -> Down repassou hwnd=0x{targetWindow:X} x={hookStruct.pt.X} y={hookStruct.pt.Y}"); // ChordDiag
                }
                else
                {
                    _isDownOnDesktop = false;
                    _isDragging = false;
                    ChordDiagLog.Log($"RDown offDesktop repassou hwnd=0x{targetWindow:X}"); // ChordDiag
                }
            }
            else if (msg == NativeMethods.WM_MOUSEMOVE)
            {
                if (_isDownOnDesktop)
                {
                    // Se o botão direito não estiver mais pressionado fisicamente (ex: solto durante Alt+Tab), cancela
                    if ((NativeMethods.GetKeyState(NativeMethods.VK_RBUTTON) & 0x8000) == 0)
                    {
                        ChordDiagLog.Log($"Move rbtn-solto dragging={_isDragging} -> cancel repassou"); // ChordDiag
                        _isDownOnDesktop = false;
                        if (_isDragging)
                        {
                            _isDragging = false;
                            DrawingCancelled?.Invoke(this, EventArgs.Empty);
                        }
                        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
                    }

                    var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                    double dx = hookStruct.pt.X - _startPt.X;
                    double dy = hookStruct.pt.Y - _startPt.Y;
                    double distance = Math.Sqrt(dx * dx + dy * dy);

                    if (!_isDragging && distance >= 15)
                    {
                        _isDragging = true;
                        ChordDiagLog.Log($"Move -> Dragging repassou d={distance:F0}"); // ChordDiag
                    }

                    if (_isDragging)
                    {
                        double left = Math.Min(_startPt.X, hookStruct.pt.X);
                        double top = Math.Min(_startPt.Y, hookStruct.pt.Y);
                        double width = Math.Abs(hookStruct.pt.X - _startPt.X);
                        double height = Math.Abs(hookStruct.pt.Y - _startPt.Y);

                        DrawingProgress?.Invoke(this, new Rect2D(left, top, width, height));
                    }
                }
            }
            else if (msg == NativeMethods.WM_RBUTTONUP)
            {
                if (_isDownOnDesktop)
                {
                    var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                    bool wasDragging = _isDragging;

                    _isDownOnDesktop = false;
                    _isDragging = false;

                    if (wasDragging)
                    {
                        double left = Math.Min(_startPt.X, hookStruct.pt.X);
                        double top = Math.Min(_startPt.Y, hookStruct.pt.Y);
                        double width = Math.Abs(hookStruct.pt.X - _startPt.X);
                        double height = Math.Abs(hookStruct.pt.Y - _startPt.Y);

                        DrawingCompleted?.Invoke(this, new Rect2D(left, top, width, height));

                        ChordDiagLog.Log($"RUp dragging -> Completed ENGOLIU(1) {ChordDiagLog.Snapshot()}"); // ChordDiag
                        // Neutraliza a mensagem para o Windows não exibir o menu de contexto padrão
                        return (IntPtr)1;
                    }

                    ChordDiagLog.Log($"RUp down-sem-drag -> repassou {ChordDiagLog.Snapshot()}"); // ChordDiag
                }
                else
                {
                    ChordDiagLog.Log($"RUp sem-chord repassou {ChordDiagLog.Snapshot()}"); // ChordDiag
                    DrawingCancelled?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (msg == 0x0201 /* WM_LBUTTONDOWN */)
            {
                GlobalLeftClick?.Invoke(this, EventArgs.Empty);

                if (_isDragging || _isDownOnDesktop)
                {
                    ChordDiagLog.Log($"LDown chord dragging={_isDragging} -> Cancelled repassou {ChordDiagLog.Snapshot()}"); // ChordDiag
                    _isDownOnDesktop = false;
                    _isDragging = false;
                    DrawingCancelled?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    ChordDiagLog.Log($"LDown sem-chord repassou {ChordDiagLog.Snapshot()}"); // ChordDiag
                }
            }
            else if (msg == NativeMethods.WM_WINDOWPOSCHANGING)
            {
                if (_isDragging)
                {
                    ChordDiagLog.Log("PosChanging dragging -> Cancelled repassou"); // ChordDiag
                    _isDownOnDesktop = false;
                    _isDragging = false;
                    DrawingCancelled?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static bool IsDesktopWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == Environment.ProcessId)
        {
            return false;
        }

        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        string className = sb.ToString();

        if (className is "Progman" or "WorkerW" or "SHELLDLL_DefView" or "SysListView32")
        {
            return true;
        }

        IntPtr root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
        if (root != IntPtr.Zero && root != hwnd)
        {
            sb.Clear();
            NativeMethods.GetClassName(root, sb, sb.Capacity);
            string rootClassName = sb.ToString();
            if (rootClassName is "Progman" or "WorkerW")
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Stop();
    }
}
