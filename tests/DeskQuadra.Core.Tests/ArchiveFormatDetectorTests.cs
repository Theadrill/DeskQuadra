using DeskQuadra.Core.FileSystem;

namespace DeskQuadra.Core.Tests;

/// <summary>
/// Detecção de arquivo container para drop (F1 do `docs/PLANO_DROP_CONTAINER.md`).
/// Usa diretório temporário real, sem tocar no Desktop do usuário.
/// </summary>
public sealed class ArchiveFormatDetectorTests : IDisposable
{
    private readonly TempDirectory _temp = new("DeskQuadra_Archive_");
    private string _root => _temp.Path;

    public void Dispose() => _temp.Dispose();

    private string CreateFile(string name)
    {
        Directory.CreateDirectory(_root);
        string file = Path.Combine(_root, name);
        File.WriteAllText(file, "x");
        return file;
    }

    [Theory]
    [InlineData("a.zip")]
    [InlineData("a.7z")]
    [InlineData("a.rar")]
    [InlineData("a.tar")]
    [InlineData("a.gz")]
    [InlineData("a.tgz")]
    [InlineData("a.bz2")]
    [InlineData("a.xz")]
    public void IsContainer_ExtensaoConhecida_RetornaVerdadeiro(string name)
    {
        string file = CreateFile(name);

        Assert.True(ArchiveFormatDetector.IsContainer(file));
    }

    [Fact]
    public void IsContainer_CaseInsensitive_RetornaVerdadeiro()
    {
        string file = CreateFile("A.ZIP");

        Assert.True(ArchiveFormatDetector.IsContainer(file));
    }

    [Fact]
    public void IsContainer_TxtNaoEContainer_RetornaFalso()
    {
        string file = CreateFile("doc.txt");

        Assert.False(ArchiveFormatDetector.IsContainer(file));
    }

    [Fact]
    public void IsContainer_Diretorio_RetornaFalso()
    {
        Directory.CreateDirectory(_root);

        Assert.False(ArchiveFormatDetector.IsContainer(_root));
    }

    [Fact]
    public void IsContainer_LnkNaoEContainer_RetornaFalso()
    {
        string file = CreateFile("atalho.lnk");

        Assert.False(ArchiveFormatDetector.IsContainer(file));
    }

    [Fact]
    public void IsContainer_Inexistente_RetornaFalso()
    {
        Assert.False(ArchiveFormatDetector.IsContainer(Path.Combine(_root, "nao-existe.zip")));
    }

    [Fact]
    public void IsContainer_NuloVazioOuBranco_RetornaFalso()
    {
        Assert.False(ArchiveFormatDetector.IsContainer(null));
        Assert.False(ArchiveFormatDetector.IsContainer(string.Empty));
        Assert.False(ArchiveFormatDetector.IsContainer("   "));
    }
}
