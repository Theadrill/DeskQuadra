namespace DeskQuadra.Application.Snap;

/// <summary>
/// Resultado da quantização de UMA dimensão (largura ou altura) na grade de ícones.
/// </summary>
/// <param name="SnappedSize">Tamanho final da dimensão (já com o chrome somado e o mínimo aplicado).</param>
/// <param name="Snapped">Verdadeiro quando o tamanho grudou num múltiplo da grade.</param>
/// <param name="Cells">
/// Quantidade de células (colunas ou linhas) correspondente ao tamanho final.
/// Quando grudou, é o múltiplo exato; quando passou direto, é a contagem vizinha
/// mais próxima (informativa, ex: para exibir "3 colunas" durante o arrasto).
/// Mínimo 1 (a não ser que a entrada não seja um número finito, quando é 0).
/// </param>
public readonly record struct SizeSnapResult(double SnappedSize, bool Snapped, int Cells);
