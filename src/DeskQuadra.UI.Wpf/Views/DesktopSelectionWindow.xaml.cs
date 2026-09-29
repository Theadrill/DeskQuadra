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
        var (dipLeft, dipTop, rawWidth, rawHeight) = DpiHelper.MapPhysicalToDip(this, physicalLeft, physicalTop, physicalWidth, physicalHeight);

        Left = dipLeft;
        Top = dipTop;
        Width = Math.Max(10, rawWidth);
        Height = Math.Max(10, rawHeight);

        DimensionText.Text = $"{Math.Round(Width)} × {Math.Round(Height)}";
    }
}
