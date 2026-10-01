namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

// T2 terceiros: filtro "só terceiros" puro/testável (denylist §1 do PLANO_MENU_TERCEIROS.md).
// Regra: MFT_SEPARATOR sempre fora; denylist canônica aplicada a GCS_VERBW E label;
// resto (inclusive verbo vazio) = terceiro. Sem P/Invoke, sem thread, sem estado.
internal static class ThirdPartyVerbFilter
{
    // Verbos canônicos nativos do Explorer (GCS_VERBW). Refinado com dados reais
    // da máquina do PO (2026-10-01: OneDrive/GUID-brace, pin, PreviousVersions,
    // copyaspath, includelibrary). T6 segue refinando com o que vazar no campo.
    private static readonly HashSet<string> BlockedVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "open", "opennew", "openas", "opencontaining", "explore", "find",
        "print", "printto", "preview",
        "edit",
        "runas", "runasuser",
        "properties",
        "sendto",
        "cut", "copy", "paste", "pasteshortcut",
        "link", "delete", "rename",
        "new", "refresh",
        "share",
        "extract", "mount", "burn",
        "casttodevice",
        // Observados no campo (máquina do PO): pin/favoritos, OneDrive,
        // versões anteriores, copiar-como-caminho, biblioteca.
        "pintohome", "pintohomefile", "pintostartscreen", "pintotaskbar",
        "makeavailableoffline", "makeavailableonline",
        "previousversions",
        "copyaspath",
        "includelibrary",
    };

    // Rótulos nativos exatos (normalizados: sem '&', sem sufixo '\t', minúsculos).
    // pt-BR + en-US. Só igualdade (NUNCA contém): "Open with Code" de terceiro
    // não pode cair por conter "open with".
    private static readonly HashSet<string> BlockedLabels = new(StringComparer.Ordinal)
    {
        "abrir", "open",
        "abrir com", "open with",
        "imprimir", "print",
        "editar", "edit",
        "executar como administrador", "run as administrator",
        "propriedades", "properties",
        "enviar para", "send to",
        "recortar", "cut",
        "copiar", "copy",
        "colar", "paste",
        "criar atalho", "create shortcut",
        "excluir", "delete",
        "renomear", "rename",
        "compartilhar", "share",
        "novo", "new",
        "atualizar", "refresh",
        "fixar na tela inicial", "pin to start",
        "fixar na barra de tarefas", "pin to taskbar",
        "fixar no acesso rápido", "pin to quick access",
        "mostrar mais opções", "show more options",
        "extrair tudo", "extract all",
        "montar", "mount",
        // Observados no campo (máquina do PO, OneDrive/pin/biblioteca). Cuidado:
        // "Abrir arquivo compactado" é do 7-Zip (verbo SevenZipOpen) — NÃO entra.
        "manter sempre este dispositivo", "manter sempre neste dispositivo",
        "sempre manter neste dispositivo", "always keep on this device",
        "liberar espaço", "free up space",
        "copiar link", "copy link",
        "gerenciar acesso", "manage access",
        "exibir online", "view online",
        "gerenciar backup do onedrive", "manage onedrive backup",
        "cor da pasta", "folder color",
        "mover para o onedrive", "move to onedrive",
        "copiar como caminho", "copy as path",
        "restaurar versões anteriores", "restore previous versions",
        "adicionar aos favoritos", "add to favorites",
        "fixar em iniciar", "pin to start screen",
        "incluir na biblioteca", "include in library",
    };

    // Normaliza rótulo do HMENU fantasma p/ comparação e exibição: corta sufixo
    // de atalho ("\tCtrl+..."), remove acelerador '&', apara e minúscula.
    // (Exibição no WPF usa a variante sem minúscula — ver CleanLabelForDisplay.)
    public static string NormalizeLabel(string? label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return string.Empty;
        }

        int tab = label.IndexOf('\t');
        string head = tab >= 0 ? label.Substring(0, tab) : label;
        return head.Replace("&", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();
    }

    // Rótulo pronto p/ Header do WPF: sem '&' (senão vira mnemônico) e sem sufixo '\t'.
    public static string CleanLabelForDisplay(string? label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return string.Empty;
        }

        int tab = label.IndexOf('\t');
        string head = tab >= 0 ? label.Substring(0, tab) : label;
        return head.Replace("&", string.Empty, StringComparison.Ordinal).Trim();
    }

    public static bool IsBlockedVerb(string? verb)
    {
        if (string.IsNullOrWhiteSpace(verb))
        {
            return false;
        }

        string v = verb.Trim();
        if (BlockedVerbs.Contains(v))
        {
            return true;
        }

        // Verbo em forma de GUID "{...}" = share/sync do OneDrive (observado no
        // campo: {5250E46F-BB09-D602-5891-F476DC89B702...}). Nativo, não canônico.
        if (v.Length > 2 && v[0] == '{' && v[v.Length - 1] == '}')
        {
            return true;
        }

        // Padrão §1 "windows.*share*": começa com windows e contém share.
        return v.StartsWith("windows", StringComparison.OrdinalIgnoreCase)
            && v.Contains("share", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsNativeLabel(string? label)
    {
        string n = NormalizeLabel(label);
        if (n.Length == 0)
        {
            return false;
        }

        if (BlockedLabels.Contains(n))
        {
            return true;
        }

        // Flyout nativo de compartilhamento ("Compartilhar com ..."/"Share with ...").
        return n.StartsWith("compartilhar ", StringComparison.Ordinal)
            || n.StartsWith("share ", StringComparison.Ordinal);
    }

    // Decisão §1: separador nunca é terceiro; verbo OU label nativo bloqueia;
    // resto (inclusive verbo vazio) = terceiro.
    public static bool IsThirdParty(string? verb, string? label, bool isSeparator)
    {
        if (isSeparator)
        {
            return false;
        }

        return !IsBlockedVerb(verb) && !IsNativeLabel(label);
    }
}
