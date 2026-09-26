using System.IO;
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

    private void DesktopItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            if (sender is FrameworkElement fe && fe.DataContext is DesktopItemViewModel item)
            {
                _launcherService.Launch(item.FilePath);
                e.Handled = true;
                return;
            }
        }

        if (e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed)
        {
            _itemDragStartPos = e.GetPosition(this);
            _draggedItemCandidate = (sender as FrameworkElement)?.DataContext as DesktopItemViewModel;
        }
    }

    private void DesktopItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isItemDragging || _draggedItemCandidate == null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point currentPos = e.GetPosition(this);
        Vector diff = _itemDragStartPos - currentPos;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            _isItemDragging = true;
            var item = _draggedItemCandidate;
            var payload = new QuadraDragPayload(_viewModel.Id, item);
            var dataObject = new DataObject();
            dataObject.SetData(typeof(QuadraDragPayload), payload);

            if (File.Exists(item.FilePath) || Directory.Exists(item.FilePath))
            {
                dataObject.SetData(DataFormats.FileDrop, new[] { item.FilePath });
            }

            var dragSource = sender as DependencyObject ?? this;
            DragDropEffects result = DragDrop.DoDragDrop(dragSource, dataObject, DragDropEffects.Move | DragDropEffects.Copy);

            if (payload.WasHandledAsMove)
            {
                _viewModel.RemoveItem(payload.Item);
                _coordinator.NotifyQuadraChanged(_viewModel.Model);
            }

            _isItemDragging = false;
            _draggedItemCandidate = null;
        }
    }

    private void DesktopItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _draggedItemCandidate = null;
        _isItemDragging = false;
    }

    private void Quadra_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(QuadraDragPayload)) || e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            bool isControlPressed = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;
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
        if (e.Data.GetDataPresent(typeof(QuadraDragPayload)))
        {
            var payload = e.Data.GetData(typeof(QuadraDragPayload)) as QuadraDragPayload;
            if (payload != null)
            {
                if (payload.SourceQuadraId == _viewModel.Id)
                {
                    // Mesmo container, sem movimentação necessária
                    e.Handled = true;
                    return;
                }

                bool isCopy = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;
                if (isCopy)
                {
                    // Duplicação física no disco (Ctrl + Drag)
                    string duplicatedPath = DuplicateFileOnDisk(payload.Item.FilePath);
                    _viewModel.AddItem(duplicatedPath);
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    // Mover item entre Quadras
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
                    _viewModel.AddItem(file);
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

                File.Copy(path, targetPath);
                return targetPath;
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

                CopyDirectoryRecursively(path, targetPath);
                return targetPath;
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
