namespace DeskQuadra.UI.Wpf.Services;

/// <summary>
/// Validação pura do título da Quadra (fatia Renomear).
/// Molde do <c>QuadraNaming</c>: helper interno, sem resx/XAML/timers,
/// testável via xUnit. A Quadra padrão é identificada por <c>IsDefault</c>
/// (flag técnica, nunca pelo título) — renomear a TUDO é permitido e seguro.
/// </summary>
/// <remarks>
/// Regra única: vazio ou só-espaços é rejeitado (mantém o anterior);
/// o chamador persiste via <c>NotifyQuadraChanged</c> (caminho existente,
/// fecha e reabre mantendo o nome no <c>quadras.json</c>).
/// </remarks>
internal static class QuadraTitleValidator
{
    /// <summary>
    /// true se o candidato tem ao menos um caractere não-branco.
    /// </summary>
    public static bool IsValid(string? candidate) =>
        !string.IsNullOrWhiteSpace(candidate);

    /// <summary>
    /// Normaliza (Trim) e valida num passo só.
    /// Retorna false + <c>normalized</c> vazio quando vazio/só-espaços.
    /// </summary>
    public static bool TryNormalize(string? candidate, out string normalized)
    {
        normalized = (candidate ?? string.Empty).Trim();
        return !string.IsNullOrWhiteSpace(normalized);
    }
}
