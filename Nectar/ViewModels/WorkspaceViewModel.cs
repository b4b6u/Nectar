using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Nectar.Data;
using Nectar.Data.Entities;
using Nectar.Hooks;
using Nectar.Services;
using Nectar.Services.Calendar;
using Nectar.Settings;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Nectar.ViewModels;

/// <summary>
/// State for the whole workspace: tasks, note, focus timer and today's agenda. Every mutation
/// writes through to SQLite and then updates the bound collections, so the UI is always a
/// projection of the database rather than a separate copy of it.
/// </summary>
public sealed class WorkspaceViewModel : ObservableObject
{
    private readonly WorkspaceRepository _repository;
    private readonly LinkedCalendarService _calendar;
    private readonly ReminderScheduler _reminders;
    private readonly DispatcherQueue _dispatcher;
    private readonly DispatcherQueueTimer _noteSaveTimer;

    private string _noteText = string.Empty;
    private string _noteStatus = "Saved";
    private string _banner = string.Empty;
    private string _agendaHint = "Nothing scheduled today";

    internal WorkspaceViewModel(
        WorkspaceRepository repository,
        LinkedCalendarService calendar,
        ReminderScheduler reminders,
        FocusTimerService timer,
        DispatcherQueue dispatcher)
    {
        _repository = repository;
        _calendar = calendar;
        _reminders = reminders;
        _dispatcher = dispatcher;
        Timer = timer;

        Insights = new InsightsViewModel(repository, message => Banner = message);
        Timer.SessionCompleted += OnFocusSessionCompleted;

        _noteSaveTimer = dispatcher.CreateTimer();
        _noteSaveTimer.Interval = TimeSpan.FromMilliseconds(700);
        _noteSaveTimer.IsRepeating = false;
        _noteSaveTimer.Tick += OnNoteSaveTick;

        LoadSettings();
        CardRows.CollectionChanged += OnCardOrderChanged;
    }

    public ObservableCollection<TaskRow> Tasks { get; } = new();

    public ObservableCollection<CalendarEventRow> Agenda { get; } = new();

    /// <summary>Seven-day momentum and chart data for the Insights tab.</summary>
    public InsightsViewModel Insights { get; }

    public FocusTimerService Timer { get; }

    public string NoteText
    {
        get => _noteText;
        set => SetProperty(ref _noteText, value);
    }

    public string NoteStatus
    {
        get => _noteStatus;
        private set => SetProperty(ref _noteStatus, value);
    }

    public string Banner
    {
        get => _banner;
        private set
        {
            if (SetProperty(ref _banner, value))
            {
                OnPropertyChanged(nameof(BannerVisibility));
            }
        }
    }

    public Visibility BannerVisibility =>
        string.IsNullOrWhiteSpace(_banner) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Surfaces a one-line message in the existing banner (dictation fallbacks, etc.).</summary>
    public void ShowMessage(string message) => Banner = message;

    /// <summary>Microphone state shown in Customize → General; set by the window on tab open.</summary>
    public string MicrophoneStatusLabel { get; private set; } = "Checking…";

    public void SetMicrophoneStatus(string label)
    {
        MicrophoneStatusLabel = label;
        OnPropertyChanged(nameof(MicrophoneStatusLabel));
    }

    /// <summary>SQLCipher state of the local database, read from disk rather than assumed.</summary>
    public string DatabaseStatusLabel => DatabaseEncryption.Describe(_repository.DatabasePath);

    public string DataFolderPath => Path.GetDirectoryName(_repository.DatabasePath) ?? string.Empty;

    /// <summary>Re-reads the privacy labels (the window calls this whenever Customize opens).</summary>
    public void RefreshPrivacyLabels() => OnPropertyChanged(nameof(DatabaseStatusLabel));

    /// <summary>"To do 4" chip in the tasks card.</summary>
    public string TaskOpenChip => $"To do {Tasks.Count(t => !t.IsCompleted)}";

    /// <summary>"Completed 1" chip in the tasks card.</summary>
    public string TaskDoneChip => $"Completed {Tasks.Count(t => t.IsCompleted)}";

    /// <summary>Headline of the timer card: whatever the user is working on right now.</summary>
    public string FocusTaskTitle =>
        Tasks.FirstOrDefault(t => !t.IsCompleted)?.Title ?? "Focus session";

    public string TodayLabel => DateTime.Now.ToString("ddd d MMM yyyy");

    // ---- Insights -------------------------------------------------------------------

    public string CalendarState => _calendar.IsLinked ? "Calendar: On" : "Calendar: Off";

    /// <summary>Re-reads the seven-day window from SQLite and rebuilds the Insights card.</summary>
    public Task RefreshInsightsAsync() => Insights.RefreshAsync();

    /// <summary>A finished countdown becomes a row in FocusSessions, then the week is recounted.</summary>
    private void OnFocusSessionCompleted(int minutes)
    {
        _ = LogFocusSessionAsync(minutes);
    }

    private async Task LogFocusSessionAsync(int minutes)
    {
        try
        {
            await _repository.LogFocusSessionAsync(minutes, DateTime.Now);
            await Insights.RefreshAsync();
        }
        catch (WorkspaceDataException ex)
        {
            Banner = ex.Message;
        }
    }

    public string EmptyTasksText =>
        Tasks.Count == 0
            ? "The hummingbird is resting. Add the next thing that matters."
            : "All clear. The hummingbird is perched.";

    /// <summary>
    /// Only shown when there are no rows at all: with completed rows still listed, the message
    /// used to sit on top of them because the list and the text share the same card area.
    /// </summary>
    public Visibility EmptyTasksVisibility =>
        Tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public string AgendaSummary => $"TODAY · {DateTime.Now:ddd d MMM}".ToUpperInvariant();

    public string AgendaFooter => _calendar.IsLinked ? $"{_calendar.ProviderName} · Connected" : "No calendar linked";

    public string NoteWordCount
    {
        get
        {
            var count = NoteText
                .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Length;
            return $"{count} word{(count == 1 ? string.Empty : "s")}";
        }
    }

    public string AgendaHint
    {
        get => _agendaHint;
        private set => SetProperty(ref _agendaHint, value);
    }

    public Visibility AgendaEmptyVisibility =>
        Agenda.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AgendaListVisibility =>
        Agenda.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

    public async Task InitializeAsync()
    {
        var ready = await _repository.InitializeAsync();
        if (!ready)
        {
            Banner = _repository.InitializationError ?? "The local database could not be opened.";
        }

        try
        {
            var tasks = await _repository.LoadTasksAsync();
            Tasks.Clear();
            foreach (var task in tasks)
            {
                Tasks.Add(Attach(new TaskRow(task)));
            }

            NoteText = await _repository.LoadNoteAsync();
        }
        catch (WorkspaceDataException ex)
        {
            Banner = ex.Message;
        }

        RefreshTaskSummary();
        await Insights.RefreshAsync();
        await RefreshAgendaAsync();
    }

    public async Task RefreshAgendaAsync()
    {
        var events = await _calendar.GetTodayAsync();
        Agenda.Clear();
        foreach (var item in events)
        {
            Agenda.Add(new CalendarEventRow(item, _reminders.Find(item.Id)?.LeadMinutes));
        }

        AgendaHint = _calendar.IsLinked
            ? _calendar.LastError ?? "Nothing scheduled today"
            : "No calendar linked. Link Google or Microsoft Calendar in Customize.";

        OnPropertyChanged(nameof(AgendaSummary));
        OnPropertyChanged(nameof(AgendaFooter));
        OnPropertyChanged(nameof(AgendaEmptyVisibility));
        OnPropertyChanged(nameof(AgendaListVisibility));
        OnPropertyChanged(nameof(AgendaLinkVisibility));
        RaiseCalendarLinkState();
    }

    public async Task AddTaskAsync(string title)
    {
        var trimmed = (title ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        var model = new WorkspaceTask
        {
            Title = trimmed.Length > 500 ? trimmed[..500] : trimmed,
            CreatedAt = DateTime.Now,
        };

        try
        {
            await _repository.AddTaskAsync(model);
            Tasks.Insert(0, Attach(new TaskRow(model)));
            RefreshTaskSummary();
        }
        catch (WorkspaceDataException ex)
        {
            Banner = ex.Message;
        }
    }

    public async Task DeleteTaskAsync(TaskRow row)
    {
        try
        {
            await _repository.DeleteTaskAsync(row.Id);
            row.CompletionChanged -= OnCompletionChanged;
            Tasks.Remove(row);
            RefreshTaskSummary();
        }
        catch (WorkspaceDataException ex)
        {
            Banner = ex.Message;
        }
    }

    /// <summary>
    /// Completes the task the timer was running for — the open task at the top of the list, which
    /// is the same one <see cref="FocusTaskTitle"/> shows. Returns false when nothing was open.
    /// </summary>
    public bool CompleteFocusTask()
    {
        var row = Tasks.FirstOrDefault(t => !t.IsCompleted);
        if (row is null)
        {
            return false;
        }


        // The row setter persists both IsCompleted and CompletedAt via CompletionChanged.
        row.IsCompleted = true;
        return true;
    }

    public void OnNoteChanged(string text)
    {
        NoteText = text;
        NoteStatus = "Saving…";
        OnPropertyChanged(nameof(NoteWordCount));
        _noteSaveTimer.Stop();
        _noteSaveTimer.Start();
    }

    public async Task SaveNoteNowAsync()
    {
        _noteSaveTimer.Stop();
        try
        {
            await _repository.SaveNoteAsync(NoteText);
            NoteStatus = "Saved";
        }
        catch (WorkspaceDataException ex)
        {
            NoteStatus = "Not saved";
            Banner = ex.Message;
        }
    }

    public void DismissBanner() => Banner = string.Empty;

    /// <summary>Appends a non-fatal startup problem to the banner without losing earlier ones.</summary>
    public void ReportProblem(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Banner = string.IsNullOrWhiteSpace(Banner) ? message : $"{Banner}  ·  {message}";
    }

    private void OnNoteSaveTick(DispatcherQueueTimer sender, object args)
    {
        _ = SaveNoteNowAsync();
    }

    private TaskRow Attach(TaskRow row)
    {
        row.CompletionChanged += OnCompletionChanged;
        return row;
    }

    private void OnCompletionChanged(object? sender, EventArgs e)
    {
        if (sender is not TaskRow row)
        {
            return;
        }

        _ = PersistCompletionAsync(row);
        Nectar.Services.UiThread.Post(_dispatcher, () => Reorder(row), "WorkspaceViewModel: reorder after completion");
    }

    private async Task PersistCompletionAsync(TaskRow row)
    {
        try
        {
            // Completion owns its own timestamp: write both fields in one repository call so the
            // chart's day grouping can never disagree with the checkbox.
            await _repository.SetTaskCompletionAsync(row.Id, row.IsCompleted);
            if (Insights.IsLoaded)
            {
                await Insights.RefreshAsync();
            }
        }
        catch (WorkspaceDataException ex)
        {
            Banner = ex.Message;
        }
    }

    /// <summary>Keeps completed items sunk to the bottom of the list.</summary>
    private void Reorder(TaskRow row)
    {
        var index = Tasks.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        Tasks.RemoveAt(index);

        if (row.IsCompleted)
        {
            Tasks.Add(row);
        }
        else
        {
            var firstCompleted = Tasks.IndexOf(Tasks.FirstOrDefault(t => t.IsCompleted)!);
            var target = firstCompleted < 0 ? Tasks.Count : firstCompleted;
            Tasks.Insert(Math.Clamp(target, 0, Tasks.Count), row);
        }

        RefreshTaskSummary();
    }

    private void RefreshTaskSummary()
    {
        OnPropertyChanged(nameof(TaskOpenChip));
        OnPropertyChanged(nameof(TaskDoneChip));
        OnPropertyChanged(nameof(FocusTaskTitle));
        OnPropertyChanged(nameof(EmptyTasksText));
        OnPropertyChanged(nameof(EmptyTasksVisibility));
    }
    // ===== Preferences: which cards are shown, their order, and focus defaults =====

    private const double CardRowWidth = 980d;
    private const double CardGap = 8d;

    private readonly SettingsStore _settingsStore = new();
    private WorkspaceSettings _settings = new();

    /// <summary>Customize list, in card display order. The ListView reorders this collection.</summary>
    public ObservableCollection<CardSettingRow> CardRows { get; } = new();

    /// <summary>Raised whenever visibility or order changes, so the view can re-lay the cards.</summary>
    public event EventHandler? CardLayoutChanged;

    public int VisibleCardCount => CardRows.Count(c => c.IsVisible);

    /// <summary>Evenly divides the card row between the cards that are currently shown.</summary>
    public double CardWidth
    {
        get
        {
            var shown = Math.Max(1, VisibleCardCount);
            return Math.Floor((CardRowWidth - (CardGap * (shown - 1))) / shown);
        }
    }

    public Visibility CardsEmptyVisibility =>
        VisibleCardCount == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TasksCardVisibility => CardVisibility(WorkspaceSettings.TasksCard);

    public Visibility TimerCardVisibility => CardVisibility(WorkspaceSettings.TimerCard);

    public Visibility NotesCardVisibility => CardVisibility(WorkspaceSettings.NotesCard);

    public Visibility EventsCardVisibility => CardVisibility(WorkspaceSettings.EventsCard);

    public string FocusDefaultLabel => "Countdown";

    public string FocusMinutesLabel => $"{_settings.DefaultFocusMinutes} min";

    public bool CompletionSound
    {
        get => _settings.CompletionSound;
        set
        {
            if (_settings.CompletionSound == value)
            {
                return;
            }

            _settings.CompletionSound = value;
            _settingsStore.TrySave(_settings);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// The calendar reminder ring, switched separately from the focus completion chime: a quiet
    /// timer should not cost the user their meeting alert, and muting meetings should not
    /// silence the end of a session.
    /// </summary>
    public bool ReminderSound
    {
        get => _settings.ReminderSound;
        set
        {
            if (_settings.ReminderSound == value)
            {
                return;
            }

            _settings.ReminderSound = value;
            _settingsStore.TrySave(_settings);
            OnPropertyChanged();
        }
    }

    /// <summary>Chime loudness in percent for both sounds; 0 silences the bird call entirely.</summary>
    public int ChimeVolume => _settings.ChimeVolume;

    public string ChimeVolumeLabel => $"{_settings.ChimeVolume}%";

    public void NudgeChimeVolume(int delta)
    {
        var next = Math.Clamp(_settings.ChimeVolume + delta, 0, 100);
        if (next == _settings.ChimeVolume)
        {
            return;
        }

        _settings.ChimeVolume = next;
        _settingsStore.TrySave(_settings);
        OnPropertyChanged(nameof(ChimeVolume));
        OnPropertyChanged(nameof(ChimeVolumeLabel));
    }

    /// <summary>Whether 00:00 may drop the compact tray. The sound is controlled by <see cref="CompletionSound"/>.</summary>
    public bool ExpiryTray
    {
        get => _settings.ExpiryTray;
        set
        {
            if (_settings.ExpiryTray == value)
            {
                return;
            }

            _settings.ExpiryTray = value;
            _settingsStore.TrySave(_settings);
            OnPropertyChanged();
        }
    }

    /// <summary>The extend amount currently selected on the tray, remembered across sessions.</summary>
    public int ExtendMinutes => _settings.ExtendMinutes;

    public Brush Extend5Background => ExtendChipBackground(5);

    public Brush Extend10Background => ExtendChipBackground(10);

    public Brush Extend15Background => ExtendChipBackground(15);

    public Brush Extend5Foreground => ExtendChipForeground(5);

    public Brush Extend10Foreground => ExtendChipForeground(10);

    public Brush Extend15Foreground => ExtendChipForeground(15);

    private static readonly SolidColorBrush ExtendSelectedBackground =
        new(global::Windows.UI.Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF));

    private static readonly SolidColorBrush ExtendIdleBackground =
        new(global::Microsoft.UI.Colors.Transparent);

    private static readonly SolidColorBrush ExtendSelectedForeground =
        new(global::Windows.UI.Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A));

    private static readonly SolidColorBrush ExtendIdleForeground =
        new(global::Windows.UI.Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));

    /// <summary>Selects a 5/10/15 chip and adds that many minutes to the countdown.</summary>
    public void ExtendFocus(int minutes)
    {
        var amount = Math.Clamp(minutes, 1, 180);
        _settings.ExtendMinutes = amount;
        _settingsStore.TrySave(_settings);
        RaiseExtendChips();
        Timer.Extend(TimeSpan.FromMinutes(amount));
    }

    private Brush ExtendChipBackground(int minutes) =>
        minutes == _settings.ExtendMinutes ? ExtendSelectedBackground : ExtendIdleBackground;

    private Brush ExtendChipForeground(int minutes) =>
        minutes == _settings.ExtendMinutes ? ExtendSelectedForeground : ExtendIdleForeground;

    private void RaiseExtendChips()
    {
        OnPropertyChanged(nameof(ExtendMinutes));
        OnPropertyChanged(nameof(Extend5Background));
        OnPropertyChanged(nameof(Extend10Background));
        OnPropertyChanged(nameof(Extend15Background));
        OnPropertyChanged(nameof(Extend5Foreground));
        OnPropertyChanged(nameof(Extend10Foreground));
        OnPropertyChanged(nameof(Extend15Foreground));
    }

    // ===== General: open gesture, hover and the linked calendar =====

    private bool _capturingShortcut;

    public string ShortcutLabel => _capturingShortcut
        ? "Press keys…"
        : GlobalHotKeyService.Describe((uint)_settings.HotKeyModifiers, (uint)_settings.HotKeyKey);

    public string ShortcutUpperLabel => ShortcutLabel.ToUpperInvariant();

    /// <summary>Swaps the General card button into "press a chord" mode.</summary>
    public void BeginShortcutCapture()
    {
        if (_capturingShortcut)
        {
            return;
        }

        _capturingShortcut = true;
        OnPropertyChanged(nameof(ShortcutLabel));
        OnPropertyChanged(nameof(ShortcutUpperLabel));
    }

    public void EndShortcutCapture()
    {
        if (!_capturingShortcut)
        {
            return;
        }

        _capturingShortcut = false;
        OnPropertyChanged(nameof(ShortcutLabel));
        OnPropertyChanged(nameof(ShortcutUpperLabel));
    }

    public int HotKeyModifiers => _settings.HotKeyModifiers;

    public int HotKeyKey => _settings.HotKeyKey;

    /// <summary>Whether entering the top-edge band opens the workspace.</summary>
    public bool HoverToOpen
    {
        get => _settings.HoverToOpen;
        set
        {
            if (_settings.HoverToOpen == value)
            {
                return;
            }

            _settings.HoverToOpen = value;
            _settingsStore.TrySave(_settings);
            OnPropertyChanged();
            HoverToOpenChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Raised so the host can arm or retire the top-edge pointer zone.</summary>
    public event EventHandler? HoverToOpenChanged;

    /// <summary>Whether the collapsed notch pill is shown. Off leaves Nectar hidden between uses.</summary>
    public bool ShowPill
    {
        get => _settings.ShowPill;
        set
        {
            if (_settings.ShowPill == value)
            {
                return;
            }

            _settings.ShowPill = value;
            _settingsStore.TrySave(_settings);
            OnPropertyChanged();
            ShowPillChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Raised so the host can show or park the pill immediately.</summary>
    public event EventHandler? ShowPillChanged;

    /// <summary>Stores a chord that the host already registered successfully.</summary>
    public void ApplyShortcut(uint modifiers, uint key)
    {
        _settings.HotKeyModifiers = (int)modifiers;
        _settings.HotKeyKey = (int)key;
        _settingsStore.TrySave(_settings);
        OnPropertyChanged(nameof(ShortcutLabel));
        OnPropertyChanged(nameof(ShortcutUpperLabel));
    }

    /// <summary>Current chord as the saved (non-capturing) label, for logging and rollback.</summary>
    public string SavedShortcutLabel =>
        GlobalHotKeyService.Describe((uint)_settings.HotKeyModifiers, (uint)_settings.HotKeyKey);

    private CalendarProviderKind _pendingProvider = CalendarProviderKind.Google;

    private bool _calendarPanelOpen;

    private string _calendarStatus = string.Empty;

    public string CalendarStatus
    {
        get => _calendarStatus;
        private set => SetProperty(ref _calendarStatus, value);
    }

    public Visibility CalendarPanelVisibility =>
        _calendarPanelOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CalendarLinkVisibility =>
        _calendar.IsLinked ? Visibility.Collapsed : Visibility.Visible;

    public Visibility CalendarDisconnectVisibility =>
        _calendar.IsLinked ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AgendaLinkVisibility => CalendarLinkVisibility;

    public string CalendarLinkLabel => _calendar.IsLinked ? _calendar.ProviderName : "Not linked";

    public string CalendarClientId
    {
        get => _settings.CalendarClientId;
        set
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (string.Equals(_settings.CalendarClientId, trimmed, StringComparison.Ordinal))
            {
                return;
            }

            _settings.CalendarClientId = trimmed;
            _settingsStore.TrySave(_settings);
            OnPropertyChanged();
        }
    }

    public string CalendarTenant
    {
        get => _settings.CalendarTenant;
        set
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (string.Equals(_settings.CalendarTenant, trimmed, StringComparison.Ordinal))
            {
                return;
            }

            _settings.CalendarTenant = trimmed;
            _settingsStore.TrySave(_settings);
            OnPropertyChanged();
        }
    }

    public Visibility CalendarTenantVisibility =>
        _pendingProvider == CalendarProviderKind.Microsoft ? Visibility.Visible : Visibility.Collapsed;

    public Brush GoogleChipBackground => ProviderChipBackground(CalendarProviderKind.Google);

    public Brush GoogleChipForeground => ProviderChipForeground(CalendarProviderKind.Google);

    public Brush MicrosoftChipBackground => ProviderChipBackground(CalendarProviderKind.Microsoft);

    public Brush MicrosoftChipForeground => ProviderChipForeground(CalendarProviderKind.Microsoft);

    private static readonly SolidColorBrush ProviderSelectedBackground =
        new(global::Windows.UI.Color.FromArgb(0xFF, 0x23, 0x2A, 0x38));

    private static readonly SolidColorBrush ProviderSelectedForeground =
        new(global::Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xF4, 0xE6));

    private static readonly SolidColorBrush ProviderIdleBackground =
        new(global::Microsoft.UI.Colors.Transparent);

    private static readonly SolidColorBrush ProviderIdleForeground =
        new(global::Windows.UI.Color.FromArgb(0xFF, 0x1B, 0x24, 0x1C));

    public void SelectCalendarProvider(CalendarProviderKind provider)
    {
        if (provider is not (CalendarProviderKind.Google or CalendarProviderKind.Microsoft) ||
            _pendingProvider == provider)
        {
            return;
        }

        _pendingProvider = provider;
        OnPropertyChanged(nameof(GoogleChipBackground));
        OnPropertyChanged(nameof(GoogleChipForeground));
        OnPropertyChanged(nameof(MicrosoftChipBackground));
        OnPropertyChanged(nameof(MicrosoftChipForeground));
        OnPropertyChanged(nameof(CalendarTenantVisibility));
    }

    private Brush ProviderChipBackground(CalendarProviderKind provider) =>
        _pendingProvider == provider ? ProviderSelectedBackground : ProviderIdleBackground;

    private Brush ProviderChipForeground(CalendarProviderKind provider) =>
        _pendingProvider == provider ? ProviderSelectedForeground : ProviderIdleForeground;

    /// <summary>Reveals the provider + client-id panel in the General card.</summary>
    public void OpenCalendarLinkPanel()
    {
        if (_calendar.IsLinked)
        {
            return;
        }

        _calendarPanelOpen = true;
        CalendarStatus = string.Empty;
        OnPropertyChanged(nameof(CalendarPanelVisibility));
    }

    /// <summary>Runs the browser sign-in and reloads the agenda when it succeeds.</summary>
    public async Task ConnectCalendarAsync()
    {
        CalendarStatus = "Waiting for the browser…";

        var result = await _calendar.ConnectAsync(_pendingProvider, CalendarClientId, CalendarTenant);
        CalendarStatus = result.Message;

        if (!result.Ok)
        {
            return;
        }

        _calendarPanelOpen = false;
        OnPropertyChanged(nameof(CalendarPanelVisibility));
        await RefreshAgendaAsync();
        RaiseCalendarLinkState();
    }

    public void DisconnectCalendar()
    {
        _calendar.Disconnect();
        CalendarStatus = string.Empty;
        RaiseCalendarLinkState();
        _ = RefreshAgendaAsync();
    }

    private void RaiseCalendarLinkState()
    {
        OnPropertyChanged(nameof(CalendarLinkLabel));
        OnPropertyChanged(nameof(CalendarLinkVisibility));
        OnPropertyChanged(nameof(CalendarDisconnectVisibility));
        OnPropertyChanged(nameof(AgendaLinkVisibility));
        OnPropertyChanged(nameof(CalendarState));
        OnPropertyChanged(nameof(AgendaFooter));
    }

    // ===== Event reminders =====

    /// <summary>Arms (or replaces) the reminder for one event row.</summary>
    public void SetReminder(CalendarEventRow row, int leadMinutes)
    {
        _reminders.Set(row.Id, row.Subject, row.Start, leadMinutes);
        row.LeadMinutes = Math.Max(0, leadMinutes);
    }

    public void ClearReminder(CalendarEventRow row)
    {
        _reminders.Clear(row.Id);
        row.LeadMinutes = null;
    }

    /// <summary>Space and Enter both step the default session length in the Customize view.</summary>
    public void NudgeDefaultFocus(int delta)
    {
        var next = Math.Clamp(_settings.DefaultFocusMinutes + delta, 5, 120);
        if (next == _settings.DefaultFocusMinutes)
        {
            return;
        }

        _settings.DefaultFocusMinutes = next;
        _settingsStore.TrySave(_settings);

        if (!Timer.IsRunning)
        {
            Timer.SetLength(TimeSpan.FromMinutes(next));
        }

        OnPropertyChanged(nameof(FocusMinutesLabel));
    }

    public void MoveCard(int from, int to)
    {
        if (from < 0 || from >= CardRows.Count || to < 0 || to >= CardRows.Count || from == to)
        {
            return;
        }

        CardRows.Move(from, to);
        PersistCards();
    }

    public void ResetCustomization()
    {
        _settings.Reset();
        _settingsStore.TrySave(_settings);

        CardRows.CollectionChanged -= OnCardOrderChanged;
        CardRows.Clear();
        foreach (var pref in _settings.Cards)
        {
            CardRows.Add(CreateRow(pref));
        }

        if (!Timer.IsRunning)
        {
            Timer.SetLength(TimeSpan.FromMinutes(_settings.DefaultFocusMinutes));
        }

        CardRows.CollectionChanged += OnCardOrderChanged;

        OnPropertyChanged(nameof(CompletionSound));
        OnPropertyChanged(nameof(ReminderSound));
        OnPropertyChanged(nameof(FocusMinutesLabel));
        OnPropertyChanged(nameof(ChimeVolume));
        OnPropertyChanged(nameof(ChimeVolumeLabel));
        OnPropertyChanged(nameof(ExpiryTray));
        RaiseExtendChips();
        PersistCards();
    }

    private void OnCardOrderChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        PersistCards();

    private void LoadSettings()
    {
        CardRows.CollectionChanged -= OnCardOrderChanged;
        _settings = _settingsStore.Load();

        // A linked calendar remembers its client id in the encrypted record; reuse it when the
        // settings file predates the panel or was reset.
        if (string.IsNullOrWhiteSpace(_settings.CalendarClientId) && !string.IsNullOrWhiteSpace(_calendar.SavedClientId))
        {
            _settings.CalendarClientId = _calendar.SavedClientId;
            _settings.CalendarTenant = _calendar.SavedTenant;
        }

        if (_calendar.Provider != CalendarProviderKind.None)
        {
            _pendingProvider = _calendar.Provider;
        }

        Timer.SetLength(TimeSpan.FromMinutes(_settings.DefaultFocusMinutes));

        CardRows.Clear();
        foreach (var pref in _settings.Cards)
        {
            CardRows.Add(CreateRow(pref));
        }

        CardRows.CollectionChanged += OnCardOrderChanged;
    }

    private CardSettingRow CreateRow(CardPreference pref)
    {
        var (title, glyph) = pref.Id switch
        {
            WorkspaceSettings.TasksCard => ("Tasks", ""),
            WorkspaceSettings.TimerCard => ("Focus timer", ""),
            WorkspaceSettings.NotesCard => ("Notepad", ""),
            _ => ("Calendar events", ""),
        };

        var row = new CardSettingRow(pref.Id, title, glyph, pref.Visible);
        row.VisibilityChanged += OnCardVisibilityChanged;
        return row;
    }

    private void OnCardVisibilityChanged(object? sender, EventArgs e)
    {
        if (sender is not CardSettingRow row)
        {
            return;
        }

        var pref = _settings.Cards.FirstOrDefault(c => c.Id == row.Id);
        if (pref is not null)
        {
            pref.Visible = row.IsVisible;
        }

        PersistCards();
    }

    private void PersistCards()
    {
        _settings.Cards = CardRows
            .Select(r => new CardPreference { Id = r.Id, Visible = r.IsVisible })
            .ToList();

        _settingsStore.TrySave(_settings);

        OnPropertyChanged(nameof(VisibleCardCount));
        OnPropertyChanged(nameof(CardWidth));
        OnPropertyChanged(nameof(CardsEmptyVisibility));
        OnPropertyChanged(nameof(TasksCardVisibility));
        OnPropertyChanged(nameof(TimerCardVisibility));
        OnPropertyChanged(nameof(NotesCardVisibility));
        OnPropertyChanged(nameof(EventsCardVisibility));

        CardLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private Visibility CardVisibility(string id) =>
        IsCardVisible(id) ? Visibility.Visible : Visibility.Collapsed;

    private bool IsCardVisible(string id) =>
        CardRows.FirstOrDefault(r => r.Id == id)?.IsVisible ?? true;
}

