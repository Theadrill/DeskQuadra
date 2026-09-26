namespace DeskQuadra.Core.Models;

/// <summary>
/// Representa um atalho ou arquivo físico exibido dentro de uma Quadra.
/// Mantém estrita conformidade com Clean-Room Design e Clean Architecture.
/// </summary>
public sealed class DesktopItem
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string? TargetPath { get; set; }

    public bool IsDirectory { get; set; }

    public int OrderIndex { get; set; }

    public DesktopItem()
    {
    }

    public DesktopItem(string name, string filePath, string? targetPath = null, bool isDirectory = false, int orderIndex = 0)
    {
        Name = name;
        FilePath = filePath;
        TargetPath = targetPath;
        IsDirectory = isDirectory;
        OrderIndex = orderIndex;
    }
}
