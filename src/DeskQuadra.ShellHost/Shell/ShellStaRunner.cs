using System.Diagnostics;
using DeskQuadra.Core.ThirdParty;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;

namespace DeskQuadra.ShellHost.Shell;

// T3 terceiros (movido em T5 p/ dentro do host, sem mudar regra): corredor STA
// compartilhado pela query (T2) e pelo invoke (T3). Extração literal do padrão
// T2 (thread STA dedicada com message queue via PeekMessage + Join com
// timeout): handlers do Shell podem PostMessage, e a thread órfã é background
// e morre sozinha.
// Orçamentos no ShellHostProtocol (fonte única T5: cliente espera o mesmo no
// processo; query ~3s intacto, invoke ~30s — o handler pode abrir modal).
internal static class ShellStaRunner
{
    public const int QueryTimeoutMs = ShellHostProtocol.QueryTimeoutMs;
    public const int InvokeTimeoutMs = ShellHostProtocol.InvokeTimeoutMs;

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
