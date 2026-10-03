using System.IO;
using DeskQuadra.Core;

namespace DeskQuadra.Core.Tests;

public class NewFolderCreatorTests
{
    [Fact]
    public void GetUniqueFolderPath_SemColisao_RetornaNomePadrao()
    {
        string dir = @"C:\Desktop";
        string result = NewFolderCreator.GetUniqueFolderPath(
            dir,
            "Nova pasta",
            "Nova pasta ({0})",
            directoryExists: _ => false);

        Assert.Equal(Path.Combine(dir, "Nova pasta"), result);
    }

    [Fact]
    public void GetUniqueFolderPath_ComUmaColisao_RetornaIndex2()
    {
        string dir = @"C:\Desktop";
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(dir, "Nova pasta")
        };

        string result = NewFolderCreator.GetUniqueFolderPath(
            dir,
            "Nova pasta",
            "Nova pasta ({0})",
            directoryExists: path => existing.Contains(path));

        Assert.Equal(Path.Combine(dir, "Nova pasta (2)"), result);
    }

    [Fact]
    public void GetUniqueFolderPath_ComMultiplasColisoes_IncrementaAteLivre()
    {
        string dir = @"C:\Desktop";
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(dir, "Nova pasta"),
            Path.Combine(dir, "Nova pasta (2)"),
            Path.Combine(dir, "Nova pasta (3)")
        };

        string result = NewFolderCreator.GetUniqueFolderPath(
            dir,
            "Nova pasta",
            "Nova pasta ({0})",
            directoryExists: path => existing.Contains(path));

        Assert.Equal(Path.Combine(dir, "Nova pasta (4)"), result);
    }
}
