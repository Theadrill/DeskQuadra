using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DeskQuadra.Application;
using DeskQuadra.Application.Services;
using DeskQuadra.Application.Snap;
using DeskQuadra.Core;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.Persistence;
using DeskQuadra.Infrastructure.WindowsShell;
using DeskQuadra.Infrastructure.WindowsShell.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Native;
using DeskQuadra.Infrastructure.WindowsShell.Services;
using DeskQuadra.Infrastructure.WindowsShell.Shell;
using DeskQuadra.UI.Wpf.Services;
using DeskQuadra.UI.Wpf.ViewModels;
using DeskQuadra.UI.Wpf.Views;
using DeskQuadra.UI.Wpf.Theme;
using Microsoft.Extensions.DependencyInjection;
using WinForms = System.Windows.Forms;
using UiStrings = DeskQuadra.UI.Wpf.Properties.Strings;

namespace DeskQuadra.UI.Wpf;

/// <summary>
/// Ponto de entrada do aplicativo DeskQuadra, configurando o contêiner de Injeção de Dependências,
/// orquestrando o ciclo de vida das Quadras e garantindo a ocultação/restauração segura dos ícones nativos do desktop.
/// </summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;
    private INativeDesktopIconService? _nativeIconService;
    private IDesktopDrawingService? _drawingService;
    private DesktopSelectionWindow? _selectionWindow;
    private SettingsWindow? _settingsWindow;
    // Janelas abertas rastreadas por Id da Quadra (ciclo de vida Esconder/Restaurar/Excluir)
    private readonly Dictionary<Guid, QuadraWindow> _quadraWindows = new();
    private WinForms.NotifyIcon? _trayIcon;
    // Live sync do Desktop (Fase 6): watcher + debounce 250ms; null se falhar.
    private Services.DesktopLiveSync? _liveSync;
    // Trava de instância única (PO): segunda instância sai quieta, primeira intocada.
    // Guardado em campo até o fim do processo (nunca liberado).
    private Mutex? _singleInstanceMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Segunda instância: sai imediato sem tocar em nada (sem janela, sem tray,
        // sem fechar a instância dona — perfil não salvo não pode ser tocado).
        try
        {
            _singleInstanceMutex = new Mutex(true, @"Local\DeskQuadra.UI.Wpf", out bool createdNew);
            if (!createdNew)
            {
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                Shutdown();
                return;
            }
        }
        catch
        {
            // Best-effort: falha na trava nunca impede o app de abrir.
            _singleInstanceMutex = null;
        }

        base.OnStartup(e);

        // Dono do botão de pânico é o Guardian (hotkey global vive lá: com a UI travada o app não recebe WM_HOTKEY).
        // Aqui o app só escuta o pedido de saída limpa do Guardian; caminho não-crítico (se travar, o Guardian faz kill).
        try
        {
            var gracefulExit = new EventWaitHandle(false, EventResetMode.AutoReset, "DeskQuadraGracefulExit");
            _ = Task.Run(() =>
            {
                try
                {
                    if (gracefulExit.WaitOne())
                    {
                        Dispatcher.Invoke(() => Shutdown());
                    }
                }
                catch { /* saída limpa é best-effort; o Guardian segue para o kill */ }
            });
        }
        catch { /* evento nomeado indisponível: o Guardian segue para o kill; nunca derruba o startup */ }

        // Proteção técnica global para garantir que os ícones do Windows sejam restaurados em caso de falha não tratada
        AppDomain.CurrentDomain.UnhandledException += (s, args) => _nativeIconService?.ShowDesktopIcons();
        DispatcherUnhandledException += (s, args) => _nativeIconService?.ShowDesktopIcons();

        var services = new ServiceCollection();
        services.AddSingleton<IInputDeviceDetector, DeskQuadra.UI.Wpf.Services.InputDeviceDetector>();
        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();

        // Densidade inicial (Fatia 2): preferência global + probe para WM_DISPLAYCHANGE.
        var densityService = _serviceProvider.GetService<IDensitySettingsService>();
        if (densityService != null)
        {
            EnsureDensityWiring(densityService);
        }

        _nativeIconService = _serviceProvider.GetRequiredService<INativeDesktopIconService>();

        if (e.Args.Contains("--restore-icons", StringComparer.OrdinalIgnoreCase))
        {
            _nativeIconService.ShowDesktopIcons();
            Shutdown();
            return;
        }

        // --silent/--autostart (boot via Run): sobe silencioso só para o tray,
        // sem janela intrusiva. O fluxo abaixo já é tray-only (restaura as Quadras
        // visíveis via coordinator.InitializeAsync + OpenQuadraWindow) e nunca
        // abre Configurações sozinho — a flag fica documentada aqui para a Fatia 1.
        _ = StartupCommandBuilder.IsSilentLaunch(e.Args);
        var coordinator = _serviceProvider.GetRequiredService<ILayoutCoordinator>();
        _drawingService = _serviceProvider.GetRequiredService<IDesktopDrawingService>();

        // 1. Oculta os ícones nativos do desktop do Windows (Zero-Flicker)
        _nativeIconService.HideDesktopIcons();

        // 2. Inicia o Processo Guardião (Sidecar Watcher) para restaurar ícones em caso de encerramento abrupto/crash
        SpawnGuardianProcess();

        // 3. Registra ouvintes do ciclo de vida das Quadras (criar/esconder/restaurar/excluir)
        coordinator.QuadraCreated += (s, quadra) =>
        {
            Dispatcher.Invoke(() => OpenQuadraWindow(quadra));
        };

        coordinator.QuadraHidden += (s, id) =>
        {
            Dispatcher.Invoke(() => CloseQuadraWindow(id));
        };

        coordinator.QuadraRestored += (s, id) =>
        {
            Dispatcher.Invoke(() =>
            {
                var quadra = coordinator.ActiveQuadras.FirstOrDefault(q => q.Id == id);
                if (quadra != null)
                {
                    OpenQuadraWindow(quadra);
                }
            });
        };

        coordinator.QuadraRemoved += (s, id) =>
        {
            Dispatcher.Invoke(() => CloseQuadraWindow(id));
        };

        // Rescan (manual ou live sync): atualiza o modelo da Quadra padrão no
        // coordinator; o clique pode ter partido de outra Quadra, então o
        // refresh mira a janela CERTA pelo Id (sem XAML, sem timer novo).
        coordinator.DesktopItemsRescanned += (s, id) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_quadraWindows.TryGetValue(id, out var window))
                {
                    try
                    {
                        window.RefreshItemsFromModel();
                    }
                    catch
                    {
                        // Best-effort: persistência já ocorreu no coordinator.
                    }
                }
            });
        };

        // 3b. Cria o ícone da bandeja do sistema (acesso às Quadras escondidas + Sair)
        CreateTrayIcon();

        // 3. Configura serviço de desenho de Quadra com botão direito na Área de Trabalho
        _selectionWindow = new DesktopSelectionWindow();
        _selectionWindow.SourceInitialized += (s, e) => ChordDiagLog.OverlayHwnd = new WindowInteropHelper(_selectionWindow).Handle; // ChordDiag (TEMP: expõe HWND do overlay ao SnapshotUnder do hook; handle estável, só log)

        _drawingService.DrawingProgress += (s, rect) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_selectionWindow != null)
                {
                    _selectionWindow.UpdateBounds(rect.Left, rect.Top, rect.Width, rect.Height);
                    if (!_selectionWindow.IsVisible)
                    {
                        if (_drawingService?.IsDrawingActive != true)
                        {
                            _selectionWindow.Hide();
                        }
                        else
                        {
                            _selectionWindow.Show();
                            ChordDiagLog.Log("overlay Show (Progress)"); // ChordDiag
                        }
                    }
                }
            }, System.Windows.Threading.DispatcherPriority.Render);
        };

        _drawingService.DrawingCancelled += (s, ev) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _selectionWindow?.Hide();
                ChordDiagLog.Log("overlay Hide (Cancelled)"); // ChordDiag
            });
        };

        _drawingService.GlobalLeftClick += (s, ev) =>
        {
            // Carimbo do LDown real: o BeginInvoke abaixo (Background) roda DEPOIS do
            // Click que arma MOVER/RESIZE; o cancela-fora ignora o LDown que é o
            // próprio tap do menu (armado depois deste instante).
            DateTime raiseUtc = DateTime.UtcNow;
            Dispatcher.BeginInvoke(() =>
            {
                if (NativeMethods.GetCursorPos(out var pt))
                {
                    IntPtr hwndUnder = NativeMethods.WindowFromPoint(pt);
                    if (!NativeMethods.IsOwnProcessWindow(hwndUnder))
                    {
                        QuadraWindow.DeselectAllGlobally();
                        // MOVER armado: tap fora de qualquer Quadra cancela sem mover
                        QuadraWindow.CancelTouchMoveFromOutside(raiseUtc);
                        // Resize armado: tap fora restaura o título (mesmo caminho do MOVER)
                        QuadraWindow.CancelTouchResizeFromOutside(raiseUtc);
                    }
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        };

        _drawingService.DrawingCompleted += (s, rect) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_selectionWindow == null)
                {
                    return;
                }

                _selectionWindow.Hide();
                ChordDiagLog.Log($"overlay Hide (Completed) {ChordDiagLog.Snapshot()}"); // ChordDiag

                var (rawLeft, rawTop, rawWidth, rawHeight) = Services.DpiHelper.MapPhysicalToDip(_selectionWindow, rect.Left, rect.Top, rect.Width, rect.Height);

                double dipLeft = rawLeft;
                double dipTop = rawTop;
                double dipWidth = Math.Max(200, rawWidth);
                double dipHeight = Math.Max(140, rawHeight);

                ShowDualCreationMenu(dipLeft, dipTop, dipWidth, dipHeight, coordinator, ResolveEffectiveIsTouch());
            });
        };

        _drawingService.Start();

        // 3c. Recuperação de queda de energia (BRAINSTORMING 21/219-224): .tmp íntegro
        // avaliado ANTES do .bak dentro do repositório; aqui só o caso com diálogo
        // (.tmp íntegro E mais recente que .json). Best-effort: nunca derruba o boot.
        try
        {
            var layoutRepository = _serviceProvider.GetRequiredService<ILayoutRepository>();
            var pendingRecovery = await layoutRepository.CheckCrashRecoveryAsync();
            if (pendingRecovery is not null)
            {
                bool restoreRecent = Services.DarkDialog.Show(
                    UiStrings.Dialog_RecoveryTitle,
                    UiStrings.Dialog_RecoveryMessage,
                    UiStrings.Dialog_RestoreRecent,
                    UiStrings.Dialog_KeepPrevious,
                    owner: null,
                    width: 360);
                await layoutRepository.ResolveCrashRecoveryAsync(restoreRecent);
            }
        }
        catch
        {
            // Sem .tmp ou falha transitória: segue com o fluxo idêntico ao atual.
        }

        // 4. Carrega o layout transacional persistido em disco (%APPDATA%\DeskQuadra\quadras.json)
        await coordinator.InitializeAsync();

        // 5. Instancia e exibe as janelas de cada Quadra visível (escondidas ficam só no tray)
        foreach (var quadra in coordinator.ActiveQuadras)
        {
            if (quadra.IsHidden)
            {
                continue;
            }

            OpenQuadraWindow(quadra);
        }

        // 6. Live sync do Desktop físico (Fase 6): Created/Renamed com debounce
        // de 250ms via OneShotTimer existente → Rescan → evento refresca a
        // janela certa. Best-effort: nunca derruba o startup.
        try
        {
            var scanner = _serviceProvider.GetRequiredService<IDesktopScannerService>();
            _liveSync = new Services.DesktopLiveSync(coordinator, scanner, Dispatcher);
        }
        catch
        {
            _liveSync = null;
        }
    }

    // Abre a janela da Quadra e passa a rastreá-la pelo Id (ignora se já aberta)
    private void OpenQuadraWindow(Quadra quadra)
    {
        if (_serviceProvider == null || _quadraWindows.ContainsKey(quadra.Id))
        {
            return;
        }

        var anchorService = _serviceProvider.GetRequiredService<IWindowAnchorService>();
        var snapEngine = _serviceProvider.GetRequiredService<ISnapEngine>();
        var iconExtractor = _serviceProvider.GetRequiredService<IIconExtractorService>();
        var coordinator = _serviceProvider.GetRequiredService<ILayoutCoordinator>();
        var launcherService = _serviceProvider.GetRequiredService<IFileLauncherService>();
        var deletionService = _serviceProvider.GetRequiredService<IFileDeletionService>();
        var thirdPartyMenuService = _serviceProvider.GetRequiredService<IThirdPartyMenuService>();

        var viewModel = new QuadraViewModel(quadra, iconExtractor);
        var window = new QuadraWindow(viewModel, anchorService, snapEngine, coordinator, launcherService, deletionService, thirdPartyMenuService);
        window.Closed += (s, e) => _quadraWindows.Remove(quadra.Id);
        _quadraWindows[quadra.Id] = window;
        window.Show();
        window.ApplyDensity(ResolveEffectiveIsTouch());
    }

    // Densidade (Fatia 2): leitura sob demanda via GetSystemMetrics; sem hook/timer novo.
    private bool ResolveEffectiveIsTouch()
    {
        var density = _serviceProvider?.GetService<IDensitySettingsService>();
        bool hasHardware = NativeMethods.IsTouchHardwarePresent();
        return DensityResolver.ResolveIsTouch(density?.Current ?? DensityPreference.Auto, hasHardware);
    }

    private void ApplyDensityToAllOpen()
    {
        bool isTouch = ResolveEffectiveIsTouch();
        foreach (var window in _quadraWindows.Values)
        {
            try
            {
                window.ApplyDensity(isTouch);
            }
            catch
            {
                // Best-effort: uma janela não bloqueia as demais.
            }
        }
    }

    // Fecha a janela da Quadra sem remover o modelo (usado por Esconder/Excluir via eventos)
    private void CloseQuadraWindow(Guid id)
    {
        if (_quadraWindows.TryGetValue(id, out var window))
        {
            window.Close();
        }
    }

    // Cria o ícone da bandeja com menu das Quadras escondidas e opção Sair
    private void CreateTrayIcon()
    {
        // Ícone próprio (Fase 5): Assets/DeskQuadra.ico via AppIcon; fallback
        // silencioso para o ícone padrão do sistema se o recurso falhar.
        _trayIcon = new WinForms.NotifyIcon
        {
            Text = UiStrings.TrayTooltip,
            Icon = Services.AppIcon.Tray ?? System.Drawing.SystemIcons.Application,
            Visible = true
        };

        var menu = new WinForms.ContextMenuStrip
        {
            // Densidade touch SEMPRE ativa (decisão PO): sem detecção por-tap, sem toggle.
            // Só propriedades WinForms, sem owner-draw. Padding+AutoSize garante o alvo
            // touch e escala com DPI.
            Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point),
            Padding = new WinForms.Padding(4, 4, 4, 4),
            AutoSize = true,
            ImageScalingSize = new System.Drawing.Size(24, 24),
            ShowImageMargin = false,
        };
        var hiddenRoot = new WinForms.ToolStripMenuItem(UiStrings.TrayShowHiddenQuadras);
        var showAll = new WinForms.ToolStripMenuItem(UiStrings.TrayShowAll);
        // Trava global: item checkable antes do separador do Sair
        var lockAll = new WinForms.ToolStripMenuItem(UiStrings.TrayLockAllQuadras)
        {
            CheckOnClick = false
        };
        var settings = new WinForms.ToolStripMenuItem(UiStrings.TraySettings);
        var exit = new WinForms.ToolStripMenuItem(UiStrings.TrayExit);

        ApplyTouchDensity(hiddenRoot);
        ApplyTouchDensity(showAll);
        ApplyTouchDensity(lockAll);
        ApplyTouchDensity(settings);
        ApplyTouchDensity(exit);

        showAll.Click += (s, e) => RestoreAllHiddenQuadras();
        lockAll.Click += (s, e) => ToggleLockAll(lockAll);
        settings.Click += (s, e) => OpenSettings();
        exit.Click += (s, e) => Shutdown();

        menu.Items.Add(hiddenRoot);
        menu.Items.Add(showAll);
        menu.Items.Add(lockAll);
        menu.Items.Add(settings);

        // HangTestSwitch (TESTE DE FOGO, temporário): item que congela a UI de verdade.
        // Remover junto com Services/HangTestSwitch.cs somente no final do projeto.
        // (Antes do separador/Sair de propósito: Sair é sempre o último item do menu.)
        if (Services.HangTestSwitch.Enabled)
        {
            var hangTest = new WinForms.ToolStripMenuItem(Services.HangTestSwitch.MenuLabel);
            hangTest.Click += (s, e) =>
            {
                var owner = System.Windows.Application.Current?.MainWindow;
                string mensagem = UiStrings.HangTestConfirmMessage;
                string titulo = UiStrings.HangTestConfirmTitle;
                System.Windows.MessageBoxResult resposta = owner != null
                    ? System.Windows.MessageBox.Show(owner, mensagem, titulo, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning)
                    : System.Windows.MessageBox.Show(mensagem, titulo, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                if (resposta == System.Windows.MessageBoxResult.Yes)
                {
                    Services.HangTestSwitch.FreezeUi();
                }
            };
            ApplyTouchDensity(hangTest);
            menu.Items.Add(hangTest);
        }

        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(exit);

        // Reconstrói o submenu a cada abertura (lista de escondidas é dinâmica)
        menu.Opening += (s, e) => RebuildTrayMenu(hiddenRoot, showAll, lockAll);
        _trayIcon.ContextMenuStrip = menu;

        // Toque simples (e clique esquerdo) também abre o menu: o toque vira clique esquerdo,
        // que por padrão não abre ContextMenuStrip. Botão direito segue com o comportamento nativo.
        _trayIcon.MouseClick += (s, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                menu.Show(WinForms.Cursor.Position);
            }
        };

        // Duplo-clique restaura todas as escondidas (nada a fazer se não houver)
        _trayIcon.DoubleClick += (s, e) => RestoreAllHiddenQuadras();
    }

    // Densidade touch única do menu da bandeja: sempre ativa, sem owner-draw.
    // Padding amplo + fonte Segoe UI 12pt + AutoSize dão o alvo touch >=44px e escalam com DPI (DPI-safe).
    private static void ApplyTouchDensity(WinForms.ToolStripMenuItem item)
    {
        item.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        item.Padding = new WinForms.Padding(12, 10, 16, 10);
        item.Margin = new WinForms.Padding(0);
        item.AutoSize = true;
    }

    // Preenche o submenu com uma entrada por Quadra escondida + "Mostrar todas" quando houver >1
    private void RebuildTrayMenu(WinForms.ToolStripMenuItem hiddenRoot, WinForms.ToolStripMenuItem showAll, WinForms.ToolStripMenuItem lockAll)
    {
        hiddenRoot.DropDownItems.Clear();

        var coordinator = _serviceProvider?.GetService<ILayoutCoordinator>();
        var hidden = coordinator?.HiddenQuadras ?? (IReadOnlyList<Quadra>)Array.Empty<Quadra>();

        if (hidden.Count == 0)
        {
            var empty = new WinForms.ToolStripMenuItem(UiStrings.TrayNoHiddenQuadras) { Enabled = false };
            ApplyTouchDensity(empty);
            hiddenRoot.DropDownItems.Add(empty);
        }
        else
        {
            foreach (var quadra in hidden)
            {
                var id = quadra.Id;
                var item = new WinForms.ToolStripMenuItem(quadra.Title);
                item.Click += (s, e) => coordinator?.RestoreQuadra(id);
                ApplyTouchDensity(item);
                hiddenRoot.DropDownItems.Add(item);
            }
        }

        showAll.Visible = hidden.Count > 1;

        // Marcado somente se houver ≥1 Quadra e todas travadas (a UI lê o estado ao abrir o menu)
        var active = coordinator?.ActiveQuadras ?? (IReadOnlyList<Quadra>)Array.Empty<Quadra>();
        lockAll.Checked = active.Count > 0 && active.All(q => q.IsLocked);
    }

    // Alterna a trava global e reflete nas janelas abertas (cadeado/trava via RefreshLockState)
    private void ToggleLockAll(WinForms.ToolStripMenuItem lockAll)
    {
        var coordinator = _serviceProvider?.GetService<ILayoutCoordinator>();
        if (coordinator == null)
        {
            return;
        }

        var active = coordinator.ActiveQuadras;
        bool allLocked = active.Count > 0 && active.All(q => q.IsLocked);
        coordinator.SetAllLocked(!allLocked);
        lockAll.Checked = !allLocked;

        foreach (var window in _quadraWindows.Values)
        {
            window.RefreshLockState();
        }
    }

    // Restaura todas as Quadras escondidas (sem efeito quando não há nenhuma)
    private void RestoreAllHiddenQuadras()
    {
        var coordinator = _serviceProvider?.GetService<ILayoutCoordinator>();
        if (coordinator == null)
        {
            return;
        }

        foreach (var quadra in coordinator.HiddenQuadras)
        {
            coordinator.RestoreQuadra(quadra.Id);
        }
    }

    // Abre a janela Configurações (instância única; reativa se já visível)
    private void OpenSettings()
    {
        if (_settingsWindow != null && _settingsWindow.IsVisible)
        {
            _settingsWindow.Activate();
            return;
        }

        var startupService = _serviceProvider?.GetService<IStartupService>();
        var densityService = _serviceProvider?.GetService<IDensitySettingsService>();
        if (startupService == null || densityService == null)
        {
            return;
        }

        EnsureDensityWiring(densityService);
        // Leitura sob demanda + na abertura da janela (sem timer/hook novo).
        bool hasHardware = NativeMethods.IsTouchHardwarePresent();
        _settingsWindow = new SettingsWindow(new SettingsViewModel(startupService, densityService, hasHardware));
        _settingsWindow.Closed += (s, e) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private bool _densityWired;

    // Liga preferência global + probe para WM_DISPLAYCHANGE + aplica em todas ao trocar.
    private void EnsureDensityWiring(IDensitySettingsService densityService)
    {
        QuadraWindow.CurrentDensityPreference = densityService.Current;
        QuadraWindow.HasTouchHardwareProvider = NativeMethods.IsTouchHardwarePresent;
        if (_densityWired)
        {
            return;
        }

        _densityWired = true;
        densityService.PreferenceChanged += (s, pref) =>
        {
            Dispatcher.Invoke(() =>
            {
                QuadraWindow.CurrentDensityPreference = pref;
                ApplyDensityToAllOpen();
            });
        };
    }

    // Id do hotkey ESC do menu dual (escopo estrito: registra ao abrir o popup,
    // desregistra em todos os fechamentos; dono é o HWND da _selectionWindow).
    private const int DualMenuEscHotkeyId = 0xD9AD;

    // Dismiss do dual via hook único do desenho (sem segundo WH_MOUSE_LL):
    // lifetime explícito enquanto o popup dual estiver aberto — assinante do
    // hook + juiz do BeginInvoke enraizados em campo, null em DesligarEscDual.
    // Nunca só em variável local de método que retorna (GC coletaria e o
    // Windows chamaria memória liberada).
    private Action<NativeMethods.POINT>? _dualDismissWatcher;
    private Action? _dualDismissUiJudge;

    private void ShowDualCreationMenu(double left, double top, double width, double height, ILayoutCoordinator coordinator, bool isTouch)
    {
        var popup = new Popup
        {
            AllowsTransparency = true,
            Placement = PlacementMode.MousePoint,
            StaysOpen = false,
            PopupAnimation = PopupAnimation.Fade
        };

        // Seam de temas (Default): valores via ThemeResolver (TryFindResource + fallback).

        var border = new Border
        {
            Background = ThemeResolver.Get("CreationMenu.Background", new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x24))),
            BorderBrush = ThemeResolver.Get("CreationMenu.BorderBrush", new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF))),
            BorderThickness = ThemeResolver.Get("CreationMenu.BorderThickness", new Thickness(1)),
            CornerRadius = ThemeResolver.Get("CreationMenu.CornerRadius", new CornerRadius(8)),
            Padding = ThemeResolver.Get("CreationMenu.Padding", new Thickness(4)),
            Effect = new DropShadowEffect
            {
                BlurRadius = ThemeResolver.Get("CreationMenu.Shadow.BlurRadius", 14.0),
                ShadowDepth = ThemeResolver.Get("CreationMenu.Shadow.Depth", 2.0),
                Opacity = ThemeResolver.Get("CreationMenu.Shadow.Opacity", 0.55),
                Color = ThemeResolver.Get("CreationMenu.Shadow.Color", Colors.Black)
            }
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical };

        // Chrome claro padrão do Button neutralizado: template flat local só com Border +
        // ContentPresenter, sem trigger de sistema, para o token de hover aparecer
        // (precedente QuadraWindow.xaml:48-63). Só troca cor, sem shift de layout.
        var flatStyle = new Style(typeof(Button));
        flatStyle.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Transparent));
        flatStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
        var flatTemplate = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        borderFactory.SetBinding(Border.PaddingProperty, new Binding("Padding") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0));
        var presenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        presenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        presenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(presenterFactory);
        flatTemplate.VisualTree = borderFactory;
        flatStyle.Setters.Add(new Setter(Button.TemplateProperty, flatTemplate));
        flatStyle.Seal();

        // ESC fecha o dual: RegisterHotKey com escopo estrito ao popup.
        // Dono é o HWND da _selectionWindow (handle estável: o overlay já foi
        // exibido durante o gesto); o hook de WM_HOTKEY vive no HwndSource dela.
        // Tudo best-effort e silencioso: sem o ESC o popup segue fechando por
        // clique fora (StaysOpen=false).
        nint escOwnerHwnd = nint.Zero;
        HwndSource? escSource = null;
        try
        {
            if (_selectionWindow != null)
            {
                escOwnerHwnd = new WindowInteropHelper(_selectionWindow).Handle;
                escSource = HwndSource.FromHwnd(escOwnerHwnd);
            }
        }
        catch { /* silencioso, padrão do projeto */ }

        HwndSourceHook escHook = (IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == DualMenuEscHotkeyId)
            {
                handled = true;
                try { popup.IsOpen = false; } catch { /* silencioso */ }
                // O ESC reservado pelo hotkey seria engolido; republicar via
                // SendInput entrega ao destino original (padrão par-gesto).
                try { NativeMethods.RepublishEscapeKey(); } catch { /* silencioso */ }
            }
            return nint.Zero;
        };

        // Desliga em TODOS os fechamentos (Criar, Cancelar, Closed).
        // Sem segundo hook: desassina o watcher do hook único do desenho e
        // libera os delegates enraizados em campo. Guarda per-invocação só
        // p/ identidade (o lifetime segue no campo, nunca só no local).
        Action<NativeMethods.POINT>? installedDualWatcher = null;
        void DesligarEscDual()
        {
            try { escSource?.RemoveHook(escHook); } catch { /* silencioso */ }
            try { if (escOwnerHwnd != nint.Zero) NativeMethods.UnregisterHotKey(escOwnerHwnd, DualMenuEscHotkeyId); } catch { /* silencioso */ }
            var selfDualWatcher = installedDualWatcher;
            if (selfDualWatcher != null)
            {
                try
                {
                    if (_drawingService is DesktopDrawingService svcOff
                        && svcOff.DualDismissWatcher == selfDualWatcher)
                    {
                        svcOff.DualDismissWatcher = null;
                    }
                }
                catch { /* silencioso */ }
                if (_dualDismissWatcher == selfDualWatcher)
                {
                    _dualDismissWatcher = null;
                    _dualDismissUiJudge = null;
                }
                installedDualWatcher = null;
            }
        }

        try { escSource?.AddHook(escHook); } catch { /* silencioso */ }

        var btnCreate = new Button
        {
            Style = flatStyle,
            Content = UiStrings.CreationMenuCreateQuadraHere,
            Background = Brushes.Transparent,
            Foreground = ThemeResolver.Get("CreationMenu.Primary.Foreground", new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5))),
            BorderThickness = ThemeResolver.Get("CreationMenu.Button.BorderThickness", new Thickness(0)),
            Padding = ThemeResolver.Get("CreationMenu.Primary.Padding", new Thickness(12, 8, 12, 8)),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
            FontWeight = ThemeResolver.Get("CreationMenu.Primary.FontWeight", FontWeights.SemiBold),
            FontSize = ThemeResolver.Get("CreationMenu.Primary.FontSize", 12.0)
        };

        var btnCancel = new Button
        {
            Style = flatStyle,
            Content = UiStrings.CreationMenuCancelAndShowWindowsMenu,
            Background = Brushes.Transparent,
            Foreground = ThemeResolver.Get("CreationMenu.Secondary.Foreground", new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xBB))),
            BorderThickness = ThemeResolver.Get("CreationMenu.Button.BorderThickness", new Thickness(0)),
            Padding = ThemeResolver.Get("CreationMenu.Secondary.Padding", new Thickness(12, 6, 12, 6)),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
            FontSize = ThemeResolver.Get("CreationMenu.Secondary.FontSize", 11.0)
        };

        // Altura mínima por densidade (Normal ~32px, Touch 44px+): sem MinHeight o
        // Padding fixo esmagava os botões (secundário ~26px). Só altura, sem shift de layout.
        double buttonMinHeight = isTouch
            ? ThemeResolver.Get("CreationMenu.Button.MinHeight.Touch", 44.0)
            : ThemeResolver.Get("CreationMenu.Button.MinHeight", 32.0);
        btnCreate.MinHeight = buttonMinHeight;
        btnCancel.MinHeight = buttonMinHeight;

        // Fallback alinhado ao token CreationMenu.Hover.Background (#33FFFFFF).
        var hoverBrush = ThemeResolver.Get("CreationMenu.Hover.Background", new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)));
        // Foreground branco do hover via token (contraste 4.5:1 sobre o fundo escuro).
        var hoverFg = ThemeResolver.Get("CreationMenu.Hover.Foreground", (Brush)Brushes.White);
        // Feedback de hover sem shift de layout: troca só Background/Foreground, preservando
        // o Foreground original de cada botão (primário e secundário têm cores diferentes).
        void AttachHover(Button btn, Brush normalFg)
        {
            btn.MouseEnter += (s, e) => { btn.Background = hoverBrush; btn.Foreground = hoverFg; };
            btn.MouseLeave += (s, e) => { btn.Background = Brushes.Transparent; btn.Foreground = normalFg; };
        }
        AttachHover(btnCreate, (Brush)btnCreate.Foreground);
        AttachHover(btnCancel, (Brush)btnCancel.Foreground);

        btnCreate.Click += (s, e) =>
        {
            popup.IsOpen = false;
            DesligarEscDual();
            ChordDiagLog.Log($"popup-dual close (criar) {ChordDiagLog.Snapshot()}"); // ChordDiag
            coordinator.CreateNewQuadra(QuadraNaming.NextTitle(coordinator.ActiveQuadras.Count, UiStrings.QuadraDefaultTitleFormat), left, top, width, height);
        };

        btnCancel.Click += (s, e) =>
        {
            popup.IsOpen = false;
            DesligarEscDual();
            ChordDiagLog.Log($"popup-dual close (cancelar) {ChordDiagLog.Snapshot()}"); // ChordDiag
        };

        stack.Children.Add(btnCreate);
        stack.Children.Add(new Separator
        {
            Margin = ThemeResolver.Get("CreationMenu.Separator.Margin", new Thickness(4, 2, 4, 2)),
            Background = ThemeResolver.Get("CreationMenu.Separator.Background", new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)))
        });
        stack.Children.Add(btnCancel);

        border.Child = stack;
        popup.Child = border;
        popup.Closed += (s, e) =>
        {
            DesligarEscDual();
            ChordDiagLog.Log($"popup-dual Closed {ChordDiagLog.Snapshot()}"); // ChordDiag (dismiss leve: StaysOpen=false fecha sem Click)
        };
        // Toque: fecha fora no Down (tap qualquer duração) e no RDown (segurar). // toque: cobre tap+segurar
        // REUSE sem segundo WH_MOUSE_LL: assina o watcher do hook único do
        // desenho (vê injetado que o desenho ignora). Delegates enraizados em
        // campo (_dualDismissWatcher/_dualDismissUiJudge) enquanto o dual está
        // aberto; nunca só em local (GC). Nunca engole — o repasse segue no hook.
        _dualDismissWatcher = (NativeMethods.POINT ptDual) => // toque: sensor cego e rápido
        {
            try // toque: hook nunca lança
            {
                var ptCopyDual = ptDual; // toque: copia p/ closure (sem race entre taps)
                Action judgeDual = () => // toque: juiz na UI
                {
                    try // toque: best-effort silencioso
                    {
                        if (!popup.IsOpen) return; // toque: já fechado
                        IntPtr popupHwndDual = IntPtr.Zero; // toque: HWND atual
                        try { var srcDual = PresentationSource.FromVisual(border) as HwndSource; if (srcDual != null) popupHwndDual = srcDual.Handle; } catch { /* silencioso */ } // toque: HWND fresco
                        if (popupHwndDual == IntPtr.Zero) return; // toque: sem HWND nunca fecha (não se sabe onde foi o clique)
                        IntPtr underDual = IntPtr.Zero; // toque: quem está sob ponto
                        try { underDual = NativeMethods.WindowFromPoint(ptCopyDual); } catch { /* silencioso */ } // toque: sondagem DPI-safe
                        if (underDual == popupHwndDual) return; // toque: dentro vira botão
                        ChordDiagLog.Log($"popup-dual close (fora/watcher) {ChordDiagLog.Snapshot()}"); // toque: rastro no log // ChordDiag
                        try { popup.IsOpen = false; } catch { /* silencioso */ } // toque: fecha fora
                    }
                    catch { /* silencioso */ } // toque: nunca quebra UI
                };
                _dualDismissUiJudge = judgeDual; // toque: enraíza o juiz enquanto o dual está aberto
                try // toque: decide na UI
                {
                    Dispatcher.BeginInvoke(judgeDual, System.Windows.Threading.DispatcherPriority.Input); // toque: fecha rápido
                }
                catch { /* silencioso */ } // toque: sem dispatcher segue mouse
            }
            catch { /* silencioso */ } // toque: hook nunca falha
        };
        try { if (_drawingService is DesktopDrawingService svcOn) svcOn.DualDismissWatcher = _dualDismissWatcher; } catch { /* silencioso */ } // toque: liga watcher
        installedDualWatcher = _dualDismissWatcher; // toque: arma o desligar (sem isso nunca desassina)
        popup.IsOpen = true;
        // Liga o ESC só com o popup aberto (escopo estrito, modificador zero =
        // ESC puro); falha aqui só perde o ESC, o popup segue normal.
        try
        {
            if (escOwnerHwnd != nint.Zero)
            {
                NativeMethods.RegisterHotKey(escOwnerHwnd, DualMenuEscHotkeyId, 0, NativeMethods.VK_ESCAPE);
            }
        }
        catch { /* silencioso, padrão do projeto */ }
        ChordDiagLog.Log($"popup-dual open {ChordDiagLog.Snapshot()}"); // ChordDiag
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddWindowsShellInfrastructure();
        services.AddPersistenceInfrastructure();
        services.AddApplicationServices();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        if (_serviceProvider != null)
        {
            // Para o live sync antes do save final (sem rescan no shutdown).
            try
            {
                _liveSync?.Dispose();
            }
            catch
            {
                // Silencioso, padrão do projeto.
            }
            finally
            {
                _liveSync = null;
            }

            _drawingService?.Stop();
            _selectionWindow?.Close();
            _settingsWindow?.Close();

            var coordinator = _serviceProvider.GetService<ILayoutCoordinator>();
            coordinator?.SaveNowAsync().GetAwaiter().GetResult();

            // Restaura instantaneamente os ícones originais do Windows na saída
            _nativeIconService?.ShowDesktopIcons();
            _serviceProvider.Dispose();
        }

        base.OnExit(e);
    }

    private static void SpawnGuardianProcess()
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string[] possiblePaths =
            [
                Path.Combine(baseDir, "DeskQuadra.Guardian.exe"),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\DeskQuadra.Guardian\bin\Debug\net8.0-windows\DeskQuadra.Guardian.exe")),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\DeskQuadra.Guardian\bin\Release\net8.0-windows\DeskQuadra.Guardian.exe"))
            ];

            string? guardianExe = possiblePaths.FirstOrDefault(File.Exists);
            if (guardianExe != null)
            {
                LogGuardianSpawn(guardianExe);
                var psi = new ProcessStartInfo
                {
                    FileName = guardianExe,
                    Arguments = $"--parent-pid {Environment.ProcessId}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(psi);
            }
            else
            {
                LogGuardianSpawn("<nenhum Guardian encontrado nos caminhos conhecidos>");
            }
        }
        catch
        {
            // O guardião é um mecanismo de segurança autônomo e não deve bloquear o app
        }
    }

    // Diagnóstico: registra QUAL binário do Guardian foi disparado (caminho + data), no mesmo
    // guardian.log do Guardian — se o Guardian que roda é velho, aparece aqui na hora.
    private static void LogGuardianSpawn(string guardianExe)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskQuadra");
            Directory.CreateDirectory(dir);
            string detail = guardianExe;
            try
            {
                if (File.Exists(guardianExe))
                {
                    detail += $" (modificado em {File.GetLastWriteTime(guardianExe):dd/MM HH:mm})";
                }
            }
            catch
            {
                // Detalhe best-effort: o caminho basta para o diagnóstico.
            }

            File.AppendAllText(
                Path.Combine(dir, "guardian.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [App PID {Environment.ProcessId}] Guardian disparado: {detail}{Environment.NewLine}");
        }
        catch
        {
            // Log best-effort: nunca interfere no startup.
        }
    }
}
