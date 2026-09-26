using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.UI.Wpf.Views;

public partial class DesktopSelectionWindow : Window
{
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    public DesktopSelectionWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        int exStyle = NativeMethods.GetWindowLong(helper.Handle, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongPtr(
            helper.Handle,
            NativeMethods.GWL_EXSTYLE,
            (IntPtr)(exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));
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
