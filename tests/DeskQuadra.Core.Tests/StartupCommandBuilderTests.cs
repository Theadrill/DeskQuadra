using DeskQuadra.Core;
using Xunit;

namespace DeskQuadra.Core.Tests;

/// <summary>
/// Lógica pura do autostart (sem registry/UI): aspas da linha de comando e detecção de --silent.
/// </summary>
public class StartupCommandBuilderTests
{
    [Theory]
    [InlineData(@"C:\App\DeskQuadra.exe", "\"C:\\App\\DeskQuadra.exe\" --silent")]
    [InlineData(@"C:\Minha Pasta\DeskQuadra.exe", "\"C:\\Minha Pasta\\DeskQuadra.exe\" --silent")]
    public void Build_WrapsExeInQuotes_WithSilent(string exePath, string expected)
    {
        Assert.Equal(expected, StartupCommandBuilder.Build(exePath));
    }

    [Fact]
    public void Build_WithAlreadyQuotedPath_DoesNotDoubleQuote()
    {
        Assert.Equal(
            "\"C:\\App\\DeskQuadra.exe\" --silent",
            StartupCommandBuilder.Build("\"C:\\App\\DeskQuadra.exe\""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_WithInvalidPath_ReturnsEmpty(string? exePath)
    {
        Assert.Equal(string.Empty, StartupCommandBuilder.Build(exePath));
    }

    [Theory]
    [InlineData(new[] { "--silent" }, true)]
    [InlineData(new[] { "--autostart" }, true)]
    [InlineData(new[] { "--SILENT" }, true)]
    [InlineData(new[] { "--parent-pid", "123", "--silent" }, true)]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "--restore-icons" }, false)]
    public void IsSilentLaunch_DetectsSilentFlags(string[] args, bool expected)
    {
        Assert.Equal(expected, StartupCommandBuilder.IsSilentLaunch(args));
    }

    [Fact]
    public void IsSilentLaunch_WithNullArgs_ReturnsFalse()
    {
        Assert.False(StartupCommandBuilder.IsSilentLaunch(null));
    }
}
