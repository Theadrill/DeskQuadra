using System.Windows;
using DeskQuadra.UI.Wpf.Properties;
using Xunit;

namespace DeskQuadra.UI.Wpf.Tests;

public class ContextMenuQuickActionsResourceTests
{
    [Fact]
    public void QuickActionStrings_AreDefinedAndNonEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(Strings.QuickAction_Cut));
        Assert.False(string.IsNullOrWhiteSpace(Strings.QuickAction_Copy));
        Assert.False(string.IsNullOrWhiteSpace(Strings.QuickAction_Rename));
        Assert.False(string.IsNullOrWhiteSpace(Strings.QuickAction_Share));
        Assert.False(string.IsNullOrWhiteSpace(Strings.QuickAction_Delete));

        Assert.Equal("Recortar", Strings.QuickAction_Cut);
        Assert.Equal("Copiar", Strings.QuickAction_Copy);
        Assert.Equal("Renomear", Strings.QuickAction_Rename);
        Assert.Equal("Compartilhar", Strings.QuickAction_Share);
        Assert.Equal("Excluir", Strings.QuickAction_Delete);
    }

    [Fact]
    public void ThemeDictionary_LoadsQuickActionResources()
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
                    // Já instanciado em outra thread concorrente
                }
            }

            var dict = new ResourceDictionary
            {
                Source = new Uri("/DeskQuadra.UI.Wpf;component/Theme/Default.xaml", UriKind.RelativeOrAbsolute)
            };

            Assert.True(dict.Contains("Menu.QuickAction.Accent"));
            Assert.True(dict.Contains("Menu.QuickAction.Foreground"));
            Assert.True(dict.Contains("Menu.QuickAction.Button.Width"));
            Assert.True(dict.Contains("Menu.QuickAction.Button.Height"));
            Assert.True(dict.Contains("Menu.QuickAction.Icon.Size"));
            Assert.True(dict.Contains("Menu.QuickAction.Text.Size"));
            Assert.True(dict.Contains("Icon.QuickAction.Cut.Blades"));
            Assert.True(dict.Contains("Icon.QuickAction.Cut.Handles"));
            Assert.True(dict.Contains("Icon.QuickAction.Copy.Back"));
            Assert.True(dict.Contains("Icon.QuickAction.Copy.Front"));
            Assert.True(dict.Contains("Icon.QuickAction.Rename.Frame"));
            Assert.True(dict.Contains("Icon.QuickAction.Rename.Cursor"));
            Assert.True(dict.Contains("Icon.QuickAction.Share.Box"));
            Assert.True(dict.Contains("Icon.QuickAction.Share.Arrow"));
            Assert.True(dict.Contains("Icon.QuickAction.Delete"));
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
