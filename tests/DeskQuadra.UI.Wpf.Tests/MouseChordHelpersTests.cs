using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.UI.Wpf.Tests;

// Par-gesto R: exercita só as decisões puras (sem hook/COM/HWND).
public class MouseChordHelpersTests
{
    [Fact]
    public void IsInjectedMouseEvent_FlagInjetada_RetornaTrue()
    {
        Assert.True(NativeMethods.IsInjectedMouseEvent(NativeMethods.LLMHF_INJECTED, IntPtr.Zero));
    }

    [Fact]
    public void IsInjectedMouseEvent_SemFlagNemMarca_RetornaFalse()
    {
        Assert.False(NativeMethods.IsInjectedMouseEvent(0u, IntPtr.Zero));
    }

    [Fact]
    public void IsInjectedMouseEvent_MarcaPropria_RetornaTrue()
    {
        Assert.True(NativeMethods.IsInjectedMouseEvent(0u, NativeMethods.MouseChordRepublishTag));
    }

    [Fact]
    public void IsInjectedMouseEvent_ExtraInfoAlheio_RetornaFalse()
    {
        Assert.False(NativeMethods.IsInjectedMouseEvent(0u, new IntPtr(0x1234)));
    }

    [Fact]
    public void ShouldStartDrag_AbaixoDoLimiar_RetornaFalse()
    {
        Assert.False(NativeMethods.ShouldStartDrag(NativeMethods.DragThresholdPx - 0.1));
    }

    [Fact]
    public void ShouldStartDrag_NoLimiar_RetornaTrue()
    {
        Assert.True(NativeMethods.ShouldStartDrag(NativeMethods.DragThresholdPx));
    }

    [Fact]
    public void ShouldStartDrag_AcimaDoLimiar_RetornaTrue()
    {
        Assert.True(NativeMethods.ShouldStartDrag(NativeMethods.DragThresholdPx + 10));
    }
}
