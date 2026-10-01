namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

// T2 terceiros: nó BRUTO da enumeração do HMENU fantasma (antes do filtro §1).
// IsSeparator = MFT_SEPARATOR; IsPopup = tem hSubMenu (cascata, ex.: 7-Zip);
// folha sem texto exibível cai no Build (sem Header não há o que espelhar).
internal sealed record ShellMenuNode(
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
