namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato para detecção e rastreamento global do tipo de dispositivo de entrada (Mouse vs. Touch),
/// permitindo que a interface se adapte dinamicamente em densidade e comportamento de arraste.
/// </summary>
public interface IInputDeviceDetector
{
    /// <summary>
    /// Indica se a última interação ativa foi originada por tela de toque (Touch).
    /// </summary>
    bool IsTouchActive { get; }

    /// <summary>
    /// Registra manualmente o estado de interação de entrada como Touch ou Mouse.
    /// </summary>
    void SetTouchActive(bool isTouch);

    /// <summary>
    /// Evento disparado quando o estado de interação alterna entre Mouse e Touch.
    /// </summary>
    event EventHandler<bool>? TouchStateChanged;
}
