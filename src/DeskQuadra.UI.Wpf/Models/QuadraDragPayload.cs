using DeskQuadra.UI.Wpf.ViewModels;

namespace DeskQuadra.UI.Wpf.Models;

/// <summary>
/// Dados de transferência durante operações de Drag and Drop entre Quadras.
/// </summary>
public sealed class QuadraDragPayload
{
    public Guid SourceQuadraId { get; }
    public DesktopItemViewModel Item { get; }
    public bool WasHandledAsMove { get; set; }

    public QuadraDragPayload(Guid sourceQuadraId, DesktopItemViewModel item)
    {
        SourceQuadraId = sourceQuadraId;
        Item = item;
    }
}
