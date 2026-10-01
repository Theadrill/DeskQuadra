using System.Diagnostics;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;

namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

// T3 terceiros: corredor STA compartilhado pela query (T2) e pelo invoke (T3).
// Extração literal do padrão T2 (thread STA dedicada com message queue via
// PeekMessage + Join com timeout): handlers do Shell podem PostMessage, e a
// thread órfã é background e morre sozinha. NUNCA a UI do WPF.
// Query usa QueryTimeoutMs (3s, intacto); invoke usa InvokeTimeoutMs (30s —
// o handler pode abrir diálogo modal, ex.: "Add to archive" do 7-Zip).
internal static class ShellStaRunner
{
    public const int QueryTimeoutMs = 3000;
    public const int InvokeTimeoutMs = 30000;

    public static T Run<T>(Func<T> work, int timeoutMs, string threadName)
    {
        T? result = default;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                PeekMessage(out MSG _, HWND.NULL, 0, 0, PM.M_NOREMOVE);
                result = work();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Name = threadName;
        thread.Start();

        if (!thread.Join(timeoutMs))
        {
            throw new TimeoutException($"{threadName} excedeu o limite de {timeoutMs}ms.");
        }

        if (error is not null)
        {
            throw error;
        }

        return result!;
    }

    // Variante bool sem exceção para o invoke: timeout/falha = false
    // (silencioso, padrão do projeto). Loga só em Debug.
    public static bool TryRun(Func<bool> work, int timeoutMs, string threadName, string path)
    {
        try
        {
            return Run(work, timeoutMs, threadName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{threadName}] invoke falhou p/ '{path}': {ex.GetType().Name}");
            return false;
        }
    }
}
