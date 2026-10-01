namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

// T2 terceiros: item filtrado "só terceiros" pronto p/ espelhar no menu WPF.
// Label já limpo (sem '&'/'\t'). CommandOffset = offset do QueryContextMenu
// (id - idCmdFirst), guardado como dado p/ o InvokeCommand da T3 — NENHUMA
// execução nesta fase. Cascata = Children não vazio (submenu desabilitado).
// T2 exibe tudo desabilitado; T3 habilita + invoca.
public sealed record ThirdPartyMenuEntry(
    string Label,
    string Verb,
    uint CommandOffset,
    IReadOnlyList<ThirdPartyMenuEntry> Children);
