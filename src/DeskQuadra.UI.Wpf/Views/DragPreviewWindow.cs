using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.UI.Wpf.Views;

/// <summary>
/// Janela fantasma leve e translúcida que acompanha o cursor do mouse ou o dedo do usuário
/// durante o arrasto de atalhos pelo desktop, exibindo o ícone e o nome do item flutuante.
/// </summary>
public sealed class DragPreviewWindow : Window
{
    private double _dpiScaleX = 1.0;
    private double _dpiScaleY = 1.0;

    public DragPreviewWindow(ImageSource? icon, string name)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        IsHitTestVisible = false;
        Focusable = false;
        SizeToContent = SizeToContent.WidthAndHeight;

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 24, 24, 30)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 6, 12, 6),
            Effect = new DropShadowEffect
            {
                BlurRadius = 14,
                ShadowDepth = 3,
                Opacity = 0.55,
                Color = Colors.Black
            }
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (icon != null)
        {
            var image = new Image
            {
                Source = icon,
                Width = 28,
                Height = 28,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            stack.Children.Add(image);
        }

        var text = new TextBlock
        {
            Text = name,
            Foreground = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 180,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        stack.Children.Add(text);

        border.Child = stack;
        Content = border;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var dpi = VisualTreeHelper.GetDpi(this);
        _dpiScaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        _dpiScaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        // Adiciona estilos Win32 para garantir que o preview seja 100% invisível ao mouse/toque e foco
        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongPtr(
            hwnd,
            NativeMethods.GWL_EXSTYLE,
            new IntPtr(exStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE));
    }

    public void UpdatePosition(int screenX, int screenY)
    {
        Left = (screenX / _dpiScaleX) + 14;
        Top = (screenY / _dpiScaleY) + 14;
    }
}
