using DeskQuadra.Core.ThirdParty;
using Vanara.PInvoke;

namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

// T3 terceiros (T5: mesma interface, motor no ShellHost): detecção (só
// leitura) + execução dos verbos clássicos de terceiros de um item
// (.lnk = o próprio link) ou pasta.
// GetForPath = lista (falha = lista vazia, a UI mantém o placeholder T1,
// silencioso). CreateHandle + TryInvoke = executa por VERBO canônico estável
// na STA dedicada (query + invoke na MESMA interface raiz); offset +
// GCS_VALIDATEW são fallback p/ verbo vazio (raro, risco documentado).
public interface IThirdPartyMenuService
{
    // background = menu de FUNDO da pasta (espaço vazio/barra): query/invoke
    // no IContextMenu da própria pasta, com cache separado ("<background>",
    // nunca "<folder>"). Default false = item (intacto).
    IReadOnlyList<ThirdPartyMenuEntry> GetForPath(string? path, bool background = false);

    // Alça resolvida junto com a query: captura verbo + Shift/EXTENDEDVERBS
    // usados na listagem daquele caminho (o Shift do clique pode já ter sido
    // solto). Verbo vazio = fallback por rótulos (invoke-by-label) ou, sem
    // rótulos, por offset (legado). labelPath = rótulos do caminho como o
    // usuário VIU (entry.Label + ancestrais, já CleanLabelForDisplay) — a UI
    // passa; o host compara exato após CleanLabelForDisplay nos dois lados.
    ThirdPartyInvokeHandle? CreateHandle(string? path, string? verb, uint commandOffset, IReadOnlyList<string>? labelPath = null, bool background = false);

    // Executa fora da UI do WPF (STA dedicada + Join com timeout); hwnd =
    // dono das UIs do handler (diálogos), ponto = cursor no momento do clique.
    // Retorna false em qualquer falha (silencioso, padrão do projeto).
    bool TryInvoke(ThirdPartyInvokeHandle? handle, IntPtr hwnd, POINT? invokePoint);

    // T8c refresh manual do tray: limpa buckets item/fundo/extended; próxima
    // abertura refaz query.
    void ClearCache();
}
