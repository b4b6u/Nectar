using System.Runtime.InteropServices;
using Nectar.Interop;
using Nectar.Services;
using Microsoft.UI.Dispatching;

namespace Nectar.Hooks;

/// <summary>
/// Captures the next chord while Customize is waiting for one.
///
/// The overlay is non-activating by design, so it cannot rely on keyboard focus to receive the
/// keys; a low-level keyboard hook (installed only during capture) sees them no matter which
/// window is in front. The hook is removed as soon as a chord is taken or the capture is
/// cancelled, so normal typing is never inspected.
/// </summary>
internal sealed class KeyboardCaptureService : IDisposable
{
    private readonly DispatcherQueue _dispatcher;
    private readonly ManualResetEventSlim _installed = new(false);

    private Thread? _thread;
    private uint _threadId;
    private IntPtr _hook;
    private NativeMethods.HookProc? _callback;
    private volatile bool _running;
    private volatile bool _capturing;
    private uint _heldModifiers;
    private bool _disposed;

    internal KeyboardCaptureService(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>A key press with the modifier state that was held while it happened.</summary>
    internal sealed record CapturedKey(uint Key, uint Modifiers);

    /// <summary>Raised on the UI thread with the pressed key and its modifiers.</summary>
    internal event EventHandler<CapturedKey>? KeyDown;

    internal bool IsCapturing => _capturing;

    internal void Start()
    {
        _heldModifiers = 0;
        _capturing = true;

        if (_running)
        {
            return;
        }

        _running = true;
        _callback = HookCallback;
        _thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = "Nectar.KeyCapture",
        };

        _thread.Start();
        _installed.Wait(TimeSpan.FromSeconds(2));
    }

    internal void Stop()
    {
        _capturing = false;

        if (!_running)
        {
            return;
        }

        _running = false;

        try
        {
            if (_threadId != 0)
            {
                NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }

            _thread?.Join(TimeSpan.FromMilliseconds(400));
        }
        catch (Exception)
        {
            // The hook is released on the next successful stop or at process exit.
        }

        _thread = null;
        _threadId = 0;
    }

    private void ThreadMain()
    {
        try
        {
            _threadId = NativeMethods.GetCurrentThreadId();
            _hook = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_KEYBOARD_LL,
                _callback!,
                NativeMethods.GetModuleHandle(null),
                0);

            if (_hook == IntPtr.Zero)
            {
                _installed.Set();
                return;
            }

            _installed.Set();

            while (_running && NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("Shortcut capture hook failed", ex);
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
        if (nCode >= 0 && _capturing)
        {
            var message = wParam.ToInt32();

            try
            {
                var info = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                TrackModifier((int)info.vkCode, message);

                // Key down only: the chord is complete as soon as the non-modifier lands.
                if (message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
                {
                    // Modifiers are resolved here, not on the UI thread: a chord releases
                    // Ctrl/Alt immediately, long before this event is handled.
                    var captured = new CapturedKey(info.vkCode, _heldModifiers | ReadModifiers());

                    Nectar.Services.UiThread.Post(_dispatcher, () =>
                    {
                        if (!_disposed && _capturing)
                        {
                            KeyDown?.Invoke(this, captured);
                        }
                    }, "KeyboardCaptureService: captured chord");
                }
            }
            catch (Exception)
            {
                // A malformed hook payload must never break the input chain.
            }
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <summary>
    /// Follows modifier presses from the hook stream itself. The asynchronous key state is
    /// unreliable for injected chords (it can still read idle while the chord is in flight),
    /// and the hook sees every transition, so the two sources are combined.
    /// </summary>
    private void TrackModifier(int virtualKey, int message)
    {
        var modifier = ModifierFor(virtualKey);
        if (modifier == 0)
        {
            return;
        }

        if (message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
        {
            _heldModifiers |= modifier;
        }
        else if (message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP)
        {
            _heldModifiers &= ~modifier;
        }
    }

    private static uint ModifierFor(int virtualKey) => virtualKey switch
    {
        NativeMethods.VK_SHIFT or NativeMethods.VK_LSHIFT or NativeMethods.VK_RSHIFT =>
            NativeMethods.MOD_SHIFT,
        NativeMethods.VK_CONTROL or NativeMethods.VK_LCONTROL or NativeMethods.VK_RCONTROL =>
            NativeMethods.MOD_CONTROL,
        NativeMethods.VK_MENU or NativeMethods.VK_LMENU or NativeMethods.VK_RMENU =>
            NativeMethods.MOD_ALT,
        NativeMethods.VK_LWIN or NativeMethods.VK_RWIN => NativeMethods.MOD_WIN,
        _ => 0,
    };

    private static uint ReadModifiers()
    {
        uint modifiers = 0;

        if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0)
        {
            modifiers |= NativeMethods.MOD_CONTROL;
        }

        if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) != 0)
        {
            modifiers |= NativeMethods.MOD_ALT;
        }

        if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0)
        {
            modifiers |= NativeMethods.MOD_SHIFT;
        }

        if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0 ||
            (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0)
        {
            modifiers |= NativeMethods.MOD_WIN;
        }

        return modifiers;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();

        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
