using System.Runtime.InteropServices;

namespace Nectar.Interop;

/// <summary>
/// Safe helpers around the raw GWL_EXSTYLE calls. Every mutation verifies that the style
/// actually changed and that no Win32 error was raised, because a silently failed
/// WS_EX_NOACTIVATE write is exactly what makes an overlay start stealing focus.
/// </summary>
internal static class WindowStyle
{
    internal static long GetExStyle(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return 0;
        }

        var value = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        return value.ToInt64();
    }

    internal static bool HasExStyle(IntPtr hwnd, long flag) => (GetExStyle(hwnd) & flag) == flag;

    /// <summary>
    /// Adds or removes one or more extended style flags. Returns false when Win32 reported a
    /// failure (or when the requested state was not applied).
    /// </summary>
    internal static bool SetExStyle(IntPtr hwnd, long flags, bool enable)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var current = GetExStyle(hwnd);
            var updated = enable ? current | flags : current & ~flags;
            if (updated == current)
            {
                return true;
            }

            Marshal.SetLastPInvokeError(0);
            var previous = NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(updated));
            var error = Marshal.GetLastPInvokeError();
            if (previous == IntPtr.Zero && error != 0)
            {
                return false;
            }

            return (GetExStyle(hwnd) & flags) == (enable ? flags : 0);
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            return false;
        }
    }

    private const long WsDlgFrame = 0x00400000L;

    /// <summary>
    /// Clears WS_DLGFRAME. <c>SetBorderAndTitleBar(false, false)</c> leaves that bit set, and a
    /// dialog frame is drawn as a 2 px light double border around the whole window — the hairline
    /// that survives both the presenter change and DWMWA_BORDER_COLOR.
    /// </summary>
    internal static bool RemoveDialogFrame(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var current = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE).ToInt64();
            if ((current & WsDlgFrame) == 0)
            {
                return true;
            }

            var updated = current & ~WsDlgFrame;
            Marshal.SetLastPInvokeError(0);
            var previous = NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE, new IntPtr(updated));
            var error = Marshal.GetLastPInvokeError();
            if (previous == IntPtr.Zero && error != 0)
            {
                return false;
            }

            RefreshFrame(hwnd);
            return (NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE).ToInt64() & WsDlgFrame) == 0;
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Forces Win32 to recompute the non-client frame after a style change.</summary>
    internal static void RefreshFrame(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_FRAMECHANGED
            | NativeMethods.SWP_NOMOVE
            | NativeMethods.SWP_NOSIZE
            | NativeMethods.SWP_NOZORDER
            | NativeMethods.SWP_NOACTIVATE);
    }
}
