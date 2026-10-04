using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using DeskQuadra.UI.Wpf.ViewModels;
using Xunit;

namespace DeskQuadra.UI.Wpf.Tests;

public class SettingsViewModelVisualEffectsTests
{
    private sealed class FakeStartupService : IStartupService
    {
        public bool Enabled { get; set; }
        public bool IsEnabled() => Enabled;
        public void SetEnabled(bool enable) { Enabled = enable; }
    }

    private sealed class FakeDensityService : IDensitySettingsService
    {
        public DensityPreference Current { get; set; } = DensityPreference.Auto;
        public event EventHandler<DensityPreference>? PreferenceChanged;
        public void Set(DensityPreference preference)
        {
            Current = preference;
            PreferenceChanged?.Invoke(this, preference);
        }
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
    }

    private sealed class FakeVisualCapabilityService : IWindowsVisualCapabilityService
    {
        public int WindowsBuildNumber { get; set; } = 22631;
        public WindowsVisualTier SupportedTier { get; set; } = WindowsVisualTier.ModernBackdrop;
        public bool IsBlurSupported => SupportedTier != WindowsVisualTier.Basic;
        public bool IsHardwareAccelerationEnabled { get; set; } = true;
    }

    [Fact]
    public void IsVisualEffectsSupported_True_WhenCapabilitySupportsBlur()
    {
        var vm = new SettingsViewModel(
            new FakeStartupService(),
            new FakeDensityService(),
            hasTouchHardware: false,
            new FakeVisualSettingsService(),
            new FakeVisualCapabilityService { SupportedTier = WindowsVisualTier.ModernBackdrop });

        Assert.True(vm.IsVisualEffectsSupported);
    }

    [Fact]
    public void IsVisualEffectsSupported_False_WhenCapabilityIsBasic()
    {
        var vm = new SettingsViewModel(
            new FakeStartupService(),
            new FakeDensityService(),
            hasTouchHardware: false,
            new FakeVisualSettingsService(),
            new FakeVisualCapabilityService { SupportedTier = WindowsVisualTier.Basic });

        Assert.False(vm.IsVisualEffectsSupported);
    }

    [Fact]
    public void ToggleEnableWindows11VisualEffects_UpdatesPropertyAndService()
    {
        var fakeVisual = new FakeVisualSettingsService { EnableWindows11VisualEffects = true };
        var vm = new SettingsViewModel(
            new FakeStartupService(),
            new FakeDensityService(),
            hasTouchHardware: false,
            fakeVisual,
            new FakeVisualCapabilityService());

        Assert.True(vm.EnableWindows11VisualEffects);

        vm.EnableWindows11VisualEffects = false;

        Assert.False(vm.EnableWindows11VisualEffects);
        Assert.False(fakeVisual.EnableWindows11VisualEffects);
    }
}
