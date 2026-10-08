using System.IO;
using System.IO.Compression;
using DeskQuadra.Infrastructure.WindowsShell.Shell;

namespace DeskQuadra.UI.Wpf.Tests;

public sealed class ArchiveFallbackHandlerTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "DeskQuadra_FallbackTests_" + Guid.NewGuid().ToString("N"));

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
        }
    }

    private string CreateFile(string name, string content = "teste")
    {
        Directory.CreateDirectory(_root);
        string file = Path.Combine(_root, name);
        File.WriteAllText(file, content);
        return file;
    }

    private string CreateEmptyZip(string name)
    {
        Directory.CreateDirectory(_root);
        string file = Path.Combine(_root, name);
        using (var archive = ZipFile.Open(file, ZipArchiveMode.Create))
        {
        }
        return file;
    }

    [Fact]
    public void TryAddToArchive_Zip_SucessoComDotnetNativo()
    {
        string zip = CreateEmptyZip("pacote.zip");
        string file1 = CreateFile("item1.txt", "conteudo 1");
        string file2 = CreateFile("item2.txt", "conteudo 2");

        var sut = new ArchiveFallbackHandler(new FakeArchiverLocator());
        bool result = sut.TryAddToArchive(zip, new[] { file1, file2 }, out string? missingTool);

        Assert.True(result);
        Assert.Null(missingTool);

        // Verifica que os itens realmente entraram no zip
        using var archive = ZipFile.OpenRead(zip);
        Assert.NotNull(archive.GetEntry("item1.txt"));
        Assert.NotNull(archive.GetEntry("item2.txt"));
    }

    [Fact]
    public void TryAddToArchive_7z_SemCompactador_RetornaFalsoENomeDaFerramenta()
    {
        string file = CreateFile("dados.7z");
        string src = CreateFile("item.txt");

        var locator = new FakeArchiverLocator { SevenZipPath = null };
        var sut = new ArchiveFallbackHandler(locator);

        bool result = sut.TryAddToArchive(file, new[] { src }, out string? missingTool);

        Assert.False(result);
        Assert.Equal("7-Zip", missingTool);
    }

    [Fact]
    public void TryAddToArchive_Rar_SemCompactador_RetornaFalsoENomeDaFerramenta()
    {
        string file = CreateFile("dados.rar");
        string src = CreateFile("item.txt");

        var locator = new FakeArchiverLocator { WinRarPath = null };
        var sut = new ArchiveFallbackHandler(locator);

        bool result = sut.TryAddToArchive(file, new[] { src }, out string? missingTool);

        Assert.False(result);
        Assert.Equal("WinRAR", missingTool);
    }

    [Fact]
    public void TryAddToArchive_Tar_SemCompactador_RetornaFalsoENomeDaFerramenta()
    {
        string file = CreateFile("dados.tar");
        string src = CreateFile("item.txt");

        var locator = new FakeArchiverLocator { SevenZipPath = null, WinRarPath = null };
        var sut = new ArchiveFallbackHandler(locator);

        bool result = sut.TryAddToArchive(file, new[] { src }, out string? missingTool);

        Assert.False(result);
        Assert.Equal("7-Zip", missingTool);
    }

    [Fact]
    public void ArchiveDropService_FallbackSucesso_RetornaTrue()
    {
        string zip = CreateEmptyZip("teste.zip");
        string src = CreateFile("item.txt");

        var fakeFallback = new FakeFallbackHandler { ShouldSucceed = true };
        var sut = new ArchiveDropService(new ShellHostClient(), fakeFallback);

        bool result = sut.TryAddToContainer(zip, new[] { src });

        Assert.True(result);
    }

    [Fact]
    public void ArchiveDropService_FallbackComFerramentaFaltante_DisparaEventoToolMissing()
    {
        string archive = CreateFile("arquivo.7z");
        string src = CreateFile("item.txt");

        var fakeFallback = new FakeFallbackHandler
        {
            ShouldSucceed = false,
            MissingToolToReport = "7-Zip"
        };
        var sut = new ArchiveDropService(new ShellHostClient(), fakeFallback);

        ArchiveToolMissingEventArgs? capturedEvent = null;
        sut.ToolMissing += (s, e) => capturedEvent = e;

        bool result = sut.TryAddToContainer(archive, new[] { src });

        Assert.False(result);
        Assert.NotNull(capturedEvent);
        Assert.Equal(".7z", capturedEvent.Extension);
        Assert.Equal("7-Zip", capturedEvent.RequiredToolName);
        Assert.Equal(archive, capturedEvent.ContainerPath);
    }

    private sealed class FakeArchiverLocator : IExternalArchiverLocator
    {
        public string? SevenZipPath { get; set; }
        public string? WinRarPath { get; set; }

        public string? FindSevenZip() => SevenZipPath;
        public string? FindWinRar() => WinRarPath;
    }

    private sealed class FakeFallbackHandler : IArchiveFallbackHandler
    {
        public bool ShouldSucceed { get; set; }
        public string? MissingToolToReport { get; set; }

        public bool TryAddToArchive(string containerPath, IReadOnlyList<string> sourcePaths, out string? missingToolName)
        {
            missingToolName = MissingToolToReport;
            return ShouldSucceed;
        }
    }
}
