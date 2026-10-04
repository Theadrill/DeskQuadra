namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato do serviço de persistência da preferência de efeitos visuais e transparência do Windows 11.
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

    /// <summary>
    /// Opacidade recomendada das Quadras (5 a 90, padrão 30%).
    /// </summary>
    double GeneralOpacity { get; }

    /// <summary>
    /// Indica se o modo avançado de múltiplos sliders está ativo.
    /// </summary>
    bool IsAdvancedMode { get; }

    /// <summary>
    /// Transparência do fundo WPF da Quadra (5 a 90, padrão 28%).
    /// </summary>
    double BackgroundAlpha { get; }

    /// <summary>
    /// Escurecimento / Tint do DWM (0 a 100, padrão 15%).
    /// </summary>
    double TintIntensity { get; }

    /// <summary>
    /// Atualiza e persiste a opacidade geral.
    /// </summary>
    void SetGeneralOpacity(double opacity);

    /// <summary>
    /// Atualiza e persiste a preferência de modo avançado.
    /// </summary>
    void SetAdvancedMode(bool isAdvanced);

    /// <summary>
    /// Atualiza e persiste a transparência de fundo (Alpha).
    /// </summary>
    void SetBackgroundAlpha(double alpha);

    /// <summary>
    /// Atualiza e persiste o escurecimento / tint DWM.
    /// </summary>
    void SetTintIntensity(double tint);

    /// <summary>
    /// Restaura os valores de transparência e tint para os padrões de fábrica (30%, 28%, 15%).
    /// </summary>
    void ResetToDefaults();

    /// <summary>
    /// Evento disparado quando qualquer parâmetro de opacidade ou tint é modificado.
    /// </summary>
    event EventHandler? VisualOpacityChanged;
}
