namespace DeskQuadra.Core;

/// <summary>
/// Lógica pura do autostart (sem registry/UI): montagem da linha de comando
/// com aspas e detecção de <c>--silent</c>/<c>--autostart</c>. Coberta por xUnit.
/// </summary>
public static class StartupCommandBuilder
{
    /// <summary>
    /// Monta <c>"&lt;exe&gt;" --silent</c> com aspas (idempotente a aspas pré-existentes).
    /// Retorna <see cref="string.Empty"/> quando o caminho é nulo/vazio.
    /// </summary>
    public static string Build(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return string.Empty;
        }

        string trimmed = exePath.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        return $"\"{trimmed}\" --silent";
    }

    /// <summary>
    /// Verdadeiro quando os args contêm <c>--silent</c> ou <c>--autostart</c> (case-insensitive).
    /// </summary>
    public static bool IsSilentLaunch(IEnumerable<string>? args)
    {
        if (args is null)
        {
            return false;
        }

        foreach (string? arg in args)
        {
            if (arg is null)
            {
                continue;
            }

            if (arg.Equals("--silent", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--autostart", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
