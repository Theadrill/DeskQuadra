using DeskQuadra.Core.Contracts;
using DeskQuadra.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace DeskQuadra.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistenceInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ILayoutRepository, JsonLayoutRepository>();
        services.AddSingleton<IDensitySettingsService, JsonDensitySettingsService>();
        services.AddSingleton<IVisualSettingsService, JsonVisualSettingsService>();
        return services;
    }
}

