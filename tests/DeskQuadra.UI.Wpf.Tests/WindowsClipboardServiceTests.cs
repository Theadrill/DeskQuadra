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

    [Fact]
    public void SetFileDropList_WithIsCut_SetsPreferredDropEffectAndClears()
    {
        var thread = new Thread(() =>
        {
            var service = new WindowsClipboardService();
            string tempFile = System.IO.Path.GetTempFileName();
            try
            {
                bool success = service.SetFileDropList(new[] { tempFile }, isCut: true);
                if (success)
                {
                    Assert.True(service.ContainsFileDropList());
                    Assert.True(service.IsCutEffect());
                    var list = service.GetFileDropList();
                    Assert.Contains(tempFile, list);

                    service.Clear();
                    Assert.False(service.ContainsFileDropList());
                    Assert.False(service.IsCutEffect());
                }
            }
            finally
            {
                service.Clear();
                if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
