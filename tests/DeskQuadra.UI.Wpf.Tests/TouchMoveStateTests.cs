using DeskQuadra.UI.Wpf.Services;

namespace DeskQuadra.UI.Wpf.Tests;

/// <summary>
/// Lógica pura do modo mover (sem Visual/Dispatcher): armado, destino, cancela e timeout.
/// </summary>
public class TouchMoveStateTests
{
    [Fact]
    public void Arm_GuardaOrigemEItem_NaoExpiradoDeImediato()
    {
        var source = Guid.NewGuid();
        var item = Guid.NewGuid();

        var state = new TouchMoveState(source, item);

        Assert.Equal(source, state.SourceQuadraId);
        Assert.Equal(item, state.ItemId);
        Assert.False(state.IsExpired(DateTime.UtcNow));
    }

    [Fact]
    public void ResolveDestination_MesmaQuadra_CancelaSemMover()
    {
        var source = Guid.NewGuid();
        var state = new TouchMoveState(source, Guid.NewGuid());

        Assert.Equal(TouchMoveResolution.CancelSameQuadra, state.ResolveDestination(source));
    }

    [Fact]
    public void ResolveDestination_QuadraDistinta_Move()
    {
        var state = new TouchMoveState(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(TouchMoveResolution.Move, state.ResolveDestination(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(7999, false)]
    [InlineData(8000, true)]
    [InlineData(9000, true)]
    public void IsExpired_SegueTimeoutDe8s(int millisDepois, bool esperado)
    {
        var state = new TouchMoveState(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(esperado, state.IsExpired(state.ArmedAtUtc.AddMilliseconds(millisDepois)));
    }

    [Fact]
    public void Timeout_ConstanteDocumentada_8s()
    {
        Assert.Equal(8000, TouchMoveState.TimeoutMilliseconds);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Arm_GuidVazio_Rejeita(bool origemVazia, bool itemVazio)
    {
        var source = origemVazia ? Guid.Empty : Guid.NewGuid();
        var item = itemVazio ? Guid.Empty : Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new TouchMoveState(source, item));
    }
}
