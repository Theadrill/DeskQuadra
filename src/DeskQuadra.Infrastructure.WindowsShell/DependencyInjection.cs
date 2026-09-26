using DeskQuadra.Core.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DeskQuadra.Infrastructure.WindowsShell;

public static class DependencyInjection
{
    public static IServiceCollection AddWindowsShellInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IWindowAnchorService, DesktopWindowAnchorService>();
        return services;
    }
}
