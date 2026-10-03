using System.ComponentModel;
using System.Windows.Media;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Contracts;
using DeskQuadra.UI.Wpf.Properties;
using DeskQuadra.UI.Wpf.ViewModels;

namespace DeskQuadra.UI.Wpf.Tests;

public class DesktopItemRenameViewModelTests
{
    private sealed class DummyIconExtractor : IIconExtractorService
    {
        public ImageSource GetIcon(string filePath, bool large = true) => null!;
    }

    [Fact]
    public void Strings_LocalizacaoNovaPastaERenomear_Corretos()
    {
        Assert.Equal("Nova pasta", Strings.QuadraMenu_NewFolder);
        Assert.Equal("Nova pasta", Strings.NewFolder_DefaultName);
        Assert.Equal("Nova pasta ({0})", Strings.NewFolder_IndexedFormat);
        Assert.Equal("Renomear", Strings.ItemMenu_Rename);
        Assert.False(string.IsNullOrWhiteSpace(Strings.Dialog_RenameConflictTitle));
        Assert.False(string.IsNullOrWhiteSpace(Strings.Dialog_RenameConflictMessage));
        Assert.Equal("OK", Strings.Dialog_Ok);
    }

    [Fact]
    public void DesktopItemViewModel_IsRenamingEEditName_NotificamAlteracao()
    {
        var item = new DesktopItem("Teste", @"C:\Desktop\Teste.txt");
        var vm = new DesktopItemViewModel(item, new DummyIconExtractor());

        var changedProps = new List<string>();
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                changedProps.Add(e.PropertyName);
            }
        };

        vm.IsRenaming = true;
        vm.EditName = "Novo Nome";

        Assert.True(vm.IsRenaming);
        Assert.Equal("Novo Nome", vm.EditName);
        Assert.Contains(nameof(DesktopItemViewModel.IsRenaming), changedProps);
        Assert.Contains(nameof(DesktopItemViewModel.EditName), changedProps);
    }

    [Fact]
    public void DesktopItemViewModel_UpdateNameAndPath_AtualizaModeloENotifica()
    {
        var item = new DesktopItem("Antigo", @"C:\Desktop\Antigo.txt");
        var vm = new DesktopItemViewModel(item, new DummyIconExtractor());

        var changedProps = new List<string>();
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                changedProps.Add(e.PropertyName);
            }
        };

        vm.UpdateNameAndPath("Novo", @"C:\Desktop\Novo.txt");

        Assert.Equal("Novo", vm.Name);
        Assert.Equal(@"C:\Desktop\Novo.txt", vm.FilePath);
        Assert.Equal("Novo", item.Name);
        Assert.Equal(@"C:\Desktop\Novo.txt", item.FilePath);
        Assert.Contains(nameof(DesktopItemViewModel.Name), changedProps);
        Assert.Contains(nameof(DesktopItemViewModel.FilePath), changedProps);
        Assert.Contains(nameof(DesktopItemViewModel.Icon), changedProps);
    }

    [Fact]
    public void QuadraViewModel_AddItem_RetornaViewModelCriado()
    {
        var quadra = new Quadra("Quadra Teste", 0, 0);
        var qvm = new QuadraViewModel(quadra, new DummyIconExtractor());

        var created = qvm.AddItem(@"C:\Desktop\Arquivo.txt");

        Assert.NotNull(created);
        Assert.Equal("Arquivo.txt", created.Name);
        Assert.Equal(@"C:\Desktop\Arquivo.txt", created.FilePath);
        Assert.Contains(created, qvm.Items);
    }
}
