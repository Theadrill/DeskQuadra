using DeskQuadra.Core.Models;

namespace DeskQuadra.Core;

/// <summary>
/// Lógica pura de densidade (sem PInvoke/UI): decisão Auto a partir de flags
/// SM_DIGITIZER + mapeamento preferência -&gt; dimensões. Coberta por xUnit.
/// </summary>
public static class DensityResolver
{
    // GetSystemMetrics indexes (documentados aqui para teste puro; PInvoke vive em WindowsShell).
    public const int SM_DIGITIZER = 94;
    public const int SM_MAXIMUMTOUCHES = 95;

    // NID_* de SM_DIGITIZER (WinUser.h).
    public const int NID_INTEGRATED_TOUCH = 0x01;
    public const int NID_EXTERNAL_TOUCH = 0x02;

    // Dimensões Normal (atual, seção 24) vs Touch (Fatia 2).
    public const double NormalTitleBarHeight = 28.0;
    public const double TouchTitleBarHeight = 42.0;
    public const double NormalTitleButtonSize = 24.0;
    public const double TouchTitleButtonHitbox = 44.0;

    public const double NormalIconSize = 38.0;
    public const double TouchIconSize = 48.0;

    /// <summary>
    /// Verdadeiro quando o hardware tem touch: digitizer indica touch integrado/externo
    /// OU SM_MAXIMUMTOUCHES &gt; 0 (cobre digitizer sem NID mas com toques reportados).
    /// </summary>
    public static bool HasTouchHardware(int digitizerFlags, int maxTouches)
    {
        if ((digitizerFlags & (NID_INTEGRATED_TOUCH | NID_EXTERNAL_TOUCH)) != 0)
        {
            return true;
        }

        return maxTouches > 0;
    }

    /// <summary>
    /// Resolve o modo efetivo: Touch/Normal forçam; Auto segue o hardware.
    /// </summary>
    public static bool ResolveIsTouch(DensityPreference preference, bool hasTouchHardware)
    {
        return preference switch
        {
            DensityPreference.Touch => true,
            DensityPreference.Normal => false,
            _ => hasTouchHardware,
        };
    }

    public static double TitleBarHeight(bool isTouch) => isTouch ? TouchTitleBarHeight : NormalTitleBarHeight;

    public static double TitleButtonSize(bool isTouch) => isTouch ? TouchTitleButtonHitbox : NormalTitleButtonSize;

    // Ícone cresce dentro da célula fixa 78x96 (não mexe no snap D9).
    public static double IconSize(bool isTouch) => isTouch ? TouchIconSize : NormalIconSize;
}
