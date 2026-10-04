using System.Runtime.InteropServices;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.Infrastructure.WindowsShell.Services;

/// <summary>
/// Implementação do serviço de efeitos visuais via SetWindowCompositionAttribute (AccentPolicy clássica do Windows 10/11).
/// </summary>
public sealed class WindowVisualEffectService : IWindowVisualEffectService
{
    private readonly IWindowsVisualCapabilityService _capabilityService;
    private readonly IVisualSettingsService _settingsService;

    // Cor do tint escuro translúcido padrão do Fluent (ARGB: A=0xCC, R=0x1E, G=0x1E, B=0x24)
    // O AccentPolicy GradientColor espera o formato BGR / ABGR: (A << 24) | (B << 16) | (G << 8) | R
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

        // Se o usuário desativou a opção ou se o hardware não suportar blur, não aplica
        if (!_settingsService.EnableWindows11VisualEffects || !_capabilityService.IsBlurSupported)
        {
            return false;
        }

        try
        {
            uint color = accentColor != 0 ? accentColor : DefaultAcrylicColor;

            var policy = new NativeMethods.AccentPolicy
            {
                AccentState = NativeMethods.AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                AccentFlags = 2, // 2 = desenha todas as bordas com blend
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
        catch
        {
            // Silencioso, padrão do projeto: falha de DWM nunca derruba a janela
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

                int result = NativeMethods.SetWindowCompositionAttribute(windowHandle, ref data);
                return result != 0;
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
}
