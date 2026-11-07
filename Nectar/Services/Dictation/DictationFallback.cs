namespace Nectar.Services.Dictation;

/// <summary>What the dictation chip produced. Kept UI-free so the fallback policy is testable.</summary>
internal enum DictationOutcome
{
    Started,
    Stopped,
    MicrophoneDenied,
    MicrophoneBlocked,
    Unavailable,
    Failed,
}

/// <summary>The decision for a chip press: use Windows Voice Typing, and what to tell the user.</summary>
internal sealed record DictationFallbackPlan(bool UseWindowsVoiceTyping, string? Banner, string? LogMessage);

/// <summary>
/// The single place that decides what happens when in-app dictation cannot run. Pulled out of the
/// window so the policy — which outcomes fall back, and what the user is told — is covered by tests
/// rather than only by clicking a mic chip on a machine that happens to have a microphone.
/// </summary>
internal static class DictationFallback
{
    internal const string DeniedBanner =
        "Microphone access is off for desktop apps. Windows Voice Typing (Win + H) is starting " +
        "instead — turn it on in Settings → Privacy → Microphone to dictate here.";

    internal const string BlockedBanner =
        "Your organisation blocks microphone access. Windows Voice Typing (Win + H) is the fallback.";

    internal static DictationFallbackPlan For(DictationOutcome outcome) => outcome switch
    {
        DictationOutcome.Started or DictationOutcome.Stopped =>
            new DictationFallbackPlan(false, null, null),

        DictationOutcome.MicrophoneDenied =>
            new DictationFallbackPlan(true, DeniedBanner, null),

        DictationOutcome.MicrophoneBlocked =>
            new DictationFallbackPlan(true, BlockedBanner, null),

        DictationOutcome.Unavailable =>
            new DictationFallbackPlan(
                true,
                null,
                "In-app dictation unavailable; falling back to Win + H"),

        DictationOutcome.Failed =>
            new DictationFallbackPlan(
                true,
                null,
                "In-app dictation failed; falling back to Win + H"),

        _ => new DictationFallbackPlan(true, null, "Unknown dictation outcome; falling back to Win + H"),
    };
}
