using DeskQuadra.UI.Wpf.Services;

namespace DeskQuadra.UI.Wpf.Tests;

/// <summary>
/// Título da próxima Quadra: formato do resx aplicado à posição
/// (<c>existingCount + 1</c>). Puro, sem timers/XAML.
/// Trava a eliminação da duplicação <c>Count + 1 + string.Format</c> que
/// existia no menu dual (<c>App</c>) e em <c>QuadraWindow.NewQuadraMenu_Click</c>.
/// </summary>
public class QuadraNamingTests
{
    [Theory]
    [InlineData(0, "Quadra {0}", "Quadra 1")]
    [InlineData(1, "Quadra {0}", "Quadra 2")]
    [InlineData(4, "Quadra {0}", "Quadra 5")]
    public void NextTitle_AplicaFormatoComMaisUm(int existingCount, string format, string expected)
    {
        Assert.Equal(expected, QuadraNaming.NextTitle(existingCount, format));
    }

    [Fact]
    public void NextTitle_PreservaFormatoLocalizado()
    {
        // Formato hipotético traduzido: o helper não conhece resx, só aplica o recebido.
        Assert.Equal("Quadro 3", QuadraNaming.NextTitle(2, "Quadro {0}"));
    }
}
