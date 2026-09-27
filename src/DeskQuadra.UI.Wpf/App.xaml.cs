using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DeskQuadra.Application;
using DeskQuadra.Application.Services;
using DeskQuadra.Application.Snap;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.Persistence;
using DeskQuadra.Infrastructure.WindowsShell;
using DeskQuadra.Infrastructure.WindowsShell.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Native;
using DeskQuadra.UI.Wpf.ViewModels;
using DeskQuadra.UI.Wpf.Views;
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
    // Janelas abertas rastreadas por Id da Quadra (ciclo de vida Esconder/Restaurar/Excluir)
    private readonly Dictionary<Guid, QuadraWindow> _quadraWindows = new();
    private WinForms.NotifyIcon? _trayIcon;

    protected override async void OnStartup(StartupEventArgs e)
    {
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

        _nativeIconService = _serviceProvider.GetRequiredService<INativeDesktopIconService>();

        if (e.Args.Contains("--restore-icons", StringComparer.OrdinalIgnoreCase))
        {
            _nativeIconService.ShowDesktopIcons();
            Shutdown();
            return;
        }
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

        // 3b. Cria o ícone da bandeja do sistema (acesso às Quadras escondidas + Sair)
        CreateTrayIcon();

        // 3. Configura serviço de desenho de Quadra com botão direito na Área de Trabalho
        _selectionWindow = new DesktopSelectionWindow();

        _drawingService.DrawingProgress += (s, rect) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_selectionWindow != null)
                {
                    _selectionWindow.UpdateBounds(rect.Left, rect.Top, rect.Width, rect.Height);
                    if (!_selectionWindow.IsVisible)
                    {
                        _selectionWindow.Show();
                    }
                }
            }, System.Windows.Threading.DispatcherPriority.Render);
        };

        _drawingService.DrawingCancelled += (s, ev) =>
        {
            Dispatcher.Invoke(() => _selectionWindow?.Hide());
        };

        _drawingService.GlobalLeftClick += (s, ev) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (NativeMethods.GetCursorPos(out var pt))
                {
                    IntPtr hwndUnder = NativeMethods.WindowFromPoint(pt);
                    NativeMethods.GetWindowThreadProcessId(hwndUnder, out uint pid);
                    if (pid != Environment.ProcessId)
                    {
                        QuadraWindow.DeselectAllGlobally();
                    }
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        };

        _drawingService.DrawingCompleted += (s, rect) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_selectionWindow == null)
                {
                    return;
                }

                _selectionWindow.Hide();

                var dpi = VisualTreeHelper.GetDpi(_selectionWindow);
                double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

                double dipLeft = rect.Left / dpiX;
                double dipTop = rect.Top / dpiY;
                double dipWidth = Math.Max(200, rect.Width / dpiX);
                double dipHeight = Math.Max(140, rect.Height / dpiY);

                ShowDualCreationMenu(dipLeft, dipTop, dipWidth, dipHeight, coordinator);
            });
        };

        _drawingService.Start();

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

        var viewModel = new QuadraViewModel(quadra, iconExtractor);
        var window = new QuadraWindow(viewModel, anchorService, snapEngine, coordinator, launcherService);
        window.Closed += (s, e) => _quadraWindows.Remove(quadra.Id);
        _quadraWindows[quadra.Id] = window;
        window.Show();
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
        // TODO Fase 5: ícone próprio (.ico do DeskQuadra); provisório usa o ícone padrão do sistema.
        _trayIcon = new WinForms.NotifyIcon
        {
            Text = UiStrings.TrayTooltip,
            Icon = System.Drawing.SystemIcons.Application,
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
        var exit = new WinForms.ToolStripMenuItem(UiStrings.TrayExit);

        ApplyTouchDensity(hiddenRoot);
        ApplyTouchDensity(showAll);
        ApplyTouchDensity(lockAll);
        ApplyTouchDensity(exit);

        showAll.Click += (s, e) => RestoreAllHiddenQuadras();
        lockAll.Click += (s, e) => ToggleLockAll(lockAll);
        exit.Click += (s, e) => Shutdown();

        menu.Items.Add(hiddenRoot);
        menu.Items.Add(showAll);
        menu.Items.Add(lockAll);

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

    private static void ShowDualCreationMenu(double left, double top, double width, double height, ILayoutCoordinator coordinator)
    {
        var popup = new Popup
        {
            AllowsTransparency = true,
            Placement = PlacementMode.MousePoint,
            StaysOpen = false,
            PopupAnimation = PopupAnimation.Fade
        };

        // Seam de temas (Default): valores idênticos aos literais anteriores via DynamicResource/TryFindResource.
        static T Theme<T>(string key, T fallback) =>
            Current?.TryFindResource(key) is T hit ? hit : fallback;

        var border = new Border
        {
            Background = Theme("CreationMenu.Background", new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x24))),
            BorderBrush = Theme("CreationMenu.BorderBrush", new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF))),
            BorderThickness = Theme("CreationMenu.BorderThickness", new Thickness(1)),
            CornerRadius = Theme("CreationMenu.CornerRadius", new CornerRadius(8)),
            Padding = Theme("CreationMenu.Padding", new Thickness(4)),
            Effect = new DropShadowEffect
            {
                BlurRadius = Theme("CreationMenu.Shadow.BlurRadius", 14.0),
                ShadowDepth = Theme("CreationMenu.Shadow.Depth", 2.0),
                Opacity = Theme("CreationMenu.Shadow.Opacity", 0.55),
                Color = Theme("CreationMenu.Shadow.Color", Colors.Black)
            }
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical };

        var btnCreate = new Button
        {
            Content = UiStrings.CreationMenuCreateQuadraHere,
            Background = Brushes.Transparent,
            Foreground = Theme("CreationMenu.Primary.Foreground", new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5))),
            BorderThickness = Theme("CreationMenu.Button.BorderThickness", new Thickness(0)),
            Padding = Theme("CreationMenu.Primary.Padding", new Thickness(12, 8, 12, 8)),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
            FontWeight = Theme("CreationMenu.Primary.FontWeight", FontWeights.SemiBold),
            FontSize = Theme("CreationMenu.Primary.FontSize", 12.0)
        };

        var btnCancel = new Button
        {
            Content = UiStrings.CreationMenuCancelAndShowWindowsMenu,
            Background = Brushes.Transparent,
            Foreground = Theme("CreationMenu.Secondary.Foreground", new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xBB))),
            BorderThickness = Theme("CreationMenu.Button.BorderThickness", new Thickness(0)),
            Padding = Theme("CreationMenu.Secondary.Padding", new Thickness(12, 6, 12, 6)),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Cursor = Cursors.Hand,
            FontSize = Theme("CreationMenu.Secondary.FontSize", 11.0)
        };

        var hoverBrush = Theme("CreationMenu.Hover.Background", new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)));
        btnCreate.MouseEnter += (s, e) => btnCreate.Background = hoverBrush;
        btnCreate.MouseLeave += (s, e) => btnCreate.Background = Brushes.Transparent;
        btnCancel.MouseEnter += (s, e) => btnCancel.Background = hoverBrush;
        btnCancel.MouseLeave += (s, e) => btnCancel.Background = Brushes.Transparent;

        btnCreate.Click += (s, e) =>
        {
            popup.IsOpen = false;
            int count = coordinator.ActiveQuadras.Count + 1;
            coordinator.CreateNewQuadra(string.Format(UiStrings.QuadraDefaultTitleFormat, count), left, top, width, height);
        };

        btnCancel.Click += (s, e) =>
        {
            popup.IsOpen = false;
        };

        stack.Children.Add(btnCreate);
        stack.Children.Add(new Separator
        {
            Margin = Theme("CreationMenu.Separator.Margin", new Thickness(4, 2, 4, 2)),
            Background = Theme("CreationMenu.Separator.Background", new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)))
        });
        stack.Children.Add(btnCancel);

        border.Child = stack;
        popup.Child = border;
        popup.IsOpen = true;
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
            _drawingService?.Stop();
            _selectionWindow?.Close();

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
