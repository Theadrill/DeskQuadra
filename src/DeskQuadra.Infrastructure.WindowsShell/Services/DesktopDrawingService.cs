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

            // Par-gesto: a republicação via SendInput volta pelo hook marcada como
            // injetada — repassa de imediato sem processar para não realimentar o gesto.
            if (IsInjectedHookEvent(lParam))
            {
                ChordDiagLog.LogVerbose("Injetado -> repassou sem processar"); // ChordDiag
                return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            if (msg == NativeMethods.WM_RBUTTONDOWN)
            {
                GlobalLeftClick?.Invoke(this, EventArgs.Empty);
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                IntPtr targetWindow = NativeMethods.WindowFromPoint(hookStruct.pt);

                if (IsDesktopWindow(targetWindow))
                {
                    if (_isDownOnDesktop)
                    {
                        // RDown com pendente ativo (edge raro: multitoque/driver republicou
                        // RDown): SOBRESCREVE o pendente em vez de repassar sem aninhar.
                        // O RDown anterior também foi engolido, então o Windows nunca viu
                        // par aberto — sobrescrever mantém o invariante (só pares completos
                        // visíveis) e elimina estado preso permanente. RUp órfão eventual
                        // é ignorado pelo Windows.
                        _isDownOnDesktop = true;
                        _isDragging = false;
                        _startPt = hookStruct.pt;
                        ChordDiagLog.LogVerbose($"RDown onDesktop com-pendente -> sobrescreveu pendente ENGOLIU(1) hwnd=0x{targetWindow:X} x={hookStruct.pt.X} y={hookStruct.pt.Y}"); // ChordDiag
                        return (IntPtr)1;
                    }

                    // Par-gesto: engole o RDown e guarda o pendente. Sem isso o RUp
                    // final engolido dessincroniza o Windows (DefView/taskbar creem
                    // que o R segue pressionado e ignoram o esquerdo).
                    _isDownOnDesktop = true;
                    _isDragging = false;
                    _startPt = hookStruct.pt;
                    ChordDiagLog.LogVerbose($"RDown onDesktop -> pendente ENGOLIU(1) hwnd=0x{targetWindow:X} x={hookStruct.pt.X} y={hookStruct.pt.Y}"); // ChordDiag
                    return (IntPtr)1;
                }
                else
                {
                    _isDownOnDesktop = false;
                    _isDragging = false;
                    ChordDiagLog.LogVerbose($"RDown offDesktop repassou hwnd=0x{targetWindow:X}"); // ChordDiag
                }
            }
            else if (msg == NativeMethods.WM_MOUSEMOVE)
            {
                if (_isDownOnDesktop)
                {
                    // Rastreio puro pelo fluxo de eventos do hook, sem API de estado de tecla:
                    // com o RDown engolido, GetKeyState/GetAsyncKeyState reportam solto mesmo
                    // pressionado — sem API não há o que dessincronizar. INVARIANTE: o Windows
                    // só vê pares RDown/RUp completos (engolidos = invisíveis; republicados =
                    // par completo; RUp órfão repassado sem pendente = ignorado pelo Windows).
                    // O RUp é sempre observado pelo hook global (LL vê todos os RUp do sistema)
                    // e limpa o pendente — não há como soltar sem o hook ver. Alt+Tab no meio
                    // do gesto tinha o mesmo comportamento antes (a API física também veria
                    // pressionado) — sem regressão.
                    var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                    double dx = hookStruct.pt.X - _startPt.X;
                    double dy = hookStruct.pt.Y - _startPt.Y;
                    double distance = Math.Sqrt(dx * dx + dy * dy);

                    if (!_isDragging && NativeMethods.ShouldStartDrag(distance))
                    {
                        _isDragging = true;
                        ChordDiagLog.LogVerbose($"Move -> Dragging repassou d={distance:F0}"); // ChordDiag
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

                    // Clique simples com pendente: o RDown original foi engolido, então
                    // devolve o par ao Windows no mesmo ponto — o menu nativo abre normal
                    // e o estado do botão direito permanece sincronizado.
                    RepublishRightClick(hookStruct.pt);
                    ChordDiagLog.Log($"RUp clique-simples -> republicou par ENGOLIU(1) x={hookStruct.pt.X} y={hookStruct.pt.Y} {ChordDiagLog.Snapshot()}"); // ChordDiag
                    return (IntPtr)1;
                }
                else
                {
                    if (ChordDiagLog.Verbose) // ChordDiag
                    {
                        ChordDiagLog.LogVerbose($"RUp sem-chord repassou {ChordDiagLog.Snapshot()}"); // ChordDiag
                    }
                    DrawingCancelled?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (msg == 0x0201 /* WM_LBUTTONDOWN */)
            {
                GlobalLeftClick?.Invoke(this, EventArgs.Empty);

                // ChordDiag TEMP (só log): quem está sob o ponto + vis/ex do under e do overlay; sem mudar fluxo.
                // ChordDiag: SnapshotUnder é o mais caro do hot-path — só em caminho verbose.
                NativeMethods.POINT lPt = default;
                string chordUnder = string.Empty; // ChordDiag
                if (ChordDiagLog.Verbose) // ChordDiag
                {
                    try // ChordDiag
                    { // ChordDiag
                        var hookStructL = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam); // ChordDiag
                        lPt = hookStructL.pt;
                        chordUnder = " " + ChordDiagLog.SnapshotUnder(lPt); // ChordDiag
                    } // ChordDiag
                    catch { chordUnder = " under=?"; } // ChordDiag
                }

                if (_isDragging || _isDownOnDesktop)
                {
                    if (!_isDragging && _isDownOnDesktop)
                    {
                        // Não republica o par RDown+RUp aqui: o RDown original foi
                        // engolido (o Windows nunca viu RDown) e o LDown físico já
                        // está em processamento no hook — o par sintético entraria
                        // na fila DEPOIS e seria entregue como LDown, RDown, RUp,
                        // abrindo o menu nativo justo no caminho de cancelamento.
                        // Basta limpar o pendente e repassar o LDown (par simétrico:
                        // nada visto, nada a ressincronizar).
                        if (ChordDiagLog.Verbose) // ChordDiag
                        {
                            ChordDiagLog.LogVerbose($"LDown pendente -> cancela sem republicar repassou {ChordDiagLog.Snapshot()}{chordUnder}"); // ChordDiag
                        }
                    }
                    else
                    {
                        if (ChordDiagLog.Verbose) // ChordDiag
                        {
                            ChordDiagLog.LogVerbose($"LDown chord dragging={_isDragging} -> Cancelled repassou {ChordDiagLog.Snapshot()}{chordUnder}"); // ChordDiag
                        }
                    }
                    _isDownOnDesktop = false;
                    _isDragging = false;
                    DrawingCancelled?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    if (ChordDiagLog.Verbose) // ChordDiag
                    {
                        ChordDiagLog.LogVerbose($"LDown sem-chord repassou {ChordDiagLog.Snapshot()}{chordUnder}"); // ChordDiag
                    }
                }
            }
            else if (msg == NativeMethods.WM_WINDOWPOSCHANGING)
            {
                if (_isDragging)
                {
                    ChordDiagLog.LogVerbose("PosChanging dragging -> Cancelled repassou"); // ChordDiag
                    _isDownOnDesktop = false;
                    _isDragging = false;
                    DrawingCancelled?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    // Leitura best-effort do marcador de injetado (nunca lança dentro do hook).
    private static bool IsInjectedHookEvent(IntPtr lParam)
    {
        if (lParam == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            return NativeMethods.IsInjectedMouseEvent(hookStruct.flags, hookStruct.dwExtraInfo);
        }
        catch
        {
            return false;
        }
    }

    // Devolve ao Windows o par RDown+RUp engolido. Sem MOVE: o clique cai no
    // cursor atual (= ponto do evento que gerou a republicação, chamada síncrona
    // dentro do hook) — evita normalização absoluta 0-65535 e bugs de DPI/multimonitor.
    // Carrega a marca MouseChordRepublishTag para o topo do hook repassar de volta
    // sem processar. Best-effort: o hook nunca pode lançar.
    private static void RepublishRightClick(NativeMethods.POINT pt)
    {
        _ = pt;
        try
        {
            var inputs = new NativeMethods.INPUT[2];
            inputs[0] = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_MOUSE,
                mi = new NativeMethods.MOUSEINPUT
                {
                    dwFlags = NativeMethods.MOUSEEVENTF_RIGHTDOWN,
                    dwExtraInfo = NativeMethods.MouseChordRepublishTag,
                },
            };
            inputs[1] = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_MOUSE,
                mi = new NativeMethods.MOUSEINPUT
                {
                    dwFlags = NativeMethods.MOUSEEVENTF_RIGHTUP,
                    dwExtraInfo = NativeMethods.MouseChordRepublishTag,
                },
            };
            NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        }
        catch
        {
            // Best-effort: sem o par o clique simples só não abre o menu; o estado
            // do gesto já foi limpo pelo chamador.
        }
    }

    private static bool IsDesktopWindow(IntPtr hwnd)
    {
        if (NativeMethods.IsOwnProcessWindow(hwnd))
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
