using DeskQuadra.Core.Models;

namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato do serviço que detecta gestos de desenho com o botão direito do mouse diretamente sobre a área de trabalho.
/// </summary>
public interface IDesktopDrawingService : IDisposable
{
    /// <summary>
    /// Inicia o monitoramento global de gestos na Área de Trabalho.
    /// </summary>
    void Start();

    /// <summary>
    /// Encerra o monitoramento global.
    /// </summary>
    void Stop();

    /// <summary>
    /// Disparado durante o arrasto com botão direito (>15px) na Área de Trabalho com as coordenadas do retângulo atual.
    /// </summary>
    event EventHandler<Rect2D>? DrawingProgress;

    /// <summary>
    /// Disparado quando o botão direito é solto após um arrasto válido, informando as dimensões finais do retângulo desenhado.
    /// </summary>
    event EventHandler<Rect2D>? DrawingCompleted;

    /// <summary>
    /// Disparado quando o desenho é cancelado ou interrompido.
    /// </summary>
    event EventHandler? DrawingCancelled;

    /// <summary>
    /// Disparado em qualquer clique com o botão esquerdo do mouse no sistema.
    /// </summary>
    event EventHandler? GlobalLeftClick;

    /// <summary>
    /// Indica se há um desenho ativo em andamento (botão direito pressionado no desktop e arrasto além do threshold).
    /// </summary>
    bool IsDrawingActive { get; }
}
