using DeskQuadra.UI.Wpf.Services;

namespace DeskQuadra.UI.Wpf.Tests;

/// <summary>
/// Lógica pura do modo redimensionar (sem Visual/Dispatcher): armado, título guardado e timeout.
/// </summary>
public class TouchResizeStateTests
{
    [Fact]
    public void Arm_GuardaQuadraETitulo_NaoExpiradoDeImediato()
    {
        var quadra = Guid.NewGuid();

        var state = new TouchResizeState(quadra, "Quadra 1");

        Assert.Equal(quadra, state.QuadraId);
        Assert.Equal("Quadra 1", state.OriginalTitle);
        Assert.False(state.IsExpired(DateTime.UtcNow));
    }

    [Theory]
    [InlineData(7999, false)]
    [InlineData(8000, true)]
    [InlineData(9000, true)]
    public void IsExpired_SegueTimeoutDe8s(int millisDepois, bool esperado)
    {
        var state = new TouchResizeState(Guid.NewGuid(), "Quadra 1");

        Assert.Equal(esperado, state.IsExpired(state.ArmedAtUtc.AddMilliseconds(millisDepois)));
    }

    [Fact]
    public void Timeout_ConstanteDocumentada_8s()
    {
        Assert.Equal(8000, TouchResizeState.TimeoutMilliseconds);
    }

    [Fact]
    public void Arm_QuadraVazia_Rejeita()
    {
        Assert.Throws<ArgumentException>(() => new TouchResizeState(Guid.Empty, "Quadra 1"));
    }

    [Fact]
    public void Arm_TituloNulo_Rejeita()
    {
        Assert.Throws<ArgumentNullException>(() => new TouchResizeState(Guid.NewGuid(), null!));
    }
}
