using DeskQuadra.Core.Models;

namespace DeskQuadra.Application.Services;

/// <summary>
/// Orquestrador de ciclo de vida e persistência transacional com debounce das Quadras ativas.
/// </summary>
public interface ILayoutCoordinator : IDisposable
{
    IReadOnlyList<Quadra> ActiveQuadras { get; }

    event EventHandler<IReadOnlyList<Quadra>>? LayoutLoaded;
    event EventHandler<Quadra>? QuadraCreated;
    event EventHandler<Guid>? QuadraRemoved;

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Quadra CreateNewQuadra(string title, double left, double top, double width = 340, double height = 260);

    void RemoveQuadra(Guid id);

    void NotifyQuadraChanged(Quadra quadra);

    void RescanDesktopItems();

    Task SaveNowAsync(CancellationToken cancellationToken = default);
}
