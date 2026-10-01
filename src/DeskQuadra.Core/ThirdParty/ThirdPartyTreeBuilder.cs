namespace DeskQuadra.Core.ThirdParty;

// T2 terceiros (movido em T5 p/ o Core, sem mudar regra): aplica o filtro §1
// sobre a árvore bruta do HMENU fantasma. Roda DENTRO do ShellHost (a UI só
// espelha o JSON já filtrado) — definição única, sem duplicar nas duas pontas.
// Puro/testável: separador fora; folha nativa (verbo OU label) fora; folha sem
// texto exibível fora; cascata sobrevive só com ≥1 filho mantido (espelhada
// como submenu desabilitado); resto (inclusive verbo vazio) = terceiro.
public static class ThirdPartyTreeBuilder
{
    public static IReadOnlyList<ThirdPartyMenuEntry> Build(IReadOnlyList<ShellMenuNode>? nodes)
    {
        var result = new List<ThirdPartyMenuEntry>();
        if (nodes is null)
        {
            return result;
        }

        foreach (var node in nodes)
        {
            var entry = BuildOne(node);
            if (entry is not null)
            {
                result.Add(entry);
            }
        }

        return result;
    }

    private static ThirdPartyMenuEntry? BuildOne(ShellMenuNode node)
    {
        if (node.IsSeparator)
        {
            return null;
        }

        if (node.IsPopup)
        {
            // Cascata nativa ("Enviar para", "Incluir na biblioteca") cai INTEIRA
            // pelo próprio rótulo — os filhos genéricos ("Documentos", ...) não
            // precisam (nem podem) entrar na denylist.
            string popupVerb = node.Verb ?? string.Empty;
            if (ThirdPartyVerbFilter.IsBlockedVerb(popupVerb)
                || ThirdPartyVerbFilter.IsNativeLabel(node.Label))
            {
                return null;
            }

            var children = Build(node.Children);
            if (children.Count == 0)
            {
                return null;
            }

            string label = ThirdPartyVerbFilter.CleanLabelForDisplay(node.Label);
            if (label.Length == 0)
            {
                return null;
            }

            return new ThirdPartyMenuEntry(label, string.Empty, 0, children);
        }

        string display = ThirdPartyVerbFilter.CleanLabelForDisplay(node.Label);
        if (display.Length == 0)
        {
            return null;
        }

        string verb = node.Verb ?? string.Empty;
        if (!ThirdPartyVerbFilter.IsThirdParty(verb, node.Label, isSeparator: false))
        {
            return null;
        }

        return new ThirdPartyMenuEntry(display, verb, node.CommandOffset, Array.Empty<ThirdPartyMenuEntry>());
    }
}
