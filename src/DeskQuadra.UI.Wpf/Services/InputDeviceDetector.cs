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
    /// Predicado por evento: indica se um evento de mouse foi promovido (sintetizado) a partir
    /// de toque — assinatura Stylus com Tablet touch ou mensagem atual com origem touch no Win32.
    /// Não consulta a flag global; é a mesma regra antes duplicada como IsTouchPromotedMouse na QuadraWindow.
    /// </summary>
    public static bool IsPromotedTouch(MouseEventArgs e)
    {
        return (e.StylusDevice != null && e.StylusDevice.TabletDevice?.Type == TabletDeviceType.Touch)
               || NativeMethods.IsCurrentMessageFromTouch();
    }

    /// <summary>
    /// Decisão unificada de interação touch para eventos de mouse: flag global (última interação
    /// conhecida) OU assinatura de toque do evento atual. Cobre o antigo padrão local
    /// (_isTouchActive || stylus-touch || mensagem touch) dos handlers de peek, down e move.
    /// </summary>
    public static bool IsTouchInteraction(MouseEventArgs e)
    {
        return _isTouchActive || IsPromotedTouch(e);
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
