using System.Runtime.InteropServices;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Serviço de aplicação de efeitos visuais nas janelas do DeskQuadra.
/// Suporta o modo moderno nativo do Windows 11 (DWMWA_SYSTEMBACKDROP_TYPE - Mica/Acrylic com DWM)
/// e o modo clássico (SetWindowCompositionAttribute / AccentPolicy) com fallback gracioso.
/// </summary>
public sealed class WindowVisualEffectService : IWindowVisualEffectService
{
    private readonly IWindowsVisualCapabilityService _capabilityService;
    private readonly IVisualSettingsService _settingsService;

    // Cor de tint suave para transparência translúcida (ARGB: A=0xCC, R=0x1E, G=0x1E, B=0x24)
    public const uint DefaultAcrylicColor = 0xCC241E1E;

    public WindowVisualEffectService(
        IWindowsVisualCapabilityService capabilityService,
        IVisualSettingsService settingsService)
    {
        _capabilityService = capabilityService;
        _settingsService = settingsService;
    }

    public bool ApplyBlur(IntPtr windowHandle, uint accentColor = 0)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        // Se o usuário desativou a opção ou se o hardware não suportar blur, remove qualquer efeito
        if (!_settingsService.EnableWindows11VisualEffects || !_capabilityService.IsBlurSupported)
        {
            RemoveBlur(windowHandle);
            return false;
        }

        try
        {
            // 1. Tenta o método moderno do Windows 11 (Build 22621+) se compatível
            if (_capabilityService.SupportedTier == WindowsVisualTier.ModernBackdrop)
            {
                if (ApplyModernBackdrop(windowHandle))
                {
                    return true;
                }
            }

            // 2. Fallback: método clássico AccentPolicy
            return ApplyClassicAccentPolicy(windowHandle, accentColor);
        }
        catch
        {
            return false;
        }
    }

    public bool RemoveBlur(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            // Se estiver em ModernBackdrop, reseta o backdrop do DWM
            if (_capabilityService.SupportedTier == WindowsVisualTier.ModernBackdrop)
            {
                int backdropNone = (int)NativeMethods.DWM_SYSTEMBACKDROP_TYPE.DWMSBT_NONE;
                NativeMethods.DwmSetWindowAttribute(
                    windowHandle,
                    NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE,
                    ref backdropNone,
                    sizeof(int));
            }

            // Desativa AccentPolicy clássico
            var policy = new NativeMethods.AccentPolicy
            {
                AccentState = NativeMethods.AccentState.ACCENT_DISABLED,
                AccentFlags = 0,
                GradientColor = 0,
                AnimationId = 0
            };

            int sizeOfPolicy = Marshal.SizeOf(policy);
            IntPtr policyPtr = Marshal.AllocHGlobal(sizeOfPolicy);

            try
            {
                Marshal.StructureToPtr(policy, policyPtr, false);

                var data = new NativeMethods.WindowCompositionAttributeData
                {
                    Attribute = NativeMethods.WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    Data = policyPtr,
                    SizeOfData = sizeOfPolicy
                };

                NativeMethods.SetWindowCompositionAttribute(windowHandle, ref data);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(policyPtr);
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool ApplyModernBackdrop(IntPtr windowHandle)
    {
        try
        {
            // 1. Ativa Dark Mode imersivo no frame
            int darkMode = 1;
            NativeMethods.DwmSetWindowAttribute(
                windowHandle,
                NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref darkMode,
                sizeof(int));

            // 2. Cantos arredondados nativos do Windows 11
            int cornerPref = (int)NativeMethods.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(
                windowHandle,
                NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref cornerPref,
                sizeof(int));

            // 3. Estende o frame para a área cliente (-1 em todas as margens para cobrir toda a janela)
            var margins = new NativeMethods.MARGINS(-1, -1, -1, -1);
            NativeMethods.DwmExtendFrameIntoClientArea(windowHandle, ref margins);

            // 4. Aplica o Backdrop do Windows 11:
            // DWMSBT_MAINWINDOW (2) = Mica nativo (desfoca o papel de parede oficial com altíssimo desempenho)
            // ou DWMSBT_TRANSIENTWINDOW (3) = Acrylic nativo
            int backdropType = (int)NativeMethods.DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW;
            int result = NativeMethods.DwmSetWindowAttribute(
                windowHandle,
                NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE,
                ref backdropType,
                sizeof(int));

            return result == 0; // S_OK
        }
        catch
        {
            return false;
        }
    }

    private static bool ApplyClassicAccentPolicy(IntPtr windowHandle, uint accentColor)
    {
        uint color = accentColor != 0 ? accentColor : DefaultAcrylicColor;

        var policy = new NativeMethods.AccentPolicy
        {
            AccentState = NativeMethods.AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
            AccentFlags = 2,
            GradientColor = unchecked((int)color),
            AnimationId = 0
        };

        int sizeOfPolicy = Marshal.SizeOf(policy);
        IntPtr policyPtr = Marshal.AllocHGlobal(sizeOfPolicy);

        try
        {
            Marshal.StructureToPtr(policy, policyPtr, false);

            var data = new NativeMethods.WindowCompositionAttributeData
            {
                Attribute = NativeMethods.WindowCompositionAttribute.WCA_ACCENT_POLICY,
                Data = policyPtr,
                SizeOfData = sizeOfPolicy
            };

            int result = NativeMethods.SetWindowCompositionAttribute(windowHandle, ref data);
            return result != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(policyPtr);
        }
    }
}
