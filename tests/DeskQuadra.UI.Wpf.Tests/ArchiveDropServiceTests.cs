using System.IO;
using DeskQuadra.Infrastructure.WindowsShell.Shell;

namespace DeskQuadra.UI.Wpf.Tests;

// F3 drop-em-container: guardas do ArchiveDropService (puro, sem spawn do
// host — todos os casos abaixo retornam antes de tocar no ShellHost).
public sealed class ArchiveDropServiceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "DeskQuadra_ArchiveDrop_" + Guid.NewGuid().ToString("N"));

    private readonly ArchiveDropService _sut = new();

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
            // Best-effort: temp de teste nunca falha a suite.
        }
    }

    private string CreateFile(string name)
    {
        Directory.CreateDirectory(_root);
        string file = Path.Combine(_root, name);
        File.WriteAllText(file, "x");
        return file;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryAddToContainer_ContainerInvalido_RetornaFalso(string? container)
    {
        string source = CreateFile("doc.txt");

        Assert.False(_sut.TryAddToContainer(container, new List<string> { source }));
    }

    [Fact]
    public void TryAddToContainer_NaoContainer_RetornaFalso()
    {
        string txt = CreateFile("doc.txt");
        string source = CreateFile("outro.txt");

        Assert.False(_sut.TryAddToContainer(txt, new List<string> { source }));
    }

    [Fact]
    public void TryAddToContainer_ContainerInexistente_RetornaFalso()
    {
        string source = CreateFile("doc.txt");

        Assert.False(_sut.TryAddToContainer(
            Path.Combine(_root, "nao-existe.zip"), new List<string> { source }));
    }

    [Fact]
    public void TryAddToContainer_SemFontes_RetornaFalso()
    {
        string container = CreateFile("pacote.zip");

        Assert.False(_sut.TryAddToContainer(container, null));
        Assert.False(_sut.TryAddToContainer(container, new List<string>()));
    }

    [Fact]
    public void TryAddToContainer_FontesInexistentes_RetornaFalso()
    {
        string container = CreateFile("pacote.zip");

        Assert.False(_sut.TryAddToContainer(
            container, new List<string> { Path.Combine(_root, "nao-existe.txt") }));
    }

    [Fact]
    public void TryAddToContainer_SelfDrop_RetornaFalso()
    {
        string container = CreateFile("pacote.zip");

        Assert.False(_sut.TryAddToContainer(container, new List<string> { container }));
    }
}
