using Nectar.Services;
using Microsoft.UI.Xaml;

namespace Nectar;

/// <summary>
/// Application entry point. Nectar is a background agent: the overlay window is created,
/// primed (hotkeys + mouse hook attach to its HWND) and immediately hidden.
/// </summary>
public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\Nectar.SingleInstance";

    private static Mutex? _singleInstance;
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();

        // Three nets, because WinUI only reports the first one. The UI handler catches dispatch
        // exceptions; the AppDomain handler catches background threads, finalizers and anything
        // thrown during shutdown; TaskScheduler catches faults nobody awaited.
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Runs before the repository, settings store or logger touch the disk.
        MigrateLegacyWorkspace();

        if (!TryClaimSingleInstance())
        {
            // A second copy would double-register Alt+N and the mouse hook.
            Exit();
            return;
        }

        // Tripwire for the offline promise: any HttpClient call to something other than a
        // calendar provider is written to nectar.log instead of happening silently.
        NetworkGuard.Start();

        // Packaged builds get a desktop shortcut on first launch (MSIX has no install scripts);
        // the portable build has no package identity, so this is a no-op there.
        DesktopShortcut.Ensure();

        try
        {
            _window = new MainWindow();

            // Activate once so the XAML/composition tree is live, then hide immediately:
            // Nectar must not present a primary window on launch.
            _window.Activate();
            _window.HideToBackground();
        }
        catch (Exception ex)
        {
            AppLogger.Log("Nectar failed to start", ex);
            Exit();
        }
    }

    /// <summary>
    /// One-time move of the pre-rebrand data folder (%LOCALAPPDATA%\TopSpace) to the Nectar
    /// folder, renaming the database and log files with it. Best effort: a failure leaves the
    /// old files untouched rather than starting from an empty workspace.
    /// </summary>
    private static void MigrateLegacyWorkspace()
    {
        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var legacy = Path.Combine(local, "TopSpace");
            var current = Path.Combine(local, "Nectar");

            if (!Directory.Exists(legacy) || Directory.Exists(current))
            {
                return;
            }

            Directory.Move(legacy, current);

            foreach (var (oldName, newName) in new[]
                     {
                         ("canopy.db", "nectar.db"),
                         ("canopy.db-wal", "nectar.db-wal"),
                         ("canopy.db-shm", "nectar.db-shm"),
                         ("canopy.log", "nectar.log"),
                     })
            {
                var from = Path.Combine(current, oldName);
                var to = Path.Combine(current, newName);
                if (File.Exists(from) && !File.Exists(to))
                {
                    File.Move(from, to);
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("Workspace migration from the legacy folder failed", ex);
        }
    }

    private static bool TryClaimSingleInstance()
    {
        try
        {
            _singleInstance = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
            return createdNew;
        }
        catch (Exception ex)
        {
            // If the mutex cannot be created we still start; duplicates are the lesser evil.
            AppLogger.Log("Single-instance check failed", ex);
            return true;
        }
    }

    /// <summary>
    /// Exceptions raised on the XAML dispatcher. Handled is set so the overlay does not vanish
    /// mid-session; the cost is that a genuinely corrupt UI state survives until the next action
    /// fails, which is why every one of these is written to crashlog.txt as well as nectar.log.
    /// </summary>
    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        CrashReporter.Write("Unhandled UI exception (dispatcher)", e.Exception);
        AppLogger.Log("Unhandled UI exception", e.Exception);

        e.Handled = true;
    }

    /// <summary>
    /// Fatal exceptions from non-UI threads. Nothing can prevent termination here, so the only job
    /// is to leave a record and give the log a chance to flush.
    /// </summary>
    private static void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        var verdict = e.IsTerminating ? "terminating" : "non-terminating";

        CrashReporter.Write($"AppDomain unhandled exception ({verdict})", exception);
        if (exception is not null)
        {
            AppLogger.Log($"AppDomain unhandled exception ({verdict})", exception);
        }
        else
        {
            AppLogger.Log($"AppDomain unhandled exception ({verdict}): {e.ExceptionObject}");
        }
    }

    /// <summary>
    /// Faulted tasks that were never awaited. Without SetObserved these are rethrown on the
    /// finalizer thread and take the process down long after the failure was recoverable.
    /// </summary>
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        CrashReporter.Write("Unobserved task exception", e.Exception);
        AppLogger.Log("Unobserved task exception", e.Exception);

        e.SetObserved();
    }
}
