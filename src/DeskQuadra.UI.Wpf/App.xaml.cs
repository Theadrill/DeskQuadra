using System.Windows;
using DeskQuadra.Application;
using DeskQuadra.Application.Services;
using DeskQuadra.Application.Snap;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.Persistence;
using DeskQuadra.Infrastructure.WindowsShell;
using DeskQuadra.UI.Wpf.ViewModels;
using DeskQuadra.UI.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DeskQuadra.UI.Wpf;

/// <summary>
/// Ponto de entrada do aplicativo DeskQuadra, configurando o contêiner de Injeção de Dependências
/// e orquestrando o ciclo de vida das janelas de Quadras através do ILayoutCoordinator.
/// </summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();

        var coordinator = _serviceProvider.GetRequiredService<ILayoutCoordinator>();
        var anchorService = _serviceProvider.GetRequiredService<IWindowAnchorService>();
        var snapEngine = _serviceProvider.GetRequiredService<ISnapEngine>();

        // Registra ouvinte para abrir janelas de novas Quadras criadas durante a execução
        coordinator.QuadraCreated += (s, quadra) =>
        {
            Dispatcher.Invoke(() =>
            {
                var viewModel = new QuadraViewModel(quadra);
                var window = new QuadraWindow(viewModel, anchorService, snapEngine, coordinator);
                window.Show();
            });
        };

        // Carrega o layout transacional persistido em disco (%APPDATA%\DeskQuadra\quadras.json)
        await coordinator.InitializeAsync();

        foreach (var quadra in coordinator.ActiveQuadras)
        {
            var viewModel = new QuadraViewModel(quadra);
            var quadraWindow = new QuadraWindow(viewModel, anchorService, snapEngine, coordinator);
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
            _serviceProvider.Dispose();
        }

        base.OnExit(e);
    }
}
