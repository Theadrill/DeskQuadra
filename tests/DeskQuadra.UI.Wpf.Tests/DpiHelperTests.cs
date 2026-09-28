using DeskQuadra.UI.Wpf.Services;

namespace DeskQuadra.UI.Wpf.Tests;

/// <summary>
/// Lógica pura de conversão DPI (sem Visual/Dispatcher): divisão físico→DIP,
/// multiplicação com Round DIP→físico e guard null-safe do GetScale.
/// O guard de escala inválida (&lt;=0 → 1.0) vive no GetScale e é exercitado
/// via visual null; Visual real exigiria thread STA com Dispatcher e fica fora.
/// </summary>
public class DpiHelperTests
{
    [Theory]
    [InlineData(192.0, 1.5, 128.0)]
    [InlineData(200.0, 2.0, 100.0)]
    [InlineData(100.0, 1.0, 100.0)]
    [InlineData(0.0, 1.25, 0.0)]
    public void PhysicalToDip_DividePeloScale(double physical, double scale, double expected)
    {
        Assert.Equal(expected, DpiHelper.PhysicalToDip(physical, scale));
    }

    [Theory]
    [InlineData(128.0, 1.5, 192)]
    [InlineData(100.0, 1.0, 100)]
    [InlineData(10.4, 2.0, 21)] // 20.8 → 21
    [InlineData(10.2, 2.0, 20)] // 20.4 → 20
    public void DipToPhysical_MultiplicaComRound(double dip, double scale, int expected)
    {
        Assert.Equal(expected, DpiHelper.DipToPhysical(dip, scale));
    }

    [Fact]
    public void GetScale_NullRetornaUm()
    {
        var (x, y) = DpiHelper.GetScale(null);

        Assert.Equal(1.0, x);
        Assert.Equal(1.0, y);
    }
}
