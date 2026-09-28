using System.Runtime.CompilerServices;
using System.Windows.Media;

[assembly: InternalsVisibleTo("DeskQuadra.UI.Wpf.Tests")]

namespace DeskQuadra.UI.Wpf.Services;

/// <summary>
/// Conversão entre pixels físicos e DIPs do WPF, extraída dos 5 pontos que
/// repetiam o mesmo molde (leitura via VisualTreeHelper.GetDpi + guard).
/// O guard de escala inválida (&lt;=0 → 1.0) vive no <see cref="GetScale"/>;
/// as conversões puras recebem o scale já protegido e ficam testáveis sem Dispatcher.
/// </summary>
internal static class DpiHelper
{
    /// <summary>
    /// Lê a escala de DPI do visual (null-safe: visual null → 1.0,1.0).
    /// Protege escala inválida (&lt;=0 → 1.0 por eixo).
    /// Deve ser chamado na thread da UI, como o molde original.
    /// </summary>
    public static (double X, double Y) GetScale(Visual? visual)
    {
        if (visual is null)
        {
            return (1.0, 1.0);
        }

        var dpi = VisualTreeHelper.GetDpi(visual);
        double x = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        double y = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;
        return (x, y);
    }

    /// <summary>
    /// Físico→DIP: divisão pura pelo scale (scale já protegido via <see cref="GetScale"/>).
    /// </summary>
    public static double PhysicalToDip(double physical, double scale) => physical / scale;

    /// <summary>
    /// DIP→físico: multiplicação pura com Round→int p/ SetWindowPos.
    /// </summary>
    public static int DipToPhysical(double dip, double scale) => (int)Math.Round(dip * scale);
}
