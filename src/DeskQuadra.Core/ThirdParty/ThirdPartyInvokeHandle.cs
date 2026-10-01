namespace DeskQuadra.Core.ThirdParty;

// T3 terceiros (movido em T5 p/ o Core, sem mudar forma): alça de invoke
// resolvida JUNTO com a query (§1: query e invoke compartilham o ciclo de
// vida). Guarda o caminho + VERBO canônico estável (GCS_VERBW da folha T2,
// ex. "SevenZipCompressToZip") + offset de fallback + flags da query
// (Shift/EXTENDEDVERBS no momento da abertura).
// MICRO-FIX offsets instáveis: handlers dinâmicos reordenam os offsets entre
// duas QueryContextMenu (sonda headless + shell-menu.log: offset 112 =
// "Comprimir e enviar por email" numa query e outra coisa noutra), então o
// invoke prefere o VERBO (HIWORD(lpVerbW)!=0 = string, imune a reordenação).
// Offset + GCS_VALIDATEW viraram fallback p/ verbo vazio/nulo (raro; risco
// documentado no engine do ShellHost). O invoke reexecuta o MESMO
// pipeline/flags na MESMA interface IContextMenu raiz (QueryContextMenu →
// InvokeCommand) na STA dedicada — agora dentro do processo host.
// Dado puro (sem COM, sem thread) — o COM vive só dentro do host.
public sealed record ThirdPartyInvokeHandle(
    string Path,
    string Verb,
    uint CommandOffset,
    bool IncludeExtendedVerbs);
