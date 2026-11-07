using System.Runtime.InteropServices;
using Nectar.Interop;
using Nectar.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Nectar.Windows;

/// <summary>
/// Turns a stock WinUI 3 window into a chrome-less, taskbar-invisible, non-activating overlay.
/// Everything here is reversible and every failure is reported instead of thrown: an overlay
/// that cannot strip its chrome should still appear, degraded, rather than crash on launch.
/// </summary>
internal sealed class OverlayWindowController
{
    private readonly AppWindow _appWindow;
    private readonly IntPtr _hwnd;

    internal OverlayWindowController(AppWindow appWindow, IntPtr hwnd)
    {
        _appWindow = appWindow;
        _hwnd = hwnd;
    }

    /// <summary>Non-fatal problems collected while configuring the window.</summary>
    internal string? SetupWarning { get; private set; }

    internal IntPtr Handle => _hwnd;

    internal void Configure()
    {
        // 1. Overlapped presenter: strip chrome, freeze sizing, float above everything.
        try
        {
            _appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
                presenter.IsResizable = false;
                presenter.IsAlwaysOnTop = true;
            }
            else
            {
                SetupWarning = "Unexpected presenter; window chrome may not be fully stripped.";
            }
        }
        catch (Exception ex)
        {
            SetupWarning = $"Could not configure the window presenter: {ex.Message}";
        }

        // 2. Keep it out of the taskbar and the Alt+Tab switcher.
        try
        {
            _appWindow.IsShownInSwitchers = false;
        }
        catch (Exception ex)
        {
            SetupWarning ??= $"Could not hide the window from switchers: {ex.Message}";
        }

        // 3. Extended styles: tool window (no taskbar entry) + never activate on click.
        if (!WindowStyle.SetExStyle(
                _hwnd,
                NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE,
                enable: true))
        {
            SetupWarning ??= "WS_EX_NOACTIVATE could not be applied; the overlay may steal focus.";
        }

        WindowStyle.RefreshFrame(_hwnd);

        // 3b. The presenter keeps WS_DLGFRAME, which Windows paints as a 2 px light double border
        //     around the window. Clearing it is what actually erases the hairline frame.
        if (!WindowStyle.RemoveDialogFrame(_hwnd))
        {
            SetupWarning ??= "The dialog frame could not be removed; a hairline may remain.";
        }

        // The presenter flag alone is not always honoured before the first show, so the
        // top-most band is also applied directly to the HWND.
        EnsureTopMost();

        // 4. Rounded corners, dark frame and no system border so the overlay reads as a pure
        //    black surface dropping from the bezel.
        ApplyDwmPolish();
    }

    /// <summary>Re-asserts HWND_TOPMOST without moving, resizing or activating the window.</summary>
    internal void EnsureTopMost()
    {
        try
        {
            NativeMethods.SetWindowPos(
                _hwnd,
                NativeMethods.HWND_TOPMOST,
                0,
                0,
                0,
                0,
                NativeMethods.SWP_NOMOVE
                | NativeMethods.SWP_NOSIZE
                | NativeMethods.SWP_NOACTIVATE);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            SetupWarning ??= "Always-on-top could not be enforced on this system.";
        }
    }

    /// <summary>
    /// Suppresses the 1 px Windows 11 system border (DWMWA_BORDER_COLOR = DWMWA_COLOR_NONE),
    /// rounds the corners and opts into dark mode. Without the border override the shell shows a
    /// light hairline that reads as a picture frame around the whole overlay.
    /// </summary>
    internal void ApplyDwmPolish()
    {
        try
        {
            var corner = NativeMethods.DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(
                _hwnd,
                NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref corner,
                sizeof(int));

            var dark = 1;
            NativeMethods.DwmSetWindowAttribute(
                _hwnd,
                NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref dark,
                sizeof(int));

            // DWMWA_COLOR_NONE (0xFFFFFFFE) removes the system border entirely.
            var border = 0x00000000;
            var hr = NativeMethods.DwmSetWindowAttribute(
                _hwnd,
                NativeMethods.DWMWA_BORDER_COLOR,
                ref border,
                sizeof(int));

            if (hr < 0)
            {
                SetupWarning ??= $"DWM border override failed (0x{hr:X8}); a hairline may remain.";
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Windows 10 without the DWM attribute: purely cosmetic, ignore.
        }
        catch (Exception ex)
        {
            // dwmapi is optional polish. A stripped-down build, a stale HWND or a shell hook can
            // make an attribute call fail in ways the narrow filter above does not cover; the
            // overlay must still work, so record it and carry on.
            SetupWarning ??= "Window styling could not be applied on this build.";
            Nectar.Services.AppLogger.Log("DWM polish failed", ex);
        }
    }

    internal void Show()
    {
        _appWindow.Show();

        // Presenting can restore the WinUI window style, so the frame removal and the DWM border
        // override are re-asserted after every show.
        WindowStyle.RemoveDialogFrame(_hwnd);
        ApplyDwmPolish();
        EnsureTopMost();
    }

    internal void Hide() => _appWindow.Hide();

    internal bool IsVisible => _appWindow.IsVisible;

    internal void Resize(RectInt32 bounds) => _appWindow.MoveAndResize(bounds);
}
