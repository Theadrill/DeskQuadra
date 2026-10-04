using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Services;
using Xunit;

namespace DeskQuadra.UI.Wpf.Tests;

public class WindowVisualEffectServiceTests
{
    private sealed class FakeVisualCapabilityService : IWindowsVisualCapabilityService
    {
        public int WindowsBuildNumber { get; set; } = 22631;
        public WindowsVisualTier SupportedTier { get; set; } = WindowsVisualTier.ClassicBlur;
        public bool IsBlurSupported => SupportedTier != WindowsVisualTier.Basic;
        public bool IsAcrylicSupported => SupportedTier == WindowsVisualTier.ModernBackdrop;
        public bool IsHardwareAccelerationEnabled { get; set; } = true;
    }

    private sealed class FakeVisualSettingsService : IVisualSettingsService
    {
        public bool EnableWindows11VisualEffects { get; set; } = true;
        public event EventHandler<bool>? VisualEffectsChanged;
        public void SetEnableWindows11VisualEffects(bool enabled)
        {
            EnableWindows11VisualEffects = enabled;
            VisualEffectsChanged?.Invoke(this, enabled);
        }

        public VisualEffectTechnique PreferredTechnique { get; set; } = VisualEffectTechnique.Auto;
        public event EventHandler<VisualEffectTechnique>? TechniqueChanged;
        public void SetPreferredTechnique(VisualEffectTechnique technique)
        {
            PreferredTechnique = technique;
            TechniqueChanged?.Invoke(this, technique);
        }
    }

    [Fact]
    public void ApplyBlur_ReturnsFalse_WhenHandleIsZero()
    {
        var service = new WindowVisualEffectService(
            new FakeVisualCapabilityService(),
            new FakeVisualSettingsService());

        bool result = service.ApplyBlur(IntPtr.Zero);
        Assert.False(result);
    }

    [Fact]
    public void ApplyBlur_ReturnsFalse_WhenUserDisabledEffects()
    {
        var settings = new FakeVisualSettingsService { EnableWindows11VisualEffects = false };
        var service = new WindowVisualEffectService(
            new FakeVisualCapabilityService(),
            settings);

        // Mesmo com um handle hipotético, não deve aplicar
        bool result = service.ApplyBlur(new IntPtr(12345));
        Assert.False(result);
    }

    [Fact]
    public void ApplyBlur_ReturnsFalse_WhenCapabilityIsBasic()
    {
        var capability = new FakeVisualCapabilityService { SupportedTier = WindowsVisualTier.Basic };
        var service = new WindowVisualEffectService(
            capability,
            new FakeVisualSettingsService { EnableWindows11VisualEffects = true });

        bool result = service.ApplyBlur(new IntPtr(12345));
        Assert.False(result);
    }

    [Fact]
    public void RemoveBlur_ReturnsFalse_WhenHandleIsZero()
    {
        var service = new WindowVisualEffectService(
            new FakeVisualCapabilityService(),
            new FakeVisualSettingsService());

        bool result = service.RemoveBlur(IntPtr.Zero);
        Assert.False(result);
    }
}
