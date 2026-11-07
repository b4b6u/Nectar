using Nectar.Interop;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace Nectar.Windows;

/// <summary>
/// Anchors the overlay to the top-centre of the current display and keeps it there across
/// resolution changes, work-area changes (taskbar moves), monitor topology changes and DPI
/// changes. All window maths is done in physical pixels (what AppWindow/DisplayArea speak),
/// while the UI asks for sizes in DIPs.
/// </summary>
internal sealed class WindowGeometryService : IDisposable
{
    private readonly AppWindow _appWindow;
    private readonly WindowId _windowId;

    private double _scale = 1d;
    private SizeInt32 _desiredPhysical = new(1, 1);
    private bool _disposed;

    internal WindowGeometryService(AppWindow appWindow, WindowId windowId)
    {
        _appWindow = appWindow;
        _windowId = windowId;
    }

    /// <summary>Raised after any reposition/resize so dependent services can re-sync.</summary>
    internal event EventHandler? GeometryChanged;

    internal double Scale => _scale;

    internal RectInt32 Bounds =>
        new(_appWindow.Position.X, _appWindow.Position.Y, _appWindow.Size.Width, _appWindow.Size.Height);

    /// <summary>Physical size, honouring the current DPI scale.</summary>
    internal SizeInt32 ToPhysical(double widthDips, double heightDips) =>
        new(
            Math.Max(1, (int)Math.Round(widthDips * _scale)),
            Math.Max(1, (int)Math.Round(heightDips * _scale)));

    internal void SetScale(double scale)
    {
        if (scale <= 0 || Math.Abs(scale - _scale) < 0.001)
        {
            return;
        }

        _scale = scale;
        Apply();
    }

    /// <summary>Sizes the window from DIPs and snaps it back to the top-centre anchor.</summary>
    internal void Fit(double widthDips, double heightDips)
    {
        _desiredPhysical = ToPhysical(widthDips, heightDips);
        Apply();
    }

    /// <summary>
    /// Re-reads the display after a WM_DISPLAYCHANGE / WM_SETTINGCHANGE / WM_DPICHANGED and
    /// re-applies the last requested size.
    /// </summary>
    internal void ReAnchor(IntPtr hwnd)
    {
        try
        {
            var dpi = NativeMethods.GetDpiForWindow(hwnd);
            if (dpi > 0)
            {
                _scale = dpi / 96d;
            }
        }
        catch (Exception)
        {
            // Keep the previous scale.
        }

        Apply();
    }

    private void Apply()
    {
        if (_disposed)
        {
            return;
        }

        var workArea = GetWorkArea();
        var width = Math.Clamp(_desiredPhysical.Width, 1, Math.Max(1, workArea.Width));
        var height = Math.Clamp(_desiredPhysical.Height, 1, Math.Max(1, workArea.Height));

        // Top-centre anchor: X centres the window inside the work area, Y is the top edge.
        var x = workArea.X + Math.Max(0, (workArea.Width - width) / 2);
        var y = workArea.Y;

        try
        {
            _appWindow.MoveAndResize(new RectInt32(x, y, width, height));
        }
        catch (Exception)
        {
            // Resizes can fail transiently while the window is hidden/shown; the next
            // Fit()/ReAnchor() retries.
            return;
        }

        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private RectInt32 GetWorkArea()
    {
        try
        {
            var area = DisplayArea.GetFromWindowId(_windowId, DisplayAreaFallback.Primary);
            return area.WorkArea;
        }
        catch (Exception)
        {
            return new RectInt32(0, 0, 1920, 1080);
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
