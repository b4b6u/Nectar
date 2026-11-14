using System.ComponentModel;
using System.Diagnostics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Nectar.Interop;
using Nectar.Services;
using Nectar.Services.Calendar;
using Nectar.ViewModels;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace Nectar;

/// <summary>
/// The settings window: the calendar link, what is on disk and in the microphone, permissions and
/// updates, the look, and the reset. It shares the workspace view model with the overlay, so a
/// calendar linked here shows up on the Events card without a restart, and a theme chosen here
/// repaints the notch behind it.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private const string WebsiteUrl = "https://nectarnotch.vercel.app";
    private const string ReleasesUrl = "https://github.com/b4b6u/Nectar/releases";

    private readonly Func<Task<string>> _readMicrophone;
    private readonly Dictionary<string, RadioButton> _themeChoices = new();
    private bool _syncingTheme;

    public SettingsWindow(WorkspaceViewModel viewModel, Func<Task<string>> readMicrophone)
    {
        InitializeComponent();

        ViewModel = viewModel;
        _readMicrophone = readMicrophone;

        Title = "Nectar settings";
        SizeToContent();

        BuildThemeChoices();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += OnClosed;

        _ = RefreshMicrophoneAsync();
    }

    public WorkspaceViewModel ViewModel { get; }

    /// <summary>
    /// 760 x 720 DIP is comfortable at 100% and unusable at 200%, so the size is scaled by the
    /// window's own DPI and capped against the work area of the display it opens on.
    /// </summary>
    private void SizeToContent()
    {
        var scale = NativeMethods.GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96.0;
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;

        var width = Math.Min((int)(760 * scale), (int)(workArea.Width * 0.9));
        var height = Math.Min((int)(720 * scale), (int)(workArea.Height * 0.9));

        AppWindow.Resize(new SizeInt32(width, height));
    }

    public string VersionLabel
    {
        get
        {
            var version = typeof(App).Assembly.GetName().Version;
            return version is null ? "1.0" : $"v{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    /// <summary>
    /// One radio per theme, each with its four card colours as swatches so the choice can be made
    /// without applying it first.
    /// </summary>
    private void BuildThemeChoices()
    {
        _syncingTheme = true;

        foreach (var theme in AppTheme.Options)
        {
            var swatches = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
            foreach (var color in new[] { theme.Mint, theme.Lilac, theme.Butter, theme.Sky })
            {
                swatches.Children.Add(new Ellipse
                {
                    Width = 11,
                    Height = 11,
                    Fill = new SolidColorBrush(color),
                });
            }

            var label = new TextBlock
            {
                Text = theme.Name,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["NectarInkBrush"],
            };

            var hint = new TextBlock
            {
                Text = theme.Hint,
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["NectarInkMutedBrush"],
            };

            var text = new StackPanel { Spacing = 1 };
            text.Children.Add(label);
            text.Children.Add(hint);

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9 };
            row.Children.Add(swatches);
            row.Children.Add(text);

            var choice = new RadioButton
            {
                GroupName = "NectarTheme",
                Content = row,
                Tag = theme.Id,
                IsChecked = string.Equals(theme.Id, ViewModel.Theme, StringComparison.OrdinalIgnoreCase),
                Padding = new Thickness(9, 6, 9, 6),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF)),
                CornerRadius = new CornerRadius(10),
            };

            choice.Checked += OnThemeChecked;
            _themeChoices[theme.Id] = choice;
            ThemeChoices.Children.Add(choice);
        }

        _syncingTheme = false;
    }

    private void OnThemeChecked(object sender, RoutedEventArgs e)
    {
        if (_syncingTheme || sender is not RadioButton { Tag: string id })
        {
            return;
        }

        ViewModel.Theme = id;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WorkspaceViewModel.Theme))
        {
            return;
        }

        if (_themeChoices.TryGetValue(ViewModel.Theme, out var choice) && choice.IsChecked != true)
        {
            _syncingTheme = true;
            choice.IsChecked = true;
            _syncingTheme = false;
        }
    }

    private async Task RefreshMicrophoneAsync()
    {
        try
        {
            ViewModel.SetMicrophoneStatus(await _readMicrophone());
        }
        catch (Exception ex)
        {
            AppLogger.Log("Could not read the microphone state", ex);
            ViewModel.SetMicrophoneStatus("Unknown");
        }

        ViewModel.RefreshPrivacyLabels();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnClosed(object sender, WindowEventArgs args) => ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

    private void OnOpenCalendarLinkClick(object sender, RoutedEventArgs e) => ViewModel.OpenCalendarLinkPanel();

    private void OnDisconnectCalendarClick(object sender, RoutedEventArgs e) => ViewModel.DisconnectCalendar();

    private void OnCalendarProviderGoogleClick(object sender, RoutedEventArgs e) =>
        ViewModel.SelectCalendarProvider(CalendarProviderKind.Google);

    private void OnCalendarProviderMicrosoftClick(object sender, RoutedEventArgs e) =>
        ViewModel.SelectCalendarProvider(CalendarProviderKind.Microsoft);

    private async void OnConnectCalendarClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.ConnectCalendarAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Log("Calendar linking failed", ex);
            ViewModel.ReportProblem($"Calendar linking failed: {ex.Message}");
        }
    }

    private async void OnCheckMicrophoneClick(object sender, RoutedEventArgs e) => await RefreshMicrophoneAsync();

    private void OnOpenMicrophoneSettingsClick(object sender, RoutedEventArgs e) =>
        OpenShellTarget("ms-settings:privacy-microphone");

    private void OnRevealDataFolderClick(object sender, RoutedEventArgs e)
    {
        var folder = ViewModel.DataFolderPath;
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(folder);
            OpenShellTarget("explorer.exe", $"\"{folder}\"");
        }
        catch (Exception ex)
        {
            AppLogger.Log("Could not open the data folder", ex);
        }
    }

    private void OnOpenWebsiteClick(object sender, RoutedEventArgs e) => OpenShellTarget(WebsiteUrl);

    private void OnOpenReleasesClick(object sender, RoutedEventArgs e) => OpenShellTarget(ReleasesUrl);

    private async void OnResetClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Reset to default?",
            Content = "Cards, focus length, sounds, the pill and the look return to their shipped values. Your shortcut, your linked calendar and your data are kept.",
            PrimaryButtonText = "Reset",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        ViewModel.ResetCustomization();
    }

    /// <summary>A missing Settings page or a locked-down shell must not take the window down.</summary>
    private static void OpenShellTarget(string file, string? arguments = null)
    {
        try
        {
            var info = new ProcessStartInfo(file) { UseShellExecute = true };
            if (arguments is not null)
            {
                info.Arguments = arguments;
            }

            Process.Start(info);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"Could not open {file}", ex);
        }
    }
}
