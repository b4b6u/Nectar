using System.Text.Json;
using Microsoft.UI.Dispatching;

namespace Nectar.Services;

/// <summary>
/// Event reminders that outlive a restart. Each reminder is anchored to a linked-calendar event
/// and a lead time; when it comes due the overlay drops the same tray the focus timer uses.
/// Snoozing re-arms it instead of deleting it, so the reminder can come back.
/// </summary>
public sealed class ReminderScheduler : IDisposable
{
    public const int DefaultSnoozeMinutes = 5;

    /// <summary>Upper bound on stored reminders; a hand-edited file cannot grow without limit.</summary>
    private const int MaxReminders = 200;

    /// <summary>Longest reminder title kept; calendar subjects are unbounded.</summary>
    private const int MaxTitleLength = 200;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly DispatcherQueueTimer _timer;
    private readonly List<Reminder> _reminders = new();
    private bool _disposed;

    public ReminderScheduler(DispatcherQueue dispatcher)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nectar");

        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "reminders.json");
        Load();

        _timer = dispatcher.CreateTimer();
        _timer.Interval = PollInterval;
        _timer.IsRepeating = true;
        _timer.Tick += OnTick;
    }

    /// <summary>Raised on the UI thread once per reminder, when its moment arrives.</summary>
    public event EventHandler<Reminder>? Due;

    public IReadOnlyList<Reminder> Items => _reminders;

    public void Start()
    {
        _timer.Start();
        Check();
    }

    public Reminder? Find(string eventId) =>
        _reminders.FirstOrDefault(r => string.Equals(r.EventId, eventId, StringComparison.Ordinal));

    /// <summary>Creates (or replaces) the reminder for one event. Returns the armed reminder.</summary>
    public Reminder Set(string eventId, string title, DateTimeOffset startsAt, int leadMinutes)
    {
        _reminders.RemoveAll(r => string.Equals(r.EventId, eventId, StringComparison.Ordinal));

        var fireAt = startsAt.AddMinutes(-Math.Max(0, leadMinutes));
        if (fireAt < DateTimeOffset.Now)
        {
            // A reminder chosen for an event that already started should arrive straight away.
            fireAt = DateTimeOffset.Now;
        }

        var reminder = new Reminder
        {
            EventId = eventId,
            Title = ClampTitle(title),
            StartsAt = startsAt,
            LeadMinutes = Math.Max(0, leadMinutes),
            FireAt = fireAt,
        };

        _reminders.Add(reminder);
        Save();
        AppLogger.Log($"Reminder armed for \"{reminder.Title}\" at {fireAt:h:mm tt} ({reminder.LeadMinutes} min before the start).");
        return reminder;
    }

    public bool Clear(string eventId)
    {
        var removed = _reminders.RemoveAll(r => string.Equals(r.EventId, eventId, StringComparison.Ordinal));
        if (removed > 0)
        {
            Save();
        }

        return removed > 0;
    }

    /// <summary>Pushes the same reminder back by <paramref name="delay"/> and lets it fire again.</summary>
    public void Snooze(Reminder reminder, TimeSpan delay)
    {
        reminder.FireAt = DateTimeOffset.Now.Add(delay);
        reminder.Notified = false;
        Save();
        AppLogger.Log($"Reminder \"{reminder.Title}\" snoozed until {reminder.FireAt:h:mm tt}.");
    }

    public void Dismiss(Reminder reminder)
    {
        _reminders.RemoveAll(r => r.Id == reminder.Id);
        Save();
        AppLogger.Log($"Reminder \"{reminder.Title}\" dismissed.");
    }

    private void OnTick(DispatcherQueueTimer sender, object args) => Check();

    private void Check()
    {
        if (_disposed)
        {
            return;
        }

        var due = _reminders.FirstOrDefault(r => !r.Notified && r.FireAt <= DateTimeOffset.Now);
        if (due is null)
        {
            return;
        }

        due.Notified = true;
        Save();
        Due?.Invoke(this, due);
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return;
            }

            var stored = JsonSerializer.Deserialize<List<Reminder>>(File.ReadAllText(_path));
            if (stored is null)
            {
                return;
            }

            var staleBefore = DateTimeOffset.Now.AddHours(-6);
            foreach (var reminder in stored)
            {
                if (reminder is null || string.IsNullOrWhiteSpace(reminder.EventId))
                {
                    continue;
                }

                var recent = reminder.Notified
                    ? reminder.FireAt >= staleBefore
                    : reminder.StartsAt >= staleBefore;

                if (!recent)
                {
                    continue;
                }

                reminder.Title = ClampTitle(reminder.Title);
                _reminders.Add(reminder);

                if (_reminders.Count >= MaxReminders)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("Reminders could not be read", ex);
        }
    }

    private static string ClampTitle(string? title)
    {
        var text = (title ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return "Event";
        }

        return text.Length > MaxTitleLength ? text[..MaxTitleLength] : text;
    }

    private void Save()
    {
        try
        {
            AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(_reminders, SerializerOptions));
        }
        catch (Exception ex)
        {
            AppLogger.Log("Reminders could not be saved", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    /// <summary>One armed reminder, serialized to reminders.json.</summary>
    public sealed class Reminder
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string EventId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public DateTimeOffset StartsAt { get; set; }

        public int LeadMinutes { get; set; }

        public DateTimeOffset FireAt { get; set; }

        public bool Notified { get; set; }
    }
}
