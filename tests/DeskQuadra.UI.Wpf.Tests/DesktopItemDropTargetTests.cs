using System.Windows.Media;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Contracts;
using DeskQuadra.UI.Wpf.Models;
using DeskQuadra.UI.Wpf.ViewModels;

namespace DeskQuadra.UI.Wpf.Tests;

public class DesktopItemDropTargetTests
{
    private sealed class DummyIconExtractor : IIconExtractorService
    {
        public ImageSource GetIcon(string filePath, bool large = true) => null!;
    }

    [Fact]
    public void DesktopItemViewModel_IsDropTarget_DisparaNotificacaoAoAlterar()
    {
        var item = new DesktopItem("Pasta", @"C:\Desktop\Pasta", isDirectory: true);
        var vm = new DesktopItemViewModel(item, new DummyIconExtractor());

        var changedProps = new List<string>();
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                changedProps.Add(e.PropertyName);
            }
        };

        Assert.False(vm.IsDropTarget);
        vm.IsDropTarget = true;

        Assert.True(vm.IsDropTarget);
        Assert.Contains(nameof(DesktopItemViewModel.IsDropTarget), changedProps);

        vm.IsDropTarget = false;
        Assert.False(vm.IsDropTarget);
    }

    [Fact]
    public void QuadraDragPayload_ConstrutorUnicoItem_PreencheItemEItems()
    {
        var item = new DesktopItem("Item1", @"C:\Desktop\Item1.txt");
        var vm = new DesktopItemViewModel(item, new DummyIconExtractor());
        var quadraId = Guid.NewGuid();

        var payload = new QuadraDragPayload(quadraId, vm);

        Assert.Equal(quadraId, payload.SourceQuadraId);
        Assert.Same(vm, payload.Item);
        Assert.Single(payload.Items);
        Assert.Same(vm, payload.Items[0]);
        Assert.False(payload.WasHandledAsMove);
    }

    [Fact]
    public void QuadraDragPayload_ConstrutorMultiplosItens_PreencheCorretamente()
    {
        var item1 = new DesktopItem("Item1", @"C:\Desktop\Item1.txt");
        var item2 = new DesktopItem("Item2", @"C:\Desktop\Item2.txt");
        var vm1 = new DesktopItemViewModel(item1, new DummyIconExtractor());
        var vm2 = new DesktopItemViewModel(item2, new DummyIconExtractor());
        var quadraId = Guid.NewGuid();

        var list = new List<DesktopItemViewModel> { vm1, vm2 };
        var payload = new QuadraDragPayload(quadraId, list);

        Assert.Equal(quadraId, payload.SourceQuadraId);
        Assert.Same(vm1, payload.Item);
        Assert.Equal(2, payload.Items.Count);
        Assert.Contains(vm1, payload.Items);
        Assert.Contains(vm2, payload.Items);
    }
}
