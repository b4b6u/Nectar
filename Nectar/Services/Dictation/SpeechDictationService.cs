using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Windows.Media.SpeechRecognition;
using Windows.Security.Authorization.AppCapabilityAccess;

namespace Nectar.Services.Dictation;

/// <summary>
/// In-app dictation using <see cref="SpeechRecognizer"/> with the platform dictation grammar.
///
/// The microphone is never assumed. Access is checked (and requested once) through
/// <see cref="AppCapability"/> before a recognizer exists; everything else — a denied privacy
/// switch, a blocked policy, a missing grammar, a failed compile — returns an outcome instead of
/// throwing, so the caller can fall back to Windows Voice Typing (Win+H) rather than dead-ending
/// the mic chip.
///
/// Results arrive on a WinRT background thread and are marshalled through
/// <see cref="UiThread.Post"/> before touching a <see cref="TextBox"/>.
/// </summary>
internal sealed class SpeechDictationService : IDisposable
{
    private readonly DispatcherQueue _dispatcher;

    private SpeechRecognizer? _recognizer;
    private TextBox? _target;
    private bool _handlersAttached;
    private bool _listening;
    private bool _disposed;

    internal SpeechDictationService(DispatcherQueue dispatcher) => _dispatcher = dispatcher;

    internal bool IsListening => _listening;

    /// <summary>
    /// Human-readable microphone state for the Settings screen. Never prompts: opening settings
    /// must not raise a consent dialog. Falls back to Win + H is spelled out because that is what
    /// the mic chips will do.
    /// </summary>
    internal async Task<string> DescribeMicrophoneAsync()
    {
        var status = await EnsureMicrophoneAsync(prompt: false).ConfigureAwait(true);
        return status switch
        {
            MicrophoneStatus.Allowed => "Allowed — Nectar can dictate in-app",
            MicrophoneStatus.Denied => "Off for desktop apps — Win + H fallback",
            MicrophoneStatus.Blocked => "Blocked by policy — Win + H fallback",
            _ => "Unavailable on this build — Win + H fallback",
        };
    }

    /// <summary>
    /// Starts dictation into <paramref name="target"/>, or stops an active session when the same
    /// chip is pressed again (a toggle, matching Win+H's behaviour).
    /// </summary>
    internal async Task<DictationOutcome> ToggleAsync(TextBox target)
    {
        if (_disposed)
        {
            return DictationOutcome.Unavailable;
        }

        if (_listening)
        {
            await StopAsync().ConfigureAwait(true);
            return DictationOutcome.Stopped;
        }

        var microphone = await EnsureMicrophoneAsync().ConfigureAwait(true);
        if (microphone == MicrophoneStatus.Denied)
        {
            return DictationOutcome.MicrophoneDenied;
        }

        if (microphone == MicrophoneStatus.Blocked)
        {
            return DictationOutcome.MicrophoneBlocked;
        }

        try
        {
            _recognizer ??= await CreateRecognizerAsync().ConfigureAwait(true);
            if (_recognizer is null)
            {
                return DictationOutcome.Unavailable;
            }

            _target = target;
            AttachHandlers(_recognizer.ContinuousRecognitionSession);

            await _recognizer.ContinuousRecognitionSession.StartAsync().AsTask().ConfigureAwait(true);
            _listening = true;
            AppLogger.Log("In-app dictation started");
            return DictationOutcome.Started;
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLogger.Log("Windows refused microphone access to Nectar", ex);
            return DictationOutcome.MicrophoneDenied;
        }
        catch (Exception ex)
        {
            // Includes the privacy-switch COMException (0x80045509) and a recognizer that cannot
            // reach its speech service. Either way the caller falls back to Win+H.
            AppLogger.Log("In-app dictation could not start", ex);
            return DictationOutcome.Unavailable;
        }
    }

    internal async Task StopAsync()
    {
        var recognizer = _recognizer;
        if (recognizer is null)
        {
            return;
        }

        try
        {
            await recognizer.ContinuousRecognitionSession.StopAsync().AsTask().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppLogger.Log("Stopping dictation failed", ex);
        }
        finally
        {
            DetachHandlers(recognizer.ContinuousRecognitionSession);
            _listening = false;
            _target = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (_recognizer is not null)
            {
                DetachHandlers(_recognizer.ContinuousRecognitionSession);
                _recognizer.Dispose();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("Speech recognizer dispose failed", ex);
        }
        finally
        {
            _recognizer = null;
            _listening = false;
            _target = null;
        }
    }

    private static async Task<MicrophoneStatus> EnsureMicrophoneAsync(bool prompt = true)
    {
        // AppCapability exists from 18362; Nectar's minimum is 17763, so guard rather than warn.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362))
        {
            AppLogger.Log("AppCapability is unavailable on this Windows build; trying the recognizer.");
            return MicrophoneStatus.Unknown;
        }

        try
        {
            var capability = AppCapability.Create("microphone");
            var status = capability.CheckAccess();

            // Prompt at most when the user has not decided, and only for an actual dictation
            // attempt; a status read must never pop a consent dialog.
            if (prompt && status == AppCapabilityAccessStatus.DeniedByUser)
            {
                status = await capability.RequestAccessAsync();
            }

            return status switch
            {
                AppCapabilityAccessStatus.Allowed => MicrophoneStatus.Allowed,
                AppCapabilityAccessStatus.DeniedByUser => MicrophoneStatus.Denied,
                AppCapabilityAccessStatus.DeniedBySystem => MicrophoneStatus.Blocked,
                _ => MicrophoneStatus.Unknown,
            };
        }
        catch (Exception ex)
        {
            // Unpackaged runs have no capability record for the process. The recognizer still
            // enforces Windows' privacy switch, so try it and let it report the failure.
            AppLogger.Log("Microphone capability check unavailable; trying the recognizer", ex);
            return MicrophoneStatus.Unknown;
        }
    }

    private static async Task<SpeechRecognizer?> CreateRecognizerAsync()
    {
        var recognizer = new SpeechRecognizer();

        try
        {
            recognizer.Constraints.Add(
                new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "dictation"));

            var compilation = await recognizer.CompileConstraintsAsync();
            if (compilation.Status != SpeechRecognitionResultStatus.Success)
            {
                AppLogger.Log($"Dictation grammar unavailable: {compilation.Status}");
                recognizer.Dispose();
                return null;
            }

            try
            {
                // Stop after a pause instead of listening forever behind a hidden overlay.
                recognizer.ContinuousRecognitionSession.AutoStopSilenceTimeout = TimeSpan.FromSeconds(12);
            }
            catch (Exception ex)
            {
                AppLogger.Log("Could not set the dictation silence timeout", ex);
            }

            return recognizer;
        }
        catch (Exception ex)
        {
            AppLogger.Log("Speech recognizer could not be created", ex);
            recognizer.Dispose();
            return null;
        }
    }

    private void AttachHandlers(SpeechContinuousRecognitionSession session)
    {
        if (_handlersAttached)
        {
            return;
        }

        session.ResultGenerated += OnResultGenerated;
        session.Completed += OnSessionCompleted;
        _handlersAttached = true;
    }

    private void DetachHandlers(SpeechContinuousRecognitionSession session)
    {
        if (!_handlersAttached)
        {
            return;
        }

        session.ResultGenerated -= OnResultGenerated;
        session.Completed -= OnSessionCompleted;
        _handlersAttached = false;
    }

    private void OnResultGenerated(
        SpeechContinuousRecognitionSession sender,
        SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
        if (args.Result.Confidence == SpeechRecognitionConfidence.Rejected)
        {
            return;
        }

        var text = args.Result.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        UiThread.Post(_dispatcher, () => AppendTranscript(text), "Dictation: append transcript");
    }

    private void OnSessionCompleted(
        SpeechContinuousRecognitionSession sender,
        SpeechContinuousRecognitionCompletedEventArgs args)
    {
        _listening = false;
        DetachHandlers(sender);

        if (args.Status is not (SpeechRecognitionResultStatus.Success or SpeechRecognitionResultStatus.TimeoutExceeded))
        {
            AppLogger.Log($"Dictation session ended: {args.Status}");
        }
    }

    /// <summary>Inserts the transcript at the caret, one separating space when one is needed.</summary>
    private void AppendTranscript(string text)
    {
        var box = _target;
        if (box is null)
        {
            return;
        }

        var existing = box.Text ?? string.Empty;
        var caret = Math.Clamp(box.SelectionStart, 0, existing.Length);
        var separator = caret > 0 && !char.IsWhiteSpace(existing[caret - 1]) ? " " : string.Empty;
        var insert = separator + text.Trim();

        box.Text = existing.Insert(caret, insert);
        box.SelectionStart = caret + insert.Length;
    }

    private enum MicrophoneStatus
    {
        Allowed,
        Denied,
        Blocked,
        Unknown,
    }
}
