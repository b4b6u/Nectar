using Nectar.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.Win32;

namespace Nectar.Services;

/// <summary>
/// Focus countdown (25 minutes by default). The remaining time is derived from an absolute
/// deadline rather than accumulated ticks, so a busy UI thread cannot make the clock drift.
/// Sleep/suspend pauses the session (SystemEvents.PowerModeChanged) instead of silently burning
/// minutes while the machine is away.
/// </summary>
public sealed class FocusTimerService : ObservableObject, IDisposable
{
    private static readonly TimeSpan DefaultLength = TimeSpan.FromMinutes(25);

    private readonly DispatcherQueue _dispatcher;
    private readonly DispatcherQueueTimer _tick;

    private TimeSpan _sessionLength = DefaultLength;
    private TimeSpan _remaining = DefaultLength;
    private DateTime _deadlineUtc;
    private bool _isRunning;
    private bool _completed;
    private bool _disposed;

    public FocusTimerService(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;

        _tick = dispatcher.CreateTimer();
        _tick.Interval = TimeSpan.FromMilliseconds(200);
        _tick.IsRepeating = true;
        _tick.Tick += OnTick;

        try
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }
        catch (Exception)
        {
            // Power notifications are a nicety; the timer still works without them.
        }
    }

    /// <summary>Raised with the session length (minutes) when a countdown runs out.</summary>
    public event Action<int>? SessionCompleted;

    public TimeSpan Remaining => _remaining;

    public string Display => $"{(int)_remaining.TotalMinutes:00}:{_remaining.Seconds:00}";

    public bool IsRunning => _isRunning;

    /// <summary>True once a countdown has reached 00:00 and nothing has restarted it yet.</summary>
    public bool IsCompleted => _completed;

    /// <summary>0 at the start of a session, 1 when the session is complete.</summary>
    public double Progress => 1d - (_remaining.TotalSeconds / _sessionLength.TotalSeconds);

    public double ProgressPercent => Math.Clamp(Progress * 100d, 0d, 100d);

    public string PlayGlyph => _isRunning ? "\uE769" : "\uE768";

    public string PlayLabel => _isRunning ? "Pause" : "Start";

    public int SessionMinutes => (int)_sessionLength.TotalMinutes;

    public string StateLabel =>
        _completed ? "COMPLETE" : _isRunning ? "IN FOCUS" : "FOCUS";

    public string DurationLabel => $"{(int)_sessionLength.TotalMinutes} min session · pauses on sleep";

    /// <summary>Toggles between running and paused.</summary>
    public void Toggle()
    {
        if (_isRunning)
        {
            Pause();
        }
        else
        {
            Start();
        }
    }

    public void Start()
    {
        if (_isRunning)
        {
            return;
        }

        if (_remaining <= TimeSpan.Zero)
        {
            _remaining = _sessionLength;
            _completed = false;
        }

        _deadlineUtc = DateTime.UtcNow + _remaining;
        _isRunning = true;
        _tick.Start();
        RaiseAll();
    }

    public void Pause()
    {
        if (!_isRunning)
        {
            return;
        }

        var left = _deadlineUtc - DateTime.UtcNow;
        _remaining = left > TimeSpan.Zero ? left : TimeSpan.Zero;
        _isRunning = false;
        _tick.Stop();
        RaiseAll();
    }

    public void Reset()
    {
        _isRunning = false;
        _completed = false;
        _tick.Stop();
        _remaining = _sessionLength;
        RaiseAll();
    }

    /// <summary>
    /// Adds time to the countdown and resumes it — the expiry tray's Extend chips. Works from
    /// a finished session too: 00:00 becomes the chosen length (5/10/15 min) and the clock restarts.
    /// </summary>
    public void Extend(TimeSpan extra)
    {
        if (extra <= TimeSpan.Zero)
        {
            return;
        }

        _remaining += extra;
        if (_remaining > _sessionLength)
        {
            // Keep the progress ring meaningful when extending past the original length.
            _sessionLength = _remaining;
            OnPropertyChanged(nameof(DurationLabel));
            OnPropertyChanged(nameof(SessionMinutes));
        }

        _completed = false;
        _deadlineUtc = DateTime.UtcNow + _remaining;
        _isRunning = true;
        _tick.Start();
        RaiseAll();
    }

    /// <summary>Changes the session length and resets to it (used by the "Set time" menu).</summary>
    public void SetLength(TimeSpan length)
    {
        var minutes = Math.Clamp((int)Math.Round(length.TotalMinutes), 1, 180);
        _sessionLength = TimeSpan.FromMinutes(minutes);
        Reset();
        OnPropertyChanged(nameof(DurationLabel));
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        var left = _deadlineUtc - DateTime.UtcNow;
        if (left <= TimeSpan.Zero)
        {
            _remaining = TimeSpan.Zero;
            _isRunning = false;
            _completed = true;
            _tick.Stop();
            RaiseAll();

            // The sound belongs to the window, which owns the bird chime and the Customize
            // switch. The timer deliberately plays nothing of its own, so a finished session
            // can never produce two signals.
            SessionCompleted?.Invoke(SessionMinutes);
            return;
        }

        // Round up so the display shows the full length the moment a session starts.
        _remaining = TimeSpan.FromSeconds(Math.Ceiling(left.TotalSeconds));
        if (_remaining > _sessionLength)
        {
            _remaining = _sessionLength;
        }

        OnPropertyChanged(nameof(Remaining));
        OnPropertyChanged(nameof(Display));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(ProgressPercent));
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Suspend)
        {
            return;
        }

        // Raised on a system thread: marshal before touching UI-bound state.
        UiThread.Post(_dispatcher, () =>
        {
            if (_isRunning)
            {
                Pause();
            }
        }, "FocusTimerService: pause on suspend");
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(Remaining));
        OnPropertyChanged(nameof(Display));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(PlayGlyph));
        OnPropertyChanged(nameof(PlayLabel));
        OnPropertyChanged(nameof(StateLabel));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tick.Stop();
        _tick.Tick -= OnTick;

        try
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        }
        catch (Exception)
        {
            // Ignore teardown races.
        }
    }
}
