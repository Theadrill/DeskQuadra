namespace DeskQuadra.Core.Models;

/// <summary>
/// Estrutura imutável de geometria 2D desacoplada de qualquer framework de UI,
/// utilizada para cálculos puros de posicionamento e colisões.
/// </summary>
public readonly record struct Rect2D(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;

    public static Rect2D FromQuadra(Quadra q) => new(q.Left, q.Top, q.Width, q.Height);
}
