using DeskQuadra.Core.Models;

namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato do serviço de detecção de capacidades visuais e efeitos do sistema operacional.
/// Responsável por identificar a versão e build reais do Windows, além de verificar se a aceleração
/// por hardware permite a aplicação segura de efeitos de desfoque sem travamentos.
/// </summary>
public interface IWindowsVisualCapabilityService
{
    /// <summary>
    /// Número da build real do Windows NT obtida via registro ou sistema.
    /// </summary>
    int WindowsBuildNumber { get; }

    /// <summary>
    /// Nível de capacidade visual suportado pela máquina.
    /// </summary>
    WindowsVisualTier SupportedTier { get; }

    /// <summary>
    /// Indica se a máquina tem suporte a qualquer efeito de desfoque (ClassicBlur ou Acrylic).
    /// Usado para determinar se o controle deve ser exibido na interface de configurações.
    /// </summary>
    bool IsBlurSupported { get; }

    /// <summary>
    /// Indica se a build do Windows (>= 17134) suporta a técnica de Acrílico Moderno com textura.
    /// </summary>
    bool IsAcrylicSupported { get; }

    /// <summary>
    /// Indica se a aceleração por hardware e a composição do DWM estão ativas.
    /// </summary>
    bool IsHardwareAccelerationEnabled { get; }
}
