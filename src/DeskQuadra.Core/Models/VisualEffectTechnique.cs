namespace DeskQuadra.Core.Models;

/// <summary>
/// Técnica visual selecionada pelo usuário para os efeitos de transparência e desfoque.
/// </summary>
public enum VisualEffectTechnique
{
    /// <summary>
    /// Automático: seleciona a técnica mais avançada suportada pela build do Windows
    /// (Acrílico em Build >= 17134, Blur Clássico em Build 14393 a 17133).
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Acrílico Moderno (estilo Windows 11 / Fluent Design): desfoque profundo com textura de granulação e iluminação.
    /// Requer Windows 10 versão 1803 (Build 17134) ou superior.
    /// </summary>
    Acrylic = 1,

    /// <summary>
    /// Blur / Vidro Clássico (estilo Windows 10 / Aero Glass): desfoque transparente puro sem ruído.
    /// Requer Windows 10 versão 1607 (Build 14393) ou superior.
    /// </summary>
    ClassicBlur = 2
}
