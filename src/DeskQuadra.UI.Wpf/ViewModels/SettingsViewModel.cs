using DeskQuadra.Core.Contracts;

namespace DeskQuadra.UI.Wpf.ViewModels;

/// <summary>
/// ViewModel da janela Configurações (Fatia 1: só autostart).
/// Lê o estado real do registry no ctor; o setter persiste via <see cref="IStartupService"/>.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly IStartupService _startupService;
    private bool _startWithWindows;

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

    public SettingsViewModel(IStartupService startupService)
    {
        _startupService = startupService;
        _startWithWindows = startupService.IsEnabled();
    }
}
