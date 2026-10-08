namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

/// <summary>
/// Contrato para localização de utilitários externos de compactação (7-Zip, WinRAR)
/// na máquina local em tempo de execução.
/// </summary>
public interface IExternalArchiverLocator
{
    string? FindSevenZip();
    string? FindWinRar();
}
