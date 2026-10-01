using DeskQuadra.Core.Models;

namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato para varredura e detecção de atalhos e arquivos presentes no desktop do usuário (físico, OneDrive e público).
/// </summary>
public interface IDesktopScannerService
{
    IReadOnlyList<DesktopItem> ScanDesktopItems();

    /// <summary>
    /// Diretórios físicos observados pela varredura (Desktop do usuário via
    /// KnownFolder — resolve OneDrive — + Desktop público + fallbacks).
    /// Mesma lista que o live sync observa; sem duplicatas (case-insensitive).
    /// </summary>
    IReadOnlyList<string> GetWatchedDirectories();
}
