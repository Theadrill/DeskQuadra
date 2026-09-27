using DeskQuadra.Core.Models;

namespace DeskQuadra.Application.Services;

/// <summary>
/// Orquestrador de ciclo de vida e persistência transacional com debounce das Quadras ativas.
/// </summary>
public interface ILayoutCoordinator : IDisposable
{
    IReadOnlyList<Quadra> ActiveQuadras { get; }

    // Quadras marcadas com IsHidden=true (visíveis no tray, sem janela aberta).
    IReadOnlyList<Quadra> HiddenQuadras { get; }

    event EventHandler<IReadOnlyList<Quadra>>? LayoutLoaded;
    event EventHandler<Quadra>? QuadraCreated;
    event EventHandler<Guid>? QuadraRemoved;
    event EventHandler<Guid>? QuadraHidden;
    event EventHandler<Guid>? QuadraRestored;

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Quadra CreateNewQuadra(string title, double left, double top, double width = 340, double height = 260);

    void RemoveQuadra(Guid id);

    // Marca a Quadra como escondida (fecha a janela, mantém o modelo).
    void HideQuadra(Guid id);

    // Desmarca a Quadra escondida (reabre a janela).
    void RestoreQuadra(Guid id);

    void NotifyQuadraChanged(Quadra quadra);

    // Trava/destrava todas as Quadras ativas (persiste via NotifyQuadraChanged de cada uma).
    void SetAllLocked(bool locked);

    void RescanDesktopItems();

    Task SaveNowAsync(CancellationToken cancellationToken = default);
}
