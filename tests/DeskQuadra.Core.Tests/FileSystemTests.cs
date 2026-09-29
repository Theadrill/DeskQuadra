using DeskQuadra.Core.FileSystem;

namespace DeskQuadra.Core.Tests;

/// <summary>
/// Existência de caminho como arquivo ou diretório (antes duplicada em
/// FileLauncherService/IconExtractorService). Usa diretório temporário real,
/// sem tocar no Desktop do usuário.
/// </summary>
public sealed class FileSystemTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DeskQuadra_FileSystem_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // Best-effort: temp de teste nunca deve falhar a suite.
        }
    }

    [Fact]
    public void PathExists_ArquivoExistente_RetornaVerdadeiro()
    {
        Directory.CreateDirectory(_root);
        string file = Path.Combine(_root, "doc.txt");
        File.WriteAllText(file, "x");

        Assert.True(FileSystemUtils.PathExists(file));
    }

    [Fact]
    public void PathExists_DiretorioExistente_RetornaVerdadeiro()
    {
        Directory.CreateDirectory(_root);

        Assert.True(FileSystemUtils.PathExists(_root));
    }

    [Fact]
    public void PathExists_CaminhoInexistente_RetornaFalso()
    {
        Assert.False(FileSystemUtils.PathExists(Path.Combine(_root, "nao-existe.txt")));
    }

    [Fact]
    public void PathExists_NuloVazioOuBranco_RetornaFalso()
    {
        Assert.False(FileSystemUtils.PathExists(null));
        Assert.False(FileSystemUtils.PathExists(string.Empty));
        Assert.False(FileSystemUtils.PathExists("   "));
    }
}
