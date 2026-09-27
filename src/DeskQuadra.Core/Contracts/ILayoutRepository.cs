using DeskQuadra.Core.Models;

namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato para persistência transacional de layouts de Quadras em disco.
/// </summary>
public interface ILayoutRepository
{
    /// <summary>
    /// Carrega as Quadras salvas, com recuperação automática em caso de queda de energia ou arquivo quebrado.
    /// </summary>
    Task<IReadOnlyList<Quadra>> LoadLayoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Salva o estado atual das Quadras utilizando gravação atômica (.tmp -> .bak -> .json).
    /// </summary>
    Task SaveLayoutAsync(IEnumerable<Quadra> quadras, CancellationToken cancellationToken = default);

    /// <summary>
    /// Oferta de recuperação quando há .tmp íntegro mais recente que o .json (BRAINSTORMING 219-224).
    /// Retorna null quando não há o que perguntar (sem .tmp, .tmp corrompido/obsoleto, ou .json ausente/inválido).
    /// Não muta arquivos; a decisão pura mora em LayoutRecoveryDecider.
    /// </summary>
    Task<PendingLayoutRecovery?> CheckCrashRecoveryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve a oferta anterior: true = promove .tmp a .json (mantendo .bak de segurança);
    /// false = segue com .json e descarta .tmp. Sem oferta pendente, é no-op.
    /// </summary>
    Task ResolveCrashRecoveryAsync(bool restoreRecent, CancellationToken cancellationToken = default);
}

/// <summary>
/// .tmp íntegro candidato a restauração (timestamps UTC para diagnóstico; o diálogo não precisa deles).
/// </summary>
public sealed record PendingLayoutRecovery(DateTime TmpModifiedUtc, DateTime JsonModifiedUtc);
