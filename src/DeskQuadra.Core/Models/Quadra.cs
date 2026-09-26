namespace DeskQuadra.Core.Models;

/// <summary>
/// Representa a entidade central de um container de atalhos ("Quadra").
/// Mantém estrita conformidade com Clean Architecture e Clean-Room Design.
/// </summary>
public sealed class Quadra
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Title { get; set; } = "Minha Quadra";

    public double Left { get; set; }

    public double Top { get; set; }

    public double Width { get; set; } = 320;

    public double Height { get; set; } = 240;

    public bool IsDefault { get; set; }

    public bool IsLocked { get; set; }

    public bool IsCollapsed { get; set; }

    public bool IsHidden { get; set; }

    public List<DesktopItem> Items { get; set; } = new();

    public Quadra()
    {
    }

    public Quadra(string title, double left, double top, double width = 320, double height = 240, bool isDefault = false)
    {
        Title = title;
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        IsDefault = isDefault;
    }
}
