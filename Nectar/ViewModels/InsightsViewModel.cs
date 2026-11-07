using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Nectar.Data;
using Nectar.Data.Entities;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.UI;
using Microsoft.UI.Xaml.Media;

namespace Nectar.ViewModels;

/// <summary>
/// The Insights tab: a seven-day window compared with the seven days before it. Every number here
/// is derived from SQLite on demand (<see cref="RefreshAsync"/>) rather than cached in memory, so
/// the card cannot drift from the rows the workspace writes.
/// </summary>
public sealed class InsightsViewModel : ObservableObject
{
    private const int WindowDays = 7;

    private static readonly SolidColorBrush PillActiveBrush = new(Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush PillIdleBrush = new(Colors.Transparent);
    private static readonly SolidColorBrush InkBrush = new(Color.FromArgb(0xF2, 0x1B, 0x24, 0x1C));
    private static readonly SolidColorBrush InkMutedBrush = new(Color.FromArgb(0x8A, 0x1B, 0x24, 0x1C));

    private readonly WorkspaceRepository _repository;
    private readonly Action<string>? _reportProblem;

    private bool _tasksMode = true;
    private bool _loaded;
    private int _totalTasksCompleted;
    private int _previousTasksCompleted;
    private int _plannedTotal;
    private string _focusTimeFormatted = "0m";
    private string _activeDays = "0 of 7";
    private string _momentumText = "Nothing logged in the last seven days yet.";
    private string _momentumTitle = "Your week starts here";
    private string _weekRangeText = string.Empty;

    internal InsightsViewModel(WorkspaceRepository repository, Action<string>? reportProblem = null)
    {
        _repository = repository;
        _reportProblem = reportProblem;

        ShowTasksCommand = new RelayCommand(_ => SetMode(tasksMode: true));
        ShowFocusCommand = new RelayCommand(_ => SetMode(tasksMode: false));
        ResetWeekRange();
    }

    /// <summary>Seven bottom-aligned columns: planned behind, completed in front.</summary>
    public ObservableCollection<DailyInsight> WeeklyChartData { get; } = new();

    public ICommand ShowTasksCommand { get; }

    public ICommand ShowFocusCommand { get; }

    /// <summary>Tasks finished inside the current seven-day window.</summary>
    public int TotalTasksCompleted
    {
        get => _totalTasksCompleted;
        private set
        {
            if (SetProperty(ref _totalTasksCompleted, value))
            {
                OnPropertyChanged(nameof(TotalTasksText));
            }
        }
    }

    public int PreviousTasksCompleted
    {
        get => _previousTasksCompleted;
        private set => SetProperty(ref _previousTasksCompleted, value);
    }

    /// <summary>Distinct tasks that were live at some point in the window; the "of N planned" line.</summary>
    public int PlannedTotal
    {
        get => _plannedTotal;
        private set
        {
            if (SetProperty(ref _plannedTotal, value))
            {
                OnPropertyChanged(nameof(PlannedText));
            }
        }
    }

    public string PlannedText => $"of {PlannedTotal} planned";

    /// <summary>String projection of the hero number: x:Bind has no int-to-Text coercion.</summary>
    public string TotalTasksText => TotalTasksCompleted.ToString(CultureInfo.InvariantCulture);

    public string FocusTimeFormatted
    {
        get => _focusTimeFormatted;
        private set => SetProperty(ref _focusTimeFormatted, value);
    }

    public string ActiveDays
    {
        get => _activeDays;
        private set => SetProperty(ref _activeDays, value);
    }

    public string MomentumText
    {
        get => _momentumText;
        private set => SetProperty(ref _momentumText, value);
    }

    public string MomentumTitle
    {
        get => _momentumTitle;
        private set => SetProperty(ref _momentumTitle, value);
    }

    public string WeekRangeText
    {
        get => _weekRangeText;
        private set => SetProperty(ref _weekRangeText, value);
    }

    public string ChartCaption => _tasksMode ? "Completed" : "Focus minutes";

    /// <summary>Shown over an all-zero chart so an empty week reads as empty, not broken.</summary>
    public Visibility ChartEmptyVisibility =>
        _chartEmpty ? Visibility.Visible : Visibility.Collapsed;

    private bool _chartEmpty = true;
    private double _chartWidth = 48d * WindowDays;

    public Visibility PlannedVisibility =>
        _tasksMode ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FocusLegendVisibility =>
        _tasksMode ? Visibility.Collapsed : Visibility.Visible;

    public Brush TasksPillBackground => _tasksMode ? PillActiveBrush : PillIdleBrush;

    public Brush FocusPillBackground => _tasksMode ? PillIdleBrush : PillActiveBrush;

    public Brush TasksPillForeground => _tasksMode ? InkBrush : InkMutedBrush;

    public Brush FocusPillForeground => _tasksMode ? InkMutedBrush : InkBrush;

    private void SetMode(bool tasksMode)
    {
        if (_tasksMode == tasksMode)
        {
            return;
        }

        _tasksMode = tasksMode;
        foreach (var day in WeeklyChartData)
        {
            day.ShowSeries(_tasksMode);
        }

        OnPropertyChanged(nameof(ChartCaption));
        OnPropertyChanged(nameof(PlannedVisibility));
        OnPropertyChanged(nameof(FocusLegendVisibility));
        OnPropertyChanged(nameof(TasksPillBackground));
        OnPropertyChanged(nameof(FocusPillBackground));
        OnPropertyChanged(nameof(TasksPillForeground));
        OnPropertyChanged(nameof(FocusPillForeground));
    }

    /// <summary>
    /// Spreads the seven columns evenly across the plot. Called from the view's SizeChanged so the
    /// bars stay aligned with the grid lines at any card width.
    /// </summary>
    public void SetChartWidth(double width)
    {
        _chartWidth = width;

        var column = Math.Max(24d, width / WindowDays);
        foreach (var day in WeeklyChartData)
        {
            day.ColumnWidth = column;
        }
    }

    /// <summary>Reloads the window from SQLite and recomputes every headline and bar.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var windowStart = today.AddDays(-(WindowDays - 1));
        var previousStart = windowStart.AddDays(-WindowDays);

        IReadOnlyList<WorkspaceTask> tasks;
        IReadOnlyList<FocusSession> sessions;

        try
        {
            tasks = await _repository.LoadTaskHistoryAsync(previousStart, cancellationToken)
                .ConfigureAwait(true);
            sessions = await _repository.LoadFocusSessionsAsync(previousStart, cancellationToken)
                .ConfigureAwait(true);
        }
        catch (WorkspaceDataException ex)
        {
            _reportProblem?.Invoke(ex.Message);
            return;
        }

        var days = Enumerable.Range(0, WindowDays)
            .Select(offset => windowStart.AddDays(offset))
            .ToList();

        var planned = days.Select(day => tasks.Count(t => IsLiveOn(t, day))).ToList();
        var completed = days.Select(day => tasks.Count(t => t.CompletedAt?.Date == day)).ToList();
        var focused = days
            .Select(day => sessions.Where(s => s.CompletedAt.Date == day).Sum(s => s.DurationMinutes))
            .ToList();

        var plannedPeak = Math.Max(1, planned.Max());
        var focusPeak = Math.Max(1, focused.Max());

        WeeklyChartData.Clear();
        for (var i = 0; i < days.Count; i++)
        {
            var column = new DailyInsight(days[i]);
            column.SetCounts(
                planned[i],
                completed[i],
                focused[i],
                planned[i] / (double)plannedPeak,
                completed[i] / (double)plannedPeak,
                focused[i] / (double)focusPeak);
            column.ShowSeries(_tasksMode);
            column.ColumnWidth = Math.Max(24d, _chartWidth / WindowDays);
            WeeklyChartData.Add(column);
        }

        TotalTasksCompleted = completed.Sum();
        PreviousTasksCompleted = tasks.Count(t =>
            t.CompletedAt is { } done && done.Date >= previousStart && done.Date < windowStart);
        PlannedTotal = tasks.Count(t =>
            t.CreatedAt.Date <= today && (t.CompletedAt is null || t.CompletedAt.Value.Date >= windowStart));

        var focusMinutes = focused.Sum();
        FocusTimeFormatted = FormatMinutes(focusMinutes);
        ActiveDays = $"{completed.Where((count, index) => count > 0 || focused[index] > 0).Count()} of {WindowDays}";

        MomentumTitle = BuildMomentumTitle(TotalTasksCompleted, PreviousTasksCompleted);
        MomentumText = BuildMomentumText(TotalTasksCompleted, PreviousTasksCompleted, FocusTimeFormatted);
        WeekRangeText = $"{windowStart:d MMM} \u2013 {today:d MMM}";

        _chartEmpty = completed.Sum() == 0 && focusMinutes == 0;
        OnPropertyChanged(nameof(ChartEmptyVisibility));

        _loaded = true;
    }

    /// <summary>True while the task was still open on that day (it was planned for that day).</summary>
    private static bool IsLiveOn(WorkspaceTask task, DateTime day)
    {
        if (task.CreatedAt.Date > day)
        {
            return false;
        }

        return task.CompletedAt is not { } completed || completed.Date >= day;
    }

    private static string BuildMomentumTitle(int current, int previous) => current switch
    {
        0 when previous == 0 => "Your week starts here",
        0 => "Quiet week so far",
        _ when current > previous => "Momentum is building",
        _ when current == previous => "Holding steady",
        _ => "Easing off this week",
    };

    private static string BuildMomentumText(int current, int previous, string focus) => current switch
    {
        0 when previous == 0 => "Nothing logged in the last seven days yet.",
        0 => $"Nothing done in the last seven days, after {previous} the week before.",
        _ when current > previous =>
            $"{current} tasks done, up from {previous} the week before, with {focus} focused.",
        _ when current == previous =>
            $"{current} tasks done, level with {previous} the week before, with {focus} focused.",
        _ =>
            $"{current} tasks done, down from {previous} the week before, with {focus} focused.",
    };

    /// <summary>470 → "7h 50m", 45 → "45m", 0 → "0m".</summary>
    private static string FormatMinutes(int minutes)
    {
        if (minutes <= 0)
        {
            return "0m";
        }

        var hours = minutes / 60;
        var rest = minutes % 60;

        if (hours == 0)
        {
            return $"{rest}m";
        }

        return rest == 0 ? $"{hours}h" : $"{hours}h {rest}m";
    }

    private void ResetWeekRange()
    {
        var today = DateTime.Today;
        WeekRangeText = $"{today.AddDays(-(WindowDays - 1)):d MMM} \u2013 {today:d MMM}";
    }

    /// <summary>True once a refresh has produced data; used to skip redundant work on first open.</summary>
    public bool IsLoaded => _loaded;
}
