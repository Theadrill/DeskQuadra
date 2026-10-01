using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DeskQuadra.Application.Services;
using DeskQuadra.Application.Snap;
using DeskQuadra.Core;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Core.FileDeletion;
using DeskQuadra.Core.FileDuplication;
using DeskQuadra.Core.Models;
using DeskQuadra.Core.ThirdParty;
using DeskQuadra.Infrastructure.WindowsShell.Native;
using DeskQuadra.Infrastructure.WindowsShell.Shell;
using DeskQuadra.UI.Wpf.Models;
using DeskQuadra.UI.Wpf.Properties;
using DeskQuadra.UI.Wpf.Services;
using DeskQuadra.UI.Wpf.Theme;
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
    private readonly IFileDeletionService _deletionService;
    private readonly IThirdPartyMenuService _thirdPartyMenuService;

    private bool _isInitializing = true;
    private bool _isDragging;
    private Point _dragStartScreenPoint;
    private double _initialLeft;
    private double _initialTop;

    // Estado do Drag and Drop de itens internos
    private Point _itemDragStartPos;
    private DesktopItemViewModel? _draggedItemCandidate;
    private bool _isItemDragging;

    // MOVER-via-touch (Fatia 1, sem overview): armado compartilhado entre Quadras.
    // Destaque REUSE o IsSelected (visual de seleção existente, sem cor/tema novo).
    private static TouchMoveState? s_touchMove;
    // Timeout ~8s: única exceção à regra de timers — dá saída sem gesto (dedo parado, menu fechado).
    private static DispatcherTimer? s_touchMoveTimeoutTimer;
    // ESC com escopo estrito (padrão do menu dual): dono é o HWND da origem, registra ao armar e solta ao desarmar.
    private static IntPtr s_touchMoveEscHwnd = IntPtr.Zero;
    private const int TouchMoveEscHotkeyId = 0xD9A1;

    // REDIMENSIONAR-via-touch (Fatia 1, só gesto + modo armado): Quadra dona + título guardado.
    // Fatia 2 engrossa as pegas; o aviso segue no título trocado pela instrução.
    private static TouchResizeState? s_touchResize;
    // REDIMENSIONAR-via-touch (Fatia 2, bordas grossas): valores armados vs normais do XAML.
    // Normal: bordas 6, cantos 10x10, container 1 (Quadra.BorderThickness). Armado: dedo agarra fácil.
    private const double TouchResizeArmedEdgeThickness = 48.0;
    private const double TouchResizeArmedCornerSize = 56.0;
    private static readonly Thickness TouchResizeArmedBorderThickness = new(4);
    // Foto fiel p/ restaurar o exato original ao desarmar (mesmo molde do título da fatia 1).
    private double? _savedResizeEdgeHeight;
    private double? _savedResizeEdgeWidth;
    private double? _savedResizeCornerSize;
    private Thickness? _savedContainerBorderThickness;
    // Botões somem no armado (sem toque acidental); foto p/ voltar exato.
    private Visibility? _savedCollapseButtonVisibility;
    private Visibility? _savedAddFileButtonVisibility;
    private Visibility? _savedCloseButtonVisibility;
    // Timeout ~8s: dedo parado/menu fechado sai sem gesto (mesmo molde do MOVER).
    private static DispatcherTimer? s_touchResizeTimeoutTimer;
    // ESC com escopo estrito (mesmo padrão do MOVER/dual): id próprio sem colidir com 0xD9A1/0xD9AD.
    private static IntPtr s_touchResizeEscHwnd = IntPtr.Zero;
    private const int TouchResizeEscHotkeyId = 0xD9A2;

    // MOVER-via-touch (Fatia 2, overview estilo launcher): foto fiel p/ restaurar sem persistir sujeira.
    // Flag de congelamento: enquanto != null, OnPositionOrSizeChanged retorna cedo + bindings TwoWay
    // ficam desligados — o save com debounce NÃO pode gravar o layout miniatura como real
    // (sem esse congelamento a fatia está errada).
    private static List<QuadraOverviewFrame>? s_overviewSnapshot;
    // Faixa inferior do overview (topmost, sem Win32 novo).
    private static TouchMoveBannerWindow? s_overviewBanner;

    // Densidade Aparência (Fatia 2): último modo aplicado + preferência global para WM_DISPLAYCHANGE.
    // Sem hook/timer novo: leitura sob demanda (App) + reavaliação trivial no WndProc existente.
    private bool _isTouchDensity;
    internal static DensityPreference CurrentDensityPreference = DensityPreference.Auto;
    internal static Func<bool>? HasTouchHardwareProvider;

    // Modo roll-up (recolhimento no local, seção 10 do BRAINSTORMING)
    private double? _expandedHeight; // altura guardada antes de recolher
    private double _savedMinHeight = 140; // MinHeight original para restaurar ao expandir
    private bool _isApplyingCollapse; // suprime sincronização de altura na troca programática
    private bool _springExpanded; // expansão temporária do spring-loaded (ainda não persistida)
    private DispatcherTimer? _springTimer; // timer único one-shot (~400ms) do spring-loaded

    // Hover-peek do roll-up (seção 10 do BRAINSTORMING): expansão temporária por hover com debounce de saída
    private bool _peekExpanded; // expansão temporária do peek (ainda não persistida)
    private DispatcherTimer? _peekEnterTimer; // one-shot de entrada (~350ms) antes de expandir
    private DispatcherTimer? _peekExitTimer; // debounce de saída (~350ms) antes de recolher

    // Recolhimento pós-drop do spring-loaded: Quadra recolhida que expandiu temporariamente e recebeu drop recolhe sozinha após ~3s de ociosidade
    private DispatcherTimer? _postDropCollapseTimer; // one-shot (~3000ms) armado após o drop
    private bool _awaitingPostDropCollapse; // Quadra expandida aguardando recolher sozinha após o drop


    public QuadraWindow(
        QuadraViewModel viewModel,
        IWindowAnchorService anchorService,
        ISnapEngine snapEngine,
        ILayoutCoordinator coordinator,
        IFileLauncherService launcherService,
        IFileDeletionService deletionService,
        IThirdPartyMenuService thirdPartyMenuService)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        _anchorService = anchorService;
        _snapEngine = snapEngine;
        _coordinator = coordinator;
        _launcherService = launcherService;
        _deletionService = deletionService;
        _thirdPartyMenuService = thirdPartyMenuService;

        // Configura posicionamento manual estrito antes da inicialização visual
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = viewModel.Left;
        Top = viewModel.Top;
        Width = viewModel.Width;
        Height = viewModel.Height;

        InitializeComponent();

        Loaded += (s, e) =>
        {
            _isInitializing = false;
            RefreshLockState();
            // Estado inicial: se o modelo já veio recolhido, abre recolhido (sem persistir)
            if (_viewModel.IsCollapsed)
            {
                ApplyCollapsed(true, persist: false);
            }
            else
            {
                RefreshCollapsedState();
            }
        };
        LocationChanged += OnPositionOrSizeChanged;
        SizeChanged += OnPositionOrSizeChanged;

        ItemsScrollViewer.PreviewMouseLeftButtonDown += ItemsScrollViewer_TouchScrollDown;
        ItemsScrollViewer.PreviewMouseMove += ItemsScrollViewer_TouchScrollMove;
        ItemsScrollViewer.PreviewMouseLeftButtonUp += ItemsScrollViewer_TouchScrollUp;

        // MOVER-via-touch: tap de destino em qualquer ponto da Quadra (vazio, barra ou ícone).
        // Tunelamento passa aqui antes dos filhos; fora do armado retorna imediato (mouse intacto).
        PreviewMouseLeftButtonDown += Quadra_TouchMoveDestinationDown;

        // Hover-peek do roll-up (seção 10): entrada arma expansão temporária, saída recolhe com debounce
        MouseEnter += Quadra_PeekMouseEnter;
        MouseLeave += Quadra_PeekMouseLeave;

        GlobalItemSelected += OnGlobalItemSelected;
        GlobalCloseMenusRequested += OnGlobalCloseMenusRequested;

        // Rastreamento robusto e instantâneo de Toque físico na janela.
        // Alimenta o estado global do InputDeviceDetector (dono único da decisão);
        // regras de transição idênticas às do campo local anterior.
        PreviewTouchDown += (s, e) => InputDeviceDetector.SetTouchActive(true);
        PreviewTouchUp += (s, e) => InputDeviceDetector.SetTouchActive(false);
        PreviewMouseDown += (s, e) =>
        {
            if (e.StylusDevice == null && !NativeMethods.IsCurrentMessageFromTouch())
            {
                InputDeviceDetector.SetTouchActive(false);
            }
        };
        PreviewMouseMove += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Released && e.RightButton == MouseButtonState.Released)
            {
                InputDeviceDetector.SetTouchActive(false);
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

    public Guid QuadraId => _viewModel.Id;

    // Atualiza o cadeado e a trava a partir do modelo (usado pelo tray global após SetAllLocked)
    public void RefreshLockState()
    {
        // Cadeado discreto na barra de título quando travada
        if (LockIndicator != null)
        {
            LockIndicator.Visibility = _viewModel.IsLocked ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // Atualiza o chevron e o tooltip a partir do modelo (padrão refresh como o RefreshLockState)
    public void RefreshCollapsedState()
    {
        if (CollapseGlyph != null)
        {
            CollapseGlyph.Text = _viewModel.IsCollapsed ? "˅" : "˄";
        }
        if (CollapseButton != null)
        {
            CollapseButton.ToolTip = _viewModel.IsCollapsed ? Strings.Quadra_ExpandTooltip : Strings.Quadra_CollapseTooltip;
        }
    }

    // Densidade (Fatia 2 + fatia vertical): barra 28/42 + botão 24/44 + ícone 38/48
    // + título 12/15 DENTRO da célula fixa 78x96 (snap D9 intacto) + respiro do item.
    // Via recursos existentes (Quadra.TitleButton.Size + chaves Quadra.Density.* e
    // Quadra.Item.Padding/Margin); não toca em roll-up/spring/peek/lock/drag/scroll
    // (só dimensões). Seguro com janela recolhida.
    public void ApplyDensity(bool isTouch)
    {
        _isTouchDensity = isTouch;
        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        app.Resources["Quadra.Density.TitleBar.Height"] = new GridLength(DensityResolver.TitleBarHeight(isTouch));
        app.Resources["Quadra.TitleButton.Size"] = DensityResolver.TitleButtonSize(isTouch);
        app.Resources["Quadra.Density.Title.Padding"] = isTouch ? new Thickness(12, 0, 8, 0) : new Thickness(8, 0, 4, 0);
        app.Resources["Quadra.Density.TitleButton.Margin"] = isTouch ? new Thickness(0, 0, 6, 0) : new Thickness(0, 0, 2, 0);

        // Fatia vertical: ícone e respiro seguem a densidade sem mexer na célula do grid.
        // Fonte da verdade: tokens do tema (Default.xaml); fallback = valores atuais (sem mudança de pixel).
        // Touch lê a variante .Touch (nunca sobrescrita); Normal lê o base do dicionário mesclado
        // (o slot app.Resources["Quadra.Item.*"] é sombreado pelo Touch, então TryFindResource
        // direto retornaria o override no toggle Touch->Normal).
        app.Resources["Quadra.Density.Icon.Size"] = DensityResolver.IconSize(isTouch);
        // Título Touch +25% via override do token (XAML já usa DynamicResource; Normal relê o base 12).
        app.Resources["Quadra.Title.FontSize"] = DensityResolver.TitleFontSize(isTouch);
        app.Resources["Quadra.Item.Padding"] = isTouch
            ? ThemeResolver.Get("Quadra.Item.Padding.Touch", new Thickness(8, 8, 8, 4))
            : GetThemeBase("Quadra.Item.Padding", new Thickness(4, 4, 4, 2));
        app.Resources["Quadra.Item.Margin"] = isTouch
            ? ThemeResolver.Get("Quadra.Item.Margin.Touch", new Thickness(4))
            : GetThemeBase("Quadra.Item.Margin", new Thickness(2));

        // Recolhida: mantém a altura recolhida coerente com a nova barra (expandida segue no modelo).
        if (_viewModel.IsCollapsed && !_isApplyingCollapse)
        {
            double bar = TitleBarBorder?.ActualHeight > 0
                ? TitleBarBorder.ActualHeight
                : DensityResolver.TitleBarHeight(isTouch);
            _isApplyingCollapse = true;
            try
            {
                Height = bar + 22; // 20 das margens + 2 das bordas (mesma fórmula do ApplyCollapsed)
            }
            finally
            {
                _isApplyingCollapse = false;
            }
        }
    }

    // Base do tema sem o sombreamento do ApplyDensity: procura o token no dicionário
    // mesclado (Default.xaml) antes do TryFindResource, com fallback idêntico ao literal anterior.
    private static T GetThemeBase<T>(string key, T fallback)
    {
        var app = System.Windows.Application.Current;
        if (app?.Resources?.MergedDictionaries != null)
        {
            foreach (var dict in app.Resources.MergedDictionaries)
            {
                if (dict.Contains(key) && dict[key] is T hit)
                {
                    return hit;
                }
            }
        }
        return ThemeResolver.Get(key, fallback);
    }

    // Alterna recolhido/expandido (duplo-clique ou chevron; o lock NÃO bloqueia)
    private void ToggleCollapsed()
    {
        // Cancela os transitórios na mesma ordem anterior (spring, peek, pós-drop);
        // os flags temporários são limpos em seguida sem mudar a semântica (campos disjuntos).
        CancelTransientTimers();
        _springExpanded = false;
        _peekExpanded = false;
        ApplyCollapsed(!_viewModel.IsCollapsed, persist: true);
    }

    // Hover-peek (seção 10): só com Quadra recolhida e mouse real; toque promovido nunca arma
    private void Quadra_PeekMouseEnter(object sender, MouseEventArgs e)
    {
        // Re-entrar cancela o debounce de saída (não fecha na cara do usuário)
        CancelPeekExitTimer();
        if (!_viewModel.IsCollapsed || _peekExpanded)
        {
            return;
        }
        // Caminho touch segue só com chevron/duplo-toque (dedo gera mouse promovido no Deck)
        if (InputDeviceDetector.IsTouchInteraction(e))
        {
            return;
        }
        // Peek e spring-loaded nunca brigam: com spring ou drag em curso, o peek não arma
        if (_springExpanded || _springTimer != null || _isItemDragging || _isDragging || _peekEnterTimer != null)
        {
            return;
        }
        OneShotTimer.Arm(ref _peekEnterTimer, 350, PeekEnterTimer_Tick);
    }

    private void PeekEnterTimer_Tick(object? sender, EventArgs e)
    {
        CancelPeekEnterTimer();
        // Revalida no tick: só expande se ainda recolhido e sem spring/drag em curso
        if (!_viewModel.IsCollapsed || _peekExpanded)
        {
            return;
        }
        if (_springExpanded || _springTimer != null || _isItemDragging || _isDragging)
        {
            return;
        }
        _peekExpanded = true;
        ApplyCollapsed(false, persist: false);
    }

    // Debounce de saída (~350ms): se o mouse escapar brevemente, não recolhe abruptamente
    private void Quadra_PeekMouseLeave(object sender, MouseEventArgs e)
    {
        // Saiu antes do atraso de entrada: só desarma
        CancelPeekEnterTimer();
        if (!_peekExpanded)
        {
            return;
        }
        // Com spring ou drag em curso, o peek não recolhe
        if (_springExpanded || _springTimer != null || _isItemDragging || _isDragging || _peekExitTimer != null)
        {
            return;
        }
        OneShotTimer.Arm(ref _peekExitTimer, 350, PeekExitTimer_Tick);
    }

    private void PeekExitTimer_Tick(object? sender, EventArgs e)
    {
        CancelPeekExitTimer();
        // O estado persistido continua recolhido (persist: false); só recolhe sem drag em curso
        if (_peekExpanded && !_springExpanded && _springTimer == null && !_isItemDragging && !_isDragging)
        {
            _peekExpanded = false;
            ApplyCollapsed(true, persist: false);
        }
    }

    private void CancelPeekEnterTimer()
    {
        OneShotTimer.Cancel(ref _peekEnterTimer, PeekEnterTimer_Tick);
    }

    private void CancelPeekExitTimer()
    {
        OneShotTimer.Cancel(ref _peekExitTimer, PeekExitTimer_Tick);
    }

    private void CancelPeekTimers()
    {
        CancelPeekEnterTimer();
        CancelPeekExitTimer();
    }

    // Cancela os timers transitórios do roll-up/peek/spring (mesma ordem dos pontos de chamada).
    // Não inclui a inércia do touch (_touchInertiaTimer, recorrente) — só os 4 one-shots via OneShotTimer.
    private void CancelTransientTimers()
    {
        CancelSpringTimer();
        CancelPeekTimers();
        CancelPostDropCollapseTimer();
    }

    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleCollapsed();
    }

    private void ApplyCollapsed(bool collapsed, bool persist)
    {
        _isApplyingCollapse = true;
        try
        {
            if (collapsed)
            {
                // Guarda a altura atual para restaurar ao expandir
                _expandedHeight = Height;
                _savedMinHeight = MinHeight;
                _viewModel.IsCollapsed = true;

                // Colapsa a linha de conteúdo e contorna o MinHeight="140" baixando-o temporariamente
                ContentRow.Height = new GridLength(0);
                ContentArea.Visibility = Visibility.Collapsed;
                SetResizeThumbsVisibility(Visibility.Collapsed);

                double collapsedHeight = (TitleBarBorder.ActualHeight > 0 ? TitleBarBorder.ActualHeight : DensityResolver.TitleBarHeight(_isTouchDensity)) + 22; // 20 das margens + 2 das bordas
                MinHeight = 0;
                Height = collapsedHeight;

                // Reforça a altura expandida no modelo (o binding TwoWay empurra a altura recolhida para o VM)
                double expanded = _expandedHeight ?? _viewModel.Model.Height;
                _viewModel.Model.Height = expanded;
                Dispatcher.BeginInvoke(() => { if (_viewModel.IsCollapsed && _expandedHeight.HasValue) _viewModel.Model.Height = _expandedHeight.Value; });
            }
            else
            {
                // Expande: restaura linha, thumbs, MinHeight e a altura guardada (ou a do modelo)
                ContentRow.Height = new GridLength(1, GridUnitType.Star);
                ContentArea.Visibility = Visibility.Visible;
                SetResizeThumbsVisibility(Visibility.Visible);
                MinHeight = _savedMinHeight;

                double restored = _expandedHeight ?? _viewModel.Model.Height;
                if (double.IsNaN(restored) || restored < MinHeight)
                {
                    restored = MinHeight;
                }
                Height = restored;
                _expandedHeight = null;
                _viewModel.IsCollapsed = false;
            }

            RefreshCollapsedState();
        }
        finally
        {
            _isApplyingCollapse = false;
        }

        if (persist)
        {
            _coordinator.NotifyQuadraChanged(_viewModel.Model);
        }
    }

    private void SetResizeThumbsVisibility(Visibility visibility)
    {
        var thumbs = new[]
        {
            ResizeThumbTop,
            ResizeThumbBottom,
            ResizeThumbLeft,
            ResizeThumbRight,
            ResizeThumbTopLeft,
            ResizeThumbTopRight,
            ResizeThumbBottomLeft,
            ResizeThumbBottomRight,
        };
        foreach (var thumb in thumbs)
        {
            thumb.Visibility = visibility;
        }
    }

    // Sincroniza a grade de itens com o modelo (usado quando outra Quadra move itens para cá)
    public void RefreshItemsFromModel()
    {
        _viewModel.RefreshItems();
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
        var (dpiX, dpiY) = DpiHelper.GetScale(this);
        int pxX = DpiHelper.DipToPhysical(_viewModel.Left, dpiX);
        int pxY = DpiHelper.DipToPhysical(_viewModel.Top, dpiY);
        int pxW = DpiHelper.DipToPhysical(_viewModel.Width, dpiX);
        int pxH = DpiHelper.DipToPhysical(_viewModel.Height, dpiY);

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
        // Reavaliação trivial de Auto ao trocar display (sem hook/timer novo; só se preferência for Auto).
        if (msg == NativeMethods.WM_DISPLAYCHANGE && CurrentDensityPreference == DensityPreference.Auto && HasTouchHardwareProvider != null)
        {
            try
            {
                ApplyDensity(HasTouchHardwareProvider());
            }
            catch
            {
                // Best-effort: nunca derruba o hook da janela.
            }
            return IntPtr.Zero;
        }

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
            int command = (int)wParam & NativeMethods.SC_MASK;
            if (command == NativeMethods.SC_MINIMIZE)
            {
                // Rejeita qualquer comando direto de minimização
                handled = true;
                return IntPtr.Zero;
            }
        }
        else if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == TouchMoveEscHotkeyId && s_touchMove != null)
        {
            // ESC com mover armado: cancela sem mover e republica o ESC (padrão par-gesto do menu dual).
            // Sem hook novo: usa o HwndSource já instalado (mesmo WndProc); escopo estrito via hotkey da origem.
            CancelTouchMove();
            try { NativeMethods.RepublishEscapeKey(); } catch { /* silencioso */ }
            handled = true;
            return IntPtr.Zero;
        }
        else if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == TouchResizeEscHotkeyId && s_touchResize != null)
        {
            // ESC com resize armado: restaura o título e republica o ESC (mesmo molde do MOVER).
            CancelTouchResize();
            try { NativeMethods.RepublishEscapeKey(); } catch { /* silencioso */ }
            handled = true;
            return IntPtr.Zero;
        }

        return IntPtr.Zero;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Em edição de título: clique não arrasta nem recolhe (só o TextBox recebe).
        if (_isRenaming)
        {
            return;
        }
        // Quadra travada: sem arraste pela barra de título (scroll, cliques e menu seguem normais)
        // (o lock NÃO bloqueia recolher/expandir: o chevron e o duplo-clique continuam ativos)
        if (_viewModel.IsLocked)
        {
            return;
        }

        // Duplo-clique na barra alterna recolhido/expandido
        // (Border não expõe evento de double-click, então detecta via ClickCount)
        if (e.ClickCount == 2)
        {
            // Interrompe um possível arraste iniciado no primeiro clique
            _isDragging = false;
            (sender as UIElement)?.ReleaseMouseCapture();
            // Peek-expandido: o 1º clique do duplo-clique não recolhe (IsCollapsed visual já é
            // false); converte direto em expansão permanente sem passar pelo recolhe.
            if (_peekExpanded)
            {
                CancelPeekTimers();
                _peekExpanded = false;
                _coordinator.NotifyQuadraChanged(_viewModel.Model);
                e.Handled = true;
                return;
            }
            ToggleCollapsed();
            e.Handled = true;
            return;
        }

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

            var (dpiX, dpiY) = DpiHelper.GetScale(this);
            double deltaX = DpiHelper.PhysicalToDip(pt.X - _dragStartScreenPoint.X, dpiX);
            double deltaY = DpiHelper.PhysicalToDip(pt.Y - _dragStartScreenPoint.Y, dpiY);

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
                var (waLeft, waTop, waWidth, waHeight) = DpiHelper.MapPhysicalToDip(dpiX, dpiY, monitorInfo.rcWork.Left, monitorInfo.rcWork.Top, monitorInfo.rcWork.Right - monitorInfo.rcWork.Left, monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top);
                workAreaRect = new Rect2D(waLeft, waTop, waWidth, waHeight);
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
        // Diálogo próprio: Esconder mantém o modelo (vai ao tray), Excluir remove, Cancelar aborta
        var choice = ShowCloseChoiceDialog();
        if (choice == CloseChoice.Hide)
        {
            _coordinator.HideQuadra(_viewModel.Id);
            Close();
        }
        else if (choice == CloseChoice.Delete)
        {
            DeleteQuadraWithRules();
        }
    }

    private void DeleteQuadraWithRules()
    {
        var model = _viewModel.Model;
        if (!model.IsDefault)
        {
            // Quadra comum: move os itens para a TUDO antes de remover
            // (só vinculação em memória + persistência do layout; arquivos intactos no disco)
            var tudo = _coordinator.ActiveQuadras.FirstOrDefault(q => q.IsDefault && q.Id != model.Id);
            if (tudo != null && model.Items.Count > 0)
            {
                tudo.Items.AddRange(model.Items);
                _coordinator.NotifyQuadraChanged(tudo);
                RefreshQuadraWindow(tudo.Id);
            }

            _coordinator.RemoveQuadra(model.Id);
            Close();
            return;
        }

        // Quadra padrão: exige confirmação extra antes de remover
        bool confirmed = ShowConfirmDialog(
            Strings.Dialog_DeleteDefaultTitle,
            Strings.Dialog_DeleteDefaultMessage);
        if (confirmed)
        {
            _coordinator.RemoveQuadra(model.Id);
            Close();
        }
    }

    // Atualiza a grade da janela destino que recebeu os itens (ex: TUDO após Excluir)
    private void RefreshQuadraWindow(Guid id)
    {
        foreach (Window window in System.Windows.Application.Current.Windows)
        {
            if (window is QuadraWindow other && other != this && other.QuadraId == id)
            {
                other.RefreshItemsFromModel();
            }
        }
    }

    private enum CloseChoice { Cancel, Hide, Delete }

    private CloseChoice ShowCloseChoiceDialog()
    {
        int index = DarkDialog.ShowOptions(
            Strings.Dialog_CloseTitle,
            string.Format(Strings.Dialog_CloseMessageFormat, _viewModel.Title),
            this,
            330,
            (Strings.Dialog_Hide, true),
            (Strings.Dialog_Delete, false),
            (Strings.Dialog_Cancel, false));
        return index switch
        {
            0 => CloseChoice.Hide,
            1 => CloseChoice.Delete,
            _ => CloseChoice.Cancel,
        };
    }

    private bool ShowConfirmDialog(string title, string message)
    {
        return DarkDialog.Show(
            title,
            message,
            Strings.Dialog_Delete,
            Strings.Dialog_Cancel,
            owner: this);
    }

    private void OnPositionOrSizeChanged(object? sender, EventArgs e)
    {
        if (_isInitializing || double.IsNaN(Left) || double.IsNaN(Top) || double.IsNaN(Width) || double.IsNaN(Height))
        {
            return;
        }

        // Overview ativo: congela a persistência — minis não sincronizam nem notificam
        // (o debounce gravaria o layout miniatura como real).
        if (s_overviewSnapshot != null)
        {
            return;
        }

        // Troca programática de recolher/expandir: não sincroniza nem persiste aqui (ApplyCollapsed cuida disso)
        if (_isApplyingCollapse)
        {
            return;
        }

        bool changed = false;
        if (Math.Abs(_viewModel.Left - Left) > 0.001)
        {
            _viewModel.Left = Left;
            changed = true;
        }
        if (Math.Abs(_viewModel.Top - Top) > 0.001)
        {
            _viewModel.Top = Top;
            changed = true;
        }
        if (Math.Abs(_viewModel.Width - Width) > 0.001)
        {
            _viewModel.Width = Width;
            changed = true;
        }
        // Recolhido: preserva a altura expandida no modelo (posição continua persistindo normalmente)
        if (!_viewModel.IsCollapsed && Math.Abs(_viewModel.Height - Height) > 0.001)
        {
            _viewModel.Height = Height;
            changed = true;
        }

        if (changed)
        {
            _coordinator.NotifyQuadraChanged(_viewModel.Model);
        }
    }

    private void AddFileButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = Strings.Dialog_AddFilesTitle
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

    private void SortByName_Click(object sender, RoutedEventArgs e) => ApplySortAndPersist(SortMode.Name);

    private void SortByType_Click(object sender, RoutedEventArgs e) => ApplySortAndPersist(SortMode.Type);

    private void SortByDate_Click(object sender, RoutedEventArgs e) => ApplySortAndPersist(SortMode.Date);

    private void SortByManual_Click(object sender, RoutedEventArgs e) => ApplySortAndPersist(SortMode.Manual);

    private void ApplySortAndPersist(SortMode m)
    {
        _viewModel.SortItems(m);
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

    // Botão direito no vazio da Quadra abre o MESMO menu da barra (REUSE: mesma instância,
    // sem duplicar markup). Preview (tunelamento) checa o alvo antes do menu do item.
    private void ItemsEmpty_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Em cima de item: não rouba — deixa o menu do item abrir normalmente.
        DependencyObject? dep = e.OriginalSource as DependencyObject;
        while (dep != null && dep != this)
        {
            if (dep is FrameworkElement fe && fe.DataContext is DesktopItemViewModel)
            {
                return;
            }
            dep = VisualTreeHelper.GetParent(dep);
        }

        var menu = TitleBarBorder?.ContextMenu;
        if (menu == null)
        {
            return;
        }

        // Reusa a instância da barra (sem duplicar markup). Abertura programática
        // (IsOpen = true) não dispara o ContextMenuOpening do dono — só Opened/Closed
        // do menu. Sem a sincronização explícita, os itens caem no estilo claro padrão
        // (submenu "Ordenar por" branco) e na densidade de mouse mesmo no modo touch.
        // Mesmo padrão do TitleBar_ContextMenuOpening: cadeado + densidade por gesto/preferência.
        LockQuadraMenuItem.IsChecked = _viewModel.IsLocked;
        bool isTouch = ResolveEffectiveIsTouch(e);
        // T2 terceiros no vazio (PIDL da pasta Desktop) antes da densidade (§2).
        PopulateThirdPartySection(menu, GetEmptySpaceFolderPath());
        ApplyMenuDensity(menu, isTouch: isTouch);
        // Visibilidade só por gesto real (sem preferência): mouse nunca vê Redimensionar.
        bool touchGesture = InputDeviceDetector.IsTouchInteraction(e);
        ResizeQuadraMenuItem.Visibility = (touchGesture && CanTransform()) ? Visibility.Visible : Visibility.Collapsed;
        menu.PlacementTarget = ItemsScrollViewer;
        menu.IsOpen = true;
        e.Handled = true;
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
        if (e.LeftButton != MouseButtonState.Pressed || !InputDeviceDetector.IsPromotedTouch(e) || IsOnScrollbar(e))
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

        if (!InputDeviceDetector.IsPromotedTouch(e))
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
        bool isTouch = InputDeviceDetector.IsTouchInteraction(e);

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
            // T3 terceiros: rótulos reais habilitados do próprio item
            // (.lnk = o próprio link). Antes da densidade (§2).
            if (fe.DataContext is DesktopItemViewModel item)
            {
                PopulateThirdPartySection(fe.ContextMenu, item.FilePath);
            }
            // Toque usa itens de 46px; mouse segue compacto (~26px).
            // Híbrido preservado: toque real abre menu grande mesmo em modo Normal
            // (gesto OU preferência, mesmo padrão do App.ResolveEffectiveIsTouch).
            bool isTouch = ResolveEffectiveIsTouch(e);
            ApplyMenuDensity(fe.ContextMenu, isTouch: isTouch);
            // MOVER só por gesto real (sem preferência): sem gesto no args, vale o estado
            // de gesto (mensagem atual + flag, já resetada no mouse real).
            bool touchGesture = InputDeviceDetector.IsEventFromTouch(e);
            var moveItem = fe.ContextMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Name == "ItemMenuMoveItem");
            if (moveItem != null)
            {
                moveItem.Visibility = touchGesture ? Visibility.Visible : Visibility.Collapsed;
            }
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

    // Tag T3: marca os MenuItem inseridos pelo PopulateThirdPartySection (topo e
    // cascata) e carrega a alça de invoke (folha) ou null (cascata = só submenu).
    // Permite limpeza idempotente sem tocar no placeholder T1.
    private sealed class ThirdPartyMenuTag
    {
        public ThirdPartyInvokeHandle? Handle;
    }

    // T3 terceiros (habilita + invoca): troca o placeholder T1 pelos rótulos
    // reais do Shell, HABILITADOS (folha invoca no clique; cascata abre o
    // submenu). Chamar ANTES do ApplyMenuDensity (itens novos sem Style
    // explícito herdam a densidade na abertura, §2). Falha/vazio = mostra o
    // placeholder T1 (silencioso, padrão do projeto). Sem timers.
    // Idempotente: o placeholder NUNCA é removido (só Collapsed/Visible) e os
    // inseridos anteriormente são removidos no início (a instância do ContextMenu
    // persiste entre aberturas — barra/vazio compartilham UMA).
    private void PopulateThirdPartySection(ContextMenu menu, string? targetPath)
    {
        var placeholder = menu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Name == "ThirdPartyPlaceholderItem");
        if (placeholder is null)
        {
            return;
        }

        for (int i = menu.Items.Count - 1; i >= 0; i--)
        {
            if (menu.Items[i] is MenuItem mi && mi.Tag is ThirdPartyMenuTag)
            {
                menu.Items.RemoveAt(i);
            }
        }

        IReadOnlyList<ThirdPartyMenuEntry> entries;
        try
        {
            entries = _thirdPartyMenuService.GetForPath(targetPath);
        }
        catch
        {
            placeholder.Visibility = Visibility.Visible;
            return;
        }

        if (entries.Count == 0)
        {
            placeholder.Visibility = Visibility.Visible;
            return;
        }

        placeholder.Visibility = Visibility.Collapsed;
        int index = menu.Items.IndexOf(placeholder);
        foreach (var entry in entries)
        {
            menu.Items.Insert(++index, CreateThirdPartyItem(entry, targetPath));
        }
    }

    // Item novo SEMPRE sem Style/Height/cor (§2: ApplyStyleRecursively aplica a
    // densidade do gesto na abertura; submenu novo idem). Folha = habilitada +
    // Click invoca e o menu fecha sozinho (padrão WPF); cascata = habilitada
    // (precisa abrir o submenu) sem invoke. Nativo/canônico continua fora
    // (filtro T2 intacto, aplicado antes de chegar aqui).
    private MenuItem CreateThirdPartyItem(ThirdPartyMenuEntry entry, string? targetPath)
    {
        bool isLeaf = entry.Children.Count == 0;
        ThirdPartyInvokeHandle? handle = isLeaf
            ? _thirdPartyMenuService.CreateHandle(targetPath, entry.Verb, entry.CommandOffset)
            : null;
        var item = new MenuItem
        {
            Header = entry.Label,
            IsEnabled = !isLeaf || handle is not null,
            Tag = new ThirdPartyMenuTag { Handle = handle },
        };
        if (isLeaf && handle is not null)
        {
            item.Click += ThirdPartyItem_Click;
        }

        foreach (var child in entry.Children)
        {
            item.Items.Add(CreateThirdPartyItem(child, targetPath));
        }

        return item;
    }

    // Clique em item de terceiro (T5: isolado no ShellHost): o invoke roda FORA
    // da UI e FORA da thread do clique — verbo lento (diálogo modal do handler)
    // não congela o app; o menu já fechou sozinho no clique (padrão WPF).
    // hwnd + ponto capturados AQUI (thread da UI); o ThreadPool só entrega ao
    // serviço (molde do RunPanicSequence do Guardian). Falha = silenciosa.
    // Sem timer novo (só o timeout do host, já existente no protocolo).
    private void ThirdPartyItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not ThirdPartyMenuTag tag || tag.Handle is null)
        {
            return;
        }

        IntPtr hwnd;
        try
        {
            hwnd = new WindowInteropHelper(this).Handle;
        }
        catch
        {
            hwnd = IntPtr.Zero;
        }

        Vanara.PInvoke.POINT? point = null;
        try
        {
            if (NativeMethods.GetCursorPos(out var cursor))
            {
                point = new Vanara.PInvoke.POINT { X = cursor.X, Y = cursor.Y };
            }
        }
        catch
        {
            // Sem ponto: o invoke segue sem PTINVOKE (best-effort).
        }

        ThirdPartyInvokeHandle handle = tag.Handle;
        IThirdPartyMenuService service = _thirdPartyMenuService;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                service.TryInvoke(handle, hwnd, point);
            }
            catch
            {
                // Silencioso, padrão do projeto.
            }
        });
    }

    // T3 terceiros no menu do VAZIO/barra: PIDL da pasta (§1). A Quadra exibe itens
    // do Desktop — o vazio equivale à pasta Desktop do usuário.
    private static string? GetEmptySpaceFolderPath()
    {
        try
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            return Directory.Exists(desktop) ? desktop : null;
        }
        catch
        {
            return null;
        }
    }

    // Preferência global (mesmo padrão do App.ResolveEffectiveIsTouch): Touch/Normal
    // forçam; Auto segue o hardware. Fallback usa o último modo aplicado.
    private bool ResolvePreferenceIsTouch()
    {
        bool hasHardware = HasTouchHardwareProvider?.Invoke() ?? _isTouchDensity;
        return DensityResolver.ResolveIsTouch(CurrentDensityPreference, hasHardware);
    }

    // E16-UI: decisão "touch efetivo" p/ densidade de menu — gesto OU preferência, num ponto só.
    // Bit a bit: p/ MouseButton/MouseEventArgs, IsEventFromTouch ≡ IsTouchInteraction
    // (mesmos 3 termos OR — Stylus-touch, mensagem touch, flag global — só muda a ordem).
    private bool ResolveEffectiveIsTouch(RoutedEventArgs e)
    {
        return InputDeviceDetector.IsEventFromTouch(e) || ResolvePreferenceIsTouch();
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
        bool isTouch = InputDeviceDetector.IsTouchInteraction(e);

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

    // E12-UI: núcleo local do "abrir local" — branch arquivo/diretório aqui dentro, preservando quoting e flags.
    // Se for atalho .lnk, abre o local do destino; destino quebrado = seleciona o próprio .lnk.
    private static void OpenInExplorer(string path)
    {
        string effective = path;
        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            // Best-effort silencioso: null = mantém o comportamento atual (o próprio .lnk).
            string? target = NativeMethods.TryResolveShortcutTarget(path);
            if (!string.IsNullOrWhiteSpace(target) && (File.Exists(target) || Directory.Exists(target)))
            {
                effective = target;
            }
        }

        if (File.Exists(effective))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{effective}\"") { UseShellExecute = true });
        }
        else if (Directory.Exists(effective))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{effective}\"") { UseShellExecute = true });
        }
    }

    private void ItemMenuOpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is DesktopItemViewModel item)
        {
            try
            {
                OpenInExplorer(item.FilePath);
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

    // Excluir-via-ícone: 1º modal escuro (Lixeira direto / Permanentemente com 2ª
    // confirmação / Cancelar nada). Execução no Shell; o item some da Quadra pelo
    // caminho existente (desvincula + persiste, como o Remover).
    private void ItemMenuDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi || mi.DataContext is not DesktopItemViewModel item)
        {
            return;
        }

        int choice = DarkDialog.ShowOptions(
            Strings.Dialog_DeleteItemTitle,
            string.Format(Strings.Dialog_DeleteItemMessageFormat, item.Name),
            this,
            330,
            (Strings.Dialog_RecycleBin, true),
            (Strings.Dialog_DeletePermanently, false),
            (Strings.Dialog_Cancel, false));

        if (ItemDeletionPlan.Resolve(choice, permanentConfirmed: false) == ItemDeleteAction.Recycle)
        {
            ExecuteItemDelete(item, recycle: true);
            return;
        }

        if (choice == 1)
        {
            bool confirmed = DarkDialog.Show(
                Strings.Dialog_DeletePermanentTitle,
                string.Format(Strings.Dialog_DeletePermanentMessageFormat, item.Name),
                Strings.Dialog_Delete,
                Strings.Dialog_Cancel,
                owner: this);
            if (ItemDeletionPlan.Resolve(choice, confirmed) == ItemDeleteAction.Permanent)
            {
                ExecuteItemDelete(item, recycle: false);
            }
        }
        // Cancelar/fechar: sem efeito.
    }

    private void ExecuteItemDelete(DesktopItemViewModel item, bool recycle)
    {
        bool deleted;
        try
        {
            deleted = _deletionService.Delete(item.FilePath, recycle);
        }
        catch
        {
            deleted = false; // Silencioso, padrão do projeto.
        }

        if (!deleted)
        {
            return;
        }

        _viewModel.RemoveItem(item);
        _coordinator.NotifyQuadraChanged(_viewModel.Model);
    }

    // MOVER-via-touch: arma o modo mover (só chega aqui pelo menu touch).
    private void ItemMenuMove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi || mi.DataContext is not DesktopItemViewModel item)
        {
            return;
        }
        // Rearme: novo MOVER substitui o anterior (limpa destaque/timeout/ESC antigos)
        CancelTouchMove();
        s_touchMove = new TouchMoveState(QuadraId, item.Id);
        // Destaque REUSE IsSelected (visual de seleção existente, sem cor/tema novo)
        item.IsSelected = true;
        // Timeout ~8s: one-shot via OneShotTimer (mesmo molde dos transitórios do roll-up)
        OneShotTimer.Arm(ref s_touchMoveTimeoutTimer, TouchMoveState.TimeoutMilliseconds, TouchMoveTimeout_Tick);
        // ESC com escopo estrito (padrão do menu dual): registra ao armar, best-effort silencioso
        try
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero && NativeMethods.RegisterHotKey(hwnd, TouchMoveEscHotkeyId, 0, NativeMethods.VK_ESCAPE))
            {
                s_touchMoveEscHwnd = hwnd;
            }
        }
        catch { /* sem ESC o modo segue cancelando por tap/timeout */ }
        // Fatia 2: ao armar, abre o overview estilo launcher (REUSE TouchMoveState/timeout/ESC/cancelas da fatia 1).
        EnterTouchMoveOverview(this);
    }

    // Tap de destino do MOVER + cancela do RESIZE em outra Quadra (qualquer ponto: vazio, barra ou ícone). Fora do armado = return (mouse intacto).
    private void Quadra_TouchMoveDestinationDown(object sender, MouseButtonEventArgs e)
    {
        var resizeArmed = s_touchResize;
        if (resizeArmed != null && resizeArmed.QuadraId != QuadraId)
        {
            // Tap em outra Quadra desarma o resize sem consumir (B reage normal).
            CancelTouchResize();
        }
        var armed = s_touchMove;
        if (armed == null)
        {
            return;
        }
        // Consome o tap como escolha de destino (sem drag/scroll/launch)
        e.Handled = true;
        CompleteTouchMove(armed);
    }

    // Destino distinto move; mesma Quadra ou expirado cancela sem mover.
    private void CompleteTouchMove(TouchMoveState armed)
    {
        if (armed.IsExpired(DateTime.UtcNow)
            || armed.ResolveDestination(QuadraId) == TouchMoveResolution.CancelSameQuadra)
        {
            CancelTouchMove();
            return;
        }
        // Overview ativo: restaura o layout fotografado FIEL e descongela antes de mover —
        // o move abaixo persiste pelo caminho normal (Notify), sem sujeira de miniatura.
        if (s_overviewSnapshot != null)
        {
            CommitOverviewRestore();
        }
        ExecuteTouchMove(this, armed);
    }

    // Núcleo do move da fatia 1 (extração p/ REUSE no commit do overview):
    // só vinculação + persistência, disco intacto.
    private static void ExecuteTouchMove(QuadraWindow dest, TouchMoveState armed)
    {
        // REUSE núcleo do drop de mouse (Quadra_Drop, ramo Move): só vinculação + persistência, disco intacto
        var source = FindQuadraWindow(armed.SourceQuadraId);
        var item = source?._viewModel.Items.FirstOrDefault(i => i.Id == armed.ItemId);
        if (source == null || source == dest || item == null)
        {
            CancelTouchMove();
            return;
        }
        dest._viewModel.AddItem(item.Model);
        dest._coordinator.NotifyQuadraChanged(dest._viewModel.Model);
        source._viewModel.RemoveItem(item);
        source._coordinator.NotifyQuadraChanged(source._viewModel.Model);
        CancelTouchMove();
    }

    // Tap fora de qualquer Quadra (desktop vazio): cancela sem mover (chamado pelo App no GlobalLeftClick).
    // Ignora o LDown que é o próprio tap do menu (enfileirado antes do arme via BeginInvoke Background).
    public static void CancelTouchMoveFromOutside(DateTime raiseUtc)
    {
        if (s_touchMove != null && s_touchMove.ArmedAtUtc <= raiseUtc)
        {
            CancelTouchMove();
        }
    }

    // Desarma por todos os motivos (ESC, timeout, fora, origem, pós-move): restaura sem mover nada.
    private static void CancelTouchMove()
    {
        // Overview ativo: cancela restaura fiel SEM mover e descongela sem persistir sujeira
        // (todos os cancelas já desembocam aqui: ESC, timeout, tap fora, origem).
        if (s_overviewSnapshot != null)
        {
            CancelOverviewRestore();
        }
        var armed = s_touchMove;
        s_touchMove = null;
        OneShotTimer.Cancel(ref s_touchMoveTimeoutTimer, TouchMoveTimeout_Tick);
        // Solta o ESC do escopo estrito registrado ao armar
        if (s_touchMoveEscHwnd != IntPtr.Zero)
        {
            try { NativeMethods.UnregisterHotKey(s_touchMoveEscHwnd, TouchMoveEscHotkeyId); } catch { /* silencioso */ }
            s_touchMoveEscHwnd = IntPtr.Zero;
        }
        // Apaga o destaque REUSE (o fora-da-Quadra já limpa via DeselectAllGlobally; aqui é belt-and-braces)
        if (armed != null)
        {
            try
            {
                var vm = FindQuadraWindow(armed.SourceQuadraId)?._viewModel.Items.FirstOrDefault(i => i.Id == armed.ItemId);
                if (vm != null)
                {
                    vm.IsSelected = false;
                }
            }
            catch { /* silencioso */ }
        }
    }

    private static void TouchMoveTimeout_Tick(object? sender, EventArgs e)
    {
        CancelTouchMove();
    }

    // --- Overview estilo launcher (fatia 2, 1 tela) ---
    // Ao armar: fotografa TODAS as Quadras abertas (id, bounds DIP, recolhida?, travada?) e
    // rearranja numa grade miniatura na tela do gesto. Escondidas ficam de fora (sem janela aberta).
    // Outros monitores: Quadras de outras telas vêm p/ a grade também (abas multi-monitor ficam p/ depois).
    // Travadas participam como destino normal (lock trava a janela, não o conteúdo).
    // Recolhidas aparecem como mini expandidas (expansão visual sem persistir; a foto guarda a expandida).
    // Mouse intacto: só entra por MOVER touch (ItemMenuMove_Click, invisível no mouse).
    private static void EnterTouchMoveOverview(QuadraWindow source)
    {
        if (s_overviewSnapshot != null)
        {
            return;
        }
        var windows = new List<QuadraWindow>();
        var app = System.Windows.Application.Current;
        if (app != null)
        {
            foreach (Window window in app.Windows)
            {
                if (window is QuadraWindow quadra)
                {
                    windows.Add(quadra);
                }
            }
        }
        if (windows.Count == 0)
        {
            return;
        }
        var frames = new List<QuadraOverviewFrame>(windows.Count);
        foreach (var quadra in windows)
        {
            bool collapsed = quadra._viewModel.IsCollapsed;
            frames.Add(new QuadraOverviewFrame(
                quadra.QuadraId, quadra.Left, quadra.Top, quadra.Width, quadra.Height,
                collapsed ? quadra._viewModel.Model.Height : quadra.Height,
                collapsed, quadra._viewModel.IsLocked,
                quadra.MinWidth, quadra.MinHeight));
        }
        var snapshot = TouchMoveOverviewLayout.Snapshot(frames);
        var (waLeft, waTop, waWidth, waHeight) = GetGestureWorkAreaDip(source);
        // Grade no topo: área útil cheia em waTop; reserva INFERIOR descontada só da altura (faixa vai embaixo).
        var slots = TouchMoveOverviewLayout.ComputeGrid(waLeft, waTop, waWidth, waHeight - TouchMoveOverviewLayout.BannerReserveDip, snapshot)
            .ToDictionary(s => s.Id);
        if (slots.Count == 0)
        {
            return;
        }
        // Congela a persistência ANTES de mexer nas janelas (ver campo: debounce não grava miniatura).
        s_overviewSnapshot = new List<QuadraOverviewFrame>(snapshot);
        try
        {
            foreach (var quadra in windows)
            {
                if (!slots.TryGetValue(quadra.QuadraId, out var slot))
                {
                    continue;
                }
                // Desliga o TwoWay p/ a mini não sujar o modelo (religa fiel na saída).
                BindingOperations.ClearBinding(quadra, Window.LeftProperty);
                BindingOperations.ClearBinding(quadra, Window.TopProperty);
                BindingOperations.ClearBinding(quadra, Window.WidthProperty);
                BindingOperations.ClearBinding(quadra, Window.HeightProperty);
                if (quadra._viewModel.IsCollapsed)
                {
                    quadra.ApplyCollapsed(false, persist: false); // mini expandida, sem persistir
                }
                // Mínimo transitório tocável p/ a mini com título legível no touch.
                quadra.MinWidth = 120;
                quadra.MinHeight = 96;
                quadra.Left = slot.Left;
                quadra.Top = slot.Top;
                quadra.Width = slot.Width;
                quadra.Height = slot.Height;
            }
            // Faixa inferior [instrução][CANCELAR]: tap nela cancela pelo funil existente.
            // Sem Owner de propósito: a origem é filha do WorkerW (SetParent) e o fechamento da
            // faixa já é garantido por todos os cancelas + commit (finally do RestoreOverviewSnapshot).
            s_overviewBanner = new TouchMoveBannerWindow(
                waLeft + TouchMoveOverviewLayout.MarginDip,
                waTop + waHeight - TouchMoveOverviewLayout.MarginDip - TouchMoveOverviewLayout.BannerReserveDip,
                Math.Max(200, waWidth - 2 * TouchMoveOverviewLayout.MarginDip),
                CancelTouchMove);
            s_overviewBanner.Show();
        }
        catch
        {
            // Best-effort: falha ao rearranjar desfaz sem derrubar o armado (fatia 1 segue válida).
            CancelOverviewRestore();
        }
    }

    // Saída por cancela (ESC/timeout/fora/origem): restaura fiel SEM mover, descongela sem persistir sujeira.
    private static void CancelOverviewRestore()
    {
        RestoreOverviewSnapshot();
    }

    // Saída por commit (tap na mini): restaura fiel e descongela; o move persiste pelo caminho normal.
    private static void CommitOverviewRestore()
    {
        RestoreOverviewSnapshot();
    }

    // Restaura fiel posição, tamanho, recolhida e mínimos; religa o TwoWay; fecha a faixa; sem Notify.
    private static void RestoreOverviewSnapshot()
    {
        var snapshot = s_overviewSnapshot;
        s_overviewSnapshot = null;
        try
        {
            if (snapshot != null)
            {
                foreach (var frame in TouchMoveOverviewLayout.RestoreTargets(snapshot))
                {
                    var quadra = FindQuadraWindow(frame.Id);
                    if (quadra == null)
                    {
                        continue;
                    }
                    quadra.MinWidth = frame.MinWidth;
                    quadra.MinHeight = frame.MinHeight;
                    if (frame.IsCollapsed)
                    {
                        // Altura expandida primeiro: o recolher guarda a atual como expandida.
                        quadra.Height = frame.ExpandedHeight;
                        quadra.Width = frame.Width;
                        quadra.Left = frame.Left;
                        quadra.Top = frame.Top;
                        if (!quadra._viewModel.IsCollapsed)
                        {
                            quadra.ApplyCollapsed(true, persist: false);
                        }
                    }
                    else
                    {
                        quadra.Left = frame.Left;
                        quadra.Top = frame.Top;
                        quadra.Width = frame.Width;
                        quadra.Height = frame.Height;
                        if (quadra._viewModel.IsCollapsed)
                        {
                            quadra.ApplyCollapsed(false, persist: false);
                        }
                    }
                    BindQuadraBounds(quadra);
                }
            }
        }
        finally
        {
            // Faixa sempre fecha, mesmo com janela faltando (sem Win32/Notify aqui).
            try { s_overviewBanner?.Close(); } catch { /* silencioso */ }
            s_overviewBanner = null;
        }
    }

    // Religa o TwoWay do XAML (Left/Top/Width/Height) após o overview — mesmos paths e modo.
    private static void BindQuadraBounds(QuadraWindow quadra)
    {
        quadra.SetBinding(Window.LeftProperty, new Binding("Left") { Mode = BindingMode.TwoWay });
        quadra.SetBinding(Window.TopProperty, new Binding("Top") { Mode = BindingMode.TwoWay });
        quadra.SetBinding(Window.WidthProperty, new Binding("Width") { Mode = BindingMode.TwoWay });
        quadra.SetBinding(Window.HeightProperty, new Binding("Height") { Mode = BindingMode.TwoWay });
    }

    // Tela do gesto em DIP (molde do arraste da barra: MonitorFromWindow + MapPhysicalToDip; 1 tela agora).
    private static (double Left, double Top, double Width, double Height) GetGestureWorkAreaDip(QuadraWindow source)
    {
        try
        {
            var (dpiX, dpiY) = DpiHelper.GetScale(source);
            IntPtr hwnd = new WindowInteropHelper(source).Handle;
            IntPtr hMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new NativeMethods.MONITORINFO();
            info.cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>();
            if (hMonitor != IntPtr.Zero && NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                return DpiHelper.MapPhysicalToDip(
                    dpiX, dpiY, info.rcWork.Left, info.rcWork.Top,
                    info.rcWork.Right - info.rcWork.Left, info.rcWork.Bottom - info.rcWork.Top);
            }
        }
        catch { /* silencioso: cai no fallback */ }
        var wa = SystemParameters.WorkArea;
        return (wa.Left, wa.Top, wa.Width, wa.Height);
    }

    // REUSE padrão do RefreshQuadraWindow: localiza a janela aberta pelo Id.
    private static QuadraWindow? FindQuadraWindow(Guid id)
    {
        var app = System.Windows.Application.Current;
        if (app == null)
        {
            return null;
        }
        foreach (Window window in app.Windows)
        {
            if (window is QuadraWindow other && other.QuadraId == id)
            {
                return other;
            }
        }
        return null;
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
            || (NativeMethods.GetKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
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

        // Pós-drop aguardando recolher: novo arrasto pairando reinicia a contagem de ~3s
        if (_awaitingPostDropCollapse)
        {
            ArmPostDropCollapseTimer();
        }

        // Spring-loaded (seção 18): sobre Quadra recolhida, arma timer único de ~400ms (one-shot)
        // Com peek já expandido não há o que o spring fazer (nunca brigam); só segura o debounce de saída
        if (_peekExpanded)
        {
            CancelPeekExitTimer();
            return;
        }
        if (_viewModel.IsCollapsed && _springTimer == null)
        {
            OneShotTimer.Arm(ref _springTimer, 400, SpringTimer_Tick);
        }
    }

    private void SpringTimer_Tick(object? sender, EventArgs e)
    {
        CancelSpringTimer();
        // O arrasto permaneceu sobre a janela até o tick (DragLeave já teria cancelado): expande temporariamente sem persistir
        if (_viewModel.IsCollapsed)
        {
            _springExpanded = true;
            ApplyCollapsed(false, persist: false);
        }
    }

    private void Quadra_DragLeave(object sender, DragEventArgs e)
    {
        CancelSpringTimer();
        // Saiu sem soltar: recolhe de novo sem persistir o estado temporário
        if (_springExpanded)
        {
            _springExpanded = false;
            ApplyCollapsed(true, persist: false);
        }
    }

    private void CancelSpringTimer()
    {
        OneShotTimer.Cancel(ref _springTimer, SpringTimer_Tick);
    }

    // Rearma a contagem pós-drop (~3s): para o timer anterior e começa nova contagem
    private void ArmPostDropCollapseTimer()
    {
        _awaitingPostDropCollapse = true;
        OneShotTimer.Arm(ref _postDropCollapseTimer, 3000, PostDropCollapseTimer_Tick);
    }

    private void CancelPostDropCollapseTimer()
    {
        OneShotTimer.Cancel(ref _postDropCollapseTimer, PostDropCollapseTimer_Tick);
        _awaitingPostDropCollapse = false;
    }

    private void PostDropCollapseTimer_Tick(object? sender, EventArgs e)
    {
        CancelPostDropCollapseTimer();
        // Só recolhe se ocioso: sem drag em curso, sem mouse sobre a janela e sem menu aberto
        if (_isItemDragging || _isDragging || IsMouseOver || IsItemMenuOpen() || (TitleBarBorder?.ContextMenu?.IsOpen == true))
        {
            return;
        }
        if (_viewModel.IsCollapsed)
        {
            return;
        }
        _springExpanded = false;
        ApplyCollapsed(true, persist: true);
    }

    // E11-UI: núcleo local da duplicação com sufixo padrão (resx) — preserva os args dos 2 call sites.
    private static string DuplicateWithStandardSuffix(string path)
    {
        return FileDuplicator.Duplicate(path, Strings.FileCopySuffix, Strings.FileCopySuffixIndexedFormat);
    }

    private void Quadra_Drop(object sender, DragEventArgs e)
    {
        CancelSpringTimer();
        bool wasSpringExpanded = _springExpanded;
        _springExpanded = false;
        // Drop converte o peek em expandido persistido (mesmo padrão do spring: Notify abaixo persiste o estado expandido)
        bool wasPeekExpanded = _peekExpanded;
        _peekExpanded = false;
        CancelPeekTimers();
        // Estava recolhida no persistido e expandiu temporariamente (spring ou peek): volta a recolher sozinha após ~3s
        bool armPostDrop = wasSpringExpanded || wasPeekExpanded || _awaitingPostDropCollapse;

        // Saída única do Drop: só rearma o colapso pós-drop (~3s), sem tocar em Handled/efeitos
        void FinishDrop(bool arm)
        {
            if (arm)
            {
                ArmPostDropCollapseTimer();
            }
        }

        bool isCopy = IsCopyRequested(e);

        if (e.Data.GetDataPresent(typeof(QuadraDragPayload)))
        {
            var payload = e.Data.GetData(typeof(QuadraDragPayload)) as QuadraDragPayload;
            if (payload != null)
            {
                if (payload.SourceQuadraId == _viewModel.Id && !isCopy)
                {
                    // Mesmo container sem Ctrl: nenhuma ação necessária (mas persiste se spring ou peek haviam expandido)
                    if (wasSpringExpanded || wasPeekExpanded)
                    {
                        _coordinator.NotifyQuadraChanged(_viewModel.Model);
                    }
                    e.Handled = true;
                    // Mantém a ordem: Handled antes do rearmar pós-drop
                    FinishDrop(armPostDrop);
                    return;
                }

                if (isCopy)
                {
                    // Duplicação física no disco (Ctrl + Drag), tanto na mesma Quadra quanto entre Quadras
                    string duplicatedPath = DuplicateWithStandardSuffix(payload.Item.FilePath);
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
                // Mantém a ordem: Handled antes do rearmar pós-drop
                FinishDrop(armPostDrop);
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
                    string targetFile = isCopy ? DuplicateWithStandardSuffix(file) : file;
                    _viewModel.AddItem(targetFile);
                }

                _coordinator.NotifyQuadraChanged(_viewModel.Model);
                e.Handled = true;
            }
        }

        // Saída do caminho FileDrop/ignorado: só rearma, sem alterar Handled aqui
        FinishDrop(armPostDrop);
    }

    private void RescanMenu_Click(object sender, RoutedEventArgs e)
    {
        _coordinator.RescanDesktopItems();
        _viewModel.RefreshItems();
    }

    private void NewQuadraMenu_Click(object sender, RoutedEventArgs e)
    {
        _coordinator.CreateNewQuadra(
            title: QuadraNaming.NextTitle(_coordinator.ActiveQuadras.Count, Strings.QuadraDefaultTitleFormat),
            left: Left + 40,
            top: Top + 40,
            width: Width,
            height: Height);
    }

    // Sincroniza o checked do menu individual ao abrir (a UI lê o estado do modelo)
    // + densidade do menu (H3): gesto real OU preferência, como no menu do item.
    private void TitleBar_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        LockQuadraMenuItem.IsChecked = _viewModel.IsLocked;
        if (sender is FrameworkElement fe && fe.ContextMenu != null)
        {
        // T3 terceiros no vazio (PIDL da pasta Desktop) antes da densidade (§2).
            PopulateThirdPartySection(fe.ContextMenu, GetEmptySpaceFolderPath());
            bool isTouch = ResolveEffectiveIsTouch(e);
            ApplyMenuDensity(fe.ContextMenu, isTouch: isTouch);
            // Visibilidade só por gesto real (sem preferência): mouse nunca vê Redimensionar.
            bool touchGesture = InputDeviceDetector.IsEventFromTouch(e);
            ResizeQuadraMenuItem.Visibility = (touchGesture && CanTransform()) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // REDIMENSIONAR-via-touch (Fatia 1): arma o modo na Quadra (só chega aqui pelo menu touch).
    private void ResizeQuadraMenu_Click(object sender, RoutedEventArgs e)
    {
        if (!CanTransform())
        {
            return;
        }
        ArmTouchResize();
    }

    // Arma: guarda o título original (sem recalcular), troca pelo de instrução e arma timeout + ESC.
    // Fatia 2: engrossa as pegas só na armada (hitbox + visual juntos).
    private void ArmTouchResize()
    {
        // Rearme: novo gesto substitui o anterior (restaura título/grossura/timeout/ESC antigos)
        CancelTouchResize();
        s_touchResize = new TouchResizeState(QuadraId, _viewModel.Title);
        _viewModel.Title = Strings.TouchResizeArmedHint;
        ApplyTouchResizeThickness();
        // Timeout ~8s: one-shot via OneShotTimer (mesmo molde do MOVER)
        OneShotTimer.Arm(ref s_touchResizeTimeoutTimer, TouchResizeState.TimeoutMilliseconds, TouchResizeTimeout_Tick);
        // ESC com escopo estrito: registra ao armar, best-effort silencioso
        try
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero && NativeMethods.RegisterHotKey(hwnd, TouchResizeEscHotkeyId, 0, NativeMethods.VK_ESCAPE))
            {
                s_touchResizeEscHwnd = hwnd;
            }
        }
        catch { /* sem ESC o modo segue cancelando por tap fora/timeout */ }
    }

    // Engrossa as pegas só enquanto armado: thumbs (hitbox do dedo) + borda do
    // container (visual). REUSE os 8 arrastes — só alarga o que o dedo agarra.
    // Destaque REUSE o acento existente (seleção), sem cor chapada/token novo.
    private void ApplyTouchResizeThickness()
    {
        // Guarda uma vez (rearme/restaura usam a foto; sem recalcular)
        _savedResizeEdgeHeight ??= ResizeThumbTop.Height;
        _savedResizeEdgeWidth ??= ResizeThumbLeft.Width;
        _savedResizeCornerSize ??= ResizeThumbTopLeft.Width;
        _savedContainerBorderThickness ??= QuadraContainer.BorderThickness;
        // Armada: esconde +, X e chevron (só aqui, só enquanto armado).
        _savedCollapseButtonVisibility ??= CollapseButton.Visibility;
        _savedAddFileButtonVisibility ??= AddFileButton.Visibility;
        _savedCloseButtonVisibility ??= CloseButton.Visibility;
        CollapseButton.Visibility = Visibility.Collapsed;
        AddFileButton.Visibility = Visibility.Collapsed;
        CloseButton.Visibility = Visibility.Collapsed;
        ResizeThumbTop.Height = TouchResizeArmedEdgeThickness;
        ResizeThumbBottom.Height = TouchResizeArmedEdgeThickness;
        ResizeThumbLeft.Width = TouchResizeArmedEdgeThickness;
        ResizeThumbRight.Width = TouchResizeArmedEdgeThickness;
        ResizeThumbTopLeft.Width = TouchResizeArmedCornerSize;
        ResizeThumbTopLeft.Height = TouchResizeArmedCornerSize;
        ResizeThumbTopRight.Width = TouchResizeArmedCornerSize;
        ResizeThumbTopRight.Height = TouchResizeArmedCornerSize;
        ResizeThumbBottomLeft.Width = TouchResizeArmedCornerSize;
        ResizeThumbBottomLeft.Height = TouchResizeArmedCornerSize;
        ResizeThumbBottomRight.Width = TouchResizeArmedCornerSize;
        ResizeThumbBottomRight.Height = TouchResizeArmedCornerSize;
        QuadraContainer.BorderThickness = TouchResizeArmedBorderThickness;
        QuadraContainer.SetResourceReference(Border.BorderBrushProperty, "Quadra.Item.Selected.BorderBrush");
        // Alças: só no armado (fora é sempre Collapsed, sem foto)
        TouchResizeHandlesOverlay.Visibility = Visibility.Visible;
    }

    // Volta ao exato original (hitbox + visual); idempotente e silencioso.
    private void RestoreTouchResizeThickness()
    {
        if (_savedResizeEdgeHeight.HasValue)
        {
            ResizeThumbTop.Height = _savedResizeEdgeHeight.Value;
            ResizeThumbBottom.Height = _savedResizeEdgeHeight.Value;
            _savedResizeEdgeHeight = null;
        }
        if (_savedResizeEdgeWidth.HasValue)
        {
            ResizeThumbLeft.Width = _savedResizeEdgeWidth.Value;
            ResizeThumbRight.Width = _savedResizeEdgeWidth.Value;
            _savedResizeEdgeWidth = null;
        }
        if (_savedResizeCornerSize.HasValue)
        {
            ResizeThumbTopLeft.Width = _savedResizeCornerSize.Value;
            ResizeThumbTopLeft.Height = _savedResizeCornerSize.Value;
            ResizeThumbTopRight.Width = _savedResizeCornerSize.Value;
            ResizeThumbTopRight.Height = _savedResizeCornerSize.Value;
            ResizeThumbBottomLeft.Width = _savedResizeCornerSize.Value;
            ResizeThumbBottomLeft.Height = _savedResizeCornerSize.Value;
            ResizeThumbBottomRight.Width = _savedResizeCornerSize.Value;
            ResizeThumbBottomRight.Height = _savedResizeCornerSize.Value;
            _savedResizeCornerSize = null;
        }
        if (_savedContainerBorderThickness.HasValue)
        {
            QuadraContainer.BorderThickness = _savedContainerBorderThickness.Value;
            _savedContainerBorderThickness = null;
        }
        // Desarme (qualquer cancela): volta exato o que estava.
        if (_savedCollapseButtonVisibility.HasValue)
        {
            CollapseButton.Visibility = _savedCollapseButtonVisibility.Value;
            _savedCollapseButtonVisibility = null;
        }
        if (_savedAddFileButtonVisibility.HasValue)
        {
            AddFileButton.Visibility = _savedAddFileButtonVisibility.Value;
            _savedAddFileButtonVisibility = null;
        }
        if (_savedCloseButtonVisibility.HasValue)
        {
            CloseButton.Visibility = _savedCloseButtonVisibility.Value;
            _savedCloseButtonVisibility = null;
        }
        // Reamarra no token original (viva DynamicResource, sem valor chapado)
        QuadraContainer.SetResourceReference(Border.BorderBrushProperty, "Quadra.BorderBrush");
        // Alças somem em todo desarme (qualquer cancela)
        TouchResizeHandlesOverlay.Visibility = Visibility.Collapsed;
    }

    // Tap fora de qualquer Quadra (desktop vazio): restaura o título (REUSE do caminho GlobalLeftClick do App).
    // Ignora o LDown que é o próprio tap do menu (enfileirado antes do arme via BeginInvoke Background).
    public static void CancelTouchResizeFromOutside(DateTime raiseUtc)
    {
        if (s_touchResize != null && s_touchResize.ArmedAtUtc <= raiseUtc)
        {
            CancelTouchResize();
        }
    }

    // Desarma por todos os motivos (ESC, timeout, fora, outra Quadra, fechar):
    // restaura título + grossura guardados, sem mais nada.
    private static void CancelTouchResize()
    {
        var armed = s_touchResize;
        s_touchResize = null;
        OneShotTimer.Cancel(ref s_touchResizeTimeoutTimer, TouchResizeTimeout_Tick);
        // Solta o ESC do escopo estrito registrado ao armar
        if (s_touchResizeEscHwnd != IntPtr.Zero)
        {
            try { NativeMethods.UnregisterHotKey(s_touchResizeEscHwnd, TouchResizeEscHotkeyId); } catch { /* silencioso */ }
            s_touchResizeEscHwnd = IntPtr.Zero;
        }
        // Restaura o título original guardado no arme (sem recalcular, sem Notify)
        if (armed != null)
        {
            try
            {
                var owner = FindQuadraWindow(armed.QuadraId);
                if (owner != null)
                {
                    owner._viewModel.Title = armed.OriginalTitle;
                    // Afina de volta ao exato original (só a armada engrossou)
                    owner.RestoreTouchResizeThickness();
                }
            }
            catch { /* silencioso */ }
        }
    }

    private static void TouchResizeTimeout_Tick(object? sender, EventArgs e)
    {
        CancelTouchResize();
    }

    // Alterna a trava individual e persiste
    private void LockQuadraMenu_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsLocked = !_viewModel.IsLocked;
        _coordinator.NotifyQuadraChanged(_viewModel.Model);
        RefreshLockState();
    }

    // RENOMEAR (fatia título): edição inline na barra, sem diálogo/timer/cor nova.
    // Enter confirma (vazio/só-espaços rejeita e mantém o anterior), ESC cancela,
    // LostFocus confirma se válido (senão cancela). Persiste via NotifyQuadraChanged
    // (caminho existente: debounce ~400ms -> quadras.json; fecha e reabre mantendo).
    // Seguro p/ a TUDO: identidade da padrão é IsDefault (flag técnica), nunca o título.
    private bool _isRenaming;
    private string _renameOriginalTitle = string.Empty;

    private void RenameQuadraMenu_Click(object sender, RoutedEventArgs e)
    {
        BeginRename();
    }

    private void BeginRename()
    {
        if (_isRenaming)
        {
            return;
        }
        // Resize armado troca o título pela instrução: desarma primeiro p/ não
        // confundir o original guardado (restaura sem Notify, mesmo molde dos cancelas).
        if (s_touchResize != null && s_touchResize.QuadraId == QuadraId)
        {
            CancelTouchResize();
        }
        _isRenaming = true;
        _renameOriginalTitle = _viewModel.Title;
        TitleTextBlock.Visibility = Visibility.Collapsed;
        TitleEditBox.Text = _viewModel.Title;
        TitleEditBox.Visibility = Visibility.Visible;
        TitleEditBox.IsEnabled = true;
        Dispatcher.BeginInvoke(() =>
        {
            TitleEditBox.Focus();
            TitleEditBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void TitleEditBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (!_isRenaming)
        {
            return;
        }
        if (e.Key == Key.Enter)
        {
            CommitRename();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelRename();
            e.Handled = true;
        }
    }

    private void TitleEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isRenaming)
        {
            CommitRename();
        }
    }

    private void CommitRename()
    {
        if (!_isRenaming)
        {
            return;
        }
        string candidate = TitleEditBox.Text;
        if (QuadraTitleValidator.TryNormalize(candidate, out string normalized))
        {
            if (!string.Equals(normalized, _viewModel.Title, StringComparison.Ordinal))
            {
                _viewModel.Title = normalized;
                _coordinator.NotifyQuadraChanged(_viewModel.Model);
            }
            EndRename();
        }
        else
        {
            // Vazio/só-espaços: rejeita e mantém o anterior.
            CancelRename();
        }
    }

    private void CancelRename()
    {
        if (!_isRenaming)
        {
            return;
        }
        EndRename();
    }

    private void EndRename()
    {
        _isRenaming = false;
        TitleEditBox.Visibility = Visibility.Collapsed;
        TitleTextBlock.Visibility = Visibility.Visible;
    }

    protected override void OnClosed(EventArgs e)
    {
        _isRenaming = false;
        _touchInertiaTimer?.Stop();
        // MOVER armado com origem aqui: fechar cancela sem mover (sem destaque/timeout/ESC órfãos)
        if (s_touchMove != null && s_touchMove.SourceQuadraId == QuadraId)
        {
            CancelTouchMove();
        }
        // Resize armado nesta Quadra: fechar restaura o título (sem timeout/ESC órfãos)
        if (s_touchResize != null && s_touchResize.QuadraId == QuadraId)
        {
            CancelTouchResize();
        }
        // Janela fechando: mesmo trio de transitórios do recolher (inércia fica acima, fora do helper).
        CancelTransientTimers();
        MouseEnter -= Quadra_PeekMouseEnter;
        MouseLeave -= Quadra_PeekMouseLeave;
        PreviewMouseLeftButtonDown -= Quadra_TouchMoveDestinationDown;
        GlobalItemSelected -= OnGlobalItemSelected;
        GlobalCloseMenusRequested -= OnGlobalCloseMenusRequested;
        LocationChanged -= OnPositionOrSizeChanged;
        SizeChanged -= OnPositionOrSizeChanged;
        base.OnClosed(e);
    }

    // ==========================================
    // Manipuladores de Redimensionamento Livre + snap de grade (Fatia 2)
    // ==========================================

    // D9: guard único dos 8 arrastes — mesma regra de antes (travada ou recolhida = return),
    // mais overview ativo (grade estável: resize só após sair do MOVER).
    private bool CanTransform() => !_viewModel.IsLocked && !_viewModel.IsCollapsed && s_overviewSnapshot == null;

    // Grade: célula 78×96 (WrapPanel ItemWidth/ItemHeight no XAML:295).
    private const double ResizeCellWidth = 78.0;
    private const double ResizeCellHeight = 96.0;
    private const double ResizeSnapThreshold = 12.0;
    private const double ResizeSnapHysteresis = 3.0;

    // Chrome horizontal 38 = 20 (Grid Margin 10×2, XAML:143) + 2 (Quadra.BorderThickness
    // 1×2, Default.xaml:14) + 12 (ContentArea Margin 6×2, XAML:265) + 4 (ScrollViewer
    // Padding 2×2, XAML:291). Não inclui o item (o slot 78 já é a célula cheia).
    private const double HorizontalResizeChrome = 38.0;

    // Parte fixa do chrome vertical 34 = 20 (margem externa) + 2 (borda do container)
    // + 8 (ContentArea Margin 4×2) + 4 (ScrollViewer Padding 2×2); soma-se a altura do
    // título por densidade (28/42 via DensityResolver, Default.xaml:48). A borda
    // 0,0,0,1 do título vive dentro da altura da linha, sem somar extra.
    private const double VerticalResizeChromeFixed = 34.0;
    private double VerticalResizeChrome => DensityResolver.TitleBarHeight(_isTouchDensity) + VerticalResizeChromeFixed;

    // Direção por eixo a partir do sinal da variação (receita do XML-doc do SizeSnapper):
    // borda direita/inferior: change > 0 = Growing; borda esquerda/superior: invertido
    // (change < 0 = Growing, a dimensão aumenta). Zero = Unknown (banda base).
    // E5-UI: núcleo único — os dois eixos usam o mesmo enum e a mesma regra; só
    // o nome da borda inicial difere (esquerda/superior), parametrizado via fromStartEdge.
    private static ResizeDirection AxisResizeDirection(double change, bool fromStartEdge)
    {
        if (change > 0)
        {
            return fromStartEdge ? ResizeDirection.Shrinking : ResizeDirection.Growing;
        }

        if (change < 0)
        {
            return fromStartEdge ? ResizeDirection.Growing : ResizeDirection.Shrinking;
        }

        return ResizeDirection.Unknown;
    }

    // Wrappers finos por eixo — preservam os call sites (8 handlers) sem cast,
    // pois ambos os eixos retornam o mesmo enum ResizeDirection.
    private static ResizeDirection HorizontalResizeDirection(double change, bool fromLeftEdge)
        => AxisResizeDirection(change, fromLeftEdge);

    private static ResizeDirection VerticalResizeDirection(double change, bool fromTopEdge)
        => AxisResizeDirection(change, fromTopEdge);

    // E3-UI: núcleo local das 4 bordas — só o snap (tamanho bruto + passo/chrome/
    // direção/mínimo variam por chamador; threshold/histerese são os mesmos).
    // Guard (CanTransform), IsFinite e atribuição/compensação seguem nos handlers.
    private SizeSnapResult SnapEdge(double rawSize, double step, double chrome, ResizeDirection direction, double minSize, double? thresholdOverride = null)
    {
        return SizeSnapper.SnapDimension(
            rawSize, step, chrome,
            threshold: thresholdOverride ?? ResizeSnapThreshold,
            direction: direction,
            hysteresis: ResizeSnapHysteresis,
            minSize: minSize);
    }

    // E3-UI: núcleo local dos 4 cantos — passos/chromes/mínimos são fixos da grade;
    // só o tamanho bruto e as direções variam por chamador. Guard, IsFinite e a
    // ordem de atribuição (Width/Left/Height/Top) seguem nos handlers.
    private SizeSnapResult2D SnapCorner(double rawWidth, double rawHeight, ResizeDirection horizontalDirection, ResizeDirection verticalDirection, double? thresholdOverride = null)
    {
        double threshold = thresholdOverride ?? ResizeSnapThreshold;
        return SizeSnapper.SnapSize(
            rawWidth, rawHeight,
            horizontalStep: ResizeCellWidth, verticalStep: ResizeCellHeight,
            horizontalChrome: HorizontalResizeChrome, verticalChrome: VerticalResizeChrome,
            threshold: threshold,
            horizontalDirection: horizontalDirection,
            verticalDirection: verticalDirection,
            hysteresis: ResizeSnapHysteresis,
            minWidth: MinWidth, minHeight: MinHeight);
    }

    private void ResizeRight_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!CanTransform())
        {
            return;
        }

        double rawWidth = Width + e.HorizontalChange;
        var snapped = SnapEdge(
            rawWidth, ResizeCellWidth, HorizontalResizeChrome,
            HorizontalResizeDirection(e.HorizontalChange, fromLeftEdge: false),
            MinWidth);
        if (!double.IsFinite(snapped.SnappedSize))
        {
            return;
        }

        Width = snapped.SnappedSize;
    }

    private void ResizeBottom_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!CanTransform())
        {
            return;
        }

        double rawHeight = Height + e.VerticalChange;
        var snapped = SnapEdge(
            rawHeight, ResizeCellHeight, VerticalResizeChrome,
            VerticalResizeDirection(e.VerticalChange, fromTopEdge: false),
            MinHeight);
        if (!double.IsFinite(snapped.SnappedSize))
        {
            return;
        }

        Height = snapped.SnappedSize;
    }

    private void ResizeLeft_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!CanTransform())
        {
            return;
        }

        double rawWidth = Width - e.HorizontalChange;
        var snapped = SnapEdge(
            rawWidth, ResizeCellWidth, HorizontalResizeChrome,
            HorizontalResizeDirection(e.HorizontalChange, fromLeftEdge: true),
            MinWidth);
        if (!double.IsFinite(snapped.SnappedSize))
        {
            return;
        }

        // Borda oposta fixa: compensa o Left pela diferença entre a largura
        // antiga e a quantizada (não pelo delta bruto, que o snap pode alterar).
        double widthDelta = Width - snapped.SnappedSize;
        Width = snapped.SnappedSize;
        Left += widthDelta;
    }

    private void ResizeTop_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!CanTransform())
        {
            return;
        }

        double rawHeight = Height - e.VerticalChange;
        var snapped = SnapEdge(
            rawHeight, ResizeCellHeight, VerticalResizeChrome,
            VerticalResizeDirection(e.VerticalChange, fromTopEdge: true),
            MinHeight);
        if (!double.IsFinite(snapped.SnappedSize))
        {
            return;
        }

        double heightDelta = Height - snapped.SnappedSize;
        Height = snapped.SnappedSize;
        Top += heightDelta;
    }

    private void ResizeBottomRight_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!CanTransform())
        {
            return;
        }

        var snapped = SnapCorner(
            Width + e.HorizontalChange, Height + e.VerticalChange,
            HorizontalResizeDirection(e.HorizontalChange, fromLeftEdge: false),
            VerticalResizeDirection(e.VerticalChange, fromTopEdge: false));
        if (!double.IsFinite(snapped.Width) || !double.IsFinite(snapped.Height))
        {
            return;
        }

        Width = snapped.Width;
        Height = snapped.Height;
    }

    private void ResizeBottomLeft_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!CanTransform())
        {
            return;
        }

        var snapped = SnapCorner(
            Width - e.HorizontalChange, Height + e.VerticalChange,
            HorizontalResizeDirection(e.HorizontalChange, fromLeftEdge: true),
            VerticalResizeDirection(e.VerticalChange, fromTopEdge: false));
        if (!double.IsFinite(snapped.Width) || !double.IsFinite(snapped.Height))
        {
            return;
        }

        double widthDelta = Width - snapped.Width;
        Width = snapped.Width;
        Left += widthDelta;
        Height = snapped.Height;
    }

    private void ResizeTopRight_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!CanTransform())
        {
            return;
        }

        var snapped = SnapCorner(
            Width + e.HorizontalChange, Height - e.VerticalChange,
            HorizontalResizeDirection(e.HorizontalChange, fromLeftEdge: false),
            VerticalResizeDirection(e.VerticalChange, fromTopEdge: true));
        if (!double.IsFinite(snapped.Width) || !double.IsFinite(snapped.Height))
        {
            return;
        }

        Width = snapped.Width;
        double heightDelta = Height - snapped.Height;
        Height = snapped.Height;
        Top += heightDelta;
    }

    private void ResizeTopLeft_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!CanTransform())
        {
            return;
        }

        var snapped = SnapCorner(
            Width - e.HorizontalChange, Height - e.VerticalChange,
            HorizontalResizeDirection(e.HorizontalChange, fromLeftEdge: true),
            VerticalResizeDirection(e.VerticalChange, fromTopEdge: true));
        if (!double.IsFinite(snapped.Width) || !double.IsFinite(snapped.Height))
        {
            return;
        }

        double widthDelta = Width - snapped.Width;
        Width = snapped.Width;
        Left += widthDelta;
        double heightDelta = Height - snapped.Height;
        Height = snapped.Height;
        Top += heightDelta;
    }

    // Assenta na grade ao soltar: o dedo anda aos saltos e pode pousar fora da
    // banda de 12px (o mouse desliza e sempre gruda); ao soltar, se estiver a até
    // 20px do detente, assenta (sem tranco: longe disso, fica onde largou, como o mouse).
    // Sem movimento = return (tap na pega não pula). Vale p/ mouse também.
    // O snap usa SEMPRE o chrome normal: a borda grossa é transitória e o que
    // trava aqui é o que persiste após afinar (detente armado deslocaria 6px p/ fora).
    private void Resize_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!CanTransform() || sender is not FrameworkElement thumb)
        {
            return;
        }
        if (Math.Abs(e.HorizontalChange) < 0.5 && Math.Abs(e.VerticalChange) < 0.5)
        {
            return;
        }
        bool horizontal = thumb.Name.Contains("Left") || thumb.Name.Contains("Right");
        bool vertical = thumb.Name.Contains("Top") || thumb.Name.Contains("Bottom");
        if (horizontal)
        {
            var settled = SnapEdge(Width, ResizeCellWidth, HorizontalResizeChrome,
                ResizeDirection.Unknown, MinWidth, thresholdOverride: 20.0);
            if (double.IsFinite(settled.SnappedSize))
            {
                double widthDelta = Width - settled.SnappedSize;
                Width = settled.SnappedSize;
                if (thumb.Name.Contains("Left"))
                {
                    Left += widthDelta;
                }
            }
        }
        if (vertical)
        {
            var settled = SnapEdge(Height, ResizeCellHeight, VerticalResizeChrome,
                ResizeDirection.Unknown, MinHeight, thresholdOverride: 20.0);
            if (double.IsFinite(settled.SnappedSize))
            {
                double heightDelta = Height - settled.SnappedSize;
                Height = settled.SnappedSize;
                if (thumb.Name.Contains("Top"))
                {
                    Top += heightDelta;
                }
            }
        }
    }
}
