using System.IO;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Services;
using Xunit;

namespace DeskQuadra.UI.Wpf.Tests;

public class WindowsShareServiceTests
{
    private sealed class FakeClipboardService : IClipboardService
    {
        public bool LastWasCut { get; private set; }
        public List<string> CopiedPaths { get; } = new();

        public bool SetFileDropList(IEnumerable<string> filePaths, bool isCut = false)
        {
            LastWasCut = isCut;
            CopiedPaths.Clear();
            CopiedPaths.AddRange(filePaths);
            return true;
        }

        public IReadOnlyList<string> GetFileDropList() => CopiedPaths;

        public bool ContainsFileDropList() => CopiedPaths.Count > 0;

        public bool IsCutEffect() => LastWasCut;

        public void Clear()
        {
            LastWasCut = false;
            CopiedPaths.Clear();
        }
    }

    [Fact]
    public void ShareFile_WhenFilePathIsNull_ReturnsFalse()
    {
        var service = new WindowsShareService();
        bool result = service.ShareFile(IntPtr.Zero, null!);
        Assert.False(result);
    }

    [Fact]
    public void ShareFile_WhenFilePathIsWhitespace_ReturnsFalse()
    {
        var service = new WindowsShareService();
        bool result = service.ShareFile(IntPtr.Zero, "   ");
        Assert.False(result);
    }

    [Fact]
    public void ShareFile_WhenFileDoesNotExist_ReturnsFalse()
    {
        var service = new WindowsShareService();
        bool result = service.ShareFile(IntPtr.Zero, @"C:\DeskQuadraNonExistentFile12345.xyz");
        Assert.False(result);
    }

    [Fact]
    public void ShareFile_WhenFileExistsAndShellVerbFails_FallbacksToClipboard()
    {
        var fakeClipboard = new FakeClipboardService();
        var service = new WindowsShareService(fakeClipboard);

        string tempFile = Path.GetTempFileName();
        try
        {
            bool result = service.ShareFile(IntPtr.Zero, tempFile);
            Assert.True(result);
            Assert.Single(fakeClipboard.CopiedPaths);
            Assert.Equal(tempFile, fakeClipboard.CopiedPaths[0]);
            Assert.False(fakeClipboard.LastWasCut);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
