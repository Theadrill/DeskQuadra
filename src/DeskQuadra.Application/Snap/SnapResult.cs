namespace DeskQuadra.Application.Snap;

/// <summary>
/// Resultado do cálculo de snap magnético contendo as novas coordenadas ajustadas
/// e flags indicando se houve atração em cada eixo.
/// </summary>
public readonly record struct SnapResult(
    double X,
    double Y,
    bool SnappedX,
    bool SnappedY);
