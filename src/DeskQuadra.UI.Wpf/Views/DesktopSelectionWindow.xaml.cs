using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.UI.Wpf.Views;

public partial class DesktopSelectionWindow : Window
{
    public DesktopSelectionWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        NativeMethods.ApplyClickThroughNoActivate(helper.Handle);
    }

    public void UpdateBounds(double physicalLeft, double physicalTop, double physicalWidth, double physicalHeight)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        Left = physicalLeft / dpiX;
        Top = physicalTop / dpiY;
        Width = Math.Max(10, physicalWidth / dpiX);
        Height = Math.Max(10, physicalHeight / dpiY);

        DimensionText.Text = $"{Math.Round(Width)} × {Math.Round(Height)}";
    }
}
