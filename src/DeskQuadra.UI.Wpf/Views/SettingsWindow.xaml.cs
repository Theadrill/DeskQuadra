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

        // Cabe sempre na tela: trava a altura em 80% da work area (sem barra de tarefas)
        // do monitor onde abriu. O ScrollViewer interno rola o excedente — nada se perde.
        // Multi-monitor aware (DIPs por monitor); CenterScreen abre uma vez, sem reavaliação.
        MaxHeight = DeskQuadra.UI.Wpf.Services.MonitorWorkArea.GetFor(this).Height * 0.8;

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
