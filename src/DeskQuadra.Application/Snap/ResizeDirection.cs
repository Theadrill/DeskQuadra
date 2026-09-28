namespace DeskQuadra.Application.Snap;

/// <summary>
/// Sentido do redimensionamento de UMA dimensão (largura ou altura).
/// Serve para a histerese: a margem de saída do detente é diferente da margem de entrada.
/// </summary>
public enum ResizeDirection
{
    /// <summary>
    /// Sentido desconhecido (ex: ajuste programático, primeiro frame do arrasto).
    /// Usa a banda base (<c>threshold</c>) sem histerese.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A dimensão está aumentando (ex: borda direita arrastada para a direita,
    /// borda esquerda arrastada para a esquerda).
    /// </summary>
    Growing = 1,

    /// <summary>
    /// A dimensão está diminuindo (ex: borda direita arrastada para a esquerda,
    /// borda esquerda arrastada para a direita).
    /// </summary>
    Shrinking = 2,
}
