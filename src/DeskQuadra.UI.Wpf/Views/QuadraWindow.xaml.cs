using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Native;
using DeskQuadra.UI.Wpf.ViewModels;

namespace DeskQuadra.UI.Wpf.Views;

public partial class QuadraWindow : Window
{
    private readonly IWindowAnchorService _anchorService;
    private bool _isDragging;
    private Point _dragStartPoint;
    private double _initialLeft;
    private double _initialTop;

    public QuadraWindow(QuadraViewModel viewModel, IWindowAnchorService anchorService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _anchorService = anchorService;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        IntPtr hwnd = helper.Handle;

        // 1. Executa a ancoragem estrutural Win32 na camada de desktop
        _anchorService.AnchorToDesktop(hwnd);

        // 2. Instala o hook de janela para blindagem contra SWP_HIDEWINDOW (Win + D)
        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Intercepta e neutraliza ordens do sistema para esconder ou minimizar a janela no Win + D
        if (msg == NativeMethods.WM_WINDOWPOSCHANGING)
        {
            var pos = Marshal.PtrToStructure<NativeMethods.WINDOWPOS>(lParam);
            if ((pos.flags & NativeMethods.SWP_HIDEWINDOW) != 0)
            {
                // Limpa a flag de ocultação forçada pelo Shell (Win + D)
                pos.flags &= ~NativeMethods.SWP_HIDEWINDOW;
                Marshal.StructureToPtr(pos, lParam, true);
            }
        }
        else if (msg == NativeMethods.WM_SYSCOMMAND)
        {
            int command = (int)wParam & 0xFFF0;
            if (command == NativeMethods.SC_MINIMIZE)
            {
                handled = true;
                return IntPtr.Zero;
            }
        }

        return IntPtr.Zero;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // Fallback para movimentação fluida quando a janela estiver como WS_CHILD de WorkerW
                _isDragging = true;
                _dragStartPoint = PointToScreen(e.GetPosition(this));
                _initialLeft = Left;
                _initialTop = Top;
                (sender as UIElement)?.CaptureMouse();
            }
        }
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            Point currentScreen = PointToScreen(e.GetPosition(this));
            double deltaX = currentScreen.X - _dragStartPoint.X;
            double deltaY = currentScreen.Y - _dragStartPoint.Y;

            Left = _initialLeft + deltaX;
            Top = _initialTop + deltaY;
        }
    }

    private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            (sender as UIElement)?.ReleaseMouseCapture();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
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
