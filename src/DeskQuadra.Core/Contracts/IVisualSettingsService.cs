namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato do serviço de persistência da preferência de efeitos visuais do Windows 11.
/// Armazena a decisão do usuário em settings.json de forma resiliente (best-effort).
/// </summary>
public interface IVisualSettingsService
{
    /// <summary>
    /// Indica se os efeitos visuais avançados de transparência e desfoque do Windows 11 estão ativados pelo usuário.
    /// Padrão: true (ativado quando o sistema suportar).
    /// </summary>
    bool EnableWindows11VisualEffects { get; }

    /// <summary>
    /// Atualiza e persiste a preferência do usuário.
    /// </summary>
    void SetEnableWindows11VisualEffects(bool enabled);

    /// <summary>
    /// Evento disparado quando a preferência é alterada.
    /// </summary>
    event EventHandler<bool>? VisualEffectsChanged;

    /// <summary>
    /// Técnica visual de desfoque preferida pelo usuário (Auto, Acrylic ou ClassicBlur).
    /// Padrão: Auto.
    /// </summary>
    DeskQuadra.Core.Models.VisualEffectTechnique PreferredTechnique { get; }

    /// <summary>
    /// Atualiza e persiste a técnica de desfoque preferida pelo usuário.
    /// </summary>
    void SetPreferredTechnique(DeskQuadra.Core.Models.VisualEffectTechnique technique);

    /// <summary>
    /// Evento disparado quando a técnica preferida é alterada.
    /// </summary>
    event EventHandler<DeskQuadra.Core.Models.VisualEffectTechnique>? TechniqueChanged;
}
