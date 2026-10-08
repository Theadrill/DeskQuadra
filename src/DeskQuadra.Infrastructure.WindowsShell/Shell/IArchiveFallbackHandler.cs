namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

/// <summary>
/// Contrato para execução de fallback de adição a contêineres quando o Shell
/// não possui um DropHandler registrado (Fase F4 do `docs/PLANO_DROP_CONTAINER.md`).
/// </summary>
public interface IArchiveFallbackHandler
{
    /// <summary>
    /// Tenta adicionar os arquivos fontes ao contêiner utilizando .NET Zip nativo ou ferramentas instaladas (7-Zip, WinRAR).
    /// </summary>
    /// <param name="containerPath">Caminho completo do contêiner.</param>
    /// <param name="sourcePaths">Lista de arquivos/pastas a adicionar.</param>
    /// <param name="missingToolName">Nome da ferramenta necessária caso falhe por ausência de compactador.</param>
    /// <returns>True se adicionado com sucesso; caso contrário False.</returns>
    bool TryAddToArchive(string containerPath, IReadOnlyList<string> sourcePaths, out string? missingToolName);
}
