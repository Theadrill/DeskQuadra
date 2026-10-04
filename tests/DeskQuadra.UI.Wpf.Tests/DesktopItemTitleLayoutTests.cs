using System.Windows;
using System.Windows.Controls;
using DeskQuadra.UI.Wpf.Theme;

namespace DeskQuadra.UI.Wpf.Tests;

public class DesktopItemTitleLayoutTests
{
    [Fact]
    public void DefaultTheme_QuadraItemTitleMaxHeight_CorrespondeAExatamenteDuasLinhas()
    {
        // Carrega o dicionário Default.xaml
        var dictionary = new ResourceDictionary
        {
            Source = new Uri("/DeskQuadra.UI.Wpf;component/Theme/Default.xaml", UriKind.RelativeOrAbsolute)
        };

        Assert.True(dictionary.Contains("Quadra.Item.LineHeight"));
        Assert.True(dictionary.Contains("Quadra.Item.Title.MaxHeight"));

        var lineHeight = (double)dictionary["Quadra.Item.LineHeight"];
        var maxHeight = (double)dictionary["Quadra.Item.Title.MaxHeight"];

        Assert.Equal(14.0, lineHeight);
        Assert.Equal(28.0, maxHeight);
        Assert.Equal(2 * lineHeight, maxHeight);
    }

    [Fact]
    public void DesktopItemTitle_MedeAteDuasLinhasSemUltrapassarLimite()
    {
        var thread = new Thread(() =>
        {
            var tb = new TextBlock
            {
                FontSize = 11,
                LineHeight = 14,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Top,
                MaxHeight = 28,
                Width = 64
            };

            // 1 linha: deve medir exatamente 14px
            tb.Text = "Curto";
            tb.Measure(new Size(64, 28));
            tb.Arrange(new Rect(0, 0, 64, 28));
            Assert.Equal(14.0, tb.ActualHeight);

            // 2 linhas: deve medir exatamente 28px
            tb.Text = "Item Com Titulo Longo";
            tb.Measure(new Size(64, 28));
            tb.Arrange(new Rect(0, 0, 64, 28));
            Assert.Equal(28.0, tb.ActualHeight);

            // 3+ linhas: deve ser travado no MaxHeight de 28px (2 linhas completas)
            tb.Text = "Item Com Titulo Extremamente Longo Que Teria Três Ou Mais Linhas Na Grade";
            tb.Measure(new Size(64, 28));
            tb.Arrange(new Rect(0, 0, 64, 28));
            Assert.Equal(28.0, tb.ActualHeight);
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
