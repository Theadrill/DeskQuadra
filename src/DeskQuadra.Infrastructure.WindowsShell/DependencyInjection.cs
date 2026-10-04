using DeskQuadra.Core.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Services;
using DeskQuadra.Infrastructure.WindowsShell.Shell;
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
        services.AddSingleton<IFileDeletionService, ShellFileDeletionService>();
        services.AddSingleton<IDesktopDrawingService, DesktopDrawingService>();
        services.AddSingleton<IStartupService, RegistryStartupService>();
        // T2 terceiros: detecção só leitura (lista, não executa) com cache por extensão.
        services.AddSingleton<IThirdPartyMenuService, ThirdPartyMenuService>();
        services.AddSingleton<IWindowsVisualCapabilityService, WindowsVisualCapabilityService>();
        services.AddSingleton<IWindowVisualEffectService, WindowVisualEffectService>();
        return services;
    }
}


