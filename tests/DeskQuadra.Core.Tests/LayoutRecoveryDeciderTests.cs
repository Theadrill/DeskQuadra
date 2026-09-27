using DeskQuadra.Infrastructure.Persistence.Repositories;
using Xunit;

namespace DeskQuadra.Core.Tests;

/// <summary>
/// Decisão pura (qual fonte carregar dado .tmp/.json/.bak + validez + timestamps).
/// Sem I/O, sem UI; o diálogo em si não tem teste.
/// </summary>
public class LayoutRecoveryDeciderTests
{
    private static readonly DateTime Older = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Newer = new(2026, 9, 26, 12, 5, 0, DateTimeKind.Utc);

    [Fact]
    public void Decide_WithoutTmp_ProceedsNormal()
    {
        var action = LayoutRecoveryDecider.Decide(
            tmpExists: false, tmpValid: false, tmpWriteUtc: DateTime.MinValue,
            jsonExists: true, jsonValid: true, jsonWriteUtc: Older);

        Assert.Equal(LayoutRecoveryAction.ProceedNormal, action);
    }

    [Fact]
    public void Decide_WithCorruptedTmp_DiscardsSilently()
    {
        var action = LayoutRecoveryDecider.Decide(
            tmpExists: true, tmpValid: false, tmpWriteUtc: Newer,
            jsonExists: true, jsonValid: true, jsonWriteUtc: Older);

        Assert.Equal(LayoutRecoveryAction.DiscardTmpKeepJson, action);
    }

    [Fact]
    public void Decide_WithIntactTmpNewerThanJson_PromptsUser()
    {
        var action = LayoutRecoveryDecider.Decide(
            tmpExists: true, tmpValid: true, tmpWriteUtc: Newer,
            jsonExists: true, jsonValid: true, jsonWriteUtc: Older);

        Assert.Equal(LayoutRecoveryAction.PromptUser, action);
    }

    [Fact]
    public void Decide_WithIntactTmpOlderThanJson_DiscardsWithoutDialog()
    {
        var action = LayoutRecoveryDecider.Decide(
            tmpExists: true, tmpValid: true, tmpWriteUtc: Older,
            jsonExists: true, jsonValid: true, jsonWriteUtc: Newer);

        Assert.Equal(LayoutRecoveryAction.DiscardTmpKeepJson, action);
    }

    [Fact]
    public void Decide_WithIntactTmpSameTimestamp_PromptsUser()
    {
        var action = LayoutRecoveryDecider.Decide(
            tmpExists: true, tmpValid: true, tmpWriteUtc: Older,
            jsonExists: true, jsonValid: true, jsonWriteUtc: Older);

        Assert.Equal(LayoutRecoveryAction.PromptUser, action);
    }

    [Fact]
    public void Decide_WithIntactTmpAndMissingJson_PromotesAutomatically()
    {
        var action = LayoutRecoveryDecider.Decide(
            tmpExists: true, tmpValid: true, tmpWriteUtc: Older,
            jsonExists: false, jsonValid: false, jsonWriteUtc: DateTime.MinValue);

        Assert.Equal(LayoutRecoveryAction.PromoteTmpAuto, action);
    }

    [Fact]
    public void Decide_WithIntactTmpAndCorruptedJson_PromotesAutomatically()
    {
        var action = LayoutRecoveryDecider.Decide(
            tmpExists: true, tmpValid: true, tmpWriteUtc: Older,
            jsonExists: true, jsonValid: false, jsonWriteUtc: Newer);

        Assert.Equal(LayoutRecoveryAction.PromoteTmpAuto, action);
    }
}
