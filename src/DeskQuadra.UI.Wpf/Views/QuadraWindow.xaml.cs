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
    private bool _isTouchActive;


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

        ItemsScrollViewer.PreviewMouseLeftButtonDown += ItemsScrollViewer_TouchScrollDown;
        ItemsScrollViewer.PreviewMouseMove += ItemsScrollViewer_TouchScrollMove;
        ItemsScrollViewer.PreviewMouseLeftButtonUp += ItemsScrollViewer_TouchScrollUp;

        GlobalItemSelected += OnGlobalItemSelected;
        GlobalCloseMenusRequested += OnGlobalCloseMenusRequested;

        // Rastreamento robusto e instantâneo de Toque físico na janela
        PreviewTouchDown += (s, e) => _isTouchActive = true;
        PreviewTouchUp += (s, e) => _isTouchActive = false;
        PreviewMouseDown += (s, e) =>
        {
            if (e.StylusDevice == null && !NativeMethods.IsCurrentMessageFromTouch())
            {
                _isTouchActive = false;
            }
        };
        PreviewMouseMove += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Released && e.RightButton == MouseButtonState.Released)
            {
                _isTouchActive = false;
            }
        };

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
        StopTouchInertia(); // qualquer novo Down/interação cancela a inércia
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

    // ==========================================
    // Scroll manual por toque (estilo celular)
    // O Deck entrega o dedo como mouse promovido (sem eventos WPF Touch e sem
    // manipulação do ScrollViewer), então o arrasto com assinatura de toque
    // desloca o VerticalOffset 1:1. Mouse real continua com drag de item.
    // ==========================================
    private bool _touchScrollActive;
    private Point _touchScrollStartPoint;
    private double _touchScrollStartOffset;

    // --- Inércia do scroll manual ---
    // Timer de ~60fps que prolonga o deslocamento após soltar o dedo em
    // movimento, com decaimento exponencial até parar ou novo Down.
    private DispatcherTimer? _touchInertiaTimer;
    private double _touchInertiaVelocityPxPerSec;
    private DateTime _touchInertiaLastTickUtc;
    private const double TouchInertiaMinStartVelocity = 150.0; // px/s p/ ativar
    private const double TouchInertiaStopVelocity = 50.0;      // px/s p/ parar
    private const double TouchInertiaFrictionPerSec = 3.5;     // decaimento exp.
    private const double TouchInertiaMaxVelocity = 5000.0;     // trava anti-salto
    private readonly List<(DateTime Time, double Y)> _touchMoveSamples = new();

    private static bool IsTouchPromotedMouse(MouseEventArgs e)
    {
        return (e.StylusDevice != null && e.StylusDevice.TabletDevice?.Type == TabletDeviceType.Touch)
               || NativeMethods.IsCurrentMessageFromTouch();
    }

    private static bool IsOnScrollbar(MouseEventArgs e)
    {
        DependencyObject? dep = e.OriginalSource as DependencyObject;
        while (dep != null)
        {
            if (dep is System.Windows.Controls.Primitives.ScrollBar)
            {
                return true;
            }
            dep = VisualTreeHelper.GetParent(dep);
        }
        return false;
    }

    private bool IsItemMenuOpen()
    {
        return _activeOpenItemContextMenu != null && _activeOpenItemContextMenu.IsOpen;
    }

    private void ItemsScrollViewer_TouchScrollDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || !IsTouchPromotedMouse(e) || IsOnScrollbar(e))
        {
            return;
        }

        StopTouchInertia(); // novo Down cancela qualquer inércia em curso
        _touchScrollActive = true;
        _touchScrollStartPoint = e.GetPosition(ItemsScrollViewer);
        _touchScrollStartOffset = ItemsScrollViewer.VerticalOffset;
        _touchMoveSamples.Clear();
        _touchMoveSamples.Add((DateTime.UtcNow, _touchScrollStartPoint.Y));
        ItemsScrollViewer.CaptureMouse();
        // Sem e.Handled aqui de propósito: o evento continua até o item para preservar "tocou, seleciona".
    }

    private void ItemsScrollViewer_TouchScrollMove(object sender, MouseEventArgs e)
    {
        if (!_touchScrollActive)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndTouchScroll();
            return;
        }

        if (!IsTouchPromotedMouse(e))
        {
            return;
        }

        // DECISÃO PO pendente: com menu de item aberto o Move touch congela
        // (só avalia/retorna, sem scrollar e sem converter em drag).
        if (IsItemMenuOpen())
        {
            return;
        }

        Point current = e.GetPosition(ItemsScrollViewer);

        ItemsScrollViewer.ScrollToVerticalOffset(_touchScrollStartOffset - (current.Y - _touchScrollStartPoint.Y));
        var now = DateTime.UtcNow;
        _touchMoveSamples.Add((now, current.Y));
        while (_touchMoveSamples.Count > 0 && (now - _touchMoveSamples[0].Time).TotalMilliseconds > 200)
        {
            _touchMoveSamples.RemoveAt(0);
        }
        if (_touchMoveSamples.Count > 20)
        {
            _touchMoveSamples.RemoveRange(0, _touchMoveSamples.Count - 20);
        }
        e.Handled = true;
    }

    private void ItemsScrollViewer_TouchScrollUp(object sender, MouseButtonEventArgs e)
    {
        double velocity = ComputeTouchReleaseVelocity();
        EndTouchScroll();
        _touchMoveSamples.Clear();
        StartTouchInertia(velocity);
    }

    private void EndTouchScroll()
    {
        if (!_touchScrollActive)
        {
            return;
        }

        _touchScrollActive = false;
        if (ItemsScrollViewer.IsMouseCaptured)
        {
            ItemsScrollViewer.ReleaseMouseCapture();
        }
    }

    // Velocidade do dedo (px/s) pelas últimas amostras (~120ms). Sinal
    // negativo = dedo subiu = conteúdo rola para baixo.
    private double ComputeTouchReleaseVelocity()
    {
        if (_touchMoveSamples.Count < 2)
        {
            return 0;
        }

        var last = _touchMoveSamples[_touchMoveSamples.Count - 1];
        int firstIndex = 0;
        while (firstIndex < _touchMoveSamples.Count - 2 &&
               (last.Time - _touchMoveSamples[firstIndex].Time).TotalMilliseconds > 120)
        {
            firstIndex++;
        }

        var first = _touchMoveSamples[firstIndex];
        double dt = (last.Time - first.Time).TotalSeconds;
        if (dt < 0.01)
        {
            return 0;
        }

        double velocity = -((last.Y - first.Y) / dt);
        if (velocity > TouchInertiaMaxVelocity)
        {
            velocity = TouchInertiaMaxVelocity;
        }
        else if (velocity < -TouchInertiaMaxVelocity)
        {
            velocity = -TouchInertiaMaxVelocity;
        }
        return velocity;
    }

    private void StartTouchInertia(double velocityPxPerSec)
    {
        if (Math.Abs(velocityPxPerSec) < TouchInertiaMinStartVelocity)
        {
            return;
        }
        if (ItemsScrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        _touchInertiaVelocityPxPerSec = velocityPxPerSec;
        _touchInertiaLastTickUtc = DateTime.UtcNow;
        if (_touchInertiaTimer == null)
        {
            _touchInertiaTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60fps
            };
            _touchInertiaTimer.Tick += TouchInertiaTimer_Tick;
        }
        _touchInertiaTimer.Start();
    }

    private void TouchInertiaTimer_Tick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        double dt = Math.Clamp((now - _touchInertiaLastTickUtc).TotalSeconds, 0.001, 0.05);
        _touchInertiaLastTickUtc = now;

        double max = ItemsScrollViewer.ScrollableHeight;
        if (max <= 0)
        {
            StopTouchInertia();
            return;
        }

        double next = ItemsScrollViewer.VerticalOffset + _touchInertiaVelocityPxPerSec * dt;
        if (next <= 0)
        {
            ItemsScrollViewer.ScrollToVerticalOffset(0);
            StopTouchInertia();
            return;
        }
        if (next >= max)
        {
            ItemsScrollViewer.ScrollToVerticalOffset(max);
            StopTouchInertia();
            return;
        }

        ItemsScrollViewer.ScrollToVerticalOffset(next);
        // Decaimento exponencial até parar.
        _touchInertiaVelocityPxPerSec *= Math.Exp(-TouchInertiaFrictionPerSec * dt);
        if (Math.Abs(_touchInertiaVelocityPxPerSec) < TouchInertiaStopVelocity)
        {
            StopTouchInertia();
        }
    }

    private void StopTouchInertia()
    {
        _touchInertiaTimer?.Stop();
        _touchInertiaVelocityPxPerSec = 0;
    }

    private void DesktopItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        StopTouchInertia(); // qualquer novo Down/interação cancela a inércia
        bool isTouch = _isTouchActive
                       || (e.StylusDevice != null && e.StylusDevice.TabletDevice?.Type == TabletDeviceType.Touch)
                       || NativeMethods.IsCurrentMessageFromTouch();

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
        // permitindo que o gesto de deslize execute o Panning suave no ScrollViewer (restaurado de 6a05e6c)
        if (!isTouch && e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed)
        {
            _itemDragStartPos = e.GetPosition(this);
            _draggedItemCandidate = (sender as FrameworkElement)?.DataContext as DesktopItemViewModel;
        }
    }

    private void DesktopItem_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // Hold nativo do Windows abre o menu do item da Quadra; tap é clique
        // esquerdo e movimento cancela o hold no próprio SO.
        if (sender is FrameworkElement fe && fe.ContextMenu != null)
        {
            // Toque usa itens de 46px; mouse segue compacto (~26px).
            bool isTouch = _isTouchActive || NativeMethods.IsCurrentMessageFromTouch();
            ApplyMenuDensity(fe.ContextMenu, isTouch: isTouch);
            fe.ContextMenu.Opened += (s, ev) => _activeOpenItemContextMenu = (ContextMenu)s;
            fe.ContextMenu.Closed += (s, ev) => { if (_activeOpenItemContextMenu == s) _activeOpenItemContextMenu = null; };
            _activeOpenItemContextMenu = fe.ContextMenu;
        }
    }

    private void ApplyMenuDensity(ContextMenu menu, bool isTouch)
    {
        var style = (Style)FindResource(isTouch ? "TouchMenuItemStyle" : "MouseMenuItemStyle");
        ApplyStyleRecursively(menu.Items, style);
    }

    private static void ApplyStyleRecursively(ItemCollection items, Style style)
    {
        foreach (var item in items)
        {
            if (item is MenuItem mi)
            {
                mi.Style = style;
                if (mi.Items.Count > 0)
                {
                    ApplyStyleRecursively(mi.Items, style);
                }
            }
        }
    }

    private void DesktopItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        bool isTouch = _isTouchActive
                       || (e.StylusDevice != null && e.StylusDevice.TabletDevice?.Type == TabletDeviceType.Touch)
                       || NativeMethods.IsCurrentMessageFromTouch();

        if (isTouch)
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
        StopTouchInertia(); // arrasto cancela a inércia do scroll manual
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
        _touchInertiaTimer?.Stop();
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
