namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

// T2 terceiros: detecção SÓ LEITURA dos verbos clássicos de terceiros de um
// item (.lnk = o próprio link) ou pasta. Falha = lista vazia (a UI mantém o
// placeholder T1, silencioso). NENHUMA execução nesta fase (T3 invoca).
public interface IThirdPartyMenuService
{
    IReadOnlyList<ThirdPartyMenuEntry> GetForPath(string? path);
}
