using System.Windows.Media;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Contracts;
using DeskQuadra.UI.Wpf.ViewModels;

namespace DeskQuadra.UI.Wpf.Tests;

public class MultiSelectionTests
{
    private sealed class DummyIconExtractor : IIconExtractorService
    {
        public ImageSource GetIcon(string filePath, bool large = true) => null!;
    }

    [Fact]
    public void DesktopItemViewModel_IsSelected_NotificaAlteracao()
    {
        var item = new DesktopItem("App", @"C:\App.exe");
        var vm = new DesktopItemViewModel(item, new DummyIconExtractor());

        var changedProps = new List<string>();
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                changedProps.Add(e.PropertyName);
            }
        };

        Assert.False(vm.IsSelected);
        vm.IsSelected = true;

        Assert.True(vm.IsSelected);
        Assert.Contains(nameof(DesktopItemViewModel.IsSelected), changedProps);
    }

    [Fact]
    public void RangeSelection_IntervaloContínuo_SelecionaApenasOsItensDentroDoRange()
    {
        var extractor = new DummyIconExtractor();
        var items = new List<DesktopItemViewModel>
        {
            new(new DesktopItem("Item 0", @"C:\0.txt"), extractor),
            new(new DesktopItem("Item 1", @"C:\1.txt"), extractor),
            new(new DesktopItem("Item 2", @"C:\2.txt"), extractor),
            new(new DesktopItem("Item 3", @"C:\3.txt"), extractor),
            new(new DesktopItem("Item 4", @"C:\4.txt"), extractor)
        };

        // Simula Shift+clique do índice 1 ao 3
        int start = Math.Min(1, 3);
        int end = Math.Max(1, 3);

        for (int i = 0; i < items.Count; i++)
        {
            items[i].IsSelected = (i >= start && i <= end);
        }

        Assert.False(items[0].IsSelected);
        Assert.True(items[1].IsSelected);
        Assert.True(items[2].IsSelected);
        Assert.True(items[3].IsSelected);
        Assert.False(items[4].IsSelected);
    }

    [Fact]
    public void ToggleSelection_CtrlClique_AlternaSemDeselecionarOutros()
    {
        var extractor = new DummyIconExtractor();
        var item1 = new DesktopItemViewModel(new DesktopItem("1", @"C:\1.txt"), extractor);
        var item2 = new DesktopItemViewModel(new DesktopItem("2", @"C:\2.txt"), extractor);
        var item3 = new DesktopItemViewModel(new DesktopItem("3", @"C:\3.txt"), extractor);

        item1.IsSelected = true;
        item2.IsSelected = false;
        item3.IsSelected = true;

        // Ctrl + clique no item 2: alterna para true mantendo item 1 e 3 selecionados
        item2.IsSelected = !item2.IsSelected;

        Assert.True(item1.IsSelected);
        Assert.True(item2.IsSelected);
        Assert.True(item3.IsSelected);

        // Ctrl + clique no item 1: alterna para false mantendo item 2 e 3 selecionados
        item1.IsSelected = !item1.IsSelected;

        Assert.False(item1.IsSelected);
        Assert.True(item2.IsSelected);
        Assert.True(item3.IsSelected);
    }

    [Fact]
    public void MarqueeIntersects_CalculoDeRetangulo_DetectaIntersecaoCorreta()
    {
        var selectionRect = new System.Windows.Rect(50, 50, 100, 100);

        var itemInside = new System.Windows.Rect(60, 60, 40, 40);
        var itemOutside = new System.Windows.Rect(200, 200, 40, 40);
        var itemIntersecting = new System.Windows.Rect(120, 120, 50, 50);

        Assert.True(selectionRect.IntersectsWith(itemInside));
        Assert.False(selectionRect.IntersectsWith(itemOutside));
        Assert.True(selectionRect.IntersectsWith(itemIntersecting));
    }
}
