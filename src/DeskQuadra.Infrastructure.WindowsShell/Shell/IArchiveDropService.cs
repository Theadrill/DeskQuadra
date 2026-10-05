namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

// F3 drop-em-container (docs/PLANO_DROP_CONTAINER.md): adiciona arquivos a um
// arquivo container delegando ao DropHandler registrado no Shell (zip nativo,
// WinRAR, 7-Zip) via ShellHost isolado. Sem codec próprio; sem handler = false
// silencioso (a F4 assume com fallback .zip nativo). Efeito sempre Copy
// (adiciona ao arquivo, origem preservada — igual ao Explorer).
public interface IArchiveDropService
{
    // Retorna true se o handler aceitou e processou o drop. Qualquer falha
    // (container inválido, sem fontes existentes, sem handler, timeout) = false.
    bool TryAddToContainer(string? containerPath, IReadOnlyList<string>? sourcePaths);
}
