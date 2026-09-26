using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.Persistence.Repositories;
using Xunit;

namespace DeskQuadra.Core.Tests;

public class JsonLayoutRepositoryTests : IDisposable
{
    private readonly string _testDir;
    private readonly JsonLayoutRepository _repository;

    public JsonLayoutRepositoryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "DeskQuadraTests_" + Guid.NewGuid().ToString("N"));
        _repository = new JsonLayoutRepository(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task SaveLayoutAsync_And_LoadLayoutAsync_RoundtripsSuccessfully()
    {
        // Arrange
        var quadras = new List<Quadra>
        {
            new("Documentos", 100, 150, 300, 200, isDefault: true),
            new("Jogos", 450, 150, 400, 350)
        };

        // Act
        await _repository.SaveLayoutAsync(quadras);
        var loaded = await _repository.LoadLayoutAsync();

        // Assert
        Assert.Equal(2, loaded.Count);

        var first = loaded[0];
        Assert.Equal("Documentos", first.Title);
        Assert.Equal(100, first.Left);
        Assert.Equal(150, first.Top);
        Assert.Equal(300, first.Width);
        Assert.Equal(200, first.Height);
        Assert.True(first.IsDefault);

        var second = loaded[1];
        Assert.Equal("Jogos", second.Title);
        Assert.Equal(450, second.Left);
        Assert.Equal(150, second.Top);
        Assert.Equal(400, second.Width);
        Assert.Equal(350, second.Height);
        Assert.False(second.IsDefault);
    }

    [Fact]
    public async Task LoadLayoutAsync_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        // Act
        var loaded = await _repository.LoadLayoutAsync();

        // Assert
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task DoubleBuffering_RotatesBackupOnSave()
    {
        // Arrange
        var initial = new[] { new Quadra("Versao 1", 10, 10) };
        var updated = new[] { new Quadra("Versao 2", 20, 20) };

        // Act
        await _repository.SaveLayoutAsync(initial);
        var bakPath = Path.Combine(_testDir, "quadras.json.bak");
        Assert.False(File.Exists(bakPath)); // Primeira gravação ainda não tem .bak

        await _repository.SaveLayoutAsync(updated);
        Assert.True(File.Exists(bakPath)); // Segunda gravação rotaciona a versão 1 para .bak

        // Assert
        var mainLoaded = await _repository.LoadLayoutAsync();
        Assert.Single(mainLoaded);
        Assert.Equal("Versao 2", mainLoaded[0].Title);
    }

    [Fact]
    public async Task LoadLayoutAsync_WhenJsonCorrupted_RecoversFromBackup()
    {
        // Arrange
        var version1 = new[] { new Quadra("Backup Valido", 50, 50) };
        var version2 = new[] { new Quadra("Sera Corrompido", 100, 100) };

        await _repository.SaveLayoutAsync(version1);
        await _repository.SaveLayoutAsync(version2);

        // Simula corrupção catastrófica de energia no quadras.json
        var jsonPath = Path.Combine(_testDir, "quadras.json");
        await File.WriteAllTextAsync(jsonPath, "{ broken json content truncation...");

        // Act
        var recovered = await _repository.LoadLayoutAsync();

        // Assert
        Assert.Single(recovered);
        Assert.Equal("Backup Valido", recovered[0].Title);
    }

    [Fact]
    public async Task SaveLayoutAsync_WithDesktopItems_PreservesAllItemsCorrectly()
    {
        // Arrange
        var quadra = new Quadra("TUDO", 200, 100, 360, 500, isDefault: true);
        quadra.Items.Add(new DesktopItem("Chrome", @"C:\Program Files\Chrome.exe", orderIndex: 0));
        quadra.Items.Add(new DesktopItem("Notas.txt", @"C:\Users\User\Desktop\Notas.txt", orderIndex: 1));
        quadra.Items.Add(new DesktopItem("Meus Projetos", @"C:\Users\User\Desktop\Projetos", isDirectory: true, orderIndex: 2));

        // Act
        await _repository.SaveLayoutAsync(new[] { quadra });
        var loaded = await _repository.LoadLayoutAsync();

        // Assert
        Assert.Single(loaded);
        var loadedQuadra = loaded[0];
        Assert.Equal("TUDO", loadedQuadra.Title);
        Assert.Equal(3, loadedQuadra.Items.Count);

        Assert.Equal("Chrome", loadedQuadra.Items[0].Name);
        Assert.Equal(@"C:\Program Files\Chrome.exe", loadedQuadra.Items[0].FilePath);
        Assert.False(loadedQuadra.Items[0].IsDirectory);

        Assert.Equal("Notas.txt", loadedQuadra.Items[1].Name);
        Assert.Equal(1, loadedQuadra.Items[1].OrderIndex);

        Assert.Equal("Meus Projetos", loadedQuadra.Items[2].Name);
        Assert.True(loadedQuadra.Items[2].IsDirectory);
    }

    [Fact]
    public async Task SaveLayoutAsync_WithSortModeAndLastModified_PreservesCorrectly()
    {
        // Arrange
        var testDate = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
        var quadra = new Quadra("Ordenada", 150, 150, 400, 300)
        {
            SortMode = SortMode.Date
        };
        quadra.Items.Add(new DesktopItem("Arquivo.txt", @"C:\Test\Arquivo.txt", orderIndex: 0, lastModified: testDate));

        // Act
        await _repository.SaveLayoutAsync(new[] { quadra });
        var loaded = await _repository.LoadLayoutAsync();

        // Assert
        Assert.Single(loaded);
        Assert.Equal(SortMode.Date, loaded[0].SortMode);
        Assert.Single(loaded[0].Items);
        Assert.Equal(testDate, loaded[0].Items[0].LastModified);
    }
}
