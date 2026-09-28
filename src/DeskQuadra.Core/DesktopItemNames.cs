namespace DeskQuadra.Core;

/// <summary>
/// Nome de exibição de itens do Desktop (extraído de QuadraViewModel.AddItem e DesktopScannerService.ScanDesktopItems).
/// Lógica pura de path via <see cref="System.IO"/> (sem WPF/timers/textos): atalhos .lnk exibem
/// o nome sem extensão, demais arquivos/diretórios exibem o nome com extensão. Coberta por xUnit.
/// </summary>
public static class DesktopItemNames
{
    /// <summary>
    /// Resolve o nome de exibição de <paramref name="filePath"/>: extensão .lnk (OrdinalIgnoreCase)
    /// retorna o nome sem extensão, senão retorna o nome com extensão.
    /// </summary>
    public static string GetDisplayName(string filePath)
    {
        // Atalhos exibem o nome sem a extensão .lnk (case-insensitive); resto preserva a extensão.
        return Path.GetExtension(filePath).Equals(".lnk", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(filePath)
            : Path.GetFileName(filePath);
    }
}
