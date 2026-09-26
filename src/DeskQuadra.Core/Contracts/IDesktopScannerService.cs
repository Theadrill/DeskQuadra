using DeskQuadra.Core.Models;

namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato para varredura e detecção de atalhos e arquivos presentes no desktop do usuário (físico, OneDrive e público).
/// </summary>
public interface IDesktopScannerService
{
    IReadOnlyList<DesktopItem> ScanDesktopItems();
}
