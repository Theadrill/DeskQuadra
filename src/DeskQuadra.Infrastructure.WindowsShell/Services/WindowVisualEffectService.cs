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

    // Cor de tint suave para transparência translúcida (ARGB: A=0x01 para permitir desfoque sem pintar camada opaca escura)
    public const uint DefaultAcrylicColor = 0x01141418;

    public string LastQuadraEffectApplied { get; private set; } = "Não inicializado";
    public string LastMenuEffectApplied { get; private set; } = "Não inicializado";

    public bool IsModernBackdropSupported =>
        _capabilityService.SupportedTier == WindowsVisualTier.ModernBackdrop;

    public WindowVisualEffectService(
        IWindowsVisualCapabilityService capabilityService,
        IVisualSettingsService settingsService)
    {
        _capabilityService = capabilityService;
        _settingsService = settingsService;
    }

    public bool ApplyBlur(IntPtr windowHandle, uint accentColor = 0)
    {
        return ApplyBlur(windowHandle, VisualEffectTarget.QuadraWindow, accentColor);
    }

    public bool ApplyBlur(IntPtr windowHandle, VisualEffectTarget target, uint accentColor = 0)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        // Se o usuário desativou a opção ou se o hardware não suportar blur, remove qualquer efeito
        if (!_settingsService.EnableWindows11VisualEffects || !_capabilityService.IsBlurSupported)
        {
            RemoveBlur(windowHandle);
            string disabledMsg = "Desativado (Tema Sólido Clássico)";
            if (target == VisualEffectTarget.QuadraWindow) LastQuadraEffectApplied = disabledMsg;
            else LastMenuEffectApplied = disabledMsg;
            System.Diagnostics.Debug.WriteLine($"[VisualEffect] {target}: {disabledMsg}");
            return false;
        }

        try
        {
            // No Windows 11 (Build 22000+), ativa cantos arredondados nativos no HWND
            if (_capabilityService.WindowsBuildNumber >= WindowsVisualCapabilityService.Windows11RtmBuild)
            {
                int cornerPref = (int)NativeMethods.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(
                    windowHandle,
                    NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref cornerPref,
                    sizeof(int));
            }

            bool isLayered = IsLayeredWindow(windowHandle);

            // 1. Janelas não-camadas (Quadra sem WS_EX_LAYERED ou diálogos) no Windows 11 Build 22621+ usam DWM Backdrop oficial
            if (!isLayered && _capabilityService.SupportedTier == WindowsVisualTier.ModernBackdrop)
            {
                if (ApplyModernBackdrop(windowHandle))
                {
                    string modernMsg = $"Windows 11 DWM Backdrop Oficial (Acrylic / Build {_capabilityService.WindowsBuildNumber})";
                    if (target == VisualEffectTarget.QuadraWindow) LastQuadraEffectApplied = modernMsg;
                    else LastMenuEffectApplied = modernMsg;
                    System.Diagnostics.Debug.WriteLine($"[VisualEffect] {target}: {modernMsg}");
                    return true;
                }
            }

            // 2. Determina a técnica efetiva baseada na preferência do usuário e na capacidade do sistema:
            VisualEffectTechnique preferred = _settingsService.PreferredTechnique;
            bool useAcrylic;

            if (preferred == VisualEffectTechnique.Acrylic)
            {
                useAcrylic = _capabilityService.IsAcrylicSupported;
            }
            else if (preferred == VisualEffectTechnique.ClassicBlur)
            {
                useAcrylic = false;
            }
            else // Auto
            {
                useAcrylic = _capabilityService.IsAcrylicSupported;
            }

            if (accentColor == 0 && target == VisualEffectTarget.QuadraWindow)
            {
                double tintPct = _settingsService.IsAdvancedMode
                    ? _settingsService.TintIntensity
                    : (_settingsService.GeneralOpacity * 0.5);
                byte tintAlpha = (byte)Math.Clamp((int)Math.Round(tintPct * 2.55), 1, 240);
                accentColor = ((uint)tintAlpha << 24) | 0x00181414;
            }

            bool result = useAcrylic
                ? ApplyAcrylicAccentPolicy(windowHandle, accentColor)
                : ApplyClassicAccentPolicy(windowHandle, accentColor);

            if (result)
            {
                string techLabel;
                if (useAcrylic)
                {
                    techLabel = _capabilityService.WindowsBuildNumber >= WindowsVisualCapabilityService.Windows11RtmBuild
                        ? "Acrílico Moderno (Win 11)"
                        : "Acrílico Fluent (Win 10)";
                }
                else
                {
                    techLabel = "Blur Clássico (Win 10)";
                }

                string msg = target == VisualEffectTarget.QuadraWindow
                    ? $"{techLabel} / Build {_capabilityService.WindowsBuildNumber}"
                    : $"{techLabel} (Menus) / Build {_capabilityService.WindowsBuildNumber}";

                if (target == VisualEffectTarget.QuadraWindow) LastQuadraEffectApplied = msg;
                else LastMenuEffectApplied = msg;
                System.Diagnostics.Debug.WriteLine($"[VisualEffect] {target}: {msg}");
            }
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VisualEffect] Erro ao aplicar blur em {target}: {ex.Message}");
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
                NativeMethods.SetWindowRgn(windowHandle, IntPtr.Zero, true);
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
            // Garante que a superfície de composição do WPF não pinte um fundo preto/branco opaco
            try
            {
                var source = System.Windows.Interop.HwndSource.FromHwnd(windowHandle);
                if (source?.CompositionTarget != null)
                {
                    source.CompositionTarget.BackgroundColor = System.Windows.Media.Colors.Transparent;
                }
            }
            catch { }

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
            // DWMSBT_TRANSIENTWINDOW (3) = Acrylic nativo (desfoque e transparência real de janelas e flyouts)
            int backdropType = (int)NativeMethods.DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW;
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

    private static bool IsLayeredWindow(IntPtr windowHandle)
    {
        try
        {
            int exStyle = NativeMethods.GetWindowLong(windowHandle, NativeMethods.GWL_EXSTYLE);
            return (exStyle & NativeMethods.WS_EX_LAYERED) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool ApplyAcrylicAccentPolicy(IntPtr windowHandle, uint accentColor)
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
            if (result != 0)
            {
                return true;
            }

            return ApplyClassicAccentPolicy(windowHandle, accentColor);
        }
        catch
        {
            return ApplyClassicAccentPolicy(windowHandle, accentColor);
        }
        finally
        {
            Marshal.FreeHGlobal(policyPtr);
        }
    }

    private static bool ApplyClassicAccentPolicy(IntPtr windowHandle, uint accentColor)
    {
        var policy = new NativeMethods.AccentPolicy
        {
            AccentState = NativeMethods.AccentState.ACCENT_ENABLE_BLURBEHIND,
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

    /// <summary>
    /// Aplica recorte de região arredondada (SetWindowRgn) no HWND com precisão de DPI,
    /// eliminando o triângulo de pixels nos 4 cantos da janela causados pela curvatura do XAML.
    /// </summary>
    public static void ApplyRoundedWindowRegion(IntPtr windowHandle, int cornerRadius = 8)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            if (!NativeMethods.GetWindowRect(windowHandle, out var rect))
            {
                return;
            }

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
            {
                return;
            }

            uint dpi = NativeMethods.GetDpiForWindow(windowHandle);
            if (dpi == 0)
            {
                dpi = 96;
            }

            int physicalRadius = (int)Math.Round(cornerRadius * (dpi / 96.0));
            int ellipse = physicalRadius * 2;

            IntPtr rgn = NativeMethods.CreateRoundRectRgn(0, 0, width + 1, height + 1, ellipse, ellipse);
            if (rgn != IntPtr.Zero)
            {
                NativeMethods.SetWindowRgn(windowHandle, rgn, true);
            }
        }
        catch
        {
            // Silencioso: fallback gracioso
        }
    }

    /// <summary>
    /// Remove o recorte de região do HWND restaurando o comportamento retangular nativo.
    /// </summary>
    public static void RemoveRoundedWindowRegion(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            NativeMethods.SetWindowRgn(windowHandle, IntPtr.Zero, true);
        }
        catch
        {
            // Silencioso
        }
    }
}
