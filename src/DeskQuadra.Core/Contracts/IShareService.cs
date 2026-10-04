namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato para compartilhamento de arquivos via Shell do Windows.
/// </summary>
public interface IShareService
{
    /// <summary>
    /// Invoca a experiência de compartilhamento do Windows para o arquivo especificado.
    /// </summary>
    /// <param name="windowHandle">Handle (HWND) da janela de contexto.</param>
    /// <param name="filePath">Caminho do arquivo a compartilhar.</param>
    /// <returns>True se a solicitação foi enviada ao sistema.</returns>
    bool ShareFile(IntPtr windowHandle, string filePath);
}
