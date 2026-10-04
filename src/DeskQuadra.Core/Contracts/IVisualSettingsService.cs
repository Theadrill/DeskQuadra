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
}
