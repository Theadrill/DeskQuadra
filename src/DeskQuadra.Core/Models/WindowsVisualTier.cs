namespace DeskQuadra.Core.Models;

/// <summary>
/// Níveis de capacidade visual e suporte a efeitos de backdrop/transparência do sistema operacional.
/// </summary>
public enum WindowsVisualTier
{
    /// <summary>
    /// Ambiente básico ou legado: drivers genéricos, RDP ou aceleração de hardware desativada.
    /// Fallback seguro: apenas transparência por software básica existente.
    /// </summary>
    Basic = 0,

    /// <summary>
    /// Windows 10 (Build 10240+) ou Windows 11 inicial com aceleração por hardware.
    /// Suporta política clássica do compositor (SetWindowCompositionAttribute / AccentPolicy).
    /// </summary>
    ClassicBlur = 1,

    /// <summary>
    /// Windows 11 Build 22621+ (22H2 em diante) com aceleração gráfica completa.
    /// Suporta DWMWA_SYSTEMBACKDROP_TYPE oficial do DWM (Mica e Acrylic nativos).
    /// </summary>
    ModernBackdrop = 2
}
