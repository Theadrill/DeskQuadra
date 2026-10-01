using DeskQuadra.Core.FileDeletion;

namespace DeskQuadra.Core.Tests;

/// <summary>
/// Matriz do fluxo Excluir do ícone: Lixeira direto, Permanente só com 2ª confirmação, resto nada.
/// Lógica pura (sem Win32/UI/timers).
/// </summary>
public sealed class ItemDeletionPlanTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Recycle_ExecutesDirectly_WithoutSecondConfirmation(bool permanentConfirmed)
    {
        Assert.Equal(ItemDeleteAction.Recycle, ItemDeletionPlan.Resolve(0, permanentConfirmed));
    }

    [Fact]
    public void Permanent_WithSecondConfirmation_Executes()
    {
        Assert.Equal(ItemDeleteAction.Permanent, ItemDeletionPlan.Resolve(1, true));
    }

    [Fact]
    public void Permanent_WithoutSecondConfirmation_DoesNothing()
    {
        Assert.Null(ItemDeletionPlan.Resolve(1, false));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(-1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(99, true)]
    public void CancelOrClosed_DoesNothing(int firstDialogIndex, bool permanentConfirmed)
    {
        Assert.Null(ItemDeletionPlan.Resolve(firstDialogIndex, permanentConfirmed));
    }
}
