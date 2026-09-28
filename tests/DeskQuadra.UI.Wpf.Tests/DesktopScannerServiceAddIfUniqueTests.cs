using DeskQuadra.Infrastructure.WindowsShell.Services;

namespace DeskQuadra.UI.Wpf.Tests;

// D14: exercita só o helper puro AddIfUnique (sem SHGetKnownFolderPath/IO).
public class DesktopScannerServiceAddIfUniqueTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddIfUnique_CandidatoNuloVazioOuEspaco_Ignorado(string? candidate)
    {
        var paths = new List<string>();

        DesktopScannerService.AddIfUnique(paths, candidate);

        Assert.Empty(paths);
    }

    [Fact]
    public void AddIfUnique_CandidatoUnico_Adicionado()
    {
        var paths = new List<string>();

        DesktopScannerService.AddIfUnique(paths, @"C:\Users\Test\Desktop");

        Assert.Single(paths);
    }

    [Fact]
    public void AddIfUnique_DuplicadoExato_Ignorado()
    {
        var paths = new List<string> { @"C:\Users\Test\Desktop" };

        DesktopScannerService.AddIfUnique(paths, @"C:\Users\Test\Desktop");

        Assert.Single(paths);
    }

    [Fact]
    public void AddIfUnique_DuplicadoComCaseDiferente_Ignorado()
    {
        var paths = new List<string> { @"C:\Users\Test\Desktop" };

        DesktopScannerService.AddIfUnique(paths, @"c:\users\test\desktop");

        Assert.Single(paths);
    }

    [Fact]
    public void AddIfUnique_DoisDistintos_Adicionados()
    {
        var paths = new List<string>();

        DesktopScannerService.AddIfUnique(paths, @"C:\Users\Test\Desktop");
        DesktopScannerService.AddIfUnique(paths, @"C:\Users\Public\Desktop");

        Assert.Equal(2, paths.Count);
    }
}
