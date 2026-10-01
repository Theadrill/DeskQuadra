namespace DeskQuadra.Core.ThirdParty;

// T2 terceiros (movido em T5 p/ o Core, sem mudar regra) + COMPLEMENTOS
// NATIVOS (T6, PO pediu de volta): filtro "só terceiros" puro/testável
// (denylist §1 do PLANO_MENU_TERCEIROS.md) com allowlist NativeComplements
// verificada ANTES.
// Regra: MFT_SEPARATOR sempre fora; COMPLEMENTO (verbo openas/sendto/
// setdesktopwallpaper/rotate90/rotate270/casttodevice, ou label SÓ p/ o popup
// sem-verbo "transmitir para dispositivo"/"cast to device", ou verbo implícito
// do popup "abrir com"→openas/"enviar para"→sendto) passa ANTES; depois,
// denylist canônica aplicada a GCS_VERBW E label; resto (inclusive verbo
// vazio) = terceiro. Sem P/Invoke, sem thread, sem estado.
// Mora no Core porque o filtro agora roda DENTRO do ShellHost (a UI só espelha
// o JSON já filtrado) — definição única, sem duplicar nas duas pontas.
public static class ThirdPartyVerbFilter
{
    // Verbos canônicos nativos do Explorer (GCS_VERBW). Refinado com dados reais
    // da máquina do PO (2026-10-01: OneDrive/GUID-brace, pin, PreviousVersions,
    // copyaspath, includelibrary). T6 segue refinando com o que vazar no campo.
    // SONDA T6 (2026-10-01, HMENU fantasma BRUTO via sonda headless temporária
    // — .txt/.jpg/.zip/pasta × normal/extended, depois apagada): vazavam como
    // "terceiro" os verbos abaixo (todos com verbo canônico NÃO-vazio, então
    // bloqueio POR VERBO — imune a idioma pt-BR/en-US):
    //   setdesktopwallpaper ("Definir como fundo da área de trabalho", só .jpg),
    //   rotate90/rotate270 ("Girar para a direita/esquerda", só imagem),
    //   opennewprocess ("Abrir em novo processo", .zip/pasta SÓ com Shift),
    //   powershell ("Abrir janela do PowerShell aqui", pasta SÓ com Shift).
    // "Send with Tailscale..." (verbo tailscale) é de TERCEIRO legítimo — passa.
    // DECISÃO T6 COMPLEMENTOS NATIVOS (PO pediu de volta, 2026-10-01): openas
    // ("Abrir com") e sendto ("Enviar para") SAEM da denylist e viram allowlist
    // explícita NativeComplements abaixo (opt-in explícito, não remoção
    // silenciosa). setdesktopwallpaper/rotate90/rotate270/casttodevice-FOLHA
    // continuam NA denylist por compatibilidade, mas a allowlist é verificada
    // ANTES — o complemento vence (override documentado no IsThirdParty).
    // "Abrir com"/"Enviar para" (labels) saem junto: o popup NÃO tem verbo (a
    // query fixa Verb=null p/ MF_POPUP — ver ShellThirdPartyQuery), então sem
    // a saída do label o popup continuaria bloqueado mesmo com o verbo
    // liberado. Resto dos nativos continua fora.
    private static readonly HashSet<string> BlockedVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "open", "opennew", "opennewprocess", "opencontaining", "explore", "find",
        "print", "printto", "preview",
        "edit",
        "runas", "runasuser",
        "properties",
        "cut", "copy", "paste", "pasteshortcut",
        "link", "delete", "rename",
        "new", "refresh",
        "share",
        "extract", "mount", "burn",
        "casttodevice",
        "setdesktopwallpaper",
        "rotate90", "rotate270",
        "powershell",
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
        "imprimir", "print",
        "editar", "edit",
        "executar como administrador", "run as administrator",
        "propriedades", "properties",
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
        // SONDA T6 + COMPLEMENTOS (PO pediu de volta): o popup "Transmitir para
        // Dispositivo" (.jpg, verbo VAZIO no popup E no filho — GCS_VERBW
        // vazio/null nos dois níveis) continua NA denylist por compatibilidade,
        // mas a allowlist ComplementPopupLabels abaixo vence ANTES (override no
        // IsThirdParty). Sem verbo, o rótulo é o ÚNICO sinal — justificativa p/
        // exceção por label apesar de pt-BR variar por idioma (verbo preferido
        // quando existe; aqui não há). en-US "cast to device" pelo mesmo
        // motivo. A folha "casttodevice" (quando o handler expõe verbo) passa
        // pelo VERBO (ComplementVerbs), não pelo label.
        "transmitir para dispositivo", "cast to device",
    };

    // COMPLEMENTOS NATIVOS (PO pediu de volta como seção de terceiros — nomes e
    // labels exibidos são os do próprio Shell, sem resx novo):
    //   openas ("Abrir com" — invoke abre o diálogo do sistema),
    //   sendto ("Enviar para" — invoke abre a cascata do SendTo),
    //   setdesktopwallpaper ("Definir como fundo"),
    //   rotate90/rotate270 ("Girar"),
    //   casttodevice-FOLHA (verbo; o POPUP homônimo não tem verbo — ver abaixo).
    // Por VERBO quando existe (preferido — imune a idioma; todos os verbais
    // têm verbo canônico GCS_VERBW, então o invoke usa o caminho estável por
    // verbo, imune a reordenação de offsets).
    private static readonly HashSet<string> ComplementVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "openas",
        "sendto",
        "setdesktopwallpaper",
        "rotate90",
        "rotate270",
        "casttodevice",
    };

    // Por LABEL SÓ o popup "Transmitir para Dispositivo"/"Cast to device":
    // popup E filho têm verbo VAZIO (sonda T6), então o verbo preferido NÃO
    // EXISTE aqui — o rótulo normalizado é o único sinal (igualdade exata,
    // nunca contém; pt-BR + en-US). Resto dos nativos continua fora.
    private static readonly HashSet<string> ComplementPopupLabels = new(StringComparer.Ordinal)
    {
        "transmitir para dispositivo",
        "cast to device",
    };

    // Popup MF_POPUP não tem verbo (a query fixa Verb=null — ver
    // ShellThirdPartyQuery.EnumerateMenu): "Abrir com"/"Enviar para" em forma
    // de cascata chegam com verbo nulo, então o reconhecimento é pelo rótulo
    // do Shell mapeado p/ o verbo implícito correspondente (continua
    // "por VERBO": a seleção do invoke usa o verbo estável openas/sendto, não
    // o offset). Só estes dois têm mapeamento — resto sem verbo cai na regra
    // geral (inclusive verbo vazio = terceiro, salvo denylist por label).
    public static string? TryGetImplicitPopupVerb(string? label)
    {
        string n = NormalizeLabel(label);
        if (n.Length == 0)
        {
            return null;
        }

        if (n == "abrir com" || n == "open with")
        {
            return "openas";
        }

        if (n == "enviar para" || n == "send to")
        {
            return "sendto";
        }

        return null;
    }

    // Allowlist explícita, verificada ANTES da denylist (ver IsThirdParty).
    public static bool IsNativeComplement(string? verb, string? label)
    {
        if (!string.IsNullOrWhiteSpace(verb)
            && ComplementVerbs.Contains(verb.Trim()))
        {
            return true;
        }

        string n = NormalizeLabel(label);
        if (n.Length != 0 && ComplementPopupLabels.Contains(n))
        {
            return true;
        }

        string? implicitVerb = TryGetImplicitPopupVerb(label);
        return implicitVerb is not null && ComplementVerbs.Contains(implicitVerb);
    }

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

    // Decisão §1 + COMPLEMENTOS: separador nunca é terceiro (nem complemento);
    // COMPLEMENTO vence ANTES da denylist (IsNativeComplement primeiro);
    // verbo OU label nativo bloqueia; resto (inclusive verbo vazio) = terceiro.
    public static bool IsThirdParty(string? verb, string? label, bool isSeparator)
    {
        if (isSeparator)
        {
            return false;
        }

        if (IsNativeComplement(verb, label))
        {
            return true;
        }

        return !IsBlockedVerb(verb) && !IsNativeLabel(label);
    }
}
