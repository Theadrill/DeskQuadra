using DeskQuadra.Core.Models;

namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato para persistência transacional de layouts de Quadras em disco.
/// </summary>
public interface ILayoutRepository
{
    /// <summary>
    /// Carrega as Quadras salvas, com recuperação automática em caso de queda de energia ou arquivo quebrado.
    /// </summary>
    Task<IReadOnlyList<Quadra>> LoadLayoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Salva o estado atual das Quadras utilizando gravação atômica (.tmp -> .bak -> .json).
    /// </summary>
    Task SaveLayoutAsync(IEnumerable<Quadra> quadras, CancellationToken cancellationToken = default);
}
