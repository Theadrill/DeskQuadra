using System.Collections.Concurrent;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;

namespace DeskQuadra.Application.Services;

/// <summary>
/// Orquestrador de layout que gerencia o estado em memória das Quadras e a persistência
/// com debounce de ~400ms para poupar operações de I/O em disco durante arraste/redimensionamento contínuo.
/// </summary>
public sealed class LayoutCoordinator : ILayoutCoordinator
{
    private readonly ILayoutRepository _repository;
    private readonly ConcurrentDictionary<Guid, Quadra> _activeQuadras = new();
    private readonly object _lock = new();
    private CancellationTokenSource? _debounceCts;
    private bool _isDisposed;

    public IReadOnlyList<Quadra> ActiveQuadras => _activeQuadras.Values.ToList();

    public event EventHandler<IReadOnlyList<Quadra>>? LayoutLoaded;
    public event EventHandler<Quadra>? QuadraCreated;
    public event EventHandler<Guid>? QuadraRemoved;

    public LayoutCoordinator(ILayoutRepository repository)
    {
        _repository = repository;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var loaded = await _repository.LoadLayoutAsync(cancellationToken).ConfigureAwait(false);

        _activeQuadras.Clear();

        if (loaded.Count > 0)
        {
            foreach (var q in loaded)
            {
                _activeQuadras[q.Id] = q;
            }
        }
        else
        {
            // First-run: cria a Quadra padrão inicial
            var defaultQuadra = new Quadra(
                title: "Quadra 1",
                left: 200,
                top: 150,
                width: 340,
                height: 260,
                isDefault: true);

            _activeQuadras[defaultQuadra.Id] = defaultQuadra;
            await _repository.SaveLayoutAsync(_activeQuadras.Values, cancellationToken).ConfigureAwait(false);
        }

        LayoutLoaded?.Invoke(this, ActiveQuadras);
    }

    public Quadra CreateNewQuadra(string title, double left, double top, double width = 340, double height = 260)
    {
        var quadra = new Quadra(title, left, top, width, height, isDefault: false);
        _activeQuadras[quadra.Id] = quadra;
        NotifyQuadraChanged(quadra);
        QuadraCreated?.Invoke(this, quadra);
        return quadra;
    }

    public void RemoveQuadra(Guid id)
    {
        if (_activeQuadras.TryRemove(id, out _))
        {
            // Salva o novo layout sem a quadra removida
            _ = Task.Run(async () =>
            {
                try
                {
                    await _repository.SaveLayoutAsync(_activeQuadras.Values).ConfigureAwait(false);
                }
                catch
                {
                    // Resiliente a falhas temporárias
                }
            });

            QuadraRemoved?.Invoke(this, id);
        }
    }

    public void NotifyQuadraChanged(Quadra quadra)
    {
        ArgumentNullException.ThrowIfNull(quadra);

        _activeQuadras[quadra.Id] = quadra;

        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }

            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();

            var token = _debounceCts.Token;

            // Debounce de ~400ms para gravação em disco
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(400, token).ConfigureAwait(false);
                    if (!token.IsCancellationRequested)
                    {
                        await _repository.SaveLayoutAsync(_activeQuadras.Values, token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Cancelado por nova alteração dentro do intervalo de debounce
                }
                catch
                {
                    // Resiliente a falhas temporárias de I/O
                }
            }, token);
        }
    }

    public async Task SaveNowAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }

        await _repository.SaveLayoutAsync(_activeQuadras.Values, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        lock (_lock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }
    }
}
