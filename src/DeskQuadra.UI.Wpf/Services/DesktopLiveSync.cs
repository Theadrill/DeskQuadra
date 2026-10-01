using System.IO;
using System.Windows.Threading;
using DeskQuadra.Application.Services;
using DeskQuadra.Core.Contracts;

namespace DeskQuadra.UI.Wpf.Services;

/// <summary>
/// Live sync mínimo do Desktop físico (Fase 6): observa os mesmos diretórios do
/// scanner (usuário/OneDrive via KnownFolder + público + fallbacks) e, com
/// debounce de 250ms via <see cref="OneShotTimer"/> existente (sem timer novo),
/// chama <c>RescanDesktopItems</c>. O refresh da janela certa viaja pelo evento
/// <c>DesktopItemsRescanned</c> (sem XAML, sem mudar regras de TUDO/default).
/// Criado/renomeado cobre o 7-Zip (temp + rename); erro/overflow também
/// revarre (best-effort, silencioso, padrão do projeto).
/// </summary>
internal sealed class DesktopLiveSync : IDisposable
{
    private readonly ILayoutCoordinator _coordinator;
    private readonly Dispatcher _dispatcher;
    private readonly List<FileSystemWatcher> _watchers = new();
    private DispatcherTimer? _debounce;
    private bool _disposed;

    public DesktopLiveSync(ILayoutCoordinator coordinator, IDesktopScannerService scanner, Dispatcher dispatcher)
    {
        _coordinator = coordinator;
        _dispatcher = dispatcher;

        IReadOnlyList<string> dirs;
        try
        {
            dirs = scanner.GetWatchedDirectories();
        }
        catch
        {
            return;
        }

        foreach (var dir in dirs)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                {
                    continue;
                }

                var watcher = new FileSystemWatcher(dir)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true,
                };
                watcher.Created += OnFsEvent;
                watcher.Deleted += OnFsEvent;
                watcher.Renamed += OnFsEvent;
                watcher.Error += OnFsError;
                _watchers.Add(watcher);
            }
            catch
            {
                // Best-effort: uma pasta inacessível não bloqueia as demais.
            }
        }
    }

    private void OnFsEvent(object sender, FileSystemEventArgs e) => ScheduleRescan();

    private void OnFsError(object sender, ErrorEventArgs e) => ScheduleRescan();

    // Watcher dispara em thread de pool: o debounce (OneShotTimer, dono UI)
    // arma no Dispatcher; o tick (já na UI) faz o Rescan.
    private void ScheduleRescan()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _dispatcher.BeginInvoke(() => OneShotTimer.Arm(ref _debounce, 250, OnDebounced));
        }
        catch
        {
            // Silencioso, padrão do projeto.
        }
    }

    private void OnDebounced(object? sender, EventArgs e)
    {
        OneShotTimer.Cancel(ref _debounce, OnDebounced);
        if (_disposed)
        {
            return;
        }

        try
        {
            _coordinator.RescanDesktopItems();
        }
        catch
        {
            // Silencioso, padrão do projeto.
        }
    }

    public void Dispose()
    {
        _disposed = true;
        foreach (var watcher in _watchers)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Created -= OnFsEvent;
                watcher.Deleted -= OnFsEvent;
                watcher.Renamed -= OnFsEvent;
                watcher.Error -= OnFsError;
                watcher.Dispose();
            }
            catch
            {
                // Silencioso, padrão do projeto.
            }
        }

        _watchers.Clear();

        try
        {
            OneShotTimer.Cancel(ref _debounce, OnDebounced);
        }
        catch
        {
            // Silencioso, padrão do projeto.
        }
    }
}
