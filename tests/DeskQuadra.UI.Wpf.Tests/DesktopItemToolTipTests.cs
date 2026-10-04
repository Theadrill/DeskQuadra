using System.Windows;
using System.Windows.Controls;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Contracts;
using DeskQuadra.UI.Wpf.ViewModels;

namespace DeskQuadra.UI.Wpf.Tests;

public class DesktopItemToolTipTests
{
    private sealed class DummyIconExtractor : IIconExtractorService
    {
        public System.Windows.Media.ImageSource GetIcon(string filePath, bool large = true) => null!;
    }

    [Fact]
    public void DefaultTheme_ToolTipTokens_ExistemComValoresEsperados()
    {
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
                    // Já instanciado em outra thread
                }
            }

            var dictionary = new ResourceDictionary
            {
                Source = new Uri("/DeskQuadra.UI.Wpf;component/Theme/Default.xaml", UriKind.RelativeOrAbsolute)
            };

            Assert.True(dictionary.Contains("ToolTip.CornerRadius"));
            Assert.True(dictionary.Contains("ToolTip.Padding"));
            Assert.True(dictionary.Contains("ToolTip.FontSize"));

            var cornerRadius = (CornerRadius)dictionary["ToolTip.CornerRadius"];
            var padding = (Thickness)dictionary["ToolTip.Padding"];
            var fontSize = (double)dictionary["ToolTip.FontSize"];

            Assert.Equal(6.0, cornerRadius.TopLeft);
            Assert.Equal(8.0, padding.Left);
            Assert.Equal(5.0, padding.Top);
            Assert.Equal(12.0, fontSize);
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void DesktopItemViewModel_Name_PropriedadeVinculadaAoToolTipExibeApenasNomeSemCaminho()
    {
        var item = new DesktopItem("Steam", @"C:\Program Files (x86)\Steam\steam.exe");
        var vm = new DesktopItemViewModel(item, new DummyIconExtractor());

        Assert.Equal("Steam", vm.Name);
        Assert.NotEqual(vm.FilePath, vm.Name);
        Assert.DoesNotContain(@"C:\", vm.Name);
    }

    [Fact]
    public void DesktopItemViewModel_UpdateNameAndPath_AtualizaNomeNotificandoPropertyChange()
    {
        var item = new DesktopItem("Antigo", @"C:\Antigo.txt");
        var vm = new DesktopItemViewModel(item, new DummyIconExtractor());

        var changed = new List<string>();
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                changed.Add(e.PropertyName);
            }
        };

        vm.UpdateNameAndPath("NovoNome", @"C:\NovoNome.txt");

        Assert.Equal("NovoNome", vm.Name);
        Assert.Equal("NovoNome", vm.ToolTipText);
        Assert.Contains(nameof(DesktopItemViewModel.Name), changed);
        Assert.Contains(nameof(DesktopItemViewModel.ToolTipText), changed);
    }

    [Fact]
    public void DesktopItemViewModel_ToolTipText_IniciaComNomeETransicionaParaCaminhoCompletoEReseta()
    {
        var item = new DesktopItem("Steam", @"C:\Program Files (x86)\Steam\steam.exe");
        var vm = new DesktopItemViewModel(item, new DummyIconExtractor());

        // Estado inicial: apenas o nome limpo
        Assert.Equal("Steam", vm.ToolTipText);

        var changed = new List<string>();
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                changed.Add(e.PropertyName);
            }
        };

        // Transição após 1s de hover contínuo: exibe o caminho completo
        vm.ShowFullPathInToolTip();
        Assert.Equal(@"C:\Program Files (x86)\Steam\steam.exe", vm.ToolTipText);
        Assert.Contains(nameof(DesktopItemViewModel.ToolTipText), changed);

        // Ao sair do item (MouseLeave / Closed): reseta de volta para o nome
        changed.Clear();
        vm.ResetToolTip();
        Assert.Equal("Steam", vm.ToolTipText);
        Assert.Contains(nameof(DesktopItemViewModel.ToolTipText), changed);
    }
}
