namespace DeskQuadra.Core.ThirdParty;

// T3 terceiros (movido em T5 p/ o Core, sem mudar forma): item filtrado "só
// terceiros" pronto p/ espelhar no menu WPF. Mora no Core porque agora é
// produzido no ShellHost (outro processo) e consumido na UI — definição única,
// sem duplicar DTO nas duas pontas (o JSON do protocolo mapeia 1:1 p/ cá).
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
