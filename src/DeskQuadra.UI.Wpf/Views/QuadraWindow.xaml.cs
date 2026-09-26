using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DeskQuadra.Application.Services;
using DeskQuadra.Application.Snap;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.Models;
using DeskQuadra.Infrastructure.WindowsShell.Native;
using DeskQuadra.UI.Wpf.ViewModels;

namespace DeskQuadra.UI.Wpf.Views;

public partial class QuadraWindow : Window
{
    private readonly QuadraViewModel _viewModel;
    private readonly IWindowAnchorService _anchorService;
    private readonly ISnapEngine _snapEngine;
    private readonly ILayoutCoordinator _coordinator;

    private bool _isInitializing = true;
    private bool _isDragging;
    private Point _dragStartScreenPoint;
    private double _initialLeft;
    private double _initialTop;

    public QuadraWindow(
        QuadraViewModel viewModel,
        IWindowAnchorService anchorService,
        ISnapEngine snapEngine,
        ILayoutCoordinator coordinator)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        _anchorService = anchorService;
        _snapEngine = snapEngine;
        _coordinator = coordinator;

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

        // Garante que o estado minimizado nunca seja mantido se for acionado externamente
        StateChanged += (s, e) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
        };
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
