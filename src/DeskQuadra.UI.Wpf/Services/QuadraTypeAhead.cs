// Type-ahead estilo Explorer da Quadra (lógica pura, sem Visual/Dispatcher):
// prefixo com timeout + ciclo na mesma letra + busca sem case/acento.
// Testável em xUnit sem STA: só strings + índices + DateTime
// (o PreviewTextInput/seleção/scroll/OneShotTimer vivem na QuadraWindow).
using System.Globalization;
using System.Text;

namespace DeskQuadra.UI.Wpf.Services;

/// <summary>
/// Estado do type-ahead por Quadra: prefixo acumulado + instante da última tecla.
/// Mesma letra em sequência rápida (&lt; timeout) cicla entre os matches;
/// letras diferentes em sequência rápida acumulam como prefixo;
/// pausa longa reinicia a busca. Dígitos valem como letras.
/// </summary>
public sealed class QuadraTypeAhead
{
    /// <summary>Janela de acúmulo do prefixo (~1s, padrão Explorer).</summary>
    public const int TimeoutMilliseconds = 1000;

    private string _prefix = string.Empty;
    private DateTime _lastKeyUtc = DateTime.MinValue;

    public string Prefix => _prefix;

    /// <summary>Limpa o prefixo (chamado pelo tick do OneShotTimer da janela).</summary>
    public void Reset()
    {
        _prefix = string.Empty;
        _lastKeyUtc = DateTime.MinValue;
    }

    /// <summary>
    /// Avança a busca com o caractere digitado. Retorna o índice do item a
    /// selecionar, ou null se nada casa (seleção inalterada).
    /// <paramref name="selectedIndex"/> é o índice atual (-1 = nada selecionado).
    /// Repetição da mesma letra (prefixo de 1 char igual) cicla a partir do
    /// atual com wrap; prefixo novo/acumulado casa do início (índice 0).
    /// Sem match no prefixo acumulado, cai para a letra sozinha antes de desistir.
    /// </summary>
    public int? Advance(IReadOnlyList<string?> names, int selectedIndex, char ch, DateTime utcNow)
    {
        string key = NormalizeChar(ch);
        if (key.Length == 0)
        {
            return null;
        }

        bool withinTimeout = (utcNow - _lastKeyUtc).TotalMilliseconds < TimeoutMilliseconds
            && _lastKeyUtc != DateTime.MinValue;
        _lastKeyUtc = utcNow;

        // Mesma letra repetida: cicla para o próximo match (wrap).
        if (withinTimeout && _prefix.Length == 1 && _prefix == key)
        {
            int? cycled = FindNext(names, key, selectedIndex);
            if (cycled.HasValue)
            {
                return cycled;
            }
            // Sem nenhum match sequer: mantém o prefixo, sem seleção.
            return null;
        }

        string candidate = withinTimeout && _prefix.Length > 0 ? _prefix + key : key;

        int? hit = FindFirst(names, candidate);
        if (hit.HasValue)
        {
            _prefix = candidate;
            return hit;
        }

        // Prefixo acumulado sem match: tenta a letra sozinha (do início).
        int? fallback = FindFirst(names, key);
        _prefix = key;
        return fallback;
    }

    private static int? FindFirst(IReadOnlyList<string?> names, string prefix)
    {
        for (int i = 0; i < names.Count; i++)
        {
            if (StartsWith(names[i], prefix))
            {
                return i;
            }
        }
        return null;
    }

    private static int? FindNext(IReadOnlyList<string?> names, string prefix, int selectedIndex)
    {
        for (int i = selectedIndex + 1; i < names.Count; i++)
        {
            if (StartsWith(names[i], prefix))
            {
                return i;
            }
        }
        for (int i = 0; i <= selectedIndex && i < names.Count; i++)
        {
            if (StartsWith(names[i], prefix))
            {
                return i;
            }
        }
        return null;
    }

    private static bool StartsWith(string? name, string prefix)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }
        return Normalize(name).StartsWith(prefix, StringComparison.Ordinal);
    }

    /// <summary>Normalização mínima p/ busca: minúsculo + sem acento (FormD, tira Mn).</summary>
    public static string Normalize(string value)
    {
        string lower = value.ToLowerInvariant();
        string decomposed = lower.Normalize(NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    /// <summary>Normaliza um caractere digitado; "" = não pesquisável (não letra/dígito).</summary>
    public static string NormalizeChar(char ch)
    {
        if (!char.IsLetterOrDigit(ch))
        {
            return string.Empty;
        }
        return Normalize(ch.ToString());
    }
}
