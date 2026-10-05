using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Xunit;

namespace DeskQuadra.UI.Wpf.Tests;

/// <summary>
/// Testes de regressão visual e comportamental da barra de rolagem Fluent (Windows 11):
/// - Tokens de dimensões e contraste presentes no tema
/// - CornerRadius do Thumb sempre correspondente a exatamente metade da largura (evita pontas de agulha elípticas no WPF)
/// - Proporcionalidade do Thumb no OverlayScrollViewerStyle
/// </summary>
public class FluentScrollBarTests
{
    private const double ViewportHeight = 200.0;
    private const double ContentHeight = 1000.0;

    [Fact]
    public void ThemeDictionary_LoadsScrollBarResources_WithCorrectSemicircularRadii()
    {
        RunInSta(() =>
        {
            var dict = LoadThemeDictionary();

            Assert.True(dict.Contains("ScrollBar.Width"));
            Assert.True(dict.Contains("ScrollBar.Thumb.IdleWidth"));
            Assert.True(dict.Contains("ScrollBar.Thumb.HoverWidth"));
            Assert.True(dict.Contains("ScrollBar.Thumb.CornerRadius.Idle"));
            Assert.True(dict.Contains("ScrollBar.Thumb.CornerRadius.Hover"));

            double idleWidth = (double)dict["ScrollBar.Thumb.IdleWidth"]!;
            double hoverWidth = (double)dict["ScrollBar.Thumb.HoverWidth"]!;
            var idleRadius = (CornerRadius)dict["ScrollBar.Thumb.CornerRadius.Idle"]!;
            var hoverRadius = (CornerRadius)dict["ScrollBar.Thumb.CornerRadius.Hover"]!;

            // Regra anti-agulha: o raio deve ser exatamente metade da largura (ex: 2 para largura 4)
            Assert.Equal(idleWidth / 2.0, idleRadius.TopLeft);
            Assert.Equal(idleWidth / 2.0, idleRadius.BottomRight);
            Assert.Equal(hoverWidth / 2.0, hoverRadius.TopLeft);
            Assert.Equal(hoverWidth / 2.0, hoverRadius.BottomRight);

            // Garante que nunca voltamos ao bug do CornerRadius 99
            Assert.NotEqual(99.0, idleRadius.TopLeft);
        });
    }

    [Fact]
    public void OverlayThumb_IsProportionalToVisibleFraction_AndHasPillGeometry()
    {
        RunInSta(() =>
        {
            var dict = LoadThemeDictionary();
            var app = System.Windows.Application.Current;
            if (app != null && !app.Resources.MergedDictionaries.Contains(dict))
            {
                app.Resources.MergedDictionaries.Add(dict);
            }

            var stack = new StackPanel();
            for (int i = 0; i < 10; i++)
            {
                stack.Children.Add(new Border { Height = 100, Width = 100 });
            }

            var viewer = new ScrollViewer
            {
                Content = stack,
                Height = ViewportHeight,
                Width = 120,
                Padding = new Thickness(2),
                PanningMode = PanningMode.VerticalOnly,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Style = (Style)dict["OverlayScrollViewerStyle"]!
            };

            viewer.Measure(new Size(120, ViewportHeight));
            viewer.Arrange(new Rect(0, 0, 120, ViewportHeight));
            viewer.ApplyTemplate();
            viewer.UpdateLayout();
            DoEvents();

            var bar = (ScrollBar)viewer.Template.FindName("PART_VerticalScrollBar", viewer);
            Assert.NotNull(bar);
            Assert.Equal(new Thickness(0, 4, -3, 4), bar.Margin);
            bar.ApplyTemplate();

            var track = (Track)bar.Template.FindName("PART_Track", bar);
            Assert.NotNull(track);

            double trackHeight = track.ActualHeight;
            double thumbHeight = track.Thumb?.ActualHeight ?? 0;

            Assert.True(trackHeight > 0, "A trilha deve ter altura mensurável.");
            Assert.True(thumbHeight > 0, "O thumb deve ter altura mensurável.");

            double expectedFraction = ViewportHeight / ContentHeight;
            double actualFraction = thumbHeight / trackHeight;

            // O thumb deve ser proporcional à fração visível (~0.2), nunca ocupar a trilha inteira (~1.0)
            Assert.InRange(actualFraction, expectedFraction * 0.5, expectedFraction * 1.5);

            // Inspeciona o elemento Pill dentro do Thumb
            var thumb = track.Thumb!;
            thumb.ApplyTemplate();
            var pill = (Border)thumb.Template.FindName("Pill", thumb);
            Assert.NotNull(pill);
            Assert.Equal(2.0, pill.CornerRadius.TopLeft);
            Assert.Equal(4.0, pill.Width);
        });
    }

    private static ResourceDictionary LoadThemeDictionary()
    {
        return new ResourceDictionary
        {
            Source = new Uri("/DeskQuadra.UI.Wpf;component/Theme/Default.xaml", UriKind.RelativeOrAbsolute)
        };
    }

    private static void RunInSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            if (System.Windows.Application.Current == null)
            {
                try
                {
                    _ = new System.Windows.Application();
                }
                catch (InvalidOperationException)
                {
                }
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error != null)
        {
            throw new AggregateException(error);
        }
    }

    private static void DoEvents()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new DispatcherOperationCallback(_ =>
            {
                frame.Continue = false;
                return null;
            }),
            null);
        Dispatcher.PushFrame(frame);
    }
}
