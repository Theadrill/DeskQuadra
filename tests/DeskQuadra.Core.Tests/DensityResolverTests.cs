using DeskQuadra.Core;
using DeskQuadra.Core.Models;

namespace DeskQuadra.Core.Tests;

/// <summary>
/// Lógica pura de densidade (sem UI/registry/PInvoke): Auto via flags SM_DIGITIZER
/// + mapeamento preferência -&gt; dimensões Normal/Touch.
/// </summary>
public class DensityResolverTests
{
    [Theory]
    [InlineData(0x01, 0, true)] // touch integrado
    [InlineData(0x02, 0, true)] // touch externo
    [InlineData(0x03, 0, true)] // ambos
    [InlineData(0x00, 1, true)] // sem NID mas com toques reportados
    [InlineData(0x00, 10, true)]
    [InlineData(0x00, 0, false)] // desktop mouse puro
    [InlineData(0x80, 0, false)] // só NID_READY, sem touch
    [InlineData(0x04, 0, false)] // só pen, sem touch
    public void HasTouchHardware_DecidesFromFlags(int digitizer, int maxTouches, bool expected)
    {
        Assert.Equal(expected, DensityResolver.HasTouchHardware(digitizer, maxTouches));
    }

    [Theory]
    [InlineData(DensityPreference.Touch, true, true)]
    [InlineData(DensityPreference.Touch, false, true)]
    [InlineData(DensityPreference.Normal, true, false)]
    [InlineData(DensityPreference.Normal, false, false)]
    [InlineData(DensityPreference.Auto, true, true)]
    [InlineData(DensityPreference.Auto, false, false)]
    public void ResolveIsTouch_MapsPreference(DensityPreference pref, bool hasHardware, bool expected)
    {
        Assert.Equal(expected, DensityResolver.ResolveIsTouch(pref, hasHardware));
    }

    [Fact]
    public void Dimensions_NormalMatchesCurrent()
    {
        Assert.Equal(28.0, DensityResolver.TitleBarHeight(false));
        Assert.Equal(24.0, DensityResolver.TitleButtonSize(false));
    }

    [Fact]
    public void Dimensions_TouchMatchesSpec()
    {
        Assert.Equal(42.0, DensityResolver.TitleBarHeight(true));
        Assert.Equal(44.0, DensityResolver.TitleButtonSize(true));
    }

    [Fact]
    public void ScrollBar_NormalMatchesFluentSlimSpec()
    {
        Assert.Equal(10.0, DensityResolver.ScrollBarWidth(false));
        Assert.Equal(4.0, DensityResolver.ScrollBarThumbIdleWidth(false));
        Assert.Equal(8.0, DensityResolver.ScrollBarThumbHoverWidth(false));
    }

    [Fact]
    public void ScrollBar_TouchMatchesGenerousHitboxSpec()
    {
        Assert.Equal(16.0, DensityResolver.ScrollBarWidth(true));
        Assert.Equal(6.0, DensityResolver.ScrollBarThumbIdleWidth(true));
        Assert.Equal(12.0, DensityResolver.ScrollBarThumbHoverWidth(true));
    }
}
