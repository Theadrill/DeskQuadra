using Microsoft.Win32;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Serviço de infraestrutura para detecção de capacidades do sistema operacional e hardware gráfico.
/// Lê a build real do Windows via registro (HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion)
/// e valida a composição do DWM e o nível de aceleração gráfica por hardware.
/// </summary>
public sealed class WindowsVisualCapabilityService : IWindowsVisualCapabilityService
{
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private const string CurrentBuildNumberValue = "CurrentBuildNumber";

    // Thresholds oficiais do Windows:
    // Base mínima oficial do DeskQuadra (Windows 10 1607 - Anniversary Update / .NET 8 floor): Build 14393
    // Introdução do Acrílico com textura no Windows 10 (1803 - Redstone 4): Build 17134
    // Windows 11 RTM (21H2): Build 22000
    // Windows 11 22H2 (Moment 1): Build 22621
    public const int Windows10MinBuild = 14393;
    public const int Windows10AcrylicBuild = 17134;
    public const int Windows11RtmBuild = 22000;
    public const int Windows11ModernBackdropBuild = 22621;

    // Retrocompatibilidade
    public const int Windows10InitialBuild = 14393;

    private readonly Func<bool>? _customHardwareCheck;
    private readonly int? _overrideBuildNumber;

    public int WindowsBuildNumber { get; }

    public bool IsHardwareAccelerationEnabled { get; }

    public WindowsVisualTier SupportedTier { get; }

    public bool IsBlurSupported => SupportedTier != WindowsVisualTier.Basic;

    public bool IsAcrylicSupported => SupportedTier == WindowsVisualTier.ModernBackdrop;

    public WindowsVisualCapabilityService(
        int? overrideBuildNumber = null,
        Func<bool>? customHardwareCheck = null)
    {
        _overrideBuildNumber = overrideBuildNumber;
        _customHardwareCheck = customHardwareCheck;

        WindowsBuildNumber = ResolveSimulatedOrRealBuildNumber(_overrideBuildNumber);
        IsHardwareAccelerationEnabled = ResolveSimulatedOrRealHardwareAcceleration(_customHardwareCheck);

        SupportedTier = EvaluateTier(WindowsBuildNumber, IsHardwareAccelerationEnabled);
    }

    public static WindowsVisualTier EvaluateTier(int buildNumber, bool isHardwareAccelerationEnabled)
    {
        // Se a aceleração gráfica ou o DWM não estiverem disponíveis, ou se a build for inferior à mínima suportada (14393)
        if (!isHardwareAccelerationEnabled || buildNumber < Windows10MinBuild)
        {
            return WindowsVisualTier.Basic;
        }

        // Build >= 17134 (Windows 10 1803+ e Windows 11): suporte a Acrílico Moderno e Blur Clássico
        if (buildNumber >= Windows10AcrylicBuild)
        {
            return WindowsVisualTier.ModernBackdrop;
        }

        // Build entre 14393 e 17133 (Windows 10 1607 a 1709): suporte apenas a Blur Clássico
        if (buildNumber >= Windows10MinBuild)
        {
            return WindowsVisualTier.ClassicBlur;
        }

        return WindowsVisualTier.Basic;
    }

    public static int ResolveSimulatedOrRealBuildNumber(int? explicitOverride = null)
    {
        if (explicitOverride.HasValue)
        {
            return explicitOverride.Value;
        }

        try
        {
            // 1. Variável de ambiente DESKQUADRA_SIMULATE_BUILD
            string? envBuild = Environment.GetEnvironmentVariable("DESKQUADRA_SIMULATE_BUILD");
            if (!string.IsNullOrWhiteSpace(envBuild) && int.TryParse(envBuild.Trim(), out int bEnv))
            {
                return bEnv;
            }

            // 2. Argumentos de linha de comando (--simulate-build 14393)
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--simulate-build", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1].Trim(), out int bArg))
                    {
                        return bArg;
                    }
                }
            }
        }
        catch
        {
            // Silencioso em caso de restrições de permissão
        }

        return ReadBuildNumberFromRegistry();
    }

    public static bool ResolveSimulatedOrRealHardwareAcceleration(Func<bool>? explicitCheck = null)
    {
        if (explicitCheck != null)
        {
            return explicitCheck();
        }

        try
        {
            // 1. Variável de ambiente DESKQUADRA_SIMULATE_NO_GPU
            string? envNoGpu = Environment.GetEnvironmentVariable("DESKQUADRA_SIMULATE_NO_GPU");
            if (!string.IsNullOrWhiteSpace(envNoGpu) &&
                (string.Equals(envNoGpu.Trim(), "1", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(envNoGpu.Trim(), "true", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            // 2. Argumentos de linha de comando (--simulate-no-gpu)
            string[] args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (string.Equals(arg, "--simulate-no-gpu", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }
        catch
        {
            // Silencioso
        }

        return CheckHardwareAccelerationAndDwm();
    }

    private static int ReadBuildNumberFromRegistry()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(CurrentVersionKey);
            if (key != null)
            {
                object? buildObj = key.GetValue(CurrentBuildNumberValue);
                if (buildObj is string buildStr && int.TryParse(buildStr, out int buildNumber))
                {
                    return buildNumber;
                }

                if (buildObj is int buildInt)
                {
                    return buildInt;
                }
            }
        }
        catch
        {
            // Silencioso em caso de restrições de permissão: fallback para Environment.OSVersion
        }

        return Environment.OSVersion.Version.Build;
    }

    private static bool CheckHardwareAccelerationAndDwm()
    {
        try
        {
            // 1. Checar se o DWM está com a composição ativa
            bool dwmEnabled = false;
            try
            {
                NativeMethods.DwmIsCompositionEnabled(out dwmEnabled);
            }
            catch
            {
                dwmEnabled = false;
            }

            if (!dwmEnabled)
            {
                return false;
            }

            // 2. Checar se estamos em sessão de Terminal Services (RDP) onde blur é contra-indicado
            // SystemParameters.IsRemoteSession não requer WPF direto se checado via Win32 GetSystemMetrics(0x1000 = SM_REMOTESESSION)
            if (NativeMethods.IsRemoteSession())
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
