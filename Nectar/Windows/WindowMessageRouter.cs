using System.Runtime.InteropServices;
using Nectar.Interop;

namespace Nectar.Windows;

/// <summary>
/// Hooks the overlay's own window procedure (SetWindowSubclass) so Nectar can observe
/// WM_HOTKEY, WM_MOUSEACTIVATE, WM_ACTIVATE, WM_DPICHANGED and Esc without fighting WinUI's
/// message pipeline. Anything not handled is forwarded to DefSubclassProc.
/// </summary>
internal sealed class WindowMessageRouter : IDisposable
{
    private static readonly IntPtr SubclassId = new(0x43414E4F); // 'CANO'

    private readonly IntPtr _hwnd;
    private readonly NativeMethods.SubclassProc _proc;
    private bool _attached;
    private bool _disposed;

    internal WindowMessageRouter(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _proc = OnMessage;
    }

    internal event EventHandler<int>? HotKeyPressed;

    /// <summary>Supplied by FocusGate: returns MA_ACTIVATE / MA_NOACTIVATE.</summary>
    internal Func<int>? MouseActivateEvaluator { get; set; }

    /// <summary>
    /// While true, every z-order change the shell or WinUI attempts is rewritten to HWND_TOPMOST.
    /// Setting the band once at startup is not enough: showing the window, moving it and the
    /// presenter's own activation bookkeeping each issue their own SetWindowPos, and any of them
    /// can drop the window into the normal band, which is exactly how an overlay ends up behind
    /// the app it is supposed to float over.
    /// </summary>
    internal bool KeepOnTop { get; set; } = true;

    /// <summary>Raised with true when the window is being activated, false when deactivated.</summary>
    internal event EventHandler<bool>? ActivationChanged;

    internal event EventHandler? DpiChanged;

    /// <summary>Resolution, monitor topology or work-area change (taskbar moved, display added).</summary>
    internal event EventHandler? DisplaySettingsChanged;

    internal event EventHandler? EscapePressed;

    internal bool Attach()
    {
        if (_attached || _hwnd == IntPtr.Zero)
        {
            return _attached;
        }

        _attached = NativeMethods.SetWindowSubclass(_hwnd, _proc, SubclassId, IntPtr.Zero);
        return _attached;
    }

    /// <summary>
    /// Forces the pending z-order change to keep the window in the top-most band. Requests that
    /// already carry SWP_NOZORDER carry no z-order intent, so they are left untouched.
    /// </summary>
    private static void PinTopMost(IntPtr lParam)
    {
        if (lParam == IntPtr.Zero)
        {
            return;
        }

        var pos = Marshal.PtrToStructure<NativeMethods.WINDOWPOS>(lParam);
        if ((pos.flags & NativeMethods.SWP_NOZORDER) != 0 || pos.hwndInsertAfter == NativeMethods.HWND_TOPMOST)
        {
            return;
        }

        pos.hwndInsertAfter = NativeMethods.HWND_TOPMOST;
        Marshal.StructureToPtr(pos, lParam, fDeleteOld: false);
    }

    private IntPtr OnMessage(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
    {
        try
        {
            switch (uMsg)
            {
                case NativeMethods.WM_HOTKEY:
                    HotKeyPressed?.Invoke(this, wParam.ToInt32());
                    return IntPtr.Zero;

                case NativeMethods.WM_MOUSEACTIVATE:
                    if (MouseActivateEvaluator is not null)
                    {
                        return new IntPtr(MouseActivateEvaluator());
                    }

                    break;

                case NativeMethods.WM_ACTIVATE:
                    var activating = ((int)wParam & 0xFFFF) != NativeMethods.WA_INACTIVE;
                    ActivationChanged?.Invoke(this, activating);
                    break;

                case NativeMethods.WM_WINDOWPOSCHANGING:
                    if (KeepOnTop)
                    {
                        PinTopMost(lParam);
                    }

                    break;

                case NativeMethods.WM_DPICHANGED:
                    DpiChanged?.Invoke(this, EventArgs.Empty);
                    break;

                case NativeMethods.WM_DISPLAYCHANGE:
                case NativeMethods.WM_SETTINGCHANGE:
                    DisplaySettingsChanged?.Invoke(this, EventArgs.Empty);
                    break;

                case NativeMethods.WM_KEYDOWN:
                case NativeMethods.WM_SYSKEYDOWN:
                    if (wParam.ToInt32() == NativeMethods.VK_ESCAPE)
                    {
                        EscapePressed?.Invoke(this, EventArgs.Empty);
                    }

                    break;

                case NativeMethods.WM_NCDESTROY:
                    // Window is going away: detach before Win32 frees the window data.
                    if (_attached)
                    {
                        NativeMethods.RemoveWindowSubclass(hWnd, _proc, uIdSubclass);
                        _attached = false;
                    }

                    break;
            }
        }
        catch (Exception)
        {
            // Never let a managed exception cross the native window-procedure boundary.
        }

        return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_attached)
        {
            try
            {
                NativeMethods.RemoveWindowSubclass(_hwnd, _proc, SubclassId);
            }
            catch (Exception)
            {
                // Best effort teardown.
            }

            _attached = false;
        }
    }
}
