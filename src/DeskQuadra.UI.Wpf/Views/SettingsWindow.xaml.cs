using System;
using System.Windows;
using System.Windows.Interop;
using DeskQuadra.Core.Contracts;
using DeskQuadra.UI.Wpf.ViewModels;

namespace DeskQuadra.UI.Wpf.Views;

public partial class SettingsWindow : Window
{
    private readonly IWindowVisualEffectService? _visualEffectService;

    public SettingsWindow(SettingsViewModel viewModel, IWindowVisualEffectService? visualEffectService = null)
    {
        _visualEffectService = visualEffectService;
        DataContext = viewModel;
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        if (helper.Handle != IntPtr.Zero && _visualEffectService != null)
        {
            try
            {
                _visualEffectService.ApplyBlur(helper.Handle);
            }
            catch
            {
                // Silencioso
            }
        }
    }

    private void ResetOpacity_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.ResetToDefaults();
        }
    }
}
