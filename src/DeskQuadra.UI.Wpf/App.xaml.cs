using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DeskQuadra.Application;
using DeskQuadra.Application.Services;
using DeskQuadra.Application.Snap;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.Persistence;
using DeskQuadra.Infrastructure.WindowsShell;
using DeskQuadra.Infrastructure.WindowsShell.Contracts;
using DeskQuadra.UI.Wpf.ViewModels;
using DeskQuadra.UI.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DeskQuadra.UI.Wpf;

/// <summary>
/// Ponto de entrada do aplicativo DeskQuadra, configurando o contêiner de Injeção de Dependências,
/// orquestrando o ciclo de vida das Quadras e garantindo a ocultação/restauração segura dos ícones nativos do desktop.
/// </summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;
    private INativeDesktopIconService? _nativeIconService;
    private IDesktopDrawingService? _drawingService;
    private DesktopSelectionWindow? _selectionWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Proteção técnica global para garantir que os ícones do Windows sejam restaurados em caso de falha não tratada
        AppDomain.CurrentDomain.UnhandledException += (s, args) => _nativeIconService?.ShowDesktopIcons();
        DispatcherUnhandledException += (s, args) => _nativeIconService?.ShowDesktopIcons();

        var services = new ServiceCollection();
        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();

        _nativeIconService = _serviceProvider.GetRequiredService<INativeDesktopIconService>();
        var coordinator = _serviceProvider.GetRequiredService<ILayoutCoordinator>();
        var anchorService = _serviceProvider.GetRequiredService<IWindowAnchorService>();
        var snapEngine = _serviceProvider.GetRequiredService<ISnapEngine>();
        var iconExtractor = _serviceProvider.GetRequiredService<IIconExtractorService>();
        var launcherService = _serviceProvider.GetRequiredService<IFileLauncherService>();
        _drawingService = _serviceProvider.GetRequiredService<IDesktopDrawingService>();

        // 1. Oculta os ícones nativos do desktop do Windows (Zero-Flicker)
        _nativeIconService.HideDesktopIcons();

        // 2. Registra ouvinte para abrir janelas de novas Quadras criadas durante a execução
        coordinator.QuadraCreated += (s, quadra) =>
        {
            Dispatcher.Invoke(() =>
            {
                var viewModel = new QuadraViewModel(quadra, iconExtractor);
                var window = new QuadraWindow(viewModel, anchorService, snapEngine, coordinator, launcherService);
                window.Show();
            });
        };

        // 3. Configura serviço de desenho de Quadra com botão direito na Área de Trabalho
        _selectionWindow = new DesktopSelectionWindow();

        _drawingService.DrawingProgress += (s, rect) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_selectionWindow != null)
                {
                    _selectionWindow.UpdateBounds(rect.Left, rect.Top, rect.Width, rect.Height);
                    if (!_selectionWindow.IsVisible)
                    {
                        _selectionWindow.Show();
                    }
                }
            });
        };

        _drawingService.DrawingCancelled += (s, ev) =>
        {
            Dispatcher.Invoke(() => _selectionWindow?.Hide());
        };

        _drawingService.DrawingCompleted += (s, rect) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_selectionWindow == null)
                {
                    return;
                }

                _selectionWindow.Hide();

                var dpi = VisualTreeHelper.GetDpi(_selectionWindow);
                double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

                double dipLeft = rect.Left / dpiX;
                double dipTop = rect.Top / dpiY;
                double dipWidth = Math.Max(200, rect.Width / dpiX);
                double dipHeight = Math.Max(140, rect.Height / dpiY);

                ShowDualCreationMenu(dipLeft, dipTop, dipWidth, dipHeight, coordinator);
            });
        };

        _drawingService.Start();

        // 4. Carrega o layout transacional persistido em disco (%APPDATA%\DeskQuadra\quadras.json)
        await coordinator.InitializeAsync();

        // 5. Instancia e exibe as janelas de cada Quadra ativa
        foreach (var quadra in coordinator.ActiveQuadras)
        {
            var viewModel = new QuadraViewModel(quadra, iconExtractor);
            var quadraWindow = new QuadraWindow(viewModel, anchorService, snapEngine, coordinator, launcherService);
            quadraWindow.Show();
        }
    }

    private static void ShowDualCreationMenu(double left, double top, double width, double height, ILayoutCoordinator coordinator)
    {
        var popup = new Popup
        {
            AllowsTransparency = true,
            Placement = PlacementMode.MousePoint,
            StaysOpen = false,
            PopupAnimation = PopupAnimation.Fade
        };

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x24)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            Effect = new DropShadowEffect
            {
                BlurRadius = 14,
                ShadowDepth = 2,
                Opacity = 0.55,
                Color = Colors.Black
            }
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical };

        var btnCreate = new Button
        {
            Content = "➕ Criar Quadra Aqui",
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8, 12, 8),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
            FontWeight = FontWeights.SemiBold,
            FontSize = 12
        };

        var btnCancel = new Button
        {
            Content = "✕ Cancelar e Exibir Menu do Windows",
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xBB)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
            FontSize = 11
        };

        btnCreate.MouseEnter += (s, e) => btnCreate.Background = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
        btnCreate.MouseLeave += (s, e) => btnCreate.Background = Brushes.Transparent;
        btnCancel.MouseEnter += (s, e) => btnCancel.Background = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
        btnCancel.MouseLeave += (s, e) => btnCancel.Background = Brushes.Transparent;

        btnCreate.Click += (s, e) =>
        {
            popup.IsOpen = false;
            int count = coordinator.ActiveQuadras.Count + 1;
            coordinator.CreateNewQuadra($"Quadra {count}", left, top, width, height);
        };

        btnCancel.Click += (s, e) =>
        {
            popup.IsOpen = false;
        };

        stack.Children.Add(btnCreate);
        stack.Children.Add(new Separator
        {
            Margin = new Thickness(4, 2, 4, 2),
            Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF))
        });
        stack.Children.Add(btnCancel);

        border.Child = stack;
        popup.Child = border;
        popup.IsOpen = true;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddWindowsShellInfrastructure();
        services.AddPersistenceInfrastructure();
        services.AddApplicationServices();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider != null)
        {
            _drawingService?.Stop();
            _selectionWindow?.Close();

            var coordinator = _serviceProvider.GetService<ILayoutCoordinator>();
            coordinator?.SaveNowAsync().GetAwaiter().GetResult();

            // Restaura instantaneamente os ícones originais do Windows na saída
            _nativeIconService?.ShowDesktopIcons();
            _serviceProvider.Dispose();
        }

        base.OnExit(e);
    }
}
