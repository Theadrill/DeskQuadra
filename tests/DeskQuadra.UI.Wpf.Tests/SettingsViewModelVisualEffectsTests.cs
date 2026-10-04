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

        public double GeneralOpacity { get; set; } = 30.0;
        public bool IsAdvancedMode { get; set; } = false;
        public double BackgroundAlpha { get; set; } = 28.0;
        public double TintIntensity { get; set; } = 15.0;
        public event EventHandler? VisualOpacityChanged;

        public void SetGeneralOpacity(double opacity)
        {
            GeneralOpacity = opacity;
            VisualOpacityChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetAdvancedMode(bool isAdvanced)
        {
            IsAdvancedMode = isAdvanced;
            VisualOpacityChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetBackgroundAlpha(double alpha)
        {
            BackgroundAlpha = alpha;
            VisualOpacityChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetTintIntensity(double tint)
        {
            TintIntensity = tint;
            VisualOpacityChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ResetToDefaults()
        {
            GeneralOpacity = 30.0;
            BackgroundAlpha = 28.0;
            TintIntensity = 15.0;
            VisualOpacityChanged?.Invoke(this, EventArgs.Empty);
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

    [Fact]
    public void OpacitySliders_DefaultValuesAndLabels_FormattedCorrectly()
    {
        var fakeService = new FakeVisualSettingsService();
        var vm = new SettingsViewModel(
            new FakeStartupService(),
            new FakeDensityService(),
            hasTouchHardware: false,
            fakeService,
            new FakeVisualCapabilityService());

        Assert.Equal(30.0, vm.GeneralOpacity);
        Assert.Equal("30%", vm.GeneralOpacityLabel);
        Assert.False(vm.IsAdvancedMode);
        Assert.Equal(28.0, vm.BackgroundAlpha);
        Assert.Equal("28%", vm.BackgroundAlphaLabel);
        Assert.Equal(15.0, vm.TintIntensity);
        Assert.Equal("15%", vm.TintIntensityLabel);

        vm.GeneralOpacity = 50.0;
        Assert.Equal("50%", vm.GeneralOpacityLabel);
        Assert.Equal(50.0, fakeService.GeneralOpacity);

        vm.BackgroundAlpha = 80.0;
        Assert.Equal("80%", vm.BackgroundAlphaLabel);
        Assert.Equal(80.0, fakeService.BackgroundAlpha);

        vm.TintIntensity = 10.0;
        Assert.Equal("10%", vm.TintIntensityLabel);
        Assert.Equal(10.0, fakeService.TintIntensity);

        vm.ResetToDefaults();
        Assert.Equal(30.0, vm.GeneralOpacity);
        Assert.Equal(28.0, vm.BackgroundAlpha);
        Assert.Equal(15.0, vm.TintIntensity);
        Assert.Equal(30.0, fakeService.GeneralOpacity);
        Assert.Equal(28.0, fakeService.BackgroundAlpha);
        Assert.Equal(15.0, fakeService.TintIntensity);
    }

    [Fact]
    public void SettingsViewModel_InitializesFromVisualSettingsService_WithPersistedValues()
    {
        var fakeService = new FakeVisualSettingsService
        {
            GeneralOpacity = 65.0,
            IsAdvancedMode = true,
            BackgroundAlpha = 45.0,
            TintIntensity = 30.0
        };

        var vm = new SettingsViewModel(
            new FakeStartupService(),
            new FakeDensityService(),
            hasTouchHardware: false,
            fakeService,
            new FakeVisualCapabilityService());

        Assert.Equal(65.0, vm.GeneralOpacity);
        Assert.Equal("65%", vm.GeneralOpacityLabel);
        Assert.True(vm.IsAdvancedMode);
        Assert.Equal(45.0, vm.BackgroundAlpha);
        Assert.Equal("45%", vm.BackgroundAlphaLabel);
        Assert.Equal(30.0, vm.TintIntensity);
        Assert.Equal("30%", vm.TintIntensityLabel);
    }

    [Fact]
    public void DialogDensityProperties_AdaptToTouchAndNormal()
    {
        var densityService = new FakeDensityService { Current = DensityPreference.Normal };
        var vm = new SettingsViewModel(
            new FakeStartupService(),
            densityService,
            hasTouchHardware: false,
            new FakeVisualSettingsService(),
            new FakeVisualCapabilityService());

        Assert.False(vm.EffectiveIsTouch);
        Assert.Equal(32.0, vm.DialogControlHeight);
        Assert.Equal(14.0, vm.DialogSliderThumbSize);
        Assert.Equal(44.0, vm.DialogSwitchWidth);
        Assert.Equal(24.0, vm.DialogSwitchHeight);
        Assert.Equal(14.0, vm.DialogSwitchThumbSize);

        vm.IsDensityTouch = true;
        Assert.True(vm.EffectiveIsTouch);
        Assert.Equal(44.0, vm.DialogControlHeight);
        Assert.Equal(24.0, vm.DialogSliderThumbSize);
        Assert.Equal(52.0, vm.DialogSwitchWidth);
        Assert.Equal(30.0, vm.DialogSwitchHeight);
        Assert.Equal(18.0, vm.DialogSwitchThumbSize);
    }

    [Fact]
    public void IsSingleSliderVisible_HidesWhenAdvancedMode_AndDualSlidersReflectCurrentVisual()
    {
        var fakeService = new FakeVisualSettingsService();
        var vm = new SettingsViewModel(
            new FakeStartupService(),
            new FakeDensityService(),
            hasTouchHardware: false,
            fakeService,
            new FakeVisualCapabilityService());

        // Por padrão, modo simples: slider único visível
        Assert.False(vm.IsAdvancedMode);
        Assert.True(vm.IsSingleSliderVisible);

        // Usuário ajusta slider único para 60%
        vm.GeneralOpacity = 60.0;
        Assert.Equal("60%", vm.GeneralOpacityLabel);

        // Usuário ativa ajustes avançados: o slider único oculta e os duplos refletem o visual de 60%
        vm.IsAdvancedMode = true;
        Assert.False(vm.IsSingleSliderVisible);
        Assert.True(vm.IsAdvancedMode);

        // BackgroundAlpha deve refletir 60 * 0.933 ~= 56% e Tint 60 * 0.5 = 30%
        Assert.Equal(Math.Round(60.0 * 0.933, 1), Math.Round(vm.BackgroundAlpha, 1));
        Assert.Equal(30.0, vm.TintIntensity);

        // Ao fechar modo avançado, slider único volta a ser visível
        vm.IsAdvancedMode = false;
        Assert.True(vm.IsSingleSliderVisible);
    }
}

