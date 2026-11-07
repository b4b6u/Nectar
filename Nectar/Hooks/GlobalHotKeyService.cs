using System.Runtime.InteropServices;
using Nectar.Interop;

namespace Nectar.Hooks;

/// <summary>
/// The open gesture via RegisterHotKey. The hotkey is bound to the overlay HWND so WM_HOTKEY
/// arrives on the UI thread and is dispatched by <see cref="Windows.WindowMessageRouter"/>.
/// The chord is configurable from Customize; it always keeps at least one modifier.
/// </summary>
internal sealed class GlobalHotKeyService : IDisposable
{
    private const int HotKeyId = 0x434E; // 'CN'

    private IntPtr _hwnd;
    private bool _disposed;

    internal bool IsRegistered { get; private set; }

    internal string? Error { get; private set; }

    internal uint Modifiers { get; private set; } = NativeMethods.MOD_ALT;

    internal uint Key { get; private set; } = NativeMethods.VK_N;

    /// <summary>Registers the saved gesture. Returns false and explains why when it is taken.</summary>
    internal bool Register(IntPtr hwnd, uint modifiers, uint key)
    {
        _hwnd = hwnd;
        Unregister();

        try
        {
            IsRegistered = NativeMethods.RegisterHotKey(
                _hwnd,
                HotKeyId,
                modifiers | NativeMethods.MOD_NOREPEAT,
                key);

            if (!IsRegistered)
            {
                var error = Marshal.GetLastPInvokeError();
                Error = $"{Describe(modifiers, key)} is unavailable (Win32 error {error}). " +
                        "Another application probably owns it.";
                return false;
            }

            Modifiers = modifiers;
            Key = key;
            Error = null;
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            Error = "The hotkey API is unavailable on this system.";
            IsRegistered = false;
            return false;
        }
    }

    internal bool Matches(int id) => id == HotKeyId;

    /// <summary>Human readable chord, e.g. "Ctrl + Alt + N".</summary>
    internal static string Describe(uint modifiers, uint key)
    {
        var parts = new List<string>(4);
        if ((modifiers & NativeMethods.MOD_CONTROL) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & NativeMethods.MOD_ALT) != 0)
        {
            parts.Add("Alt");
        }

        if ((modifiers & NativeMethods.MOD_SHIFT) != 0)
        {
            parts.Add("Shift");
        }

        if ((modifiers & NativeMethods.MOD_WIN) != 0)
        {
            parts.Add("Win");
        }

        parts.Add(KeyName(key));
        return string.Join(" + ", parts);
    }

    private static string KeyName(uint key) => key switch
    {
        >= 0x41 and <= 0x5A => ((char)key).ToString(),
        >= 0x30 and <= 0x39 => ((char)key).ToString(),
        >= 0x70 and <= 0x87 => $"F{key - 0x6F}",
        0x20 => "Space",
        0x0D => "Enter",
        0x09 => "Tab",
        0x08 => "Backspace",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x24 => "Home",
        0x23 => "End",
        0x21 => "Page Up",
        0x22 => "Page Down",
        0x2C => "Print Screen",
        0x2D => "Insert",
        0x2E => "Delete",
        _ => $"0x{key:X2}",
    };

    /// <summary>
    /// True for any modifier virtual key. The hook reports the left/right specific codes
    /// (0xA0-0xA5), while the generic codes arrive from other sources; both are modifiers.
    /// </summary>
    internal static bool IsModifierKey(uint key) =>
        key is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;

    private void Unregister()
    {
        if (!IsRegistered || _hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            NativeMethods.UnregisterHotKey(_hwnd, HotKeyId);
        }
        catch (Exception)
        {
            // Process teardown will release it anyway.
        }

        IsRegistered = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Unregister();
    }
}
