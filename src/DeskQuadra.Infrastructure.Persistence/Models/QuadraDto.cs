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
        IsHidden = IsHidden
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
        IsHidden = quadra.IsHidden
    };
}
