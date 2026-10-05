namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Espaçamento magnético (snap gap) entre Quadras e bordas da tela, em pixels.
/// settings.json global (precedente densidade/visual); quadras.json segue só layout.
/// Best-effort: falha de I/O nunca derruba a UI (leitura retorna o padrão 8).
/// </summary>
public interface ISnapSettingsService
{
    /// <summary>
    /// Espaçamento atual em pixels (0 a 24, padrão 8 — o valor histórico do app).
    /// </summary>
    double Gap { get; }

    void SetGap(double gap);

    event EventHandler<double>? GapChanged;
}
