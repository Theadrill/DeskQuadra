using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Services;
using Xunit;

namespace DeskQuadra.UI.Wpf.Tests;

public class WindowsVisualCapabilityTests
{
    [Theory]
    [InlineData(22621, true, WindowsVisualTier.ModernBackdrop)]
    [InlineData(22631, true, WindowsVisualTier.ModernBackdrop)]
    [InlineData(22000, true, WindowsVisualTier.ClassicBlur)]
    [InlineData(19045, true, WindowsVisualTier.ClassicBlur)]
    [InlineData(10240, true, WindowsVisualTier.ClassicBlur)]
    [InlineData(9600, true, WindowsVisualTier.Basic)]
    [InlineData(22621, false, WindowsVisualTier.Basic)]
    [InlineData(19045, false, WindowsVisualTier.Basic)]
    public void EvaluateTier_ReturnsExpectedTier_BasedOnBuildAndHardware(int build, bool hwEnabled, WindowsVisualTier expected)
    {
        var tier = WindowsVisualCapabilityService.EvaluateTier(build, hwEnabled);
        Assert.Equal(expected, tier);
    }

    [Fact]
    public void Service_WithOverride_ExposesCorrectProperties()
    {
        var service = new WindowsVisualCapabilityService(
            overrideBuildNumber: 22631,
            customHardwareCheck: () => true);

        Assert.Equal(22631, service.WindowsBuildNumber);
        Assert.True(service.IsHardwareAccelerationEnabled);
        Assert.Equal(WindowsVisualTier.ModernBackdrop, service.SupportedTier);
        Assert.True(service.IsBlurSupported);
    }

    [Fact]
    public void Service_WhenHardwareDisabled_FallsBackToBasicTier()
    {
        var service = new WindowsVisualCapabilityService(
            overrideBuildNumber: 22631,
            customHardwareCheck: () => false);

        Assert.Equal(22631, service.WindowsBuildNumber);
        Assert.False(service.IsHardwareAccelerationEnabled);
        Assert.Equal(WindowsVisualTier.Basic, service.SupportedTier);
        Assert.False(service.IsBlurSupported);
    }
}
