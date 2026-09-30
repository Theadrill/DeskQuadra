// Lógica pura do modo redimensionar via touch (sem Visual/Dispatcher): armado + título guardado + timeout.
// Testável em xUnit sem STA: só Guid/string + DateTime (o DispatcherTimer/ESC vivem na QuadraWindow).
namespace DeskQuadra.UI.Wpf.Services;

/// <summary>
/// Estado armado do REDIMENSIONAR-via-touch: Quadra + título original + instante do arme.
/// Fatia 1 só arma o modo e troca o título por instrução; bordas/grips ficam p/ a fatia 2.
/// </summary>
public sealed class TouchResizeState
{
    /// <summary>Timeout do armado (~8s, mesmo molde do MOVER).</summary>
    public const int TimeoutMilliseconds = 8000;

    public Guid QuadraId { get; }

    public string OriginalTitle { get; }

    public DateTime ArmedAtUtc { get; }

    public TouchResizeState(Guid quadraId, string originalTitle)
    {
        if (quadraId == Guid.Empty)
        {
            throw new ArgumentException("Quadra inválida.", nameof(quadraId));
        }
        ArgumentNullException.ThrowIfNull(originalTitle);
        QuadraId = quadraId;
        OriginalTitle = originalTitle;
        ArmedAtUtc = DateTime.UtcNow;
    }

    // Expirado quando a saída chega após o timeout (o timer one-shot da janela é o cancela oficial).
    public bool IsExpired(DateTime utcNow)
    {
        return (utcNow - ArmedAtUtc).TotalMilliseconds >= TimeoutMilliseconds;
    }
}
