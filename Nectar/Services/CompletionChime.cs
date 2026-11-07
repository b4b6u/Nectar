using Windows.Media.Core;
using Windows.Media.Playback;

namespace Nectar.Services;

/// <summary>
/// The bird call that marks the end of a focus session. It is deliberately independent of the
/// overlay: the timer fires it whether the workspace is open, folded to the pill, or parked in
/// the background, so the chime is the one signal the user can rely on.
///
/// Drop the audio file into <c>Assets</c> as <c>bird-sound.wav</c> (or <c>.mp3</c>/<c>.m4a</c>);
/// the csproj copies any of those next to the executable. Until a file is present the service
/// logs one hint and falls back to the system alert tone so the session still ends audibly.
/// </summary>
public sealed class CompletionChime : IDisposable
{
    /// <summary>File names looked up in <c>Assets</c>, in preference order.</summary>
    private static readonly string[] CandidateNames =
    {
        "bird-sound.wav",
        "bird-sound.mp3",
        "bird-sound.m4a",
    };

    private MediaPlayer? _player;
    private bool _resolved;
    private string? _sourcePath;
    private bool _missingLogged;
    private bool _disposed;

    /// <summary>Full path of the audio file in use, or null when none has been supplied yet.</summary>
    public string? SourcePath
    {
        get
        {
            if (!_resolved)
            {
                _resolved = true;
                _sourcePath = Resolve();
            }

            return _sourcePath;
        }
    }

    public bool IsAvailable => SourcePath is not null;

    /// <summary>Loudness, 0-1. The Customize volume sets this before every play.</summary>
    public double Volume { get; set; } = 0.8;

    /// <summary>Plays the chime once, from the start, whatever the UI is doing.</summary>
    public void Play()
    {
        if (_disposed)
        {
            return;
        }

        var path = SourcePath;
        if (path is null)
        {
            if (!_missingLogged)
            {
                _missingLogged = true;
                AppLogger.Log(
                    "Bird sound missing: add Assets/bird-sound.wav (or .mp3/.m4a) and rebuild; " +
                    "falling back to the system alert tone.");
            }

            PlayFallback();
            return;
        }

        if (Volume <= 0d)
        {
            AppLogger.Log("Bird chime muted: volume is 0%");
            return;
        }

        try
        {
            _player ??= CreatePlayer(path);
            _player.Volume = Math.Clamp(Volume, 0d, 1d);
            _player.PlaybackSession.Position = TimeSpan.Zero;
            _player.Play();
            AppLogger.Log($"Bird chime playing {Path.GetFileName(path)} at {_player.Volume:P0}");
        }
        catch (Exception ex)
        {
            AppLogger.Log("Bird sound playback failed", ex);
            PlayFallback();
        }
    }

    /// <summary>Silences the chime (the tray's Extend and Done both do this).</summary>
    public void Stop()
    {
        try
        {
            _player?.Pause();
        }
        catch (Exception ex)
        {
            AppLogger.Log("Bird sound stop failed", ex);
        }
    }

    private static MediaPlayer CreatePlayer(string path) =>
        new()
        {
            // file:// URI: the app is unpackaged, so an absolute path is the reliable source.
            Source = MediaSource.CreateFromUri(new Uri(path)),
            AudioCategory = MediaPlayerAudioCategory.Alerts,
            AutoPlay = false,
        };

    private static string? Resolve()
    {
        var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
        foreach (var name in CandidateNames)
        {
            var candidate = Path.Combine(assets, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void PlayFallback()
    {
        try
        {
            Interop.NativeMethods.MessageBeep(0);
        }
        catch (Exception ex)
        {
            AppLogger.Log("Fallback completion tone failed", ex);
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
            // Stop first, then drop the source: the MediaPlayer keeps the audio session and the
            // file handle alive until the source is released, so Dispose alone can leave the
            // file locked for the rest of the session.
            _player?.Pause();
            _player?.Source = null;
            _player?.Dispose();
        }
        catch (Exception ex)
        {
            AppLogger.Log("Bird sound dispose failed", ex);
        }

        _player = null;
    }
}
