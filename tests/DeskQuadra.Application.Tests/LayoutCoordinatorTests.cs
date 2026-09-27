using DeskQuadra.Application.Services;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using Xunit;

namespace DeskQuadra.Application.Tests;

public class LayoutCoordinatorTests
{
    private class FakeRepository : ILayoutRepository
    {
        public List<Quadra> StoredQuadras { get; set; } = new();

        public Task<IReadOnlyList<Quadra>> LoadLayoutAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Quadra>>(StoredQuadras);
        }

        public Task SaveLayoutAsync(IEnumerable<Quadra> quadras, CancellationToken cancellationToken = default)
        {
            StoredQuadras = quadras.ToList();
            return Task.CompletedTask;
        }
    }

    private class FakeScanner : IDesktopScannerService
    {
        public List<DesktopItem> ItemsToReturn { get; set; } = new();

        public IReadOnlyList<DesktopItem> ScanDesktopItems()
        {
            return ItemsToReturn;
        }
    }

    [Fact]
    public async Task InitializeAsync_OnFirstRun_CreatesDefaultTudoQuadraWithScannedItems()
    {
        // Arrange
        var repo = new FakeRepository();
        var scanner = new FakeScanner();
        scanner.ItemsToReturn.Add(new DesktopItem("App 1", @"C:\App1.exe"));
        scanner.ItemsToReturn.Add(new DesktopItem("Pasta 1", @"C:\Pasta1", isDirectory: true));

        using var coordinator = new LayoutCoordinator(repo, scanner);

        // Act
        await coordinator.InitializeAsync();

        // Assert
        Assert.Single(coordinator.ActiveQuadras);
        var tudo = coordinator.ActiveQuadras[0];
        Assert.Equal("TUDO", tudo.Title);
        Assert.True(tudo.IsDefault);
        Assert.Equal(2, tudo.Items.Count);
        Assert.Equal("App 1", tudo.Items[0].Name);
        Assert.Equal("Pasta 1", tudo.Items[1].Name);
    }

    [Fact]
    public async Task InitializeAsync_WhenQuadrasExistWithoutItems_PopulatesWithScannedItems()
    {
        // Arrange (Simula transição de versão onde as Quadras já existiam mas vazias)
        var repo = new FakeRepository();
        repo.StoredQuadras.Add(new Quadra("Quadra 1", 100, 100, 300, 200, isDefault: true));

        var scanner = new FakeScanner();
        scanner.ItemsToReturn.Add(new DesktopItem("Shortcut", @"C:\Test.lnk"));

        using var coordinator = new LayoutCoordinator(repo, scanner);

        // Act
        await coordinator.InitializeAsync();

        // Assert
        Assert.Single(coordinator.ActiveQuadras);
        var quadra = coordinator.ActiveQuadras[0];
        Assert.Equal("TUDO", quadra.Title);
        Assert.Single(quadra.Items);
        Assert.Equal("Shortcut", quadra.Items[0].Name);
    }

    [Fact]
    public async Task RescanDesktopItems_UpdatesItemsInDefaultQuadra()
    {
        // Arrange
        var repo = new FakeRepository();
        var scanner = new FakeScanner();
        scanner.ItemsToReturn.Add(new DesktopItem("Original", @"C:\Orig.lnk"));

        using var coordinator = new LayoutCoordinator(repo, scanner);
        await coordinator.InitializeAsync();

        // Simula surgimento de novos arquivos no desktop
        scanner.ItemsToReturn.Clear();
        scanner.ItemsToReturn.Add(new DesktopItem("Novo Item", @"C:\Novo.txt"));

        // Act
        coordinator.RescanDesktopItems();

        // Assert
        var quadra = coordinator.ActiveQuadras[0];
        Assert.Single(quadra.Items);
        Assert.Equal("Novo Item", quadra.Items[0].Name);
    }

    [Fact]
    public void HideQuadra_MarksHiddenAndAppearsInHiddenQuadras()
    {
        // Arrange
        var repo = new FakeRepository();
        var scanner = new FakeScanner();

        using var coordinator = new LayoutCoordinator(repo, scanner);
        var quadra = coordinator.CreateNewQuadra("Q1", 0, 0);
        Guid? notifiedId = null;
        coordinator.QuadraHidden += (s, id) => notifiedId = id;

        // Act
        coordinator.HideQuadra(quadra.Id);

        // Assert
        Assert.True(quadra.IsHidden);
        Assert.Contains(coordinator.HiddenQuadras, q => q.Id == quadra.Id);
        Assert.Equal(quadra.Id, notifiedId);
    }

    [Fact]
    public void RestoreQuadra_UnmarksHiddenAndLeavesHiddenList()
    {
        // Arrange
        var repo = new FakeRepository();
        var scanner = new FakeScanner();

        using var coordinator = new LayoutCoordinator(repo, scanner);
        var quadra = coordinator.CreateNewQuadra("Q1", 0, 0);
        coordinator.HideQuadra(quadra.Id);
        Guid? notifiedId = null;
        coordinator.QuadraRestored += (s, id) => notifiedId = id;

        // Act
        coordinator.RestoreQuadra(quadra.Id);

        // Assert
        Assert.False(quadra.IsHidden);
        Assert.DoesNotContain(coordinator.HiddenQuadras, q => q.Id == quadra.Id);
        Assert.Equal(quadra.Id, notifiedId);
    }

    [Fact]
    public void RemoveQuadra_StillRemovesQuadraAndFiresEvent()
    {
        // Arrange
        var repo = new FakeRepository();
        var scanner = new FakeScanner();

        using var coordinator = new LayoutCoordinator(repo, scanner);
        var quadra = coordinator.CreateNewQuadra("Q1", 0, 0);
        Guid? notifiedId = null;
        coordinator.QuadraRemoved += (s, id) => notifiedId = id;

        // Act
        coordinator.RemoveQuadra(quadra.Id);

        // Assert
        Assert.DoesNotContain(coordinator.ActiveQuadras, q => q.Id == quadra.Id);
        Assert.Equal(quadra.Id, notifiedId);
    }

    [Fact]
    public void SetAllLocked_True_LocksAllQuadras()
    {
        // Arrange
        var repo = new FakeRepository();
        var scanner = new FakeScanner();

        using var coordinator = new LayoutCoordinator(repo, scanner);
        var q1 = coordinator.CreateNewQuadra("Q1", 0, 0);
        var q2 = coordinator.CreateNewQuadra("Q2", 400, 0);

        // Act
        coordinator.SetAllLocked(true);

        // Assert
        Assert.True(q1.IsLocked);
        Assert.True(q2.IsLocked);
        Assert.All(coordinator.ActiveQuadras, q => Assert.True(q.IsLocked));
    }

    [Fact]
    public void SetAllLocked_False_UnlocksAllQuadras()
    {
        // Arrange
        var repo = new FakeRepository();
        var scanner = new FakeScanner();

        using var coordinator = new LayoutCoordinator(repo, scanner);
        var q1 = coordinator.CreateNewQuadra("Q1", 0, 0);
        var q2 = coordinator.CreateNewQuadra("Q2", 400, 0);
        coordinator.SetAllLocked(true);

        // Act
        coordinator.SetAllLocked(false);

        // Assert
        Assert.False(q1.IsLocked);
        Assert.False(q2.IsLocked);
        Assert.All(coordinator.ActiveQuadras, q => Assert.False(q.IsLocked));
    }
}
