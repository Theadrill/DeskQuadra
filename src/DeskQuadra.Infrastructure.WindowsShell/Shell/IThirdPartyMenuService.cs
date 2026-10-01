using Vanara.PInvoke;

namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

// T3 terceiros: detecção (só leitura) + execução dos verbos clássicos de
// terceiros de um item (.lnk = o próprio link) ou pasta.
// GetForPath = lista (falha = lista vazia, a UI mantém o placeholder T1,
// silencioso). CreateHandle + TryInvoke = executa por VERBO canônico estável
// na STA dedicada (query + invoke na MESMA interface raiz); offset +
// GCS_VALIDATEW são fallback p/ verbo vazio (raro, risco documentado).
public interface IThirdPartyMenuService
{
    IReadOnlyList<ThirdPartyMenuEntry> GetForPath(string? path);

    // Alça resolvida junto com a query: captura verbo + Shift/EXTENDEDVERBS
    // usados na listagem daquele caminho (o Shift do clique pode já ter sido
    // solto). Verbo vazio = fallback por offset.
    ThirdPartyInvokeHandle? CreateHandle(string? path, string? verb, uint commandOffset);

    // Executa fora da UI do WPF (STA dedicada + Join com timeout); hwnd =
    // dono das UIs do handler (diálogos), ponto = cursor no momento do clique.
    // Retorna false em qualquer falha (silencioso, padrão do projeto).
    bool TryInvoke(ThirdPartyInvokeHandle? handle, IntPtr hwnd, POINT? invokePoint);
}
