namespace DeskQuadra.Infrastructure.Persistence.Repositories;

/// <summary>
/// Decisão pura de recuperação de queda de energia (BRAINSTORMING 21 / 219-224).
/// Sem I/O, sem UI, sem relógio: recebe existência + validez + timestamps e diz a ação.
/// Testável via xUnit sem acoplar diálogo.
/// </summary>
public enum LayoutRecoveryAction
{
    /// <summary>Sem .tmp: segue fluxo atual (json -&gt; bak -&gt; vazio).</summary>
    ProceedNormal,

    /// <summary>.tmp truncado/corrompido OU íntegro porém mais antigo: descarta silencioso, sobe .json (ou .bak).</summary>
    DiscardTmpKeepJson,

    /// <summary>.tmp íntegro mas .json ausente/inválido: promove .tmp a .json sem diálogo.</summary>
    PromoteTmpAuto,

    /// <summary>.tmp íntegro E mais recente que .json válido: a UI deve perguntar Restaurar/Manter.</summary>
    PromptUser
}

public static class LayoutRecoveryDecider
{
    /// <param name="tmpWriteUtc">LastWriteTimeUtc do .tmp (ignorado se !tmpExists).</param>
    /// <param name="jsonWriteUtc">LastWriteTimeUtc do .json (ignorado se !jsonExists).</param>
    /// <returns>Ação pura. Recência = comparação (&gt;=) de LastWriteTimeUtc: igual conta
    /// como recente (cópia via Explorer preserva LastWriteTime; granularidade do FS pode
    /// igualar ticks de um crash real). Só mais antigo descarta sem diálogo.</returns>
    public static LayoutRecoveryAction Decide(
        bool tmpExists,
        bool tmpValid,
        DateTime tmpWriteUtc,
        bool jsonExists,
        bool jsonValid,
        DateTime jsonWriteUtc)
    {
        if (!tmpExists)
        {
            return LayoutRecoveryAction.ProceedNormal;
        }

        if (!tmpValid)
        {
            // Truncado/corrompido: descarta silencioso, sem diálogo.
            return LayoutRecoveryAction.DiscardTmpKeepJson;
        }

        if (!jsonExists || !jsonValid)
        {
            return LayoutRecoveryAction.PromoteTmpAuto;
        }

        // Ambos íntegros: pergunta se o .tmp for mais recente OU igual (não-obsoleto).
        // Só mais antigo = .tmp obsoleto (save anterior já consolidado): descarta sem diálogo.
        // Igual precisa perguntar: cópia manual via Explorer preserva LastWriteTime do .json
        // (creation time muda, last-write não) e o FS pode igualar ticks num crash real;
        // descartar silencioso aqui perdia dados e o diálogo nunca aparecia (caso A do PO).
        return tmpWriteUtc >= jsonWriteUtc
            ? LayoutRecoveryAction.PromptUser
            : LayoutRecoveryAction.DiscardTmpKeepJson;
    }
}
