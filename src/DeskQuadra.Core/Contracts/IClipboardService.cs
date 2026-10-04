namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato de operações de área de transferência (Clipboard) para manipulação de arquivos do Windows.
/// Suporta operações de Copiar e Recortar (Preferred DropEffect = Move) compatíveis com o Windows Explorer.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Copia ou recorta uma coleção de caminhos de arquivos para a área de transferência do Windows.
    /// </summary>
    /// <param name="filePaths">Lista de caminhos absolutos dos arquivos ou pastas.</param>
    /// <param name="isCut">Se true, marca os arquivos com o efeito DROPEFFECT_MOVE (Recortar).</param>
    /// <returns>True se a operação foi aceita pelo sistema operacional.</returns>
    bool SetFileDropList(IEnumerable<string> filePaths, bool isCut = false);

    /// <summary>
    /// Retorna a lista de caminhos presentes na área de transferência, caso contenha arquivos.
    /// </summary>
    IReadOnlyList<string> GetFileDropList();

    /// <summary>
    /// Informa se a área de transferência atualmente contém arquivos.
    /// </summary>
    bool ContainsFileDropList();

    /// <summary>
    /// Informa se os arquivos no clipboard foram marcados com a ação Recortar (DROPEFFECT_MOVE = 2).
    /// </summary>
    bool IsCutEffect();

    /// <summary>
    /// Limpa o conteúdo da área de transferência.
    /// </summary>
    void Clear();
}
