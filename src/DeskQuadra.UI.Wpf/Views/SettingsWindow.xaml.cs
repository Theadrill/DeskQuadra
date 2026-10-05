using System;
using System.Windows;
using System.Windows.Interop;
using DeskQuadra.Core.Contracts;
using DeskQuadra.UI.Wpf.Services;
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
        // Multi-monitor aware (DIPs por monitor).
        MaxHeight = MonitorWorkArea.GetFor(this).Height * 0.8;

        // O CenterScreen centraliza pelo tamanho SEM a trava (Top nasce fora da tela):
        // recentraliza após o layout e segura a janela visível se ela crescer depois
        // (ex: expandindo "Ajustes Avançados" com SizeToContent).
        Loaded += SettingsWindow_Loaded;
        SizeChanged += SettingsWindow_SizeChanged;

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

    private bool _positionInitialized;

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Após o layout (ActualHeight final), não no CenterScreen prematuro.
        Dispatcher.BeginInvoke(new Action(() => ClampToWorkArea(center: true)),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void SettingsWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Crescimento posterior (ex: painel avançado): só corrige o estouro, sem recentralizar.
        if (_positionInitialized)
        {
            ClampToWorkArea(center: false);
        }
    }

    private void ClampToWorkArea(bool center)
    {
        var wa = MonitorWorkArea.GetFor(this);

        if (center || !_positionInitialized)
        {
            Left = wa.Left + Math.Max(0, (wa.Width - ActualWidth) / 2);
            Top = wa.Top + Math.Max(0, (wa.Height - ActualHeight) / 2);
            _positionInitialized = true;
        }
        else
        {
            if (Top < wa.Top)
            {
                Top = wa.Top;
            }

            if (Left < wa.Left)
            {
                Left = wa.Left;
            }

            double bottomOverflow = (Top + ActualHeight) - (wa.Top + wa.Height);
            if (bottomOverflow > 0)
            {
                Top = Math.Max(wa.Top, Top - bottomOverflow);
            }

            double rightOverflow = (Left + ActualWidth) - (wa.Left + wa.Width);
            if (rightOverflow > 0)
            {
                Left = Math.Max(wa.Left, Left - rightOverflow);
            }
        }
    }
}
