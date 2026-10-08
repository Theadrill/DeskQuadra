namespace DeskQuadra.Infrastructure.WindowsShell.Shell;

/// <summary>
/// Argumentos de evento disparado quando um container requer um aplicativo externo
/// (como 7-Zip ou WinRAR) que não foi localizado na máquina.
/// </summary>
public sealed class ArchiveToolMissingEventArgs : EventArgs
{
    public string ContainerPath { get; }
    public string Extension { get; }
    public string RequiredToolName { get; }

    public ArchiveToolMissingEventArgs(string containerPath, string extension, string requiredToolName)
    {
        ContainerPath = containerPath;
        Extension = extension;
        RequiredToolName = requiredToolName;
    }
}
