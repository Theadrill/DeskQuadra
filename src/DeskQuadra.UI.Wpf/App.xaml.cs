using System.Windows;
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

        // 3. Carrega o layout transacional persistido em disco (%APPDATA%\DeskQuadra\quadras.json)
        await coordinator.InitializeAsync();

        // 4. Instancia e exibe as janelas de cada Quadra ativa
        foreach (var quadra in coordinator.ActiveQuadras)
        {
            var viewModel = new QuadraViewModel(quadra, iconExtractor);
            var quadraWindow = new QuadraWindow(viewModel, anchorService, snapEngine, coordinator, launcherService);
            quadraWindow.Show();
        }
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
            var coordinator = _serviceProvider.GetService<ILayoutCoordinator>();
            coordinator?.SaveNowAsync().GetAwaiter().GetResult();

            // Restaura instantaneamente os ícones originais do Windows na saída
            _nativeIconService?.ShowDesktopIcons();
            _serviceProvider.Dispose();
        }

        base.OnExit(e);
    }
}
