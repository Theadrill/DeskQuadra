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
    private readonly bool _hasTouchHardware;
    private bool _startWithWindows;
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

    public SettingsViewModel(IStartupService startupService, IDensitySettingsService densitySettings, bool hasTouchHardware)
    {
        _startupService = startupService;
        _densitySettings = densitySettings;
        _hasTouchHardware = hasTouchHardware;
        _startWithWindows = startupService.IsEnabled();
        _densityPreference = densitySettings.Current;
    }
}
