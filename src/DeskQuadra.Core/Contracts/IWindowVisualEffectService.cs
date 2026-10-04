namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Tipo de alvo da aplicação de efeitos visuais para fins de diagnóstico e estratégia de renderização.
/// </summary>
public enum VisualEffectTarget
{
    QuadraWindow,
    ContextMenu,
    Submenu,
    Dialog
}

/// <summary>
/// Contrato do serviço responsável por aplicar efeitos visuais de blur/acrílico/backdrop em janelas.
/// </summary>
public interface IWindowVisualEffectService
{
    /// <summary>
    /// Aplica o efeito visual apropriado (acrílico/blur/backdrop) na janela identificada pelo HWND.
    /// Retorna true se o efeito foi aplicado com sucesso; false se houve fallback ou falha.
    /// </summary>
    bool ApplyBlur(IntPtr windowHandle, uint accentColor = 0);

    /// <summary>
    /// Aplica o efeito visual apropriado no alvo especificado (Quadra, Menu de contexto, Submenu, Diálogo).
    /// </summary>
    bool ApplyBlur(IntPtr windowHandle, VisualEffectTarget target, uint accentColor = 0);

    /// <summary>
    /// Remove qualquer efeito visual ativo no HWND da janela, retornando ao estado padrão.
    /// </summary>
    bool RemoveBlur(IntPtr windowHandle);

    /// <summary>
    /// Diagnóstico: descrição legível da técnica aplicada para a Quadra (ModernBackdrop vs ClassicAccent).
    /// </summary>
    string LastQuadraEffectApplied { get; }

    /// <summary>
    /// Diagnóstico: descrição legível da técnica aplicada para Menus/Popups.
    /// </summary>
    string LastMenuEffectApplied { get; }

    /// <summary>
    /// Indica se o ambiente atual suporta o Backdrop Moderno nativo do Windows 11 (Build 22621+).
    /// </summary>
    bool IsModernBackdropSupported { get; }
}
