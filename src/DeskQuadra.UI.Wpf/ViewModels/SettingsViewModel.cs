using System.Windows;
using DeskQuadra.Core;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;

namespace DeskQuadra.UI.Wpf.ViewModels;

/// <summary>
/// ViewModel da janela Configurações (Fatia 1: autostart; Fatia 2: densidade Aparência).
/// Lê o estado real no ctor (registry + settings.json); setters persistem.
/// Hardware touch é snapshot sob demanda na abertura (sem hook/timer).
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly IStartupService _startupService;
    private readonly IDensitySettingsService _densitySettings;
    private readonly IVisualSettingsService? _visualSettings;
    private readonly IWindowsVisualCapabilityService? _visualCapability;
    private readonly IWindowVisualEffectService? _visualEffectService;
    private readonly bool _hasTouchHardware;
    private bool _startWithWindows;
    private bool _enableWindows11VisualEffects;
    private DensityPreference _densityPreference;

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (SetProperty(ref _startWithWindows, value))
            {
                _startupService.SetEnabled(value);
            }
        }
    }

    public bool IsVisualEffectsSupported => _visualCapability?.IsBlurSupported ?? false;
    public bool IsAcrylicSupported => _visualCapability?.IsAcrylicSupported ?? false;
    public bool IsTechniqueSelectorVisible => IsVisualEffectsSupported && EnableWindows11VisualEffects;

    public string AcrylicOptionToolTip => IsAcrylicSupported
        ? "Acrílico com textura fluida e iluminação do Windows 11."
        : "Requer Windows 10 versão 1803 (Build 17134) ou superior.";

    public bool EnableWindows11VisualEffects
    {
        get => _enableWindows11VisualEffects;
        set
        {
            if (SetProperty(ref _enableWindows11VisualEffects, value))
            {
                _visualSettings?.SetEnableWindows11VisualEffects(value);
                OnPropertyChanged(nameof(IsTechniqueSelectorVisible));
                OnPropertyChanged(nameof(VisualEffectsDetail));
            }
        }
    }

    public VisualEffectTechnique PreferredTechnique
    {
        get => _visualSettings?.PreferredTechnique ?? VisualEffectTechnique.Auto;
        set
        {
            if (_visualSettings != null && _visualSettings.PreferredTechnique != value)
            {
                _visualSettings.SetPreferredTechnique(value);
                OnPropertyChanged(nameof(PreferredTechnique));
                OnPropertyChanged(nameof(IsTechniqueAuto));
                OnPropertyChanged(nameof(IsTechniqueAcrylic));
                OnPropertyChanged(nameof(IsTechniqueClassicBlur));
                OnPropertyChanged(nameof(VisualEffectsDetail));
            }
        }
    }

    public bool IsTechniqueAuto
    {
        get => PreferredTechnique == VisualEffectTechnique.Auto;
        set { if (value) PreferredTechnique = VisualEffectTechnique.Auto; }
    }

    public bool IsTechniqueAcrylic
    {
        get => PreferredTechnique == VisualEffectTechnique.Acrylic;
        set { if (value) PreferredTechnique = VisualEffectTechnique.Acrylic; }
    }

    public bool IsTechniqueClassicBlur
    {
        get => PreferredTechnique == VisualEffectTechnique.ClassicBlur;
        set { if (value) PreferredTechnique = VisualEffectTechnique.ClassicBlur; }
    }

    public string VisualEffectsDetail
    {
        get
        {
            if (!IsVisualEffectsSupported)
            {
                int b = _visualCapability?.WindowsBuildNumber ?? 0;
                return $"Não suportado (Hardware básico ou Build {b} inferior à 14393 - Tema Sólido ativo)";
            }

            if (!EnableWindows11VisualEffects)
            {
                return "Desativado (Modo Clássico Sólido - sem desfoque)";
            }

            string quadraTech = _visualEffectService?.LastQuadraEffectApplied ?? "Pendente";
            string menuTech = _visualEffectService?.LastMenuEffectApplied ?? "Pendente";
            int build = _visualCapability?.WindowsBuildNumber ?? 0;

            return $"• Quadras: {quadraTech}\n• Menus: {menuTech}\n• Versão do SO: Windows Build {build}";
        }
    }

    // Sliders de Transparência e Modo Avançado
    private double _generalOpacity = 30.0;
    private bool _isAdvancedMode = false;
    private double _backgroundAlpha = 28.0;
    private double _tintIntensity = 15.0;

    public double GeneralOpacity
    {
        get => _generalOpacity;
        set
        {
            double clamped = Math.Clamp(value, 5.0, 90.0);
            if (SetProperty(ref _generalOpacity, clamped))
            {
                OnPropertyChanged(nameof(GeneralOpacityLabel));
                _visualSettings?.SetGeneralOpacity(clamped);
                if (!_isAdvancedMode)
                {
                    _backgroundAlpha = Math.Clamp(clamped * 0.933, 5.0, 90.0);
                    _tintIntensity = Math.Clamp(clamped * 0.5, 0.0, 100.0);
                    OnPropertyChanged(nameof(BackgroundAlpha));
                    OnPropertyChanged(nameof(BackgroundAlphaLabel));
                    OnPropertyChanged(nameof(TintIntensity));
                    OnPropertyChanged(nameof(TintIntensityLabel));
                }
            }
        }
    }

    public string GeneralOpacityLabel => $"{(int)GeneralOpacity}%";

    public bool IsSingleSliderVisible => !IsAdvancedMode;

    public bool IsAdvancedMode
    {
        get => _isAdvancedMode;
        set
        {
            if (SetProperty(ref _isAdvancedMode, value))
            {
                if (value)
                {
                    // Ao abrir os ajustes avançados, os sliders duplos refletem imediatamente o visual atual do slider único
                    _backgroundAlpha = Math.Clamp(_generalOpacity * 0.933, 5.0, 90.0);
                    _tintIntensity = Math.Clamp(_generalOpacity * 0.5, 0.0, 100.0);
                    OnPropertyChanged(nameof(BackgroundAlpha));
                    OnPropertyChanged(nameof(BackgroundAlphaLabel));
                    OnPropertyChanged(nameof(TintIntensity));
                    OnPropertyChanged(nameof(TintIntensityLabel));
                }

                OnPropertyChanged(nameof(IsSingleSliderVisible));
                _visualSettings?.SetAdvancedMode(value);
            }
        }
    }

    public double BackgroundAlpha
    {
        get => _backgroundAlpha;
        set
        {
            double clamped = Math.Clamp(value, 5.0, 90.0);
            if (SetProperty(ref _backgroundAlpha, clamped))
            {
                OnPropertyChanged(nameof(BackgroundAlphaLabel));
                _visualSettings?.SetBackgroundAlpha(clamped);
            }
        }
    }

    public string BackgroundAlphaLabel => $"{(int)BackgroundAlpha}%";

    public double TintIntensity
    {
        get => _tintIntensity;
        set
        {
            double clamped = Math.Clamp(value, 0.0, 100.0);
            if (SetProperty(ref _tintIntensity, clamped))
            {
                OnPropertyChanged(nameof(TintIntensityLabel));
                _visualSettings?.SetTintIntensity(clamped);
            }
        }
    }

    public string TintIntensityLabel => $"{(int)TintIntensity}%";

    public void ResetToDefaults()
    {
        _visualSettings?.ResetToDefaults();
        GeneralOpacity = 30.0;
        BackgroundAlpha = 28.0;
        TintIntensity = 15.0;
        OnPropertyChanged(nameof(GeneralOpacity));
        OnPropertyChanged(nameof(GeneralOpacityLabel));
        OnPropertyChanged(nameof(BackgroundAlpha));
        OnPropertyChanged(nameof(BackgroundAlphaLabel));
        OnPropertyChanged(nameof(TintIntensity));
        OnPropertyChanged(nameof(TintIntensityLabel));
    }

    public DensityPreference DensityPreference
    {
        get => _densityPreference;
        set
        {
            if (SetProperty(ref _densityPreference, value))
            {
                _densitySettings.Set(value);
                OnPropertyChanged(nameof(IsDensityAuto));
                OnPropertyChanged(nameof(IsDensityNormal));
                OnPropertyChanged(nameof(IsDensityTouch));
                OnPropertyChanged(nameof(EffectiveIsTouch));
                OnPropertyChanged(nameof(DialogControlHeight));
                OnPropertyChanged(nameof(DialogSliderThumbSize));
                OnPropertyChanged(nameof(DialogItemMargin));
                OnPropertyChanged(nameof(DialogButtonPadding));
            }
        }
    }

    public double DialogControlHeight => EffectiveIsTouch ? 44.0 : 32.0;
    public double DialogSliderThumbSize => EffectiveIsTouch ? 24.0 : 14.0;
    public Thickness DialogItemMargin => EffectiveIsTouch ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 0, 8);
    public Thickness DialogButtonPadding => EffectiveIsTouch ? new Thickness(12, 10, 12, 10) : new Thickness(8, 6, 8, 6);

    public bool IsDensityAuto
    {
        get => _densityPreference == DensityPreference.Auto;
        set { if (value) DensityPreference = DensityPreference.Auto; }
    }

    public bool IsDensityNormal
    {
        get => _densityPreference == DensityPreference.Normal;
        set { if (value) DensityPreference = DensityPreference.Normal; }
    }

    public bool IsDensityTouch
    {
        get => _densityPreference == DensityPreference.Touch;
        set { if (value) DensityPreference = DensityPreference.Touch; }
    }

    public bool EffectiveIsTouch => DensityResolver.ResolveIsTouch(_densityPreference, _hasTouchHardware);

    public SettingsViewModel(
        IStartupService startupService,
        IDensitySettingsService densitySettings,
        bool hasTouchHardware,
        IVisualSettingsService? visualSettings = null,
        IWindowsVisualCapabilityService? visualCapability = null,
        IWindowVisualEffectService? visualEffectService = null)
    {
        _startupService = startupService;
        _densitySettings = densitySettings;
        _visualSettings = visualSettings;
        _visualCapability = visualCapability;
        _visualEffectService = visualEffectService;
        _hasTouchHardware = hasTouchHardware;
        _startWithWindows = startupService.IsEnabled();
        _densityPreference = densitySettings.Current;
        _enableWindows11VisualEffects = visualSettings?.EnableWindows11VisualEffects ?? true;
        if (visualSettings != null)
        {
            _generalOpacity = visualSettings.GeneralOpacity;
            _isAdvancedMode = visualSettings.IsAdvancedMode;
            _backgroundAlpha = visualSettings.BackgroundAlpha;
            _tintIntensity = visualSettings.TintIntensity;
        }
    }
}

