using System.Windows.Media;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Contracts;
using DeskQuadra.UI.Wpf.ViewModels;
using Xunit;

namespace DeskQuadra.UI.Wpf.Tests;

public class ItemSelectionResolverTests
{
    private sealed class DummyIconExtractor : IIconExtractorService
    {
        public ImageSource GetIcon(string filePath, bool large = true) => null!;
    }

    [Fact]
    public void ResolveTargetItems_WhenMultipleItemsSelectedAndFallbackInSelection_ReturnsAllSelected()
    {
        var extractor = new DummyIconExtractor();
        var item1 = new DesktopItemViewModel(new DesktopItem("A", @"C:\A.txt"), extractor) { IsSelected = true };
        var item2 = new DesktopItemViewModel(new DesktopItem("B", @"C:\B.txt"), extractor) { IsSelected = true };
        var item3 = new DesktopItemViewModel(new DesktopItem("C", @"C:\C.txt"), extractor) { IsSelected = false };
        var all = new[] { item1, item2, item3 };

        var result = ItemSelectionResolver.ResolveTargetItems(all, item2);

        Assert.Equal(2, result.Count);
        Assert.Contains(item1, result);
        Assert.Contains(item2, result);
    }

    [Fact]
    public void ResolveTargetItems_WhenMultipleItemsSelectedAndFallbackNull_ReturnsAllSelected()
    {
        var extractor = new DummyIconExtractor();
        var item1 = new DesktopItemViewModel(new DesktopItem("A", @"C:\A.txt"), extractor) { IsSelected = true };
        var item2 = new DesktopItemViewModel(new DesktopItem("B", @"C:\B.txt"), extractor) { IsSelected = true };
        var all = new[] { item1, item2 };

        var result = ItemSelectionResolver.ResolveTargetItems(all, null);

        Assert.Equal(2, result.Count);
        Assert.Contains(item1, result);
        Assert.Contains(item2, result);
    }

    [Fact]
    public void ResolveTargetItems_WhenMultipleItemsSelectedAndFallbackOutsideSelection_ReturnsFallbackOnly()
    {
        var extractor = new DummyIconExtractor();
        var item1 = new DesktopItemViewModel(new DesktopItem("A", @"C:\A.txt"), extractor) { IsSelected = true };
        var item2 = new DesktopItemViewModel(new DesktopItem("B", @"C:\B.txt"), extractor) { IsSelected = true };
        var item3 = new DesktopItemViewModel(new DesktopItem("C", @"C:\C.txt"), extractor) { IsSelected = false };
        var all = new[] { item1, item2, item3 };

        var result = ItemSelectionResolver.ResolveTargetItems(all, item3);

        Assert.Single(result);
        Assert.Equal(item3, result[0]);
    }

    [Fact]
    public void ResolveTargetItems_WhenNoItemsSelectedAndFallbackProvided_ReturnsFallbackOnly()
    {
        var extractor = new DummyIconExtractor();
        var item1 = new DesktopItemViewModel(new DesktopItem("A", @"C:\A.txt"), extractor) { IsSelected = false };
        var all = new[] { item1 };

        var result = ItemSelectionResolver.ResolveTargetItems(all, item1);

        Assert.Single(result);
        Assert.Equal(item1, result[0]);
    }

    [Fact]
    public void ResolveTargetItems_WhenNoItemsSelectedAndFallbackNull_ReturnsEmpty()
    {
        var extractor = new DummyIconExtractor();
        var item1 = new DesktopItemViewModel(new DesktopItem("A", @"C:\A.txt"), extractor) { IsSelected = false };
        var all = new[] { item1 };

        var result = ItemSelectionResolver.ResolveTargetItems(all, null);

        Assert.Empty(result);
    }

    [Fact]
    public void QuadraViewModel_RemoveItems_RemovesMultipleItemsAndUpdatesOrderIndex()
    {
        var quadra = new Quadra { Title = "Teste" };
        var vm = new QuadraViewModel(quadra, new DummyIconExtractor());

        var item1 = vm.AddItem(new DesktopItem("1", @"C:\1.txt"));
        var item2 = vm.AddItem(new DesktopItem("2", @"C:\2.txt"));
        var item3 = vm.AddItem(new DesktopItem("3", @"C:\3.txt"));
        var item4 = vm.AddItem(new DesktopItem("4", @"C:\4.txt"));

        int removed = vm.RemoveItems(new[] { item2, item3 });

        Assert.Equal(2, removed);
        Assert.Equal(2, vm.Items.Count);
        Assert.Equal(2, quadra.Items.Count);
        Assert.Equal(item1, vm.Items[0]);
        Assert.Equal(item4, vm.Items[1]);
        Assert.Equal(0, vm.Items[0].Model.OrderIndex);
        Assert.Equal(1, vm.Items[1].Model.OrderIndex);
    }
}
