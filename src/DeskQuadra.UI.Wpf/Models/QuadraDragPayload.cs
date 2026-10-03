using DeskQuadra.UI.Wpf.ViewModels;

namespace DeskQuadra.UI.Wpf.Models;

/// <summary>
/// Dados de transferência durante operações de Drag and Drop entre Quadras.
/// </summary>
public sealed class QuadraDragPayload
{
    public Guid SourceQuadraId { get; }
    public DesktopItemViewModel Item { get; }
    public IReadOnlyList<DesktopItemViewModel> Items { get; }
    public bool WasHandledAsMove { get; set; }

    public QuadraDragPayload(Guid sourceQuadraId, DesktopItemViewModel item)
        : this(sourceQuadraId, new[] { item ?? throw new ArgumentNullException(nameof(item)) })
    {
    }

    public QuadraDragPayload(Guid sourceQuadraId, IReadOnlyList<DesktopItemViewModel> items)
    {
        SourceQuadraId = sourceQuadraId;
        Items = items ?? Array.Empty<DesktopItemViewModel>();
        Item = Items.Count > 0 ? Items[0] : null!;
    }
}
