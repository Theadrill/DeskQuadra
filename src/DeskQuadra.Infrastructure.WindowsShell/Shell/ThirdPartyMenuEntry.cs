namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

// T3 terceiros: item filtrado "só terceiros" pronto p/ espelhar no menu WPF.
// Label já limpo (sem '&'/'\t'). Verb = verbo canônico GCS_VERBW (seleção
// estável do InvokeCommand via ThirdPartyInvokeHandle); CommandOffset =
// offset do QueryContextMenu (id - idCmdFirst), guardado como FALLBACK p/
// verbo vazio (query + alça resolvidos juntos no serviço).
// Cascata = Children não vazio (submenu habilitado). T3 exibe habilitado e
// invoca no clique (folhas); placeholder continua p/ vazio/falha.
public sealed record ThirdPartyMenuEntry(
    string Label,
    string Verb,
    uint CommandOffset,
    IReadOnlyList<ThirdPartyMenuEntry> Children);
