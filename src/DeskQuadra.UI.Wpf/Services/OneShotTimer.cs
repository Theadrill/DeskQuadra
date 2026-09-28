using System.Windows.Threading;

namespace DeskQuadra.UI.Wpf.Services;

/// <summary>
/// Helper para timers one-shot de UI (mesma thread Dispatcher, mesma semântica do molde manual:
/// para, desassina, descarta, recria, assina, start).
/// Centraliza os 4 one-shots da QuadraWindow (peek-enter/peek-exit/spring/post-drop).
/// A inércia do scroll (_touchInertiaTimer, recorrente ~60fps) fica fora do escopo de propósito.
/// </summary>
internal static class OneShotTimer
{
    /// <summary>
    /// Para/desassina o slot anterior (se houver), recria com o intervalo dado e dá start.
    /// Deve ser chamado na thread da UI, como o molde original.
    /// </summary>
    public static void Arm(ref DispatcherTimer? slot, int milliseconds, EventHandler tick)
    {
        Cancel(ref slot, tick);
        slot = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(milliseconds)
        };
        slot.Tick += tick;
        slot.Start();
    }

    /// <summary>
    /// Para, desassina e zera o slot (no-op se já nulo).
    /// </summary>
    public static void Cancel(ref DispatcherTimer? slot, EventHandler tick)
    {
        if (slot != null)
        {
            slot.Stop();
            slot.Tick -= tick;
            slot = null;
        }
    }
}
