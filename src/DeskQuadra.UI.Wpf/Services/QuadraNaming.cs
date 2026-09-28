namespace DeskQuadra.UI.Wpf.Services;

/// <summary>
/// Geração do título padrão da próxima Quadra ("Quadra N").
/// Centraliza o molde duplicado entre <c>App</c> (menu dual) e
/// <c>QuadraWindow.NewQuadraMenu_Click</c>: ambos faziam
/// <c>ActiveQuadras.Count + 1</c> + <c>string.Format(QuadraDefaultTitleFormat, count)</c>.
/// </summary>
/// <remarks>
/// Escolha do formato (documentada em pt-BR):
/// <para>
/// Rejeitada a alternativa <c>NextTitle(int count, string format) =&gt; string.Format(format, count)</c>
/// porque ela é cerimonial — só renomeia <c>string.Format</c> e deixa o <c>+1</c>
/// duplicado nos call sites (o risco real de off-by-one continua em dois lugares).
/// </para>
/// <para>
/// Adotado <c>NextTitle(int existingCount, string format)</c>, que recebe a contagem
/// existente (<c>ActiveQuadras.Count</c>) e encapsula o <c>+1</c> junto com o format.
/// Assim a duplicação real (incremento + formatação) morre num ponto só, testável sem
/// timers/XAML. O formato continua vindo do resx da UI (<c>QuadraDefaultTitleFormat</c>,
/// ex. "Quadra {0}"), então idioma/formato ficam intactos — este helper não conhece resx,
/// só aplica o formato recebido (Clean Architecture: fica na UI, junto dos helpers puros
/// como <c>DpiHelper</c>, nunca no coordinator/Core).
/// </para>
/// </remarks>
internal static class QuadraNaming
{
    /// <summary>
    /// Aplica o formato do resx à próxima posição (<c>existingCount + 1</c>).
    /// Mesma semântica do molde original: <c>string.Format(format, count)</c> com a
    /// cultura corrente (sem <c>CultureInfo</c> explícito, para não mudar idioma/formato).
    /// </summary>
    /// <param name="existingCount">Contagem atual (ex. <c>ActiveQuadras.Count</c>).</param>
    /// <param name="format">Formato do resx (ex. <c>QuadraDefaultTitleFormat</c> = "Quadra {0}").</param>
    public static string NextTitle(int existingCount, string format) =>
        string.Format(format, existingCount + 1);
}
