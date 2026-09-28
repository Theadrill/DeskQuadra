namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato para exibir o menu de contexto clássico (nativo) do fundo do desktop.
/// </summary>
public interface IShellContextMenuService
{
    /// <summary>
    /// Mostra o menu nativo do fundo do desktop no ponto de tela informado e invoca
    /// o comando escolhido. Best-effort: falha devolve false, sem exceção.
    /// Deve rodar na thread STA da UI (o Click do WPF já é STA).
    /// </summary>
    /// <param name="ownerHwnd">HWND da janela dona (moda/z-order do popup).</param>
    /// <param name="screenX">X em pixels físicos de tela.</param>
    /// <param name="screenY">Y em pixels físicos de tela.</param>
    bool TryShowDesktopMenu(nint ownerHwnd, int screenX, int screenY);
}
