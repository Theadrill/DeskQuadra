using DeskQuadra.Core.Models;

namespace DeskQuadra.Application.Snap;

/// <summary>
/// Contrato do motor geométrico de snap magnético elástico.
/// </summary>
public interface ISnapEngine
{
    /// <summary>
    /// Calcula o snap de uma caixa delimitadora em relação à área de trabalho do monitor e obstáculos existentes.
    /// </summary>
    SnapResult CalculateSnap(
        Rect2D current,
        Rect2D workArea,
        IEnumerable<Rect2D>? otherQuadras = null,
        double threshold = 16.0,
        double gap = 0.0);
}
