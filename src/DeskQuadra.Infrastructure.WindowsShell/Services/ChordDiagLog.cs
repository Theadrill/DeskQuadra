// TEMPORÁRIO — diagnóstico do bug raro "chord trava cliques" (RDown+arrasto no desktop,
// LDown com direito preso abre o menu nativo, overlay fica visível e cliques morrem
// no desktop+taskbar até cliques aleatórios curarem; hipótese: loop modal invisível
// do menu com captura sistêmica). Padrão HangTestSwitch: flag + remoção documentada.
// Só LOGA, não muda comportamento: escrita best-effort em %APPDATA%\DeskQuadra\chord-diag.log
// (append, 1 linha curta por evento, try/catch; NUNCA Dispatcher.Invoke — a chamada direta
// e curta na thread do hook WH_MOUSE_LL é aceitável por ser temporário).
//
// COMO HABILITAR/DESABILITAR: const Enabled abaixo (true = loga; false = inerte).
// COMO REMOVER (obrigatório antes de qualquer push de fix):
//   1. Deletar este arquivo Services/ChordDiagLog.cs;
//   2. Remover todas as chamadas marcadas com // ChordDiag em
//      Services/DesktopDrawingService.cs e DeskQuadra.UI.Wpf/App.xaml.cs;
//   3. Remover GetForegroundWindow/GetCapture de Native/NativeMethods.cs
//      se ficarem sem outro uso (GetClassName já existia — manter).
// Sem timers, sem NuGets, sem resx, sem mudança de lógica.
// NOTA Move: por design só transições (Down/Dragging/cancel) são logadas — logar cada
// WM_MOUSEMOVE por pixel inundaria o log e pesaria o hook com I/O por movimento.
using System;
using System.IO;
using System.Text;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Log diagnóstico TEMPORÁRIO do chord RDown+arrasto (padrão HangTestSwitch).
/// Ler o comentário no topo do arquivo antes de usar ou remover.
/// </summary>
internal static class ChordDiagLog
{
    /// <summary>
    /// Liga/desliga o módulo. <c>false</c> = inerte (só o tipo compilado permanece
    /// até a remoção definitiva, como no HangTestSwitch).
    /// </summary>
    public const bool Enabled = true;

    // ChordDiag: Verbose desliga o flood do hot-path do hook (WH_MOUSE_LL).
    // Default OFF (false) = só eventos raros (Completed/republicou/overlay/popup/menu)
    // vão a disco; eventos por-movimento/clique (LDown/RDown/RUp/Move/injetado) são
    // descartados antes de qualquer AppendAllText. COMO LIGAR manualmente p/ diagnóstico
    // futuro: mudar para `true` aqui e rebuildar (temporário — remover antes do push,
    // junto com este arquivo, como descrito no topo).
    internal static bool Verbose = false;

    private const string LogFileName = "chord-diag.log";

    // Acessor não-const para o cheque interno não gerar warning CS0162 de código
    // inalcançável quando Enabled é const (o JIT dobra a constante do mesmo jeito).
    private static bool IsOn => Enabled;

    // TEMP ChordDiag: HWND do overlay (_selectionWindow, dono é o App) para o ramo
    // LDown diagnosticar "under == janela nossa? visível sem TRANSPARENT?".
    // Escrito pela UI no SourceInitialized (handle estável a vida toda), lido pela
    // thread do hook WH_MOUSE_LL. Apagado junto com este arquivo no push.
    internal static IntPtr OverlayHwnd;

    public static void Log(string message)
    {
        if (!IsOn)
        {
            return;
        }

        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DeskQuadra");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, LogFileName),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Best-effort: nunca trava o hook nem derruba o app.
        }
    }

    /// <summary>
    /// Log verbose do hot-path (por-evento do hook). Só escreve se <see cref="Verbose"/>
    /// estiver ligado; com default OFF elimina o AppendAllText por movimento/clique
    /// que engasgava o mouse e fazia o Windows remover o hook. <see cref="Log"/> segue
    /// para eventos raros (sempre ativos).
    /// </summary>
    public static void LogVerbose(string message)
    {
        if (!Verbose)
        {
            return;
        }

        Log(message);
    }

    /// <summary>
    /// Instantâneo best-effort (thread do hook ou UI): janela em foco (+classe) e captura
    /// de mouse. Só chamadas user32 rápidas, sem Dispatcher.
    /// </summary>
    public static string Snapshot()
    {
        if (!IsOn)
        {
            return string.Empty;
        }

        try
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            IntPtr cap = NativeMethods.GetCapture();
            string cls = "?";
            try
            {
                var sb = new StringBuilder(256);
                if (NativeMethods.GetClassName(fg, sb, sb.Capacity) > 0)
                {
                    cls = sb.ToString();
                }
            }
            catch
            {
                // Classe best-effort: o resto do instantâneo basta.
            }

            return $"fg=0x{fg:X} cls={cls} cap=0x{cap:X}";
        }
        catch
        {
            return "fg=? cap=?";
        }
    }

    /// <summary>
    /// TEMP ChordDiag: quem está sob o ponto do clique + vis/ex do under e do overlay.
    /// Só user32 síncronas (WindowFromPoint/IsWindowVisible/GetWindowLong/GetClassName —
    /// todas já existiam no repo), sem Dispatcher; best-effort, nunca lança.
    /// Formato: under=0x... underCls=... own=0/1 underVis=0/1 underEx=0x... ov=0x... ovVis=0/1 ovEx=0x...
    /// </summary>
    internal static string SnapshotUnder(NativeMethods.POINT pt)
    {
        if (!IsOn)
        {
            return string.Empty;
        }

        // ChordDiag: SnapshotUnder é o mais caro do hot-path (até ~7 roundtrips user32).
        // Só executa em caminho verbose; fora dele retorna vazio sem tocar user32.
        if (!Verbose)
        {
            return string.Empty;
        }

        try
        {
            IntPtr under = IntPtr.Zero;
            try { under = NativeMethods.WindowFromPoint(pt); } catch { under = IntPtr.Zero; }

            string underVis = "?";
            try { underVis = NativeMethods.IsWindowVisible(under) ? "1" : "0"; } catch { underVis = "?"; }

            string underEx = "?";
            try { underEx = $"0x{(uint)NativeMethods.GetWindowLong(under, NativeMethods.GWL_EXSTYLE):X}"; } catch { underEx = "?"; }

            bool own = false;
            try { own = NativeMethods.IsOwnProcessWindow(under); } catch { own = false; }

            IntPtr ov = OverlayHwnd;
            string underCls = "?";
            // Classe só quando é janela NOSSA (suspeito = overlay próprio comendo o clique);
            // GetClassName já existia — reuse, sem novo P/Invoke.
            if (under != IntPtr.Zero && (own || under == ov))
            {
                try
                {
                    var sb = new StringBuilder(256);
                    if (NativeMethods.GetClassName(under, sb, sb.Capacity) > 0)
                    {
                        underCls = sb.ToString();
                    }
                }
                catch { underCls = "?"; }
            }

            string ovVis = "?";
            try { ovVis = ov == IntPtr.Zero ? "n/a" : (NativeMethods.IsWindowVisible(ov) ? "1" : "0"); } catch { ovVis = "?"; }

            string ovEx = "?";
            try { ovEx = ov == IntPtr.Zero ? "n/a" : $"0x{(uint)NativeMethods.GetWindowLong(ov, NativeMethods.GWL_EXSTYLE):X}"; } catch { ovEx = "?"; }

            return $"under=0x{under:X} underCls={underCls} own={(own ? 1 : 0)} underVis={underVis} underEx={underEx} ov=0x{ov:X} ovVis={ovVis} ovEx={ovEx}";
        }
        catch
        {
            return "under=?";
        }
    }
}
