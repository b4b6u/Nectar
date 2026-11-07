using System.Runtime.InteropServices;
using Nectar.Interop;
using Microsoft.UI.Dispatching;
using Windows.Graphics;

namespace Nectar.Hooks;

/// <summary>
/// Low-level mouse hook (WH_MOUSE_LL) running on its own thread with its own message loop.
///
/// The hook callback must never block: it does two integer hit-tests against cached rectangles
/// and only marshals to the UI thread when the state actually flips (entered the top-edge
/// trigger zone / left the overlay's bounds). That keeps the global input pipeline fast and
/// removes the risk of the UI thread stalling the cursor.
/// </summary>
internal sealed class PointerProbeService : IDisposable
{
    private readonly DispatcherQueue _dispatcher;
    private readonly ManualResetEventSlim _installed = new(false);

    private Thread? _thread;
    private uint _threadId;
    private IntPtr _hook;
    private NativeMethods.HookProc? _callback;
    private volatile bool _running;
    private bool _disposed;

    // Geometry is written on the UI thread and read on the hook thread. Single ints are
    // atomic, so torn reads are the only thing to worry about - and a torn rect merely
    // delays a hover event by one mouse move.
    private int _zoneLeft;
    private int _zoneTop;
    private int _zoneRight;
    private int _zoneBottom;
    private int _windowLeft;
    private int _windowTop;
    private int _windowRight;
    private int _windowBottom;

    private bool _insideZone;
    private bool _insideWindow;

    // Read from the UI thread to decide whether an idle shell may collapse.
    private volatile bool _pointerInsideWindow;

    internal PointerProbeService(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>Raised (UI thread) when the cursor enters the top-centre trigger zone.</summary>
    internal event EventHandler? TriggerZoneEntered;

    /// <summary>Raised (UI thread) when the cursor leaves the overlay's own bounds.</summary>
    internal event EventHandler? PointerLeftWindow;

    internal string? Error { get; private set; }

    /// <summary>True while the cursor sits inside the overlay's bounds.</summary>
    internal bool IsPointerInsideWindow => _pointerInsideWindow;

    internal void SetTriggerZone(RectInt32 zone)
    {
        _zoneLeft = zone.X;
        _zoneTop = zone.Y;
        _zoneRight = zone.X + zone.Width;
        _zoneBottom = zone.Y + zone.Height;
    }

    /// <summary>Pass an empty rectangle while the window is hidden.</summary>
    internal void SetWindowBounds(RectInt32 bounds)
    {
        _windowLeft = bounds.X;
        _windowTop = bounds.Y;
        _windowRight = bounds.X + bounds.Width;
        _windowBottom = bounds.Y + bounds.Height;
    }

    internal bool Start()
    {
        if (_running)
        {
            return true;
        }

        _running = true;
        _callback = HookCallback;
        _thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = "Nectar.PointerProbe",
        };

        _thread.Start();
        _installed.Wait(TimeSpan.FromSeconds(3));
        return _hook != IntPtr.Zero;
    }

    private void ThreadMain()
    {
        try
        {
            _threadId = NativeMethods.GetCurrentThreadId();
            _hook = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_MOUSE_LL,
                _callback!,
                NativeMethods.GetModuleHandle(null),
                0);

            if (_hook == IntPtr.Zero)
            {
                Error = $"The global pointer probe could not start (Win32 error {Marshal.GetLastWin32Error()}).";
                _installed.Set();
                return;
            }

            _installed.Set();

            // Low-level hooks require a message loop on the installing thread.
            while (_running && NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }
        catch (Exception ex)
        {
            Error = $"Pointer probe failed: {ex.Message}";
            _installed.Set();
        }
        finally
        {
            if (_hook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }

            _installed.Set();
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _running)
        {
            try
            {
                var info = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                var x = info.pt.X;
                var y = info.pt.Y;

                var insideZone = x >= _zoneLeft && x < _zoneRight && y >= _zoneTop && y < _zoneBottom;
                if (insideZone != _insideZone)
                {
                    _insideZone = insideZone;
                    if (insideZone)
                    {
                        Raise(TriggerZoneEntered);
                    }
                }

                var insideWindow = _windowRight > _windowLeft
                    && x >= _windowLeft && x < _windowRight
                    && y >= _windowTop && y < _windowBottom;

                _pointerInsideWindow = insideWindow;

                if (insideWindow != _insideWindow)
                {
                    _insideWindow = insideWindow;
                    if (!insideWindow)
                    {
                        Raise(PointerLeftWindow);
                    }
                }
            }
            catch (Exception)
            {
                // A failed hit-test must never break the hook chain.
            }
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void Raise(EventHandler? handler)
    {
        if (handler is null)
        {
            return;
        }

        Nectar.Services.UiThread.Post(_dispatcher, () =>
        {
            if (!_disposed)
            {
                handler(this, EventArgs.Empty);
            }
        }, "PointerProbeService: pointer event");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _running = false;

        try
        {
            if (_threadId != 0)
            {
                NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }

            _thread?.Join(TimeSpan.FromSeconds(2));
        }
        catch (Exception)
        {
            // Nothing to do; the thread is a background thread and will die with the process.
        }

        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
