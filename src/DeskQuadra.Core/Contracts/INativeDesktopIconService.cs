namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato de controle de visibilidade da camada de ícones nativos da área de trabalho do Windows.
/// </summary>
public interface INativeDesktopIconService : IDisposable
{
    bool AreIconsHidden { get; }

    bool HideDesktopIcons();

    bool ShowDesktopIcons();
}
