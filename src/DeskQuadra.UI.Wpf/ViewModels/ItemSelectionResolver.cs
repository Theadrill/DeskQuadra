namespace DeskQuadra.UI.Wpf.ViewModels;

/// <summary>
/// Resolve a lista de itens alvo para operações em lote (Excluir, Remover, Copiar, Recortar, Abrir).
/// Se houver múltiplos itens selecionados e o item sob o cursor fizer parte da seleção (ou for nulo),
/// retorna toda a seleção. Se o item sob o cursor estiver fora da seleção, retorna apenas ele.
/// </summary>
public static class ItemSelectionResolver
{
    public static IReadOnlyList<DesktopItemViewModel> ResolveTargetItems(
        IEnumerable<DesktopItemViewModel> items,
        DesktopItemViewModel? fallbackItem)
    {
        if (items == null)
        {
            return fallbackItem != null ? new[] { fallbackItem } : Array.Empty<DesktopItemViewModel>();
        }

        var selected = items.Where(i => i.IsSelected).ToList();
        if (selected.Count > 0)
        {
            if (fallbackItem != null && !selected.Contains(fallbackItem))
            {
                return new[] { fallbackItem };
            }
            return selected;
        }

        if (fallbackItem != null)
        {
            return new[] { fallbackItem };
        }

        return Array.Empty<DesktopItemViewModel>();
    }
}
