using System.Diagnostics;
using Nectar.Data;
using Nectar.Hooks;
using Nectar.Interop;
using Nectar.Services;
using Nectar.Services.Calendar;
using Nectar.Services.Dictation;
using Nectar.Settings;
using Nectar.ViewModels;
using Nectar.Windows;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;
using VirtualKey = Windows.System.VirtualKey;
using Windows.UI;
using Windows.UI.Core;
using WinRT.Interop;

namespace Nectar;

/// <summary>
/// The overlay host. Owns the hidden→pill→workspace state machine, the Win32 plumbing that keeps
/// the window non-activating and top-anchored, and the glue between the workspace view model and
/// the XAML storyboards.
/// </summary>
public sealed partial class MainWindow : Window, IDisposable
{
    private const double ExpandedWidth = 1000;
    private const double ExpandedHeight = 340;
    private const double WorkspaceContentHeight = 320;
    private const double InsightsContentHeight = 430;
    private const double SettingsContentHeight = 460;
    private const double PillWidth = 200;
    private const double PillHeight = 30;

    /// <summary>Focus-expiry tray: a compact strip that drops down when a session runs out.</summary>
    private const double TrayWidth = 560;
    private const double TrayHeight = 92;

    /// <summary>Top-edge hover band, 240x6 DIP scaled to physical pixels.</summary>
    private const double TriggerZoneWidth = 240;
    private const double TriggerZoneHeight = 6;

    private enum OverlayState
    {
        Hidden,
        Collapsed,
        Expanded,
    }

    /// <summary>Which pair of actions the shared tray is showing.</summary>
    private enum TrayMode
    {
        Focus,
        Reminder,
    }

    private readonly WorkspaceRepository _repository = new();
    private readonly LinkedCalendarService _calendar = new();
    private readonly FocusTimerService _timer;
    private readonly ReminderScheduler _reminders;
    private readonly CompletionChime _chime = new();
    private readonly SpeechDictationService _dictation;

    private readonly IntPtr _hwnd;
    private readonly WindowId _windowId;
    private readonly AppWindow _appWindow;
    private readonly OverlayWindowController _overlay;
    private readonly WindowGeometryService _geometry;
    private readonly FocusGate _focusGate;
    private readonly WindowMessageRouter _router;
    private readonly GlobalHotKeyService _hotKeys;
    private readonly KeyboardCaptureService _keyboardCapture;
    private readonly PointerProbeService _pointerProbe;
    private readonly DispatcherQueueTimer _collapseTimer;
    private readonly DispatcherQueueTimer _agendaTimer;
    private readonly DispatcherQueue _dispatcher;

    private SettingsWindow? _settingsWindow;
    private OverlayState _state = OverlayState.Hidden;
    private bool _loadingNote;
    private bool _disposed;

    /// <summary>True while the expiry tray occupies the window instead of the pill/workspace.</summary>
    private bool _trayVisible;

    /// <summary>The state the tray interrupted, so dismissing it restores the right surface.</summary>
    private OverlayState _trayReturnState = OverlayState.Collapsed;

    private TrayMode _trayMode = TrayMode.Focus;

    private ReminderScheduler.Reminder? _activeReminder;

    private bool _capturingHotKey;

    private uint _previousHotKeyModifiers;

    private uint _previousHotKeyKey;

    public MainWindow()
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("Nectar must be created on the UI thread.");
        _dispatcher = dispatcher;
        _dictation = new SpeechDictationService(dispatcher);

        _timer = new FocusTimerService(dispatcher);
        // Expiry is handled here rather than in the view model because it depends on overlay
        // state; the chime itself is gated by the Completion sound switch.
        _timer.SessionCompleted += OnFocusTimerExpired;
        _reminders = new ReminderScheduler(dispatcher);
        _reminders.Due += OnReminderDue;
        ViewModel = new WorkspaceViewModel(_repository, _calendar, _reminders, _timer, dispatcher);
        ViewModel.HoverToOpenChanged += OnHoverToOpenChanged;
        ViewModel.ShowPillChanged += OnShowPillChanged;

        InitializeComponent();

        _hwnd = WindowNative.GetWindowHandle(this);
        _windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(_windowId);

        // Chrome, taskbar/switcher visibility, always-on-top, rounded corners.
        _overlay = new OverlayWindowController(_appWindow, _hwnd);
        _overlay.Configure();

        // Top-centre anchoring + display change tracking.
        _geometry = new WindowGeometryService(_appWindow, _windowId);
        _geometry.SetScale(GetScale());
        _geometry.GeometryChanged += OnGeometryChanged;

        // Focus policy: WS_EX_NOACTIVATE, lifted only for text entry.
        _focusGate = new FocusGate(_hwnd);
        _focusGate.InteractiveChanged += OnInteractiveChanged;

        // Message loop hooks: WM_HOTKEY / WM_MOUSEACTIVATE / WM_ACTIVATE / WM_DPICHANGED / Esc.
        _router = new WindowMessageRouter(_hwnd)
        {
            MouseActivateEvaluator = _focusGate.EvaluateMouseActivate,
        };
        _router.HotKeyPressed += OnHotKeyPressed;
        _router.ActivationChanged += OnActivationChanged;
        _router.DpiChanged += OnDpiChanged;
        _router.DisplaySettingsChanged += OnDisplaySettingsChanged;
        _router.EscapePressed += OnEscapePressed;
        _router.Attach();

        _hotKeys = new GlobalHotKeyService();
        _hotKeys.Register(_hwnd, (uint)ViewModel.HotKeyModifiers, (uint)ViewModel.HotKeyKey);

        _keyboardCapture = new KeyboardCaptureService(dispatcher);
        _keyboardCapture.KeyDown += OnKeyPressed;

        _pointerProbe = new PointerProbeService(dispatcher);
        _pointerProbe.TriggerZoneEntered += OnTriggerZoneEntered;
        _pointerProbe.PointerLeftWindow += OnPointerLeftWindow;
        _pointerProbe.Start();

        _collapseTimer = dispatcher.CreateTimer();
        _collapseTimer.Interval = TimeSpan.FromMilliseconds(1100);
        _collapseTimer.IsRepeating = false;
        _collapseTimer.Tick += OnCollapseTick;

        _agendaTimer = dispatcher.CreateTimer();
        _agendaTimer.Interval = TimeSpan.FromMinutes(10);
        _agendaTimer.IsRepeating = true;
        _agendaTimer.Tick += OnAgendaTick;

        // No system backdrop: the shell paints opaque black, and attaching Mica re-initialises
        // the DWM frame (bringing the 1 px system border back after it was cleared).

        // Start life as a tiny, invisible pill anchored to the top edge.
        _geometry.Fit(PillWidth, PillHeight);
        UpdateTriggerZone();
        _pointerProbe.SetWindowBounds(default);

        Closed += OnWindowClosed;
        _ = LoadWorkspaceAsync();
    }

    public WorkspaceViewModel ViewModel { get; }

    /// <summary>Shortcut for the Insights bindings; owned by the workspace view model.</summary>
    public InsightsViewModel Insights => ViewModel.Insights;

    // ======================================================================================
    // Lifecycle
    // ======================================================================================

    /// <summary>Called by App.OnLaunched: the app starts as a background agent, not a window.</summary>
    internal void HideToBackground()
    {
        _state = OverlayState.Hidden;

        // A reminder can come due while startup is still finishing (the scheduler starts right
        // after the database opens). The tray already owns the surface in that case, and hiding
        // the window here would leave an invisible tray until it was dismissed.
        if (_trayVisible)
        {
            return;
        }

        _overlay.Hide();
        _pointerProbe.SetWindowBounds(default);
    }

    private async Task LoadWorkspaceAsync()
    {
        try
        {
            if (_overlay.SetupWarning is { Length: > 0 } overlayWarning)
            {
                ViewModel.ReportProblem(overlayWarning);
            }

            if (_hotKeys.Error is { Length: > 0 } hotkeyError)
            {
                ViewModel.ReportProblem(hotkeyError);
            }

            if (_pointerProbe.Error is { Length: > 0 } probeError)
            {
                ViewModel.ReportProblem(probeError);
            }

            await ViewModel.InitializeAsync();
            _agendaTimer.Start();
            _reminders.Start();
        }
        catch (Exception ex)
        {
            AppLogger.Log("Workspace initialization failed", ex);
            ViewModel.ReportProblem($"Nectar could not finish starting: {ex.Message}");
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        try
        {
            // Flush the debounced note synchronously: the process is about to end.
            ViewModel.SaveNoteNowAsync().Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            AppLogger.Log("Note flush on exit failed", ex);
        }

        Dispose();
    }

    // ======================================================================================
    // State machine
    // ======================================================================================

    /// <summary>Shell height that matches the currently visible content.</summary>
    private double DesiredShellHeight =>
        Math.Round(ShellContent.Height + (ExpandedHeight - WorkspaceContentHeight));

    private static void SetShellHeightAnimation(Storyboard board, double height, bool from = false)
    {
        if (board.Children.Count == 0 || board.Children[0] is not DoubleAnimation animation)
        {
            return;
        }

        if (from)
        {
            animation.From = height;
        }
        else
        {
            animation.To = height;
        }
    }

    private void Expand()
    {
        if (_state == OverlayState.Expanded)
        {
            return;
        }

        CancelCollapse();
        _state = OverlayState.Expanded;

        // An open workspace supersedes the expiry tray.
        _trayVisible = false;
        TimerExpiryTray.Visibility = Visibility.Collapsed;

        _overlay.Show();
        _geometry.Fit(ExpandedWidth, DesiredShellHeight);
        _pointerProbe.SetWindowBounds(_geometry.Bounds);

        // DWM recomputes the frame when the window is first presented and resized, which can
        // restore the 1 px system border. Re-clear it once composition has settled.
        _dispatcher.TryEnqueue(DispatcherQueuePriority.Low, _overlay.ApplyDwmPolish);

        PillSurface.Visibility = Visibility.Collapsed;
        WorkspaceSurface.Visibility = Visibility.Visible;
        WorkspaceSurface.Height = 0;
        WorkspaceSurface.Opacity = 0;

        if (Root.Resources["ExpandStoryboard"] is Storyboard expand)
        {
            (Root.Resources["CollapseStoryboard"] as Storyboard)?.Stop();
            SetShellHeightAnimation(expand, DesiredShellHeight);
            if (Root.Resources["CollapseStoryboard"] is Storyboard collapse)
            {
                SetShellHeightAnimation(collapse, DesiredShellHeight, from: true);
            }

            expand.Begin();
        }
        else
        {
            WorkspaceSurface.Height = DesiredShellHeight;
            WorkspaceSurface.Opacity = 1;
        }
    }

    private void Collapse(bool hideAfter)
    {
        // With the pill switched off there is no surface to fold back to: park the window.
        hideAfter |= !ViewModel.ShowPill;

        if (_state != OverlayState.Expanded)
        {
            if (hideAfter && _overlay.IsVisible)
            {
                HideToBackground();
            }

            return;
        }

        CancelCollapse();
        _focusGate.EndInteractive();
        _state = hideAfter ? OverlayState.Hidden : OverlayState.Collapsed;

        if (Root.Resources["CollapseStoryboard"] is not Storyboard collapse)
        {
            FinishCollapse(hideAfter);
            return;
        }

        (Root.Resources["ExpandStoryboard"] as Storyboard)?.Stop();

        EventHandler<object>? completed = null;
        completed = (_, _) =>
        {
            collapse.Completed -= completed;
            FinishCollapse(hideAfter);
        };

        collapse.Completed += completed;
        collapse.Begin();
    }

    private void FinishCollapse(bool hideAfter)
    {

        // A collapse animation that lands after the tray appeared must not shrink the window
        // back to the pill: the tray owns the surface until it is dismissed.
        if (_trayVisible)
        {
            return;
        }

        WorkspaceSurface.Visibility = Visibility.Collapsed;
        WorkspaceSurface.Height = 0;
        WorkspaceSurface.Opacity = 0;
        PillSurface.Visibility = ViewModel.ShowPill ? Visibility.Visible : Visibility.Collapsed;

        if (hideAfter)
        {
            _overlay.Hide();
            _state = OverlayState.Hidden;
        }
        else
        {
            _geometry.Fit(PillWidth, PillHeight);
            _state = OverlayState.Collapsed;
        }

        _pointerProbe.SetWindowBounds(_overlay.IsVisible ? _geometry.Bounds : default);
    }

    private void Toggle()
    {
        if (_state == OverlayState.Expanded)
        {
            Collapse(hideAfter: false);
        }
        else
        {
            Expand();
        }
    }

    private void ScheduleCollapse()
    {
        if (_state != OverlayState.Expanded || _focusGate.IsInteractive || _capturingHotKey)
        {
            return;
        }

        _collapseTimer.Stop();
        _collapseTimer.Start();
    }

    private void CancelCollapse() => _collapseTimer.Stop();

    private void OnCollapseTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();

        if (_state != OverlayState.Expanded || _focusGate.IsInteractive || _capturingHotKey)
        {
            return;
        }

        // The probe only re-arms on movement, so a still cursor resting on the panel
        // would otherwise fold the shell while it is being read.
        if (_pointerProbe.IsPointerInsideWindow)
        {
            return;
        }

        Collapse(hideAfter: false);
    }

    private void OnAgendaTick(DispatcherQueueTimer sender, object args) =>
        _ = ViewModel.RefreshAgendaAsync();

    // ======================================================================================
    // Focus expiry: tray + chime
    // ======================================================================================

    /// <summary>
    /// 00:00 hook. The chime plays in every state; the tray is strictly conditional — if the
    /// workspace is on screen the session simply ends there and no notification appears.
    /// </summary>
    private void OnFocusTimerExpired(int minutes)
    {
        // One signal, one owner. The bird call is the completion sound: the Customize switch
        // decides whether it plays, and the volume (0%) is the second way to mute it. Nothing
        // else in the app beeps at 00:00.
        if (ViewModel.CompletionSound)
        {
            _chime.Volume = ViewModel.ChimeVolume / 100d;
            _chime.Play();
        }
        else
        {
            AppLogger.Log($"Focus session ({minutes} min) ended with the completion sound switched off.");
        }

        if (_state == OverlayState.Expanded)
        {
            AppLogger.Log($"Focus session ({minutes} min) ended with the workspace open: no tray.");
            return;
        }

        if (!ViewModel.ExpiryTray)
        {
            AppLogger.Log($"Focus session ({minutes} min) ended: the expiry tray is switched off.");
            return;
        }

        AppLogger.Log($"Focus session ({minutes} min) ended with the workspace closed: showing tray.");
        ShowExpiryTray();
    }

    private void ShowExpiryTray() =>
        ShowTray(ViewModel.FocusTaskTitle, DateTime.Now.ToString("h:mm tt"), TrayMode.Focus);

    /// <summary>
    /// Drops the shared tray. The focus expiry and a calendar reminder use the same surface and
    /// the same bird; only the action pair differs (Extend/Done versus Snooze/Okay).
    /// </summary>
    private void ShowTray(string title, string timeText, TrayMode mode)
    {
        _trayReturnState = _state;
        _trayVisible = true;
        _trayMode = mode;

        TrayTaskName.Text = title;
        TrayTimeLabel.Text = timeText;

        var reminder = mode == TrayMode.Reminder;
        TrayFocusActions.Visibility = reminder ? Visibility.Collapsed : Visibility.Visible;
        TrayReminderActions.Visibility = reminder ? Visibility.Visible : Visibility.Collapsed;

        PillSurface.Visibility = Visibility.Collapsed;
        WorkspaceSurface.Visibility = Visibility.Collapsed;
        WorkspaceSurface.Height = 0;
        WorkspaceSurface.Opacity = 0;
        TimerExpiryTray.Visibility = Visibility.Visible;

        CancelCollapse();
        _overlay.Show();
        _geometry.Fit(TrayWidth, TrayHeight);
        _pointerProbe.SetWindowBounds(_geometry.Bounds);
    }

    /// <summary>Puts the overlay back the way the tray found it.</summary>
    private void HideExpiryTray()
    {
        if (!_trayVisible)
        {
            return;
        }

        _trayVisible = false;
        _activeReminder = null;
        _trayMode = TrayMode.Focus;
        TimerExpiryTray.Visibility = Visibility.Collapsed;

        if (_trayReturnState == OverlayState.Expanded)
        {
            // A reminder can arrive while the workspace is open; re-open it afterwards.
            _state = OverlayState.Collapsed;
            Expand();
            return;
        }

        if (_trayReturnState == OverlayState.Hidden)
        {
            _state = OverlayState.Hidden;
            _overlay.Hide();
            _pointerProbe.SetWindowBounds(default);
            return;
        }

        if (!ViewModel.ShowPill)
        {
            _state = OverlayState.Hidden;
            PillSurface.Visibility = Visibility.Collapsed;
            _overlay.Hide();
            _pointerProbe.SetWindowBounds(default);
            return;
        }

        _state = OverlayState.Collapsed;
        PillSurface.Visibility = Visibility.Visible;
        _geometry.Fit(PillWidth, PillHeight);
        _pointerProbe.SetWindowBounds(_geometry.Bounds);
    }

    /// <summary>Adds the clicked chip's minutes (5 / 10 / 15) and folds the tray away.</summary>
    private void OnTrayExtendClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string minutes } || !int.TryParse(minutes, out var amount))
        {
            return;
        }

        _chime.Stop();
        ViewModel.ExtendFocus(amount);
        AppLogger.Log($"Focus session extended by {amount} min from the tray.");
        HideExpiryTray();
    }

    private void OnTrayDoneClick(object sender, RoutedEventArgs e)
    {
        _chime.Stop();
        ViewModel.CompleteFocusTask();
        ViewModel.Timer.Reset();
        HideExpiryTray();
    }

    // ======================================================================================
    // Calendar reminders: same tray, Snooze / Okay instead of Extend / Done
    // ======================================================================================

    private void OnReminderDue(object? sender, ReminderScheduler.Reminder reminder)
    {
        // The reminder ring has its own switch: silencing the focus completion must not silence
        // calendar alerts, and vice versa. The tray appears either way; only the sound is gated.
        if (ViewModel.ReminderSound)
        {
            _chime.Volume = ViewModel.ChimeVolume / 100d;
            _chime.Play();
        }
        else
        {
            AppLogger.Log($"Reminder \"{reminder.Title}\" is due with the reminder sound switched off.");
        }

        _activeReminder = reminder;

        ShowTray(reminder.Title, $"Reminder · {reminder.StartsAt:h:mm tt}", TrayMode.Reminder);
        AppLogger.Log($"Reminder \"{reminder.Title}\" is due: showing tray.");
    }

    /// <summary>Pushes the same reminder back by five minutes; the tray folds away meanwhile.</summary>
    private void OnTraySnoozeClick(object sender, RoutedEventArgs e)
    {
        if (_activeReminder is not { } reminder)
        {
            return;
        }

        _chime.Stop();
        _reminders.Snooze(reminder, TimeSpan.FromMinutes(ReminderScheduler.DefaultSnoozeMinutes));
        HideExpiryTray();
    }

    private void OnTrayOkayClick(object sender, RoutedEventArgs e)
    {
        if (_activeReminder is not { } reminder)
        {
            return;
        }

        _chime.Stop();
        _reminders.Dismiss(reminder);
        HideExpiryTray();
    }

    // ======================================================================================
    // General: open shortcut, hover and calendar linking
    // ======================================================================================

    /// <summary>Arms chord capture; the next non-modifier key becomes the open gesture.</summary>
    private void OnShortcutButtonClick(object sender, RoutedEventArgs e)
    {
        if (_capturingHotKey)
        {
            CancelShortcutCapture();
            return;
        }

        _previousHotKeyModifiers = (uint)ViewModel.HotKeyModifiers;
        _previousHotKeyKey = (uint)ViewModel.HotKeyKey;
        _capturingHotKey = true;
        ViewModel.BeginShortcutCapture();
        _keyboardCapture.Start();
    }

    private void OnKeyPressed(object? sender, KeyboardCaptureService.CapturedKey captured)
    {
        if (!_capturingHotKey)
        {
            return;
        }

        var key = captured.Key;

        if (key == NativeMethods.VK_ESCAPE)
        {
            CancelShortcutCapture();
            return;
        }

        if (GlobalHotKeyService.IsModifierKey(key))
        {
            return;
        }

        var modifiers = captured.Modifiers;
        if (modifiers == 0)
        {
            ViewModel.ReportProblem("A shortcut needs Ctrl, Alt, Shift or Win.");
            CancelShortcutCapture();
            return;
        }

        var name = GlobalHotKeyService.Describe(modifiers, key);
        if (_hotKeys.Register(_hwnd, modifiers, key))
        {
            ViewModel.ApplyShortcut(modifiers, key);
            FinishShortcutCapture();
            AppLogger.Log($"Open shortcut changed to {name}.");
            return;
        }

        // Never leave the app without a way in: put the previous chord straight back.
        var failure = _hotKeys.Error ?? $"{name} could not be registered.";
        _hotKeys.Register(_hwnd, _previousHotKeyModifiers, _previousHotKeyKey);
        FinishShortcutCapture();
        ViewModel.ReportProblem(failure);
    }

    private void FinishShortcutCapture()
    {
        _capturingHotKey = false;
        _keyboardCapture.Stop();
        ViewModel.EndShortcutCapture();
    }

    private void CancelShortcutCapture()
    {
        if (!_capturingHotKey)
        {
            return;
        }

        FinishShortcutCapture();
        AppLogger.Log("Shortcut change cancelled.");
    }

    private void OnHoverToOpenChanged(object? sender, EventArgs e) => UpdateTriggerZone();

    private void OnHoverToggled(object sender, RoutedEventArgs e) => UpdateTriggerZone();

    private void OnShowPillToggled(object sender, RoutedEventArgs e) => UpdatePillVisibility();

    private void OnShowPillChanged(object? sender, EventArgs e) => UpdatePillVisibility();

    /// <summary>
    /// Applies the pill switch to the live window: off parks Nectar completely, on brings the
    /// pill back the next time the workspace folds away.
    /// </summary>
    private void UpdatePillVisibility()
    {
        if (_trayVisible)
        {
            return;
        }

        if (ViewModel.ShowPill)
        {
            if (_state != OverlayState.Hidden)
            {
                return;
            }

            PillSurface.Visibility = Visibility.Visible;
            _state = OverlayState.Collapsed;
            _overlay.Show();
            _geometry.Fit(PillWidth, PillHeight);
            _pointerProbe.SetWindowBounds(_geometry.Bounds);
            return;
        }

        if (_state != OverlayState.Collapsed)
        {
            return;
        }

        PillSurface.Visibility = Visibility.Collapsed;
        _state = OverlayState.Hidden;
        _overlay.Hide();
        _pointerProbe.SetWindowBounds(default);
    }

    /// <summary>The Events card's empty state opens Settings with the link panel already showing.</summary>
    private void OnLinkCalendarFromAgendaClick(object sender, RoutedEventArgs e) => OpenSettings(openCalendarLink: true);

    /// <summary>The gear beside the tabs. One window at a time; the second click raises it.</summary>
    private void OnSettingsClick(object sender, RoutedEventArgs e) => OpenSettings();

    private void OpenSettings(bool openCalendarLink = false)
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        if (openCalendarLink)
        {
            ViewModel.OpenCalendarLinkPanel();
        }

        _settingsWindow = new SettingsWindow(ViewModel, _dictation.DescribeMicrophoneAsync);
        _settingsWindow.Closed += OnSettingsWindowClosed;
        _settingsWindow.Activate();
    }

    private void OnSettingsWindowClosed(object sender, WindowEventArgs args) => _settingsWindow = null;

    /// <summary>Bell on an event row: pick a lead time, or drop an existing reminder.</summary>
    private void OnEventReminderClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CalendarEventRow row } button)
        {
            return;
        }

        var flyout = new MenuFlyout();

        foreach (var lead in new[] { 0, 5, 10 })
        {
            var minutes = lead;
            var item = new MenuFlyoutItem
            {
                Text = lead == 0 ? "At the start" : $"{lead} min before",
            };

            item.Click += (_, _) => ViewModel.SetReminder(row, minutes);
            flyout.Items.Add(item);
        }

        if (row.HasReminder)
        {
            flyout.Items.Add(new MenuFlyoutSeparator());

            var remove = new MenuFlyoutItem { Text = "Remove reminder" };
            remove.Click += (_, _) => ViewModel.ClearReminder(row);
            flyout.Items.Add(remove);
        }

        flyout.ShowAt(button);
    }

    // ======================================================================================
    // Hooks / message plumbing
    // ======================================================================================
    private void OnHotKeyPressed(object? sender, int id)
    {
        if (_hotKeys.Matches(id))
        {
            Toggle();
        }
    }

    private void OnTriggerZoneEntered(object? sender, EventArgs e)
    {
        if (_state != OverlayState.Expanded)
        {
            Expand();
        }
    }

    private void OnPointerLeftWindow(object? sender, EventArgs e) => ScheduleCollapse();

    private void OnActivationChanged(object? sender, bool activated)
    {
        if (activated)
        {
            CancelCollapse();
        }
        else
        {
            _focusGate.EndInteractive();
            ScheduleCollapse();
        }
    }

    private void OnInteractiveChanged(object? sender, bool interacting)
    {
        if (interacting)
        {
            CancelCollapse();
        }
        else
        {
            ScheduleCollapse();
        }
    }

    private void OnDpiChanged(object? sender, EventArgs e) => RebuildGeometry();

    /// <summary>Resolution / monitor / work-area change: re-anchor immediately.</summary>
    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => RebuildGeometry();

    private void RebuildGeometry()
    {
        _geometry.ReAnchor(_hwnd);
        UpdateTriggerZone();

        if (_state == OverlayState.Expanded)
        {
            _geometry.Fit(ExpandedWidth, DesiredShellHeight);
        }
        else
        {
            _geometry.Fit(PillWidth, PillHeight);
        }

        _pointerProbe.SetWindowBounds(_overlay.IsVisible ? _geometry.Bounds : default);
    }

    private void OnEscapePressed(object? sender, EventArgs e)
    {
        if (_capturingHotKey)
        {
            CancelShortcutCapture();
            return;
        }

        if (_trayVisible)
        {
            _chime.Stop();

            // Esc on a reminder counts as Okay: the user has seen it and no longer needs it.
            if (_trayMode == TrayMode.Reminder && _activeReminder is { } reminder)
            {
                _reminders.Dismiss(reminder);
            }

            HideExpiryTray();
            return;
        }

        if (_state == OverlayState.Expanded)
        {
            Collapse(hideAfter: false);
        }
    }

    private void OnGeometryChanged(object? sender, EventArgs e)
    {
        if (_overlay.IsVisible)
        {
            _pointerProbe.SetWindowBounds(_geometry.Bounds);
        }
    }

    private void UpdateTriggerZone()
    {
        if (!ViewModel.HoverToOpen)
        {
            // Hover opening switched off: the top-edge band stops answering.
            _pointerProbe.SetTriggerZone(default);
            return;
        }

        var width = Math.Max(8, (int)Math.Round(TriggerZoneWidth * _geometry.Scale));
        var height = Math.Max(4, (int)Math.Round(TriggerZoneHeight * _geometry.Scale));

        try
        {
            var area = DisplayArea.GetFromWindowId(_windowId, DisplayAreaFallback.Primary);
            var work = area.WorkArea;
            _pointerProbe.SetTriggerZone(new RectInt32(work.X + (work.Width - width) / 2, work.Y, width, height));
        }
        catch (Exception)
        {
            _pointerProbe.SetTriggerZone(new RectInt32(0, 0, width, height));
        }
    }

    private double GetScale()
    {
        try
        {
            var dpi = NativeMethods.GetDpiForWindow(_hwnd);
            return dpi > 0 ? dpi / 96d : 1d;
        }
        catch (Exception)
        {
            return 1d;
        }
    }

    // ======================================================================================
    // UI handlers
    // ======================================================================================

    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        // TextBox handles PointerPressed internally, so subscribe with handledEventsToo.
        NoteBox.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnTextSurfacePressed), true);
        AddTaskBox.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnTextSurfacePressed), true);

        _loadingNote = true;
        NoteBox.Text = ViewModel.NoteText;
        _loadingNote = false;

        _pointerProbe.SetWindowBounds(_overlay.IsVisible ? _geometry.Bounds : default);

        ViewModel.CardLayoutChanged += OnCardLayoutChanged;
        SelectTab(ShellTab.Workspace);
        ApplyCardLayout();
    }

    private void OnPillPointerEntered(object sender, PointerRoutedEventArgs e) => Expand();

    private void OnWorkspacePointerEntered(object sender, PointerRoutedEventArgs e) => CancelCollapse();

    private void OnWorkspacePointerExited(object sender, PointerRoutedEventArgs e) => ScheduleCollapse();

    private void OnCollapseClick(object sender, RoutedEventArgs e) => Collapse(hideAfter: false);

    private void OnTimerToggleClick(object sender, RoutedEventArgs e) => ViewModel.Timer.Toggle();

    private void OnTimerResetClick(object sender, RoutedEventArgs e) => ViewModel.Timer.Reset();

    private void OnRefreshAgendaClick(object sender, RoutedEventArgs e) =>
        _ = ViewModel.RefreshAgendaAsync();

    /// <summary>
    /// Explicit focus opt-in: the window is non-activating, so a text field only takes keyboard
    /// focus when the user actually clicks it.
    /// </summary>
    private void OnTextSurfacePressed(object sender, PointerRoutedEventArgs e)
    {
        _focusGate.BeginInteractive();

        if (sender is TextBox box)
        {
            box.Focus(FocusState.Programmatic);
        }
    }

    private void OnSetDurationClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string minutes })
        {
            return;
        }

        if (int.TryParse(minutes, out var value))
        {
            ViewModel.Timer.SetLength(TimeSpan.FromMinutes(value));
        }
    }

    private void OnAddTaskKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        var text = AddTaskBox.Text;
        AddTaskBox.Text = string.Empty;
        _ = ViewModel.AddTaskAsync(text);
    }

    /// <summary>
    /// Keeps the seven chart columns evenly spread as the card resizes (DPI change, card width):
    /// the bars and the axis labels then stay aligned with the plot's hairline grid.
    /// </summary>
    private void OnChartPlotSizeChanged(object sender, SizeChangedEventArgs e) =>
        Insights.SetChartWidth(e.NewSize.Width);

    private void OnDeleteTaskClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TaskRow row })
        {
            _ = ViewModel.DeleteTaskAsync(row);
        }
    }

    private void OnNoteTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingNote)
        {
            return;
        }

        ViewModel.OnNoteChanged(NoteBox.Text ?? string.Empty);
    }

    /// <summary>Ctrl+Enter promotes the caret's line out of the note and into Tasks.</summary>
    private void OnNoteKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        var control = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        if ((control & CoreVirtualKeyStates.Down) != CoreVirtualKeyStates.Down)
        {
            return;
        }

        e.Handled = true;

        var text = NoteBox.Text ?? string.Empty;
        var caret = Math.Clamp(NoteBox.SelectionStart, 0, text.Length);

        var lineStart = caret > 0 ? text.LastIndexOf('\n', caret - 1) + 1 : 0;
        var lineEnd = text.IndexOf('\n', caret);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        var line = text.Substring(lineStart, lineEnd - lineStart).Trim();
        if (line.Length == 0)
        {
            return;
        }

        // Swallow the newline that separated the promoted line from the rest of the note.
        var cutLength = lineEnd - lineStart;
        if (lineEnd < text.Length)
        {
            cutLength++;
        }

        var remaining = text.Remove(lineStart, cutLength);

        _loadingNote = true;
        NoteBox.Text = remaining;
        NoteBox.SelectionStart = Math.Min(lineStart, remaining.Length);
        _loadingNote = false;

        ViewModel.OnNoteChanged(remaining);
        _ = ViewModel.AddTaskAsync(line);
    }

    private void OnDismissBannerClick(object sender, RoutedEventArgs e) => ViewModel.DismissBanner();

    /// <summary>
    /// Starts Windows Voice Typing into the Add-a-task box. The box is both the focus target and
    /// the field where dictated text lands, so we focus it first then synthesize the Win+H chord.
    /// </summary>
    private async void OnTaskDictationClick(object sender, RoutedEventArgs e)
    {
        AppLogger.Log("OnTaskDictationClick entered");
        _focusGate.BeginInteractive();

        var gotFocus = AddTaskBox.Focus(FocusState.Programmatic);
        AppLogger.Log("OnTaskDictationClick AddTaskBox.Focus returned " + gotFocus);

        if (gotFocus)
        {
            await StartDictationAsync(AddTaskBox);
        }
    }

    /// <summary>
    /// Starts Windows Voice Typing into the note box. Same pattern: focus the editable field,
    /// then toggle Voice Typing so Windows writes the transcript into the caret position.
    /// </summary>
    private async void OnNoteDictationClick(object sender, RoutedEventArgs e)
    {
        AppLogger.Log("OnNoteDictationClick entered");
        _focusGate.BeginInteractive();

        var gotFocus = NoteBox.Focus(FocusState.Programmatic);
        AppLogger.Log("OnNoteDictationClick NoteBox.Focus returned " + gotFocus);

        if (gotFocus)
        {
            await StartDictationAsync(NoteBox);
        }
    }

    /// <summary>
    /// Tries in-app dictation first and falls back to Windows Voice Typing (Win + H) whenever the
    /// recognizer is unavailable, the microphone is denied, or a policy blocks it. A dictation
    /// button must always do something audible to the user; it must never be a dead control.
    /// </summary>
    private async Task StartDictationAsync(TextBox target)
    {
        var plan = DictationFallback.For(await _dictation.ToggleAsync(target));

        if (!plan.UseWindowsVoiceTyping)
        {
            return;
        }

        if (plan.Banner is not null)
        {
            ViewModel.ShowMessage(plan.Banner);
        }

        if (plan.LogMessage is not null)
        {
            AppLogger.Log(plan.LogMessage);
        }

        // The field already has focus, so Windows writes the transcript into the caret position.
        DictationInput.Start();
        target.Focus(FocusState.Programmatic);
    }

    // ======================================================================================
    // Shell tabs and card layout
    // ======================================================================================

    private enum ShellTab
    {
        Workspace,
        Insights,
        Customize,
    }

    private void OnWorkspaceTabClick(object sender, RoutedEventArgs e) => SelectTab(ShellTab.Workspace);

    private void OnInsightsTabClick(object sender, RoutedEventArgs e) => SelectTab(ShellTab.Insights);

    private void OnCustomizeTabClick(object sender, RoutedEventArgs e) => SelectTab(ShellTab.Customize);
    /// <summary>Swaps the three views and keeps the segmented control in sync.</summary>
    private void SelectTab(ShellTab tab)
    {
        WorkspaceView.Visibility = tab == ShellTab.Workspace ? Visibility.Visible : Visibility.Collapsed;
        InsightsView.Visibility = tab == ShellTab.Insights ? Visibility.Visible : Visibility.Collapsed;
        CustomizeView.Visibility = tab == ShellTab.Customize ? Visibility.Visible : Visibility.Collapsed;

        ShellContent.Height = tab switch
        {
            ShellTab.Workspace => WorkspaceContentHeight,
            ShellTab.Insights => InsightsContentHeight,
            _ => SettingsContentHeight,
        };

        if (_state == OverlayState.Expanded)
        {
            WorkspaceSurface.Height = DesiredShellHeight;
            _geometry.Fit(ExpandedWidth, DesiredShellHeight);
            _pointerProbe.SetWindowBounds(_geometry.Bounds);
        }

        StyleTab(WorkspaceTabButton, tab == ShellTab.Workspace);
        StyleTab(InsightsTabButton, tab == ShellTab.Insights);
        StyleTab(CustomizeTabButton, tab == ShellTab.Customize);

        if (tab == ShellTab.Insights)
        {
            // Re-read the window on every open: tasks and focus sessions may have changed while
            // another tab was showing.
            _ = ViewModel.RefreshInsightsAsync();
        }
    }

    private static void StyleTab(Button button, bool isActive)
    {
        button.Background = isActive
            ? (Brush)Application.Current.Resources["NectarTabActiveBrush"]
            : new SolidColorBrush(Colors.Transparent);
        button.Foreground = isActive
            ? new SolidColorBrush(Colors.White)
            : new SolidColorBrush(Color.FromArgb(0x8A, 0xFF, 0xFF, 0xFF));
    }

    private void OnCardLayoutChanged(object? sender, EventArgs e) => ApplyCardLayout();

    /// <summary>
    /// Rebuilds the card row from the saved order: hidden cards collapse, and the remaining
    /// cards are re-inserted in the order the Customize list defines.
    /// </summary>
    private void ApplyCardLayout()
    {
        var cards = new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase)
        {
            [WorkspaceSettings.TasksCard] = TasksCard,
            [WorkspaceSettings.TimerCard] = TimerCard,
            [WorkspaceSettings.NotesCard] = NotesCard,
            [WorkspaceSettings.EventsCard] = EventsCard,
        };

        var order = new List<UIElement>(cards.Count);
        foreach (var row in ViewModel.CardRows)
        {
            if (!cards.TryGetValue(row.Id, out var card))
            {
                continue;
            }

            card.Visibility = row.IsVisible ? Visibility.Visible : Visibility.Collapsed;
            order.Add(card);
        }

        // Reparent in place: remove + insert keeps the same elements, so scroll and
        // focus state on the cards survive a reorder.
        for (var index = 0; index < order.Count; index++)
        {
            var card = order[index];
            if (WorkspaceView.Children.IndexOf(card) == index)
            {
                continue;
            }

            WorkspaceView.Children.Remove(card);
            WorkspaceView.Children.Insert(Math.Min(index, WorkspaceView.Children.Count), card);
        }
    }

    private void OnFocusMinusClick(object sender, RoutedEventArgs e) => ViewModel.NudgeDefaultFocus(-5);
    private void OnFocusPlusClick(object sender, RoutedEventArgs e) => ViewModel.NudgeDefaultFocus(5);

    private void OnChimeVolumeMinusClick(object sender, RoutedEventArgs e) => ViewModel.NudgeChimeVolume(-10);

    private void OnChimeVolumePlusClick(object sender, RoutedEventArgs e) => ViewModel.NudgeChimeVolume(10);

    private void OnQuitClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.SaveNoteNowAsync().Wait(TimeSpan.FromSeconds(1));
        }
        catch (Exception ex)
        {
            AppLogger.Log("Note flush on quit failed", ex);
        }

        Dispose();
        Application.Current.Exit();
    }

    // ======================================================================================
    // Teardown
    // ======================================================================================

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_settingsWindow is not null)
        {
            _settingsWindow.Closed -= OnSettingsWindowClosed;
            _settingsWindow.Close();
            _settingsWindow = null;
        }

        _collapseTimer.Stop();
        _agendaTimer.Stop();
        _collapseTimer.Tick -= OnCollapseTick;
        _agendaTimer.Tick -= OnAgendaTick;

        _pointerProbe.TriggerZoneEntered -= OnTriggerZoneEntered;
        _pointerProbe.PointerLeftWindow -= OnPointerLeftWindow;
        _pointerProbe.Dispose();

        _reminders.Due -= OnReminderDue;
        _reminders.Dispose();
        ViewModel.HoverToOpenChanged -= OnHoverToOpenChanged;
        ViewModel.ShowPillChanged -= OnShowPillChanged;

        _keyboardCapture.KeyDown -= OnKeyPressed;
        _keyboardCapture.Dispose();

        _router.HotKeyPressed -= OnHotKeyPressed;
        _router.ActivationChanged -= OnActivationChanged;
        _router.DpiChanged -= OnDpiChanged;
        _router.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _router.EscapePressed -= OnEscapePressed;
        _router.Dispose();

        _hotKeys.Dispose();
        _focusGate.InteractiveChanged -= OnInteractiveChanged;
        _geometry.GeometryChanged -= OnGeometryChanged;
        _geometry.Dispose();
        _timer.SessionCompleted -= OnFocusTimerExpired;
        _timer.Dispose();
        _dictation.Dispose();
        _chime.Dispose();
    }
}
