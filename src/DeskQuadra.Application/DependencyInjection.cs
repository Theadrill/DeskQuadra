using DeskQuadra.Application.Snap;
using Microsoft.Extensions.DependencyInjection;

namespace DeskQuadra.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<ISnapEngine, SnapEngine>();
        return services;
    }
}
