using DeskQuadra.Core.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DeskQuadra.Infrastructure.WindowsShell;

public static class DependencyInjection
{
    public static IServiceCollection AddWindowsShellInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IWindowAnchorService, DesktopWindowAnchorService>();
        services.AddSingleton<INativeDesktopIconService, NativeDesktopIconService>();
        services.AddSingleton<IDesktopScannerService, DesktopScannerService>();
        services.AddSingleton<IIconExtractorService, IconExtractorService>();
        services.AddSingleton<IFileLauncherService, FileLauncherService>();
        services.AddSingleton<IDesktopDrawingService, DesktopDrawingService>();
        services.AddSingleton<IStartupService, RegistryStartupService>();
        services.AddSingleton<IShellContextMenuService, ShellContextMenuService>();
        return services;
    }
}
