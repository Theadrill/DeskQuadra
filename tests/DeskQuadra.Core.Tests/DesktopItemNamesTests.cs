namespace DeskQuadra.Core.Tests;

/// <summary>
/// Nome de exibição de itens do Desktop (antes duplicado em QuadraViewModel/DesktopScannerService).
/// Lógica pura de path, sem resx/XAML/timers.
/// </summary>
public sealed class DesktopItemNamesTests
{
    [Fact]
    public void GetDisplayName_LnkMinusculo_RemoveExtensao()
    {
        Assert.Equal("atalho", DesktopItemNames.GetDisplayName(@"C:\Users\a\Desktop\atalho.lnk"));
    }

    [Fact]
    public void GetDisplayName_LnkMaiusculo_RemoveExtensao()
    {
        Assert.Equal("atalho", DesktopItemNames.GetDisplayName(@"C:\Users\a\Desktop\atalho.LNK"));
    }

    [Fact]
    public void GetDisplayName_LnkMisto_RemoveExtensao()
    {
        Assert.Equal("atalho", DesktopItemNames.GetDisplayName(@"C:\Users\a\Desktop\atalho.LnK"));
    }

    [Fact]
    public void GetDisplayName_ArquivoNormal_PreservaExtensao()
    {
        Assert.Equal("doc.txt", DesktopItemNames.GetDisplayName(@"C:\Users\a\Desktop\doc.txt"));
    }
}
