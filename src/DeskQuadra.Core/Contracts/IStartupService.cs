namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato do serviço "iniciar com o Windows" (Run do HKCU).
/// Registry é a verdade; quadras.json guarda só layout.
/// </summary>
public interface IStartupService
{
    const string ValueName = "DeskQuadra";

    const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    const string SilentArgument = "--silent";

    bool IsEnabled();

    void SetEnabled(bool enabled);
}
