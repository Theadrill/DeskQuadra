using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DeskQuadra.Infrastructure.WindowsShell.Native;
using DeskQuadra.UI.Wpf.Theme;

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
        ShowActivated = false;
        SizeToContent = SizeToContent.WidthAndHeight;

        // Seam de temas (Default): valores via ThemeResolver (TryFindResource + fallback).

        var border = new Border
        {
            Background = ThemeResolver.Get("DragPreview.Background", new SolidColorBrush(Color.FromArgb(220, 24, 24, 30))),
            BorderBrush = ThemeResolver.Get("DragPreview.BorderBrush", new SolidColorBrush(Color.FromArgb(90, 255, 255, 255))),
            BorderThickness = ThemeResolver.Get("DragPreview.BorderThickness", new Thickness(1)),
            CornerRadius = ThemeResolver.Get("DragPreview.CornerRadius", new CornerRadius(8)),
            Padding = ThemeResolver.Get("DragPreview.Padding", new Thickness(8, 6, 12, 6)),
            Effect = new DropShadowEffect
            {
                BlurRadius = ThemeResolver.Get("DragPreview.Shadow.BlurRadius", 14.0),
                ShadowDepth = ThemeResolver.Get("DragPreview.Shadow.Depth", 3.0),
                Opacity = ThemeResolver.Get("DragPreview.Shadow.Opacity", 0.55),
                Color = ThemeResolver.Get("DragPreview.Shadow.Color", Colors.Black)
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
            Foreground = ThemeResolver.Get("DragPreview.Foreground", new SolidColorBrush(Color.FromRgb(245, 245, 245))),
            FontSize = ThemeResolver.Get("DragPreview.FontSize", 12.0),
            FontWeight = ThemeResolver.Get("DragPreview.FontWeight", FontWeights.SemiBold),
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
        NativeMethods.ApplyClickThroughNoActivate(hwnd);

        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTTRANSPARENT = -1;

        if (msg == WM_NCHITTEST)
        {
            handled = true;
            return new IntPtr(HTTRANSPARENT);
        }
        return IntPtr.Zero;
    }

    public void UpdatePosition(int screenX, int screenY)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.SetWindowPos(
                hwnd,
                IntPtr.Zero,
                screenX + 24,
                screenY + 24,
                0,
                0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        }
        else
        {
            Left = (screenX / _dpiScaleX) + 24;
            Top = (screenY / _dpiScaleY) + 24;
        }
    }
}
