using DeskQuadra.Infrastructure.WindowsShell.Services;
using Xunit;

namespace DeskQuadra.UI.Wpf.Tests;

public class WindowsClipboardServiceTests
{
    [Fact]
    public void SetFileDropList_WhenFilePathsIsNull_ReturnsFalse()
    {
        var service = new WindowsClipboardService();
        bool result = service.SetFileDropList(null!);
        Assert.False(result);
    }

    [Fact]
    public void SetFileDropList_WhenFilePathsIsEmpty_ReturnsFalse()
    {
        var service = new WindowsClipboardService();
        bool result = service.SetFileDropList(Array.Empty<string>());
        Assert.False(result);
    }

    [Fact]
    public void SetFileDropList_WhenFilePathsOnlyHasWhitespace_ReturnsFalse()
    {
        var service = new WindowsClipboardService();
        bool result = service.SetFileDropList(new[] { "", "   ", "\t" });
        Assert.False(result);
    }
}
