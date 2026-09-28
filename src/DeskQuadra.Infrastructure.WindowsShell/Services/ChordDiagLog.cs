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

    private const string LogFileName = "chord-diag.log";

    // Acessor não-const para o cheque interno não gerar warning CS0162 de código
    // inalcançável quando Enabled é const (o JIT dobra a constante do mesmo jeito).
    private static bool IsOn => Enabled;

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
}
