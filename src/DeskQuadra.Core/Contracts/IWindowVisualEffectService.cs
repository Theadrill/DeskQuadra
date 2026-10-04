namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato do serviço responsável por aplicar efeitos visuais de blur/acrílico/backdrop em janelas.
/// </summary>
public interface IWindowVisualEffectService
{
    /// <summary>
    /// Aplica o efeito visual apropriado (acrílico/blur) na janela identificada pelo HWND.
    /// Retorna true se o efeito foi aplicado com sucesso; false se houve fallback ou falha.
    /// </summary>
    bool ApplyBlur(IntPtr windowHandle, uint accentColor = 0);

    /// <summary>
    /// Remove qualquer efeito visual ativo no HWND da janela, retornando ao estado padrão.
    /// </summary>
    bool RemoveBlur(IntPtr windowHandle);
}
