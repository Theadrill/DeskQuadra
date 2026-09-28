using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.UI.Wpf.Tests;

// E1-infra: exercita só a decisão pura de fallback (sem Win32/handle real).
public class NativeMethodsProgmanHandleTests
{
    [Fact]
    public void ResolveProgmanHandle_FindResolveu_RetornaFindSemChamarFallback()
    {
        var expected = new IntPtr(0x1234);
        bool fallbackChamado = false;

        IntPtr result = NativeMethods.ResolveProgmanHandle(
            () => expected,
            () => { fallbackChamado = true; return new IntPtr(0x5678); });

        Assert.Equal(expected, result);
        Assert.False(fallbackChamado);
    }

    [Fact]
    public void ResolveProgmanHandle_FindZero_RetornaFallback()
    {
        var fallback = new IntPtr(0x5678);

        IntPtr result = NativeMethods.ResolveProgmanHandle(
            () => IntPtr.Zero,
            () => fallback);

        Assert.Equal(fallback, result);
    }

    [Fact]
    public void ResolveProgmanHandle_AmbosZero_RetornaZero()
    {
        IntPtr result = NativeMethods.ResolveProgmanHandle(
            () => IntPtr.Zero,
            () => IntPtr.Zero);

        Assert.Equal(IntPtr.Zero, result);
    }
}
