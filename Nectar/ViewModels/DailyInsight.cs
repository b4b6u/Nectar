using Microsoft.UI.Xaml;

namespace Nectar.ViewModels;

/// <summary>
/// One column of the seven-day Insights chart. Raw counts stay public so the view can caption a
/// day; the fractions are normalised against the busiest day of the current mode so the bars fill
/// the plot area whatever the absolute numbers are.
/// </summary>
public sealed class DailyInsight : ObservableObject
{
    /// <summary>Height of the chart's plot area in DIPs; bars scale into it.</summary>
    public const double PlotHeight = 200d;

    /// <summary>Shortest visible bar so a single completed task still reads as a bar.</summary>
    private const double MinimumBarHeight = 3d;

    private double _columnWidth = 48d;
    private double _plannedFraction;
    private double _completedFraction;
    private bool _showPlanned = true;

    public DailyInsight(DateTime date)
    {
        Date = date;
    }

    public DateTime Date { get; }

    public string DayName => Date.ToString("ddd");

    public string DateNumber => Date.Day.ToString();

    /// <summary>Tasks that were open (or finished) on this day.</summary>
    public int PlannedCount { get; private set; }

    /// <summary>Tasks finished on this day.</summary>
    public int CompletedCount { get; private set; }

    public int FocusMinutes { get; private set; }

    /// <summary>0-1 height of the light "planned" bar, relative to the busiest planned day.</summary>
    public double TaskPlannedFraction { get; private set; }

    /// <summary>0-1 height of the dark "completed" bar, relative to the busiest planned day.</summary>
    public double TaskCompletedFraction { get; private set; }

    /// <summary>0-1 height of the focus bar, relative to the longest focused day.</summary>
    public double FocusFraction { get; private set; }

    public double PlannedFraction
    {
        get => _plannedFraction;
        private set => SetProperty(ref _plannedFraction, value);
    }

    public double CompletedFraction
    {
        get => _completedFraction;
        private set => SetProperty(ref _completedFraction, value);
    }

    /// <summary>Planned bar height in DIPs for the plot area.</summary>
    public double PlannedHeight => Scale(_plannedFraction);

    /// <summary>Completed (or focus) bar height in DIPs for the plot area.</summary>
    public double CompletedHeight => Scale(_completedFraction);

    private static double Scale(double fraction) =>
        fraction <= 0d ? 0d : Math.Max(MinimumBarHeight, fraction * PlotHeight);

    public bool ShowPlanned
    {
        get => _showPlanned;
        private set
        {
            if (SetProperty(ref _showPlanned, value))
            {
                OnPropertyChanged(nameof(PlannedBarVisibility));
            }
        }
    }

    /// <summary>Width of this column in DIPs, kept in step with the chart's plot width.</summary>
    public double ColumnWidth
    {
        get => _columnWidth;
        internal set => SetProperty(ref _columnWidth, value);
    }

    public Visibility PlannedBarVisibility =>
        _showPlanned ? Visibility.Visible : Visibility.Collapsed;

    public string Tooltip =>
        $"{Date:ddd d MMM} · {CompletedCount} of {PlannedCount} tasks · {FocusMinutes} min focused";

    internal void SetCounts(
        int planned,
        int completed,
        int focusMinutes,
        double taskPlannedFraction,
        double taskCompletedFraction,
        double focusFraction)
    {
        PlannedCount = planned;
        CompletedCount = completed;
        FocusMinutes = focusMinutes;
        TaskPlannedFraction = taskPlannedFraction;
        TaskCompletedFraction = taskCompletedFraction;
        FocusFraction = focusFraction;
        OnPropertyChanged(nameof(PlannedCount));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(FocusMinutes));
        OnPropertyChanged(nameof(Tooltip));
    }

    /// <summary>Re-points the visible bars at either the task series or the focus series.</summary>
    internal void ShowSeries(bool tasksMode)
    {
        ShowPlanned = tasksMode;
        PlannedFraction = tasksMode ? TaskPlannedFraction : 0d;
        CompletedFraction = tasksMode ? TaskCompletedFraction : FocusFraction;
        OnPropertyChanged(nameof(PlannedHeight));
        OnPropertyChanged(nameof(CompletedHeight));
    }
}
