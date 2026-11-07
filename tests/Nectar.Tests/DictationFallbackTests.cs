using Nectar.Services.Dictation;
using Xunit;

namespace Nectar.Tests;

/// <summary>
/// The fallback policy is the part of dictation that can be wrong in ways users notice: a chip that
/// does nothing, a missing explanation, or Win + H firing when in-app dictation actually worked.
/// </summary>
public sealed class DictationFallbackTests
{
    // The outcome enum is app-internal, so the public theory passes its numeric value.
    [Theory]
    [InlineData((int)DictationOutcome.Started)]
    [InlineData((int)DictationOutcome.Stopped)]
    public void ActiveSessions_DoNotFallBack(int value)
    {
        var plan = DictationFallback.For((DictationOutcome)value);

        Assert.False(plan.UseWindowsVoiceTyping);
        Assert.Null(plan.Banner);
        Assert.Null(plan.LogMessage);
    }

    [Fact]
    public void DeniedMicrophone_FallsBack_AndPointsAtThePrivacyPage()
    {
        var plan = DictationFallback.For(DictationOutcome.MicrophoneDenied);

        Assert.True(plan.UseWindowsVoiceTyping);
        Assert.NotNull(plan.Banner);
        Assert.Contains("Privacy", plan.Banner);
        Assert.Contains("Microphone", plan.Banner);
        Assert.Contains("Win + H", plan.Banner);
        Assert.Null(plan.LogMessage);
    }

    [Fact]
    public void PolicyBlock_FallsBack_WithAnOrganisationExplanation()
    {
        var plan = DictationFallback.For(DictationOutcome.MicrophoneBlocked);

        Assert.True(plan.UseWindowsVoiceTyping);
        Assert.NotNull(plan.Banner);
        Assert.Contains("organisation", plan.Banner!.ToLowerInvariant());
        Assert.Contains("Win + H", plan.Banner);
    }

    [Theory]
    [InlineData((int)DictationOutcome.Unavailable)]
    [InlineData((int)DictationOutcome.Failed)]
    [InlineData(99)]
    public void UnexpectedFailures_StillFallBack_AndAreLogged(int value)
    {
        var plan = DictationFallback.For((DictationOutcome)value);

        Assert.True(plan.UseWindowsVoiceTyping);
        Assert.Null(plan.Banner);
        Assert.NotNull(plan.LogMessage);
        Assert.Contains("Win + H", plan.LogMessage);
    }

    [Fact]
    public void EveryOutcome_DecidesWhetherToFallBack()
    {
        var plans = Enum.GetValues<DictationOutcome>()
            .Select(DictationFallback.For)
            .ToList();

        Assert.Equal(6, plans.Count);
        Assert.Equal(2, plans.Count(p => !p.UseWindowsVoiceTyping));
        Assert.All(
            plans.Where(p => p.UseWindowsVoiceTyping),
            p => Assert.True(p.Banner is not null || p.LogMessage is not null, "a fallback must be explained somewhere"));
    }
}
