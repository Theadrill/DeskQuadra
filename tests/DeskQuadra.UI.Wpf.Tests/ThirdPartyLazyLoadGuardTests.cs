using DeskQuadra.UI.Wpf.Services;

namespace DeskQuadra.UI.Wpf.Tests;

// T8a lazy load da seção de terceiros: guarda pura do token de geração.
// A continuação no Dispatcher só aplica se o token capturado ainda for o
// atual — abertura mais nova invalida a anterior.
public class ThirdPartyLazyLoadGuardTests
{
    [Fact]
    public void ShouldApply_MesmaGeração_Aplica()
    {
        Assert.True(ThirdPartyLazyLoadGuard.ShouldApply(3, 3));
    }

    [Fact]
    public void ShouldApply_GeraçãoAntiga_NãoAplica()
    {
        Assert.False(ThirdPartyLazyLoadGuard.ShouldApply(2, 3));
    }

    [Fact]
    public void ShouldApply_GeraçãoFutura_NãoAplica()
    {
        Assert.False(ThirdPartyLazyLoadGuard.ShouldApply(4, 3));
    }

    [Fact]
    public void ShouldApply_PrimeiraGeração_Aplica()
    {
        Assert.True(ThirdPartyLazyLoadGuard.ShouldApply(1, 1));
    }
}
