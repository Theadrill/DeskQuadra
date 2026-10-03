namespace DeskQuadra.Core.ThirdParty;

// T2 terceiros (movido em T5 p/ o Core, sem mudar regra) + COMPLEMENTOS
// NATIVOS (T6): aplica o filtro §1 sobre a árvore bruta do HMENU fantasma.
// Roda DENTRO do ShellHost (a UI só espelha o JSON já filtrado) — definição
// única, sem duplicar nas duas pontas.
// Puro/testável: separador fora; COMPLEMENTO (IsNativeComplement) passa ANTES
// da denylist; folha nativa (verbo OU label) fora; folha sem texto exibível
// fora; cascata sobrevive só com ≥1 filho mantido — EXCETO cascata-complemento
// ("Enviar para"/"Abrir com"/"Transmitir"), mantida MESMO VAZIA (fallback p/
// handler que nem o WM_INITMENUPOPUP do engine preenche — ex. exige HWND
// real; o invoke resolve na re-query do host; ver nota HandleMenuMsg abaixo); resto (inclusive verbo
// vazio) = terceiro.
// INVOKE (reuso, sem caminho novo): folhas verbais usam o caminho estável por
// VERBO (InvokeByVerb no ShellHost — imune a reordenação de offsets); o
// "Transmitir para Dispositivo" SEM verbo cai no fallback por offset já
// existente (BuildInvokeInfo + GCS_VALIDATEW) — RISCO DOCUMENTADO no engine:
// offsets mudam entre duas QueryContextMenu e o VALIDATEW só barra offset
// inexistente, NÃO verbo trocado (caso Firefox.7z). "Abrir com" (verbo openas)
// abre o diálogo do sistema pelo caminho-verbo; "Enviar para" abre a cascata
// (filhos invocam pelo caminho existente).
// FALLBACK MANTIDO (cascata-complemento vazia continua passando): o engine
// agora envia WM_INITMENUPOPUP via IContextMenu3/2 antes de enumerar (qualquer
// cascata lazy, sem hardcoded); se mesmo assim vier vazia (handler exige HWND
// real ou falha no populate), o item é mantido (desabilitado quando sem verbo
// estável nem offset válido, p/ nunca invocar offset errado) e a limitação
// vai no retorno ao PO.
public static class ThirdPartyTreeBuilder
{
    // background = menu de FUNDO da pasta (espaço vazio/barra): complementos
    // nativos NÃO passam (só terceiro genuíno); ITEM intacto (default false).
    public static IReadOnlyList<ThirdPartyMenuEntry> Build(IReadOnlyList<ShellMenuNode>? nodes, bool background = false)
    {
        var result = new List<ThirdPartyMenuEntry>();
        if (nodes is null)
        {
            return result;
        }

        foreach (var node in nodes)
        {
            var entry = BuildOne(node, background);
            if (entry is not null)
            {
                result.Add(entry);
            }
        }

        return result;
    }

    private static ThirdPartyMenuEntry? BuildOne(ShellMenuNode node, bool background = false)
    {
        if (node.IsSeparator)
        {
            return null;
        }

        if (node.IsPopup)
        {
            // COMPLEMENTO vence ANTES da denylist (§ complemento): popup
            // "Enviar para"/"Abrir com" (verbo implícito sendto/openas — o
            // popup real chega com Verb=null) e "Transmitir para Dispositivo"
            // (label-only, sem verbo nem no filho) passam aqui.
            // Cascata nativa restante ("Incluir na biblioteca", etc.) cai
            // INTEIRA pelo próprio rótulo — os filhos genéricos ("Documentos",
            // ...) não precisam (nem podem) entrar na denylist.
            string popupVerb = node.Verb ?? string.Empty;
            if (ThirdPartyVerbFilter.IsNativeComplement(
                string.IsNullOrWhiteSpace(popupVerb)
                    ? ThirdPartyVerbFilter.TryGetImplicitPopupVerb(node.Label)
                    : popupVerb,
                node.Label))
            {
                // FUNDO: complemento nativo NÃO autoriza — cai inteiro (mesmo
                // cheio); só terceiro genuíno passa (via caminho abaixo).
                if (background)
                {
                    return null;
                }

                var kept = Build(node.Children, background);
                string complementLabel =
                    ThirdPartyVerbFilter.CleanLabelForDisplay(node.Label);
                if (complementLabel.Length == 0)
                {
                    return null;
                }

                if (kept.Count != 0)
                {
                    // Cascata populada (SYNCCASCADEMENU): submenu normal; os
                    // filhos invocam pelo caminho existente (verbo estável
                    // quando há; offset+VALIDATEW no Transmitir sem verbo).
                    return new ThirdPartyMenuEntry(complementLabel, string.Empty, 0, kept);
                }

                // Cascata-complemento VAZIA/lazy (SendTo sem HandleMenuMsg):
                // mantém o item mesmo assim — o invoke resolve na re-query.
                // Sem verbo estável e sem offset válido o handle sai nulo e a
                // UI mostra DESABILITADO (nunca invoca offset errado); com verbo
                // implícito (sendto/openas) o handle usa o caminho-verbo.
                string? implicitVerb =
                    ThirdPartyVerbFilter.TryGetImplicitPopupVerb(node.Label);
                if (!string.IsNullOrEmpty(implicitVerb))
                {
                    return new ThirdPartyMenuEntry(
                        complementLabel, implicitVerb, 0, Array.Empty<ThirdPartyMenuEntry>());
                }

                // Sem verbo (Transmitir vazio — raro, a sonda mostra 1 filho):
                // offset fora da faixa = CreateHandle devolve nulo = item
                // desabilitado, seguro. NÃO usar offset 0 (invocaria o vizinho).
                return new ThirdPartyMenuEntry(
                    complementLabel, string.Empty, 0xFFFF, Array.Empty<ThirdPartyMenuEntry>());
            }

            if (ThirdPartyVerbFilter.IsBlockedVerb(popupVerb)
                || ThirdPartyVerbFilter.IsNativeLabel(node.Label))
            {
                return null;
            }

            var children = Build(node.Children, background);
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
        if (!ThirdPartyVerbFilter.IsThirdParty(verb, node.Label, isSeparator: false, background: background))
        {
            return null;
        }

        return new ThirdPartyMenuEntry(display, verb, node.CommandOffset, Array.Empty<ThirdPartyMenuEntry>());
    }
}
