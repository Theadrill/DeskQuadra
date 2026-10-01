namespace DeskQuadra.Core.ThirdParty;

// T2 terceiros (movido em T5 p/ o Core, sem mudar forma): nó BRUTO da
// enumeração do HMENU fantasma (antes do filtro §1). Produzido no ShellHost,
// filtrado lá mesmo; viaja serializado no protocolo e vira ThirdPartyMenuEntry.
// IsSeparator = MFT_SEPARATOR; IsPopup = tem hSubMenu (cascata, ex.: 7-Zip);
// folha sem texto exibível cai no Build (sem Header não há o que espelhar).
public sealed record ShellMenuNode(
    string Label,
    string? Verb,
    uint CommandOffset,
    bool IsSeparator,
    bool IsPopup,
    IReadOnlyList<ShellMenuNode> Children)
{
    public static ShellMenuNode Separator { get; } =
        new(string.Empty, null, 0, IsSeparator: true, IsPopup: false, Array.Empty<ShellMenuNode>());
}
