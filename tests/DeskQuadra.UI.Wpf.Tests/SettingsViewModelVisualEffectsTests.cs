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

        public VisualEffectTechnique PreferredTechnique { get; set; } = VisualEffectTechnique.Auto;
        public event EventHandler<VisualEffectTechnique>? TechniqueChanged;
        public void SetPreferredTechnique(VisualEffectTechnique technique)
        {
            PreferredTechnique = technique;
            TechniqueChanged?.Invoke(this, technique);
        }
    }

    private sealed class FakeVisualCapabilityService : IWindowsVisualCapabilityService
    {
        public int WindowsBuildNumber { get; set; } = 22631;
        public WindowsVisualTier SupportedTier { get; set; } = WindowsVisualTier.ModernBackdrop;
        public bool IsBlurSupported => SupportedTier != WindowsVisualTier.Basic;
        public bool IsAcrylicSupported => SupportedTier == WindowsVisualTier.ModernBackdrop;
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

    private sealed class FakeVisualEffectService : IWindowVisualEffectService
    {
        public string LastQuadraEffectApplied { get; set; } = "Acrílico Nativo do Shell (Composição DWM / Build 22631)";
        public string LastMenuEffectApplied { get; set; } = "Acrílico Nativo do Shell (Popups WPF / Build 22631)";
        public bool IsModernBackdropSupported { get; set; } = true;

        public bool ApplyBlur(IntPtr windowHandle, uint accentColor = 0) => true;
        public bool ApplyBlur(IntPtr windowHandle, VisualEffectTarget target, uint accentColor = 0) => true;
        public bool RemoveBlur(IntPtr windowHandle) => true;
    }

    [Fact]
    public void VisualEffectsDetail_ShowsAppliedTechniques_WhenEnabled()
    {
        var fakeVisual = new FakeVisualSettingsService { EnableWindows11VisualEffects = true };
        var fakeEffect = new FakeVisualEffectService();
        var fakeCap = new FakeVisualCapabilityService { WindowsBuildNumber = 22631 };

        var vm = new SettingsViewModel(
            new FakeStartupService(),
            new FakeDensityService(),
            hasTouchHardware: false,
            fakeVisual,
            fakeCap,
            fakeEffect);

        string detail = vm.VisualEffectsDetail;
        Assert.Contains("Quadras: Acrílico Nativo do Shell", detail);
        Assert.Contains("Menus: Acrílico Nativo do Shell", detail);
        Assert.Contains("Build 22631", detail);
    }

    [Fact]
    public void VisualEffectsDetail_ShowsDisabled_WhenSettingIsFalse()
    {
        var fakeVisual = new FakeVisualSettingsService { EnableWindows11VisualEffects = false };
        var fakeEffect = new FakeVisualEffectService();
        var fakeCap = new FakeVisualCapabilityService { WindowsBuildNumber = 22631 };

        var vm = new SettingsViewModel(
            new FakeStartupService(),
            new FakeDensityService(),
            hasTouchHardware: false,
            fakeVisual,
            fakeCap,
            fakeEffect);

        string detail = vm.VisualEffectsDetail;
        Assert.Contains("Desativado", detail);
    }

    [Fact]
    public void IsTechniqueSelectorVisible_True_WhenEffectsEnabledAndBlurSupported()
    {
        var vm = new SettingsViewModel(
            new FakeStartupService(),
            new FakeDensityService(),
            hasTouchHardware: false,
            new FakeVisualSettingsService { EnableWindows11VisualEffects = true },
            new FakeVisualCapabilityService { SupportedTier = WindowsVisualTier.ModernBackdrop });

        Assert.True(vm.IsTechniqueSelectorVisible);
    }

    [Fact]
    public void IsTechniqueSelectorVisible_False_WhenEffectsDisabled()
    {
        var vm = new SettingsViewModel(
            new FakeStartupService(),
            new FakeDensityService(),
            hasTouchHardware: false,
            new FakeVisualSettingsService { EnableWindows11VisualEffects = false },
            new FakeVisualCapabilityService { SupportedTier = WindowsVisualTier.ModernBackdrop });

        Assert.False(vm.IsTechniqueSelectorVisible);
    }

    [Fact]
    public void PreferredTechnique_UpdatesPropertyAndService()
    {
        var fakeVisual = new FakeVisualSettingsService { PreferredTechnique = VisualEffectTechnique.Auto };
        var vm = new SettingsViewModel(
            new FakeStartupService(),
            new FakeDensityService(),
            hasTouchHardware: false,
            fakeVisual,
            new FakeVisualCapabilityService());

        Assert.True(vm.IsTechniqueAuto);
        Assert.False(vm.IsTechniqueAcrylic);
        Assert.False(vm.IsTechniqueClassicBlur);

        vm.IsTechniqueClassicBlur = true;

        Assert.Equal(VisualEffectTechnique.ClassicBlur, vm.PreferredTechnique);
        Assert.Equal(VisualEffectTechnique.ClassicBlur, fakeVisual.PreferredTechnique);
        Assert.True(vm.IsTechniqueClassicBlur);
        Assert.False(vm.IsTechniqueAuto);
    }

    [Fact]
    public void AcrylicOptionToolTip_ExplainsRequirement_WhenNotSupported()
    {
        var vm = new SettingsViewModel(
            new FakeStartupService(),
            new FakeDensityService(),
            hasTouchHardware: false,
            new FakeVisualSettingsService(),
            new FakeVisualCapabilityService { SupportedTier = WindowsVisualTier.ClassicBlur }); // 14393 (ClassicBlur)

        Assert.False(vm.IsAcrylicSupported);
        Assert.Contains("1803", vm.AcrylicOptionToolTip);
        Assert.Contains("17134", vm.AcrylicOptionToolTip);
    }
}
