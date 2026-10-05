using System.Collections.Concurrent;
using System.IO;
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
    private readonly IDesktopScannerService _scannerService;
    private readonly ConcurrentDictionary<Guid, Quadra> _activeQuadras = new();
    private readonly object _lock = new();
    private CancellationTokenSource? _debounceCts;
    private bool _isDisposed;
    private bool _isInitialized;

    public IReadOnlyList<Quadra> ActiveQuadras => _activeQuadras.Values.ToList();

    public IReadOnlyList<Quadra> HiddenQuadras => _activeQuadras.Values.Where(q => q.IsHidden).ToList();

    public event EventHandler<IReadOnlyList<Quadra>>? LayoutLoaded;
    public event EventHandler<Quadra>? QuadraCreated;
    public event EventHandler<Guid>? QuadraRemoved;
    public event EventHandler<Guid>? QuadraHidden;
    public event EventHandler<Guid>? QuadraRestored;
    public event EventHandler<Guid>? DesktopItemsRescanned;

    public LayoutCoordinator(ILayoutRepository repository, IDesktopScannerService scannerService)
    {
        _repository = repository;
        _scannerService = scannerService;
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

            // Remove itens da Quadra padrão ("TUDO") que já estão organizados em Quadras customizadas
            var defaultQuadra = _activeQuadras.Values.FirstOrDefault(q => q.IsDefault);
            if (defaultQuadra != null)
            {
                var otherPaths = _activeQuadras.Values
                    .Where(q => q.Id != defaultQuadra.Id)
                    .SelectMany(q => q.Items)
                    .Select(i => NormalizePath(i.FilePath))
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (otherPaths.Count > 0)
                {
                    int removed = defaultQuadra.Items.RemoveAll(i => otherPaths.Contains(NormalizePath(i.FilePath)));
                    if (removed > 0)
                    {
                        await _repository.SaveLayoutAsync(_activeQuadras.Values, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            // Se nenhuma Quadra possuir itens (ex: transição da Fase 2 para Fase 3),
            // realiza o onboarding automático populando a Quadra padrão com a varredura do desktop
            if (!_activeQuadras.Values.Any(q => q.Items.Count > 0))
            {
                var targetQuadra = _activeQuadras.Values.FirstOrDefault(q => q.IsDefault) ?? _activeQuadras.Values.First();
                if (targetQuadra.Title == "Quadra 1")
                {
                    targetQuadra.Title = "TUDO";
                }
                targetQuadra.IsDefault = true;

                var scannedItems = _scannerService.ScanDesktopItems();
                targetQuadra.Items.AddRange(scannedItems);
                await _repository.SaveLayoutAsync(_activeQuadras.Values, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            // First-run: Onboarding automático criando a Quadra "TUDO" com todos os atalhos encontrados
            var scannedItems = _scannerService.ScanDesktopItems();

            var defaultQuadra = new Quadra(
                title: "TUDO",
                left: 360,
                top: 100,
                width: 360,
                height: 520,
                isDefault: true);

            defaultQuadra.Items.AddRange(scannedItems);

            _activeQuadras[defaultQuadra.Id] = defaultQuadra;
            await _repository.SaveLayoutAsync(_activeQuadras.Values, cancellationToken).ConfigureAwait(false);
        }

        _isInitialized = true;
        LayoutLoaded?.Invoke(this, ActiveQuadras);
    }

    public Quadra CreateNewQuadra(string title, double left, double top, double width = 340, double height = 260)
    {
        _isInitialized = true;
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

    public void HideQuadra(Guid id)
    {
        // Delega ao miolo comum (lookup + flag + Notify + evento).
        SetHidden(id, true);
    }

    public void RestoreQuadra(Guid id)
    {
        // Espelho do Hide: mesmo miolo, com flag desligada.
        SetHidden(id, false);
    }

    // Miolo comum de Hide/Restore: busca, alterna IsHidden, persiste via Notify e avisa a UI.
    // Id inexistente: sem efeito (sem Notify, sem evento), como antes.
    private void SetHidden(Guid id, bool hidden)
    {
        if (_activeQuadras.TryGetValue(id, out var quadra))
        {
            // Esconde (true) mantendo o modelo ou restaura (false) para reabrir a janela.
            quadra.IsHidden = hidden;
            NotifyQuadraChanged(quadra);
            if (hidden)
            {
                QuadraHidden?.Invoke(this, id);
            }
            else
            {
                QuadraRestored?.Invoke(this, id);
            }
        }
    }

    public void SetAllLocked(bool locked)
    {
        // Seta IsLocked em todas as ativas e persiste cada uma (o debounce coalesce as gravações)
        foreach (var quadra in _activeQuadras.Values)
        {
            quadra.IsLocked = locked;
            NotifyQuadraChanged(quadra);
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }
        try
        {
            return Path.GetFullPath(path).TrimEnd('\\', '/');
        }
        catch
        {
            return path.Trim().TrimEnd('\\', '/');
        }
    }

    public void RescanDesktopItems()
    {
        var scanned = _scannerService.ScanDesktopItems();
        var targetQuadra = _activeQuadras.Values.FirstOrDefault(q => q.IsDefault) ?? _activeQuadras.Values.FirstOrDefault();
        if (targetQuadra != null)
        {
            var otherQuadrasItems = _activeQuadras.Values
                .Where(q => q.Id != targetQuadra.Id)
                .SelectMany(q => q.Items)
                .Select(i => NormalizePath(i.FilePath))
                .Where(p => !string.IsNullOrEmpty(p))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var unassignedItems = scanned
                .Where(i => !otherQuadrasItems.Contains(NormalizePath(i.FilePath)))
                .ToList();

            targetQuadra.Items.Clear();
            targetQuadra.Items.AddRange(unassignedItems);
            NotifyQuadraChanged(targetQuadra);
            DesktopItemsRescanned?.Invoke(this, targetQuadra.Id);
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

            CancelDebounceLocked(createNew: true);

            var token = _debounceCts!.Token;

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
            if (_activeQuadras.IsEmpty && !_isInitialized)
            {
                return;
            }

            CancelDebounceLocked(createNew: false);
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
            CancelDebounceLocked(createNew: false);
        }
    }

    // Cancela e descarta o CTS atual; recria somente quando createNew for true.
    // Chamar sempre sob lock (_lock); o dispose permanece dentro do lock para evitar race.
    private void CancelDebounceLocked(bool createNew)
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = createNew ? new CancellationTokenSource() : null;
    }
}
