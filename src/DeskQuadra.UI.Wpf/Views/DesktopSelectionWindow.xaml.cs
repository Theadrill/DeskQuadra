using System.Windows;
using System.Windows.Interop;
using DeskQuadra.Infrastructure.WindowsShell.Native;
using DeskQuadra.UI.Wpf.Services;

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
        var (dpiX, dpiY) = DpiHelper.GetScale(this);

        Left = DpiHelper.PhysicalToDip(physicalLeft, dpiX);
        Top = DpiHelper.PhysicalToDip(physicalTop, dpiY);
        Width = Math.Max(10, DpiHelper.PhysicalToDip(physicalWidth, dpiX));
        Height = Math.Max(10, DpiHelper.PhysicalToDip(physicalHeight, dpiY));

        DimensionText.Text = $"{Math.Round(Width)} × {Math.Round(Height)}";
    }
}
