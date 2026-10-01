// Log diagnóstico do menu de terceiros em %APPDATA%\DeskQuadra\shell-menu.log.
// Mesmo molde de anchor.log/guardian.log: append best-effort, nunca lança,
// nunca quebra o menu. Sem timers, sem mudança de comportamento — só LOGA.
// Query: path, flags, nº de nós, falha/timeout. Invoke: path, offset,
// hr de Query/Validate/Invoke, exceção.
using System.IO;

namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

internal static class ShellMenuLog
{
    private const string LogFileName = "shell-menu.log";

    internal static string LogFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DeskQuadra",
        LogFileName);

    public static void Log(string message)
    {
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
            // Best-effort: log nunca quebra o menu nem derruba o app.
        }
    }

    // Formatação pura/testável (sem I/O) — o I/O vive só em Log().
    internal static string FormatQuery(string path, string flags, int nodeCount)
        => $"query path='{path}' flags={flags} nodes={nodeCount}";

    internal static string FormatQueryFailed(string path, string flags, string reason)
        => $"query path='{path}' flags={flags} FAILED reason={reason}";

    internal static string FormatInvoke(
        string path,
        string? verb,
        uint offset,
        string queryHr,
        string validateHr,
        string invokeHr,
        string outcome)
        => $"invoke path='{path}' verb='{verb ?? string.Empty}' offset={offset} query={queryHr} validate={validateHr} invoke={invokeHr} outcome={outcome}";
}
