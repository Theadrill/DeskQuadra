namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato para exclusão de arquivos e diretórios via Shell do Windows.
/// <c>recycle: true</c> envia para a Lixeira (com undo); <c>false</c> exclui permanentemente (sem undo).
/// </summary>
public interface IFileDeletionService
{
    bool Delete(string filePath, bool recycle);
}
