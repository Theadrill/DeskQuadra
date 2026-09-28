using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.UI.Wpf.Tests;

// D12: exercita só a comparação pura de PID (sem Win32/handle real).
public class NativeMethodsIsOwnProcessTests
{
    [Fact]
    public void IsOwnProcess_PidIgual_RetornaTrue()
    {
        Assert.True(NativeMethods.IsOwnProcess(1234u, 1234u));
    }

    [Fact]
    public void IsOwnProcess_PidDiferente_RetornaFalse()
    {
        Assert.False(NativeMethods.IsOwnProcess(1234u, 5678u));
    }

    [Fact]
    public void IsOwnProcess_PidZero_RetornaFalseContraPidAtual()
    {
        Assert.False(NativeMethods.IsOwnProcess(0u, (uint)Environment.ProcessId));
    }
}
