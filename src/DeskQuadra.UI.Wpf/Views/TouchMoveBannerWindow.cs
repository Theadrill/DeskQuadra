using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DeskQuadra.UI.Wpf.Properties;
using DeskQuadra.UI.Wpf.Theme;

namespace DeskQuadra.UI.Wpf.Views;

// Faixa do overview (fatia 2): barra inferior horizontal [instrução][CANCELAR],
// centralizada, mesma altura os dois; tap em qualquer ponto cancela (fora das Quadras).
// Sem Win32 novo: consome o tap (Handled) p/ não vazar destino nem atravessar p/ a mini.
public sealed class TouchMoveBannerWindow : Window
{
    public TouchMoveBannerWindow(double left, double top, double maxWidth, Action onCancel)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        IsHitTestVisible = true; // consome o gesto como cancela
        Focusable = false;
        ShowActivated = false; // não rouba o foco (ESC segue no HWND da origem)
        SizeToContent = SizeToContent.Height;
        Width = maxWidth;
        Left = left;
        Top = top;

        // Legibilidade (contraste alto do par Dialog): texto claro sobre fundo escuro.
        var border = new Border
        {
            Background = ThemeResolver.Get("Dialog.Background", new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x24))),
            BorderBrush = ThemeResolver.Get("Dialog.Primary.Background", new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4))),
            BorderThickness = ThemeResolver.Get("Menu.BorderThickness", new Thickness(1)),
            CornerRadius = ThemeResolver.Get("Menu.CornerRadius", new CornerRadius(8)),
            Padding = ThemeResolver.Get("Dialog.Panel.Margin", new Thickness(16, 10, 16, 10)),
            Effect = new DropShadowEffect
            {
                BlurRadius = ThemeResolver.Get("Menu.Shadow.BlurRadius", 16.0),
                ShadowDepth = 2.0,
                Opacity = 0.55,
                Color = Colors.Black
            }
        };

        double baseFont = ThemeResolver.Get("Dialog.Message.FontSize", 13.0);
        var text = new TextBlock
        {
            Text = Strings.TouchMoveOverviewHint,
            Foreground = ThemeResolver.Get("Dialog.Foreground", new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5))),
            FontSize = baseFont * 2, // instrução dobrada (barra cresce via SizeToContent)
            FontWeight = ThemeResolver.Get("CreationMenu.Primary.FontWeight", FontWeights.SemiBold),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // CANCELAR: texto REUSE Dialog_Cancel; par vermelho opaco validado (tokens próprios).
        var cancel = new Button
        {
            Content = Strings.Dialog_Cancel,
            Background = ThemeResolver.Get("TouchMove.Cancel.Background", new SolidColorBrush(Color.FromRgb(0xB3, 0x26, 0x1E))),
            Foreground = ThemeResolver.Get("TouchMove.Cancel.Foreground", new SolidColorBrush(Colors.White)),
            FontSize = ThemeResolver.Get("Dialog.Button.FontSize", 12.0),
            FontWeight = FontWeights.SemiBold,
            Padding = ThemeResolver.Get("Dialog.Button.Padding", new Thickness(14, 6, 14, 6)),
            BorderThickness = new Thickness(0),
            MinHeight = ThemeResolver.Get("CreationMenu.Button.MinHeight.Touch", 44.0), // alvo tocável
            VerticalAlignment = VerticalAlignment.Stretch, // acompanha a altura da barra
            Margin = new Thickness(12, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        cancel.Click += (s, e) => onCancel();

        // Linha única centralizada: botão estica na altura do texto.
        var panel = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        panel.Children.Add(text);
        panel.Children.Add(cancel);
        Grid.SetColumn(text, 0);
        Grid.SetColumn(cancel, 1);
        border.Child = panel;
        Content = border;

        // Barra é "fora das Quadras": qualquer tap cancela sem vazar destino.
        PreviewMouseLeftButtonDown += (s, e) => { onCancel(); e.Handled = true; };
    }
}
