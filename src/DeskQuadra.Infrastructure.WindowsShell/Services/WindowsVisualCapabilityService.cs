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
    // Windows 10 inicial (Threshold 1): Build 10240
    // Windows 11 RTM (21H2): Build 22000
    // Windows 11 22H2 (Moment 1 / Backdrops oficiais DWMWA_SYSTEMBACKDROP_TYPE): Build 22621
    public const int Windows10InitialBuild = 10240;
    public const int Windows11RtmBuild = 22000;
    public const int Windows11ModernBackdropBuild = 22621;

    private readonly Func<bool>? _customHardwareCheck;
    private readonly int? _overrideBuildNumber;

    public int WindowsBuildNumber { get; }

    public bool IsHardwareAccelerationEnabled { get; }

    public WindowsVisualTier SupportedTier { get; }

    public bool IsBlurSupported => SupportedTier != WindowsVisualTier.Basic;

    public WindowsVisualCapabilityService(
        int? overrideBuildNumber = null,
        Func<bool>? customHardwareCheck = null)
    {
        _overrideBuildNumber = overrideBuildNumber;
        _customHardwareCheck = customHardwareCheck;

        WindowsBuildNumber = _overrideBuildNumber ?? ReadBuildNumberFromRegistry();
        IsHardwareAccelerationEnabled = _customHardwareCheck != null
            ? _customHardwareCheck()
            : CheckHardwareAccelerationAndDwm();

        SupportedTier = EvaluateTier(WindowsBuildNumber, IsHardwareAccelerationEnabled);
    }

    public static WindowsVisualTier EvaluateTier(int buildNumber, bool isHardwareAccelerationEnabled)
    {
        // Se a aceleração gráfica ou o DWM não estiverem disponíveis, retorna Basic para segurança
        if (!isHardwareAccelerationEnabled)
        {
            return WindowsVisualTier.Basic;
        }

        if (buildNumber >= Windows11ModernBackdropBuild)
        {
            return WindowsVisualTier.ModernBackdrop;
        }

        if (buildNumber >= Windows10InitialBuild)
        {
            return WindowsVisualTier.ClassicBlur;
        }

        return WindowsVisualTier.Basic;
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
