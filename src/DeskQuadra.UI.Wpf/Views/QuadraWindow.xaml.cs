using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DeskQuadra.Application.Services;
using DeskQuadra.Application.Snap;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Native;
using DeskQuadra.UI.Wpf.Models;
using DeskQuadra.UI.Wpf.ViewModels;
using Microsoft.Win32;

namespace DeskQuadra.UI.Wpf.Views;

public partial class QuadraWindow : Window
{
    private readonly QuadraViewModel _viewModel;
    private readonly IWindowAnchorService _anchorService;
    private readonly ISnapEngine _snapEngine;
    private readonly ILayoutCoordinator _coordinator;
    private readonly IFileLauncherService _launcherService;

    private bool _isInitializing = true;
    private bool _isDragging;
    private Point _dragStartScreenPoint;
    private double _initialLeft;
    private double _initialTop;

    // Estado do Drag and Drop de itens internos
    private Point _itemDragStartPos;
    private DesktopItemViewModel? _draggedItemCandidate;
    private bool _isItemDragging;

    // Estado do gesto de toque prolongado (Touch Press & Hold)
    private DispatcherTimer? _touchHoldTimer;
    private DesktopItemViewModel? _touchTargetItem;
    private FrameworkElement? _touchTargetElement;
    private Point _touchStartPoint;
    private bool _isTouchHoldActive;

    public QuadraWindow(
        QuadraViewModel viewModel,
        IWindowAnchorService anchorService,
        ISnapEngine snapEngine,
        ILayoutCoordinator coordinator,
        IFileLauncherService launcherService)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        _anchorService = anchorService;
        _snapEngine = snapEngine;
        _coordinator = coordinator;
        _launcherService = launcherService;

        // Configura posicionamento manual estrito antes da inicialização visual
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = viewModel.Left;
        Top = viewModel.Top;
        Width = viewModel.Width;
        Height = viewModel.Height;

        InitializeComponent();

        Loaded += (s, e) => _isInitializing = false;
        LocationChanged += OnPositionOrSizeChanged;
        SizeChanged += OnPositionOrSizeChanged;

        GlobalItemSelected += OnGlobalItemSelected;
        GlobalCloseMenusRequested += OnGlobalCloseMenusRequested;

        // Garante que o estado minimizado nunca seja mantido se for acionado externamente
        StateChanged += (s, e) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
        };
    }

    public static event Action<DesktopItemViewModel?>? GlobalItemSelected;
    public static event Action? GlobalCloseMenusRequested;

    public static void DeselectAllGlobally()
    {
        GlobalItemSelected?.Invoke(null);
        GlobalCloseMenusRequested?.Invoke();
    }

    private ContextMenu? _activeOpenItemContextMenu;

    private void OnGlobalCloseMenusRequested()
    {
        if (_activeOpenItemContextMenu != null && _activeOpenItemContextMenu.IsOpen)
        {
            _activeOpenItemContextMenu.IsOpen = false;
            _activeOpenItemContextMenu = null;
        }

        if (TitleBarBorder?.ContextMenu != null && TitleBarBorder.ContextMenu.IsOpen)
        {
            TitleBarBorder.ContextMenu.IsOpen = false;
        }
    }

    private void OnGlobalItemSelected(DesktopItemViewModel? selectedItem)
    {
        foreach (var item in _viewModel.Items)
        {
            item.IsSelected = (selectedItem != null && item == selectedItem);
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        IntPtr hwnd = helper.Handle;

        // Posiciona a janela fisicamente nas coordenadas salvas antes de ancorar
        var dpi = VisualTreeHelper.GetDpi(this);
        int pxX = (int)Math.Round(_viewModel.Left * dpi.DpiScaleX);
        int pxY = (int)Math.Round(_viewModel.Top * dpi.DpiScaleY);
        int pxW = (int)Math.Round(_viewModel.Width * dpi.DpiScaleX);
        int pxH = (int)Math.Round(_viewModel.Height * dpi.DpiScaleY);

        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            pxX, pxY, pxW, pxH,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

        // 1. Vincula a janela ao Desktop Shell (Progman)
        _anchorService.AnchorToDesktop(hwnd);

        // 2. Instala o hook de janela para interceptar e neutralizar qualquer comando de ocultação (Win + D)
        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Intercepta e neutraliza ordens do sistema para esconder a janela no atalho Win + D
        if (msg == NativeMethods.WM_WINDOWPOSCHANGING)
        {
            var pos = Marshal.PtrToStructure<NativeMethods.WINDOWPOS>(lParam);
            bool modified = false;

            // Neutraliza a flag SWP_HIDEWINDOW enviada pelo Shell no Win + D
            if ((pos.flags & NativeMethods.SWP_HIDEWINDOW) != 0)
            {
                pos.flags &= ~NativeMethods.SWP_HIDEWINDOW;
                modified = true;
            }

            // Impede que o Shell estacione a janela fora da tela (-32000) no caso de tentativa de minimização
            if (pos.x <= -30000 || pos.y <= -30000)
            {
                pos.flags |= NativeMethods.SWP_NOMOVE;
                modified = true;
            }

            if (modified)
            {
                Marshal.StructureToPtr(pos, lParam, true);
            }
        }
        else if (msg == NativeMethods.WM_SYSCOMMAND)
        {
            int command = (int)wParam & 0xFFF0;
            if (command == NativeMethods.SC_MINIMIZE)
            {
                // Rejeita qualquer comando direto de minimização
                handled = true;
                return IntPtr.Zero;
            }
        }

        return IntPtr.Zero;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed)
        {
            if (NativeMethods.GetCursorPos(out var pt))
            {
                _isDragging = true;
                _dragStartScreenPoint = new Point(pt.X, pt.Y);
                _initialLeft = Left;
                _initialTop = Top;
                (sender as UIElement)?.CaptureMouse();
                e.Handled = true;
            }
        }
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            if (!NativeMethods.GetCursorPos(out var pt))
            {
                return;
            }

            var dpi = VisualTreeHelper.GetDpi(this);
            double deltaX = (pt.X - _dragStartScreenPoint.X) / dpi.DpiScaleX;
            double deltaY = (pt.Y - _dragStartScreenPoint.Y) / dpi.DpiScaleY;

            // Posição virtual livre calculada diretamente do ponto de partida, sem acúmulo de snap
            double rawLeft = _initialLeft + deltaX;
            double rawTop = _initialTop + deltaY;

            var proposed = new Rect2D(rawLeft, rawTop, Width, Height);

            // Obter WorkArea do monitor onde a janela se encontra
            var helper = new WindowInteropHelper(this);
            IntPtr hMonitor = NativeMethods.MonitorFromWindow(helper.Handle, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new NativeMethods.MONITORINFO();
            monitorInfo.cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>();

            Rect2D workAreaRect;
            if (hMonitor != IntPtr.Zero && NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                workAreaRect = new Rect2D(
                    monitorInfo.rcWork.Left / dpi.DpiScaleX,
                    monitorInfo.rcWork.Top / dpi.DpiScaleY,
                    (monitorInfo.rcWork.Right - monitorInfo.rcWork.Left) / dpi.DpiScaleX,
                    (monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top) / dpi.DpiScaleY);
            }
            else
            {
                workAreaRect = new Rect2D(
                    SystemParameters.WorkArea.Left,
                    SystemParameters.WorkArea.Top,
                    SystemParameters.WorkArea.Width,
                    SystemParameters.WorkArea.Height);
            }

            var obstacles = _coordinator.ActiveQuadras
                .Where(q => q.Id != _viewModel.Id)
                .Select(q => new Rect2D(q.Left, q.Top, q.Width, q.Height))
                .ToList();

            var snap = _snapEngine.CalculateSnap(proposed, workAreaRect, obstacles, threshold: 20, gap: 0);

            Left = snap.X;
            Top = snap.Y;
            e.Handled = true;
        }
    }

    private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            (sender as UIElement)?.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void TitleBar_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _isDragging = false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnPositionOrSizeChanged(object? sender, EventArgs e)
    {
        if (_isInitializing || double.IsNaN(Left) || double.IsNaN(Top) || double.IsNaN(Width) || double.IsNaN(Height))
        {
            return;
        }

        _viewModel.Left = Left;
        _viewModel.Top = Top;
        _viewModel.Width = Width;
        _viewModel.Height = Height;

        _coordinator.NotifyQuadraChanged(_viewModel.Model);
    }

    private void AddFileButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = "Adicionar Atalhos ou Arquivos à Quadra"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            foreach (var file in openFileDialog.FileNames)
            {
                _viewModel.AddItem(file);
            }

            _coordinator.NotifyQuadraChanged(_viewModel.Model);
        }
    }

    private void SortByName_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SortItems(SortMode.Name);
        _coordinator.NotifyQuadraChanged(_viewModel.Model);
    }

    private void SortByType_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SortItems(SortMode.Type);
        _coordinator.NotifyQuadraChanged(_viewModel.Model);
    }

    private void SortByDate_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SortItems(SortMode.Date);
        _coordinator.NotifyQuadraChanged(_viewModel.Model);
    }

    private void SortByManual_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SortItems(SortMode.Manual);
        _coordinator.NotifyQuadraChanged(_viewModel.Model);
    }

    private void QuadraContainer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        bool isOverItem = false;
        while (dep != null && dep != this)
        {
            if (dep is FrameworkElement fe && fe.DataContext is DesktopItemViewModel)
            {
                isOverItem = true;
                break;
            }
            dep = VisualTreeHelper.GetParent(dep);
        }

        if (!isOverItem)
        {
            DeselectAllGlobally();
        }
    }

    private void ScrollViewer_ManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
    {
        // Neutraliza o tremor/salto da janela nos limites superior e inferior da rolagem
        e.Handled = true;
    }

    private void DesktopItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        bool isTouch = e.StylusDevice != null && e.StylusDevice.TabletDevice?.Type == TabletDeviceType.Touch;

        if (sender is FrameworkElement fe && fe.DataContext is DesktopItemViewModel item)
        {
            GlobalItemSelected?.Invoke(item);

            if (e.ClickCount == 2)
            {
                _launcherService.Launch(item.FilePath);
                e.Handled = true;
                return;
            }
        }

        // Evita captura imediata de arraste quando o usuário estiver usando toque na tela (dedo),
        // permitindo que o gesto de deslize execute o Panning suave no ScrollViewer
        if (!isTouch && e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed)
        {
            _itemDragStartPos = e.GetPosition(this);
            _draggedItemCandidate = (sender as FrameworkElement)?.DataContext as DesktopItemViewModel;
        }
    }

    private void DesktopItem_PreviewTouchDown(object sender, TouchEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DesktopItemViewModel item)
        {
            _touchTargetElement = fe;
            _touchTargetItem = item;
            _touchStartPoint = e.GetTouchPoint(this).Position;
            _isTouchHoldActive = false;

            CancelTouchHoldTimer();
            _touchHoldTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(380)
            };
            _touchHoldTimer.Tick += OnTouchHoldTimerTick;
            _touchHoldTimer.Start();
        }
    }

    private void OnTouchHoldTimerTick(object? sender, EventArgs e)
    {
        CancelTouchHoldTimer();

        if (_touchTargetItem != null && _touchTargetElement != null)
        {
            _isTouchHoldActive = true;
            GlobalItemSelected?.Invoke(_touchTargetItem);

            // Desativa temporariamente o PanningMode para permitir que o movimento posterior arraste o item
            ItemsScrollViewer.PanningMode = PanningMode.None;
        }
    }

    private void ItemsScrollViewer_PreviewTouchMove(object sender, TouchEventArgs e)
    {
        Point currentPoint = e.GetTouchPoint(this).Position;
        Vector delta = currentPoint - _touchStartPoint;

        // Se o usuário mover mais de 12px antes dos 380ms, é uma rolagem (scroll): cancela o timer de Hold
        if (_touchHoldTimer != null && _touchHoldTimer.IsEnabled)
        {
            if (delta.Length > 12)
            {
                CancelTouchHoldTimer();
                _touchTargetItem = null;
                _touchTargetElement = null;
                _isTouchHoldActive = false;
            }
            return;
        }

        // Se já está em Hold e o usuário moveu o dedo além do limiar, inicia o Drag & Drop do atalho!
        if (_isTouchHoldActive && _touchTargetItem != null && _touchTargetElement != null)
        {
            if (delta.Length > 10)
            {
                _isTouchHoldActive = false;
                var item = _touchTargetItem;
                var fe = _touchTargetElement;
                _touchTargetItem = null;
                _touchTargetElement = null;

                // Desacopla o início do DoDragDrop da pipeline síncrona de eventos Touch para evitar conflito/deadlock no dispatcher
                Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
                {
                    StartItemDragDrop(item);
                });
            }
        }
    }

    private void ItemsScrollViewer_PreviewTouchUp(object sender, TouchEventArgs e)
    {
        CancelTouchHoldTimer();

        // Se o usuário manteve o dedo pressionado e soltou no mesmo lugar: abre o Menu de Contexto (botão direito)!
        if (_isTouchHoldActive && _touchTargetElement != null)
        {
            _isTouchHoldActive = false;
            ItemsScrollViewer.PanningMode = PanningMode.VerticalOnly;

            var menu = _touchTargetElement.ContextMenu;
            if (menu != null)
            {
                // Aplica dinamicamente o estilo ergonômico Touch (46px com hit targets amplos para dedos) apenas nos MenuItems
                ApplyMenuDensity(menu, isTouch: true);
                menu.Opened += (s, ev) => _activeOpenItemContextMenu = (ContextMenu)s;
                menu.Closed += (s, ev) => { if (_activeOpenItemContextMenu == s) _activeOpenItemContextMenu = null; };
                _activeOpenItemContextMenu = menu;
                menu.PlacementTarget = _touchTargetElement;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }
        else
        {
            ItemsScrollViewer.PanningMode = PanningMode.VerticalOnly;
        }

        _touchTargetItem = null;
        _touchTargetElement = null;
    }

    private void DesktopItem_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.ContextMenu != null)
        {
            // Quando acionado pelo mouse, garante estilo compacto (~26px) apenas nos MenuItems
            ApplyMenuDensity(fe.ContextMenu, isTouch: false);
            fe.ContextMenu.Opened += (s, ev) => _activeOpenItemContextMenu = (ContextMenu)s;
            fe.ContextMenu.Closed += (s, ev) => { if (_activeOpenItemContextMenu == s) _activeOpenItemContextMenu = null; };
            _activeOpenItemContextMenu = fe.ContextMenu;
        }
    }

    private void ApplyMenuDensity(ContextMenu menu, bool isTouch)
    {
        var style = (Style)FindResource(isTouch ? "TouchMenuItemStyle" : "MouseMenuItemStyle");
        foreach (var item in menu.Items)
        {
            if (item is MenuItem mi)
            {
                mi.Style = style;
            }
        }
    }

    private void CancelTouchHoldTimer()
    {
        if (_touchHoldTimer != null)
        {
            _touchHoldTimer.Stop();
            _touchHoldTimer = null;
        }
    }

    private void DesktopItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.StylusDevice != null && e.StylusDevice.TabletDevice?.Type == TabletDeviceType.Touch)
        {
            return;
        }

        if (_isItemDragging || _draggedItemCandidate == null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point currentPos = e.GetPosition(this);
        Vector diff = _itemDragStartPos - currentPos;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            var item = _draggedItemCandidate;
            StartItemDragDrop(item);
        }
    }

    private void StartItemDragDrop(DesktopItemViewModel item)
    {
        try
        {
            _isItemDragging = true;
            var payload = new QuadraDragPayload(_viewModel.Id, item);
            var dataObject = new DataObject();
            dataObject.SetData(typeof(QuadraDragPayload), payload);

            if (File.Exists(item.FilePath) || Directory.Exists(item.FilePath))
            {
                dataObject.SetData(DataFormats.FileDrop, new[] { item.FilePath });
            }

            Mouse.Capture(null);

            DragDropEffects result = DragDropEffects.None;
            try
            {
                result = DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Move | DragDropEffects.Copy);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StartItemDragDrop] DragDrop error: {ex.Message}");
            }

            if (payload.WasHandledAsMove)
            {
                _viewModel.RemoveItem(payload.Item);
                _coordinator.NotifyQuadraChanged(_viewModel.Model);
            }
        }
        finally
        {
            _isItemDragging = false;
            _draggedItemCandidate = null;
            ItemsScrollViewer.PanningMode = PanningMode.VerticalOnly;
        }
    }

    private void ItemMenuOpen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is DesktopItemViewModel item)
        {
            _launcherService.Launch(item.FilePath);
        }
    }

    private void ItemMenuOpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is DesktopItemViewModel item)
        {
            try
            {
                if (File.Exists(item.FilePath))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.FilePath}\"") { UseShellExecute = true });
                }
                else if (Directory.Exists(item.FilePath))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{item.FilePath}\"") { UseShellExecute = true });
                }
            }
            catch
            {
                // Silencioso
            }
        }
    }

    private void ItemMenuRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is DesktopItemViewModel item)
        {
            _viewModel.RemoveItem(item);
            _coordinator.NotifyQuadraChanged(_viewModel.Model);
        }
    }

    private void DesktopItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _draggedItemCandidate = null;
        _isItemDragging = false;
    }

    private static bool IsCopyRequested(DragEventArgs e)
    {
        return (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey
            || Keyboard.IsKeyDown(Key.LeftCtrl)
            || Keyboard.IsKeyDown(Key.RightCtrl)
            || Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            || (NativeMethods.GetKeyState(0x11 /* VK_CONTROL */) & 0x8000) != 0;
    }

    private void Quadra_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(QuadraDragPayload)) || e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            bool isControlPressed = IsCopyRequested(e);
            e.Effects = isControlPressed ? DragDropEffects.Copy : DragDropEffects.Move;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void Quadra_Drop(object sender, DragEventArgs e)
    {
        bool isCopy = IsCopyRequested(e);

        if (e.Data.GetDataPresent(typeof(QuadraDragPayload)))
        {
            var payload = e.Data.GetData(typeof(QuadraDragPayload)) as QuadraDragPayload;
            if (payload != null)
            {
                if (payload.SourceQuadraId == _viewModel.Id && !isCopy)
                {
                    // Mesmo container sem Ctrl: nenhuma ação necessária
                    e.Handled = true;
                    return;
                }

                if (isCopy)
                {
                    // Duplicação física no disco (Ctrl + Drag), tanto na mesma Quadra quanto entre Quadras
                    string duplicatedPath = DuplicateFileOnDisk(payload.Item.FilePath);
                    _viewModel.AddItem(duplicatedPath);
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    // Mover item entre Quadras distintas
                    payload.WasHandledAsMove = true;
                    _viewModel.AddItem(payload.Item.Model);
                    e.Effects = DragDropEffects.Move;
                }

                _coordinator.NotifyQuadraChanged(_viewModel.Model);
                e.Handled = true;
                return;
            }
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
            {
                foreach (var file in files)
                {
                    string targetFile = isCopy ? DuplicateFileOnDisk(file) : file;
                    _viewModel.AddItem(targetFile);
                }

                _coordinator.NotifyQuadraChanged(_viewModel.Model);
                e.Handled = true;
            }
        }
    }

    private static string DuplicateFileOnDisk(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                string dir = Path.GetDirectoryName(path) ?? string.Empty;
                string nameWithoutExt = Path.GetFileNameWithoutExtension(path);
                string ext = Path.GetExtension(path);

                int copyIndex = 1;
                string targetName = $"{nameWithoutExt} - Cópia{ext}";
                string targetPath = Path.Combine(dir, targetName);

                while (File.Exists(targetPath))
                {
                    copyIndex++;
                    targetName = $"{nameWithoutExt} - Cópia ({copyIndex}){ext}";
                    targetPath = Path.Combine(dir, targetName);
                }

                try
                {
                    File.Copy(path, targetPath);
                    return targetPath;
                }
                catch
                {
                    // Fallback para o Desktop do usuário se o diretório for protegido (ex: Public Desktop)
                    string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string fallbackTarget = Path.Combine(userDesktop, targetName);
                    while (File.Exists(fallbackTarget))
                    {
                        copyIndex++;
                        targetName = $"{nameWithoutExt} - Cópia ({copyIndex}){ext}";
                        fallbackTarget = Path.Combine(userDesktop, targetName);
                    }

                    File.Copy(path, fallbackTarget);
                    return fallbackTarget;
                }
            }

            if (Directory.Exists(path))
            {
                string parent = Directory.GetParent(path)?.FullName ?? string.Empty;
                string dirName = Path.GetFileName(path);

                int copyIndex = 1;
                string targetName = $"{dirName} - Cópia";
                string targetPath = Path.Combine(parent, targetName);

                while (Directory.Exists(targetPath))
                {
                    copyIndex++;
                    targetName = $"{dirName} - Cópia ({copyIndex})";
                    targetPath = Path.Combine(parent, targetName);
                }

                try
                {
                    CopyDirectoryRecursively(path, targetPath);
                    return targetPath;
                }
                catch
                {
                    string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string fallbackTarget = Path.Combine(userDesktop, targetName);
                    while (Directory.Exists(fallbackTarget))
                    {
                        copyIndex++;
                        targetName = $"{dirName} - Cópia ({copyIndex})";
                        fallbackTarget = Path.Combine(userDesktop, targetName);
                    }

                    CopyDirectoryRecursively(path, fallbackTarget);
                    return fallbackTarget;
                }
            }
        }
        catch
        {
            // Em caso de falha de I/O, usa o item original sem interromper a interface
        }

        return path;
    }

    private static void CopyDirectoryRecursively(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)));
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectoryRecursively(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
        }
    }

    private void RescanMenu_Click(object sender, RoutedEventArgs e)
    {
        _coordinator.RescanDesktopItems();
        _viewModel.RefreshItems();
    }

    private void NewQuadraMenu_Click(object sender, RoutedEventArgs e)
    {
        int count = _coordinator.ActiveQuadras.Count + 1;
        _coordinator.CreateNewQuadra(
            title: $"Quadra {count}",
            left: Left + 40,
            top: Top + 40,
            width: Width,
            height: Height);
    }

    protected override void OnClosed(EventArgs e)
    {
        CancelTouchHoldTimer();
        GlobalItemSelected -= OnGlobalItemSelected;
        GlobalCloseMenusRequested -= OnGlobalCloseMenusRequested;
        LocationChanged -= OnPositionOrSizeChanged;
        SizeChanged -= OnPositionOrSizeChanged;
        base.OnClosed(e);
    }

    // ==========================================
    // Manipuladores de Redimensionamento Livre
    // ==========================================

    private void ResizeRight_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double newWidth = Width + e.HorizontalChange;
        if (newWidth >= MinWidth)
        {
            Width = newWidth;
        }
    }

    private void ResizeBottom_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double newHeight = Height + e.VerticalChange;
        if (newHeight >= MinHeight)
        {
            Height = newHeight;
        }
    }

    private void ResizeLeft_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double newWidth = Width - e.HorizontalChange;
        if (newWidth >= MinWidth)
        {
            Width = newWidth;
            Left += e.HorizontalChange;
        }
    }

    private void ResizeTop_DragDelta(object sender, DragDeltaEventArgs e)
    {
        double newHeight = Height - e.VerticalChange;
        if (newHeight >= MinHeight)
        {
            Height = newHeight;
            Top += e.VerticalChange;
        }
    }

    private void ResizeBottomRight_DragDelta(object sender, DragDeltaEventArgs e)
    {
        ResizeRight_DragDelta(sender, e);
        ResizeBottom_DragDelta(sender, e);
    }

    private void ResizeBottomLeft_DragDelta(object sender, DragDeltaEventArgs e)
    {
        ResizeLeft_DragDelta(sender, e);
        ResizeBottom_DragDelta(sender, e);
    }

    private void ResizeTopRight_DragDelta(object sender, DragDeltaEventArgs e)
    {
        ResizeRight_DragDelta(sender, e);
        ResizeTop_DragDelta(sender, e);
    }

    private void ResizeTopLeft_DragDelta(object sender, DragDeltaEventArgs e)
    {
        ResizeLeft_DragDelta(sender, e);
        ResizeTop_DragDelta(sender, e);
    }
}
