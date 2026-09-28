using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.UI.Wpf.Tests;

// Exercita só os helpers puros do menu clássico (sem COM/HWND).
public class ShellContextMenuHelpersTests
{
    [Fact]
    public void IsContextMenuCommand_IdZero_RetornaFalse()
    {
        Assert.False(NativeMethods.IsContextMenuCommand(0));
    }

    [Fact]
    public void IsContextMenuCommand_IdPositivo_RetornaTrue()
    {
        Assert.True(NativeMethods.IsContextMenuCommand(1));
    }

    [Fact]
    public void ToContextMenuVerbOffset_PrimeiroId_RetornaZero()
    {
        Assert.Equal(0, NativeMethods.ToContextMenuVerbOffset(1, 1));
    }

    [Fact]
    public void ToContextMenuVerbOffset_IdAdiante_RetornaRelativo()
    {
        Assert.Equal(5, NativeMethods.ToContextMenuVerbOffset(6, 1));
    }
}
