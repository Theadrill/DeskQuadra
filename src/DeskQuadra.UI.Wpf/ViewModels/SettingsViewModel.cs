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
            }
        }
    }

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
    }
}

