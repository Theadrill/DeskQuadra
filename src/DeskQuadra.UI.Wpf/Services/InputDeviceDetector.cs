using System.Windows;
using System.Windows.Input;
using DeskQuadra.Core.Contracts;
using DeskQuadra.Infrastructure.WindowsShell.Native;

namespace DeskQuadra.UI.Wpf.Services;

/// <summary>
/// Detector e rastreador global de dispositivos de entrada (Mouse vs. Touch).
/// Monitora eventos em nível de aplicação para identificar dinamicamente se o usuário está
/// interagindo via tela de toque física ou mouse/touchpad tradicional, adaptando
/// densidades de menus (WCAG 44px+) e comportamentos de arrasto (sem preview no touch).
/// </summary>
public sealed class InputDeviceDetector : IInputDeviceDetector
{
    private static volatile bool _isTouchActive;
    private static bool _isInitialized;
    private static readonly object _lock = new();

    public static event EventHandler<bool>? GlobalTouchStateChanged;

    event EventHandler<bool>? IInputDeviceDetector.TouchStateChanged
    {
        add => GlobalTouchStateChanged += value;
        remove => GlobalTouchStateChanged -= value;
    }

    /// <summary>
    /// Indica se a última interação ativa foi identificada como toque direto (Touch).
    /// </summary>
    public static bool IsTouchActive => _isTouchActive;

    bool IInputDeviceDetector.IsTouchActive => _isTouchActive;

    /// <summary>
    /// Registra e atualiza o estado global de entrada.
    /// </summary>
    public static void SetTouchActive(bool isTouch)
    {
        if (_isTouchActive != isTouch)
        {
            _isTouchActive = isTouch;
            GlobalTouchStateChanged?.Invoke(null, isTouch);
        }
    }

    void IInputDeviceDetector.SetTouchActive(bool isTouch)
    {
        SetTouchActive(isTouch);
    }

    /// <summary>
    /// Inicializa os ganchos globais de eventos na aplicação WPF via Class Handlers no nível de Window.
    /// Registra na raiz (Window) para capturar túneis de eventos sem onerar centenas de UIElements filhos.
    /// </summary>
    public static void Initialize(System.Windows.Application app)
    {
        lock (_lock)
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;

            // 1. Toque capacitivo direto na tela (PreviewTouchDown)
            EventManager.RegisterClassHandler(
                typeof(Window),
                UIElement.PreviewTouchDownEvent,
                new EventHandler<TouchEventArgs>((s, e) => SetTouchActive(true)),
                handledEventsToo: true);

            // 2. Caneta/Stylus (PreviewStylusDown)
            EventManager.RegisterClassHandler(
                typeof(Window),
                UIElement.PreviewStylusDownEvent,
                new StylusDownEventHandler((s, e) =>
                {
                    bool isTouch = e.StylusDevice?.TabletDevice?.Type == TabletDeviceType.Touch;
                    SetTouchActive(isTouch);
                }),
                handledEventsToo: true);

            // 3. Eventos de mouse ou cliques sintetizados (PreviewMouseDown)
            EventManager.RegisterClassHandler(
                typeof(Window),
                UIElement.PreviewMouseDownEvent,
                new MouseButtonEventHandler((s, e) =>
                {
                    if (e.StylusDevice != null && e.StylusDevice.TabletDevice?.Type == TabletDeviceType.Touch)
                    {
                        SetTouchActive(true);
                    }
                    else if (NativeMethods.IsCurrentMessageFromTouch())
                    {
                        SetTouchActive(true);
                    }
                    else
                    {
                        SetTouchActive(false);
                    }
                }),
                handledEventsToo: true);

            // 4. Movimento físico do mouse restaura modo mouse se não for sintetizado por touch
            EventManager.RegisterClassHandler(
                typeof(Window),
                UIElement.PreviewMouseMoveEvent,
                new MouseEventHandler((s, e) =>
                {
                    if (e.StylusDevice != null && e.StylusDevice.TabletDevice?.Type == TabletDeviceType.Touch)
                    {
                        SetTouchActive(true);
                    }
                    else if (NativeMethods.IsCurrentMessageFromTouch())
                    {
                        SetTouchActive(true);
                    }
                    else
                    {
                        SetTouchActive(false);
                    }
                }),
                handledEventsToo: true);
        }
    }

    /// <summary>
    /// Avalia um evento específico para determinar se sua origem imediata é touch ou stylus.
    /// </summary>
    public static bool IsEventFromTouch(RoutedEventArgs? e)
    {
        if (e is TouchEventArgs)
        {
            return true;
        }

        if (e is StylusEventArgs se && se.StylusDevice?.TabletDevice?.Type == TabletDeviceType.Touch)
        {
            return true;
        }

        if (e is MouseButtonEventArgs me && me.StylusDevice?.TabletDevice?.Type == TabletDeviceType.Touch)
        {
            return true;
        }

        if (NativeMethods.IsCurrentMessageFromTouch())
        {
            return true;
        }

        return _isTouchActive;
    }
}
