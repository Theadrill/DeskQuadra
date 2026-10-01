namespace DeskQuadra.Core.FileDeletion;

/// <summary>
/// Ação de exclusão decidida pelo fluxo do menu "Excluir" do ícone.
/// </summary>
public enum ItemDeleteAction
{
    Recycle,
    Permanent
}

/// <summary>
/// Lógica pura do fluxo Excluir (sem Win32/UI/timers): mapeia o índice do 1º diálogo
/// (<c>DarkDialog.ShowOptions</c>: 0 = Lixeira, 1 = Permanentemente, outro/-1 = Cancelar/fechar)
/// mais a 2ª confirmação para a ação final (ou null = não fazer nada).
/// Lixeira executa direto; Permanente exige a 2ª confirmação.
/// Coberta por xUnit.
/// </summary>
public static class ItemDeletionPlan
{
    public static ItemDeleteAction? Resolve(int firstDialogIndex, bool permanentConfirmed) =>
        firstDialogIndex switch
        {
            0 => ItemDeleteAction.Recycle,
            1 => permanentConfirmed ? ItemDeleteAction.Permanent : null,
            _ => null,
        };
}
