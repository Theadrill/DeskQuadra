using System.Windows;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell;
using DeskQuadra.UI.Wpf.ViewModels;
using DeskQuadra.UI.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DeskQuadra.UI.Wpf;

/// <summary>
/// Ponto de entrada do aplicativo DeskQuadra, configurando o contêiner de Injeção de Dependências
/// e instanciando a primeira janela viva da Quadra.
/// </summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();

        // Instancia a primeira Quadra viva para validação da Fase 1
        var initialQuadra = new Quadra(
            title: "Quadra 1",
            left: 200,
            top: 150,
            width: 340,
            height: 260,
            isDefault: true);

        var viewModel = new QuadraViewModel(initialQuadra);
        var anchorService = _serviceProvider.GetRequiredService<IWindowAnchorService>();

        var quadraWindow = new QuadraWindow(viewModel, anchorService);
        quadraWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddWindowsShellInfrastructure();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
