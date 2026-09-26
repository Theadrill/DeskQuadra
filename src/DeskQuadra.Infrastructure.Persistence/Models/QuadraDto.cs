using DeskQuadra.Core.Models;

namespace DeskQuadra.Infrastructure.Persistence.Models;

/// <summary>
/// Modelo de transferência de dados (DTO) para serialização JSON do estado de uma Quadra.
/// </summary>
public sealed class QuadraDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsDefault { get; set; }
    public bool IsLocked { get; set; }
    public bool IsCollapsed { get; set; }
    public bool IsHidden { get; set; }
    public List<DesktopItemDto> Items { get; set; } = new();

    public Quadra ToDomain() => new()
    {
        Id = Id,
        Title = Title,
        Left = Left,
        Top = Top,
        Width = Width,
        Height = Height,
        IsDefault = IsDefault,
        IsLocked = IsLocked,
        IsCollapsed = IsCollapsed,
        IsHidden = IsHidden,
        Items = Items.Select(i => i.ToDomain()).ToList()
    };

    public static QuadraDto FromDomain(Quadra quadra) => new()
    {
        Id = quadra.Id,
        Title = quadra.Title,
        Left = quadra.Left,
        Top = quadra.Top,
        Width = quadra.Width,
        Height = quadra.Height,
        IsDefault = quadra.IsDefault,
        IsLocked = quadra.IsLocked,
        IsCollapsed = quadra.IsCollapsed,
        IsHidden = quadra.IsHidden,
        Items = quadra.Items.Select(DesktopItemDto.FromDomain).ToList()
    };
}

/// <summary>
/// Modelo de transferência de dados (DTO) para serialização JSON de um atalho/arquivo da Quadra.
/// </summary>
public sealed class DesktopItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? TargetPath { get; set; }
    public bool IsDirectory { get; set; }
    public int OrderIndex { get; set; }

    public DesktopItem ToDomain() => new()
    {
        Id = Id,
        Name = Name,
        FilePath = FilePath,
        TargetPath = TargetPath,
        IsDirectory = IsDirectory,
        OrderIndex = OrderIndex
    };

    public static DesktopItemDto FromDomain(DesktopItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        FilePath = item.FilePath,
        TargetPath = item.TargetPath,
        IsDirectory = item.IsDirectory,
        OrderIndex = item.OrderIndex
    };
}
