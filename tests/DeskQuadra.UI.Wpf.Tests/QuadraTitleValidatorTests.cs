using DeskQuadra.UI.Wpf.Services;

namespace DeskQuadra.UI.Wpf.Tests;

/// <summary>
/// Validação do nome da Quadra (fatia Renomear): vazio/só-espaços rejeita,
/// resto aceita com Trim. Puro, sem timers/XAML.
/// </summary>
public class QuadraTitleValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n ")]
    public void IsValid_VazioOuSoEspacos_Rejeita(string? candidate)
    {
        Assert.False(QuadraTitleValidator.IsValid(candidate));
    }

    [Theory]
    [InlineData("TUDO")]
    [InlineData("Quadra 2")]
    [InlineData("  com espaços em volta  ")]
    [InlineData("a")]
    public void IsValid_ComConteudo_Aceita(string candidate)
    {
        Assert.True(QuadraTitleValidator.IsValid(candidate));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalize_Invalido_RetornaFalse(string? candidate)
    {
        Assert.False(QuadraTitleValidator.TryNormalize(candidate, out _));
    }

    [Theory]
    [InlineData("TUDO", "TUDO")]
    [InlineData("  TUDO  ", "TUDO")]
    [InlineData("Minha Quadra", "Minha Quadra")]
    public void TryNormalize_Valido_RetornaTrimado(string candidate, string expected)
    {
        Assert.True(QuadraTitleValidator.TryNormalize(candidate, out string normalized));
        Assert.Equal(expected, normalized);
    }
}
