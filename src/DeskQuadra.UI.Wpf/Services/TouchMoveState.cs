// Lógica pura do modo mover via touch (sem Visual/Dispatcher): armado/destino/cancela + timeout.
// Testável em xUnit sem STA: só Guids + DateTime (o DispatcherTimer/ESC vivem na QuadraWindow).
namespace DeskQuadra.UI.Wpf.Services;

/// <summary>
/// Resultado do tap de destino com o modo mover armado.
/// </summary>
public enum TouchMoveResolution
{
    /// <summary>Quadra distinta: move (só vinculação, disco intacto).</summary>
    Move,
    /// <summary>Tap na origem: sem-operação, cancela sem mover.</summary>
    CancelSameQuadra,
}

/// <summary>
/// Estado armado do MOVER-via-touch: origem + item + instante do arme.
/// Timeout de ~8s dá saída sem gesto (dedo parado, menu fechado).
/// </summary>
public sealed class TouchMoveState
{
    /// <summary>Timeout do armado (~8s).</summary>
    public const int TimeoutMilliseconds = 8000;

    public Guid SourceQuadraId { get; }

    public Guid ItemId { get; }

    public DateTime ArmedAtUtc { get; }

    public TouchMoveState(Guid sourceQuadraId, Guid itemId)
    {
        if (sourceQuadraId == Guid.Empty)
        {
            throw new ArgumentException("Origem inválida.", nameof(sourceQuadraId));
        }
        if (itemId == Guid.Empty)
        {
            throw new ArgumentException("Item inválido.", nameof(itemId));
        }
        SourceQuadraId = sourceQuadraId;
        ItemId = itemId;
        ArmedAtUtc = DateTime.UtcNow;
    }

    // Destino distinto move; mesma Quadra cancela sem mover (decisão PO documentada).
    public TouchMoveResolution ResolveDestination(Guid destQuadraId)
    {
        return destQuadraId == SourceQuadraId ? TouchMoveResolution.CancelSameQuadra : TouchMoveResolution.Move;
    }

    // Expirado quando o tap chega após o timeout (o timer one-shot da janela é o cancela oficial).
    public bool IsExpired(DateTime utcNow)
    {
        return (utcNow - ArmedAtUtc).TotalMilliseconds >= TimeoutMilliseconds;
    }
}
