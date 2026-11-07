using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Nectar.Services.Calendar;

namespace Nectar.ViewModels;

/// <summary>Display projection of one linked-calendar event, plus its reminder state.</summary>
public sealed class CalendarEventRow : ObservableObject
{
    private static readonly SolidColorBrush BellMuted =
        new(global::Windows.UI.Color.FromArgb(0x8A, 0x1B, 0x24, 0x1C));

    private static readonly SolidColorBrush BellArmed =
        new(global::Windows.UI.Color.FromArgb(0xFF, 0xB4, 0x53, 0x09));

    private int? _leadMinutes;

    public CalendarEventRow(CalendarEvent source, int? leadMinutes)
    {
        Id = source.Id;
        Start = source.Start;
        RangeText = $"{source.Start:h:mm} – {source.End:h:mm}";
        Subject = source.Subject;
        Location = source.Location;
        IsNow = source.Start <= DateTimeOffset.Now && source.End > DateTimeOffset.Now;
        _leadMinutes = leadMinutes;
    }

    public string Id { get; }

    public DateTimeOffset Start { get; }

    public string RangeText { get; }

    public string Subject { get; }

    public string Location { get; }

    public Visibility LocationVisibility =>
        string.IsNullOrWhiteSpace(Location) ? Visibility.Collapsed : Visibility.Visible;

    public bool IsNow { get; }

    /// <summary>Minutes before the start the reminder fires; null when none is armed.</summary>
    public int? LeadMinutes
    {
        get => _leadMinutes;
        set
        {
            if (_leadMinutes == value)
            {
                return;
            }

            _leadMinutes = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasReminder));
            OnPropertyChanged(nameof(ReminderForeground));
            OnPropertyChanged(nameof(ReminderHint));
        }
    }

    public bool HasReminder => _leadMinutes.HasValue;

    public Brush ReminderForeground => HasReminder ? BellArmed : BellMuted;

    public string ReminderHint => _leadMinutes switch
    {
        null => "Add a reminder",
        0 => "Reminder at the start",
        _ => $"Reminder {_leadMinutes} min before",
    };
}
