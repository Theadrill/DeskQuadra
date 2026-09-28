namespace DeskQuadra.Application.Snap;

/// <summary>
/// Resultado da quantização de largura + altura na grade de ícones.
/// Espelha <see cref="SnapResult"/> (X/Y por eixo) para a fiação na UI ser simétrica
/// à do snap de posição.
/// </summary>
/// <param name="Width">Largura final (já com chrome horizontal e mínimo aplicados).</param>
/// <param name="Height">Altura final (já com chrome vertical e mínimo aplicados).</param>
/// <param name="SnappedWidth">Verdadeiro quando a largura grudou num múltiplo da grade.</param>
/// <param name="SnappedHeight">Verdadeiro quando a altura grudou num múltiplo da grade.</param>
/// <param name="Columns">Colunas de ícones correspondentes à largura final (mínimo 1).</param>
/// <param name="Rows">Linhas de ícones correspondentes à altura final (mínimo 1).</param>
public readonly record struct SizeSnapResult2D(
    double Width,
    double Height,
    bool SnappedWidth,
    bool SnappedHeight,
    int Columns,
    int Rows);
