using System.Windows.Media;

namespace DeskQuadra.Infrastructure.WindowsShell.Contracts;

/// <summary>
/// Contrato para extração e caching de ícones de arquivos e atalhos em alta resolução nativa.
/// </summary>
public interface IIconExtractorService
{
    ImageSource GetIcon(string filePath, bool large = true);
    void ClearCache() { }
    void Invalidate(string filePath) { }
}
