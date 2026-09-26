namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato para inicialização e execução segura de arquivos e atalhos via Shell do Windows.
/// </summary>
public interface IFileLauncherService
{
    bool Launch(string filePath);
}
