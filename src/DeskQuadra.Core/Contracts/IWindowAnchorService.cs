namespace DeskQuadra.Core.Contracts;

/// <summary>
/// Contrato de serviço para ancoragem de janelas ao nível do desktop (WorkerW/Progman)
/// garantindo imunidade ao comando Win + D e ocultação da barra de tarefas.
/// </summary>
public interface IWindowAnchorService
{
    /// <summary>
    /// Ancola o identificador de janela (HWND) na camada de papel de parede do Windows.
    /// </summary>
    /// <param name="windowHandle">Identificador nativo HWND da janela.</param>
    /// <returns>True se a ancoragem foi realizada com sucesso; False caso contrário.</returns>
    bool AnchorToDesktop(IntPtr windowHandle);

    /// <summary>
    /// Desancola o identificador de janela, restaurando sua hierarquia padrão do Windows.
    /// </summary>
    /// <param name="windowHandle">Identificador nativo HWND da janela.</param>
    /// <returns>True se desancorado com sucesso.</returns>
    bool UnanchorFromDesktop(IntPtr windowHandle);
}
