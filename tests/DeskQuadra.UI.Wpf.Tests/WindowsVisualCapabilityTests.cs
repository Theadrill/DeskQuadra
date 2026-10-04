using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Services;
using Xunit;

namespace DeskQuadra.UI.Wpf.Tests;

public class WindowsVisualCapabilityTests
{
    [Theory]
    [InlineData(26300, true, WindowsVisualTier.ModernBackdrop)]
    [InlineData(22621, true, WindowsVisualTier.ModernBackdrop)]
    [InlineData(22000, true, WindowsVisualTier.ModernBackdrop)]
    [InlineData(19045, true, WindowsVisualTier.ModernBackdrop)]
    [InlineData(17134, true, WindowsVisualTier.ModernBackdrop)]
    [InlineData(16299, true, WindowsVisualTier.ClassicBlur)]
    [InlineData(14393, true, WindowsVisualTier.ClassicBlur)]
    [InlineData(10240, true, WindowsVisualTier.Basic)] // Abaixo do mínimo operacional 14393
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
        Assert.True(service.IsAcrylicSupported);
    }

    [Fact]
    public void Service_Build14393_SupportsBlur_ButNotAcrylic()
    {
        var service = new WindowsVisualCapabilityService(
            overrideBuildNumber: 14393,
            customHardwareCheck: () => true);

        Assert.Equal(14393, service.WindowsBuildNumber);
        Assert.True(service.IsHardwareAccelerationEnabled);
        Assert.Equal(WindowsVisualTier.ClassicBlur, service.SupportedTier);
        Assert.True(service.IsBlurSupported);
        Assert.False(service.IsAcrylicSupported);
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
        Assert.False(service.IsAcrylicSupported);
    }

    [Fact]
    public void ResolveSimulatedOrRealBuildNumber_UsesExplicitOverride()
    {
        int resolved = WindowsVisualCapabilityService.ResolveSimulatedOrRealBuildNumber(14393);
        Assert.Equal(14393, resolved);
    }

    [Fact]
    public void ResolveSimulatedOrRealHardwareAcceleration_UsesExplicitCheck()
    {
        bool resolved = WindowsVisualCapabilityService.ResolveSimulatedOrRealHardwareAcceleration(() => false);
        Assert.False(resolved);
    }
}
