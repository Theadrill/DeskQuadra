using System.IO;

namespace DeskQuadra.Core;

/// <summary>
/// Validação e resolução de nomes ao renomear itens (arquivos, diretórios e atalhos .lnk) da Quadra.
/// Lógica pura desacoplada de UI/WPF/timers, coberta por xUnit.
/// </summary>
public static class DesktopItemRenameValidator
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

    /// <summary>
    /// Verifica se o candidato é um nome de arquivo/pasta válido no Windows (não-nulo, não-vazio,
    /// sem caracteres proibidos e diferente de '.' e '..').
    /// </summary>
    public static bool IsValid(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        string trimmed = candidate.Trim();
        if (trimmed.Length == 0 || trimmed == "." || trimmed == "..")
        {
            return false;
        }

        return trimmed.IndexOfAny(InvalidChars) < 0;
    }

    /// <summary>
    /// Normaliza (Trim) e valida o nome candidato num único passo.
    /// </summary>
    public static bool TryNormalize(string? candidate, out string normalized)
    {
        string trimmed = (candidate ?? string.Empty).Trim();
        if (IsValid(trimmed))
        {
            normalized = trimmed;
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    /// <summary>
    /// Resolve o nome do arquivo final no disco a partir do caminho original e do novo nome exibido.
    /// Para atalhos (.lnk), preserva a extensão .lnk caso o usuário não a tenha digitado.
    /// Para arquivos normais e diretórios, usa o nome normalizado diretamente.
    /// </summary>
    public static string GetTargetFileName(string originalFilePath, string candidateDisplayName)
    {
        ArgumentNullException.ThrowIfNull(originalFilePath);
        ArgumentNullException.ThrowIfNull(candidateDisplayName);

        string trimmed = candidateDisplayName.Trim();

        bool isShortcut = originalFilePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
        if (isShortcut && !trimmed.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed + ".lnk";
        }

        return trimmed;
    }
}
