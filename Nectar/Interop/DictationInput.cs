using System.Runtime.InteropServices;

namespace Nectar.Interop;

/// <summary>
/// Triggers Windows Voice Typing (Win+H) into the currently focused text control. Nectar does
/// not ship its own speech recognizer here: the card <c>TextBox</c> controls already expose TSF
/// / UI Automation, so Windows Voice Typing writes into whichever field has the caret, on
/// whatever state (online/offline, language, consent) the machine already has configured.
/// </summary>
public static class DictationInput
{
    /// <summary>
    /// Starts Windows Voice Typing. The caller must already have given keyboard focus to the
    /// target control on the UI thread, and the control must be in a writable state.
    /// </summary>
    public static bool Start()
    {
        Nectar.Services.AppLogger.Log("DictationInput.Start invoked");

        // Voice Typing is a toggle: pressing Win+H once starts listening (in a focused text
        // field), pressing it again stops. Sending the chord twice would toggle the feature
        // back off immediately, so we send it exactly once.
        return SynthesizeWinPlus("H");
    }

    private static bool SynthesizeWinPlus(string key)
    {
        try
        {
            // Release any accidentally held modifiers so a physical Alt/ Ctrl state does not
            // corrupt the chord (e.g. a held Right-Alt turning the chord into AltGr).
            // Worst case: two held modifiers-down events + three up events, then the chord itself
            // (key-down + key-down + key-up + key-up). Allocate generously and never overrun.
            var inputs = new INPUT[10];
            var index = 0;

            index = AddKeyUp(inputs, index, VK_CONTROL);
            index = AddKeyUp(inputs, index, VK_LWIN);
            index = AddKeyUp(inputs, index, VK_RWIN);

            // Win+H
            index = AddKeyDown(inputs, index, VK_LWIN);
            index = AddKeyDown(inputs, index, GetVirtualKey(key));
            index = AddKeyUp(inputs, index, GetVirtualKey(key));
            index = AddKeyUp(inputs, index, VK_LWIN);

            var count = index;
            if (count == 0)
            {
                Nectar.Services.AppLogger.Log("Voice typing input synthesis: no inputs to send");
                return false;
            }

            var size = Marshal.SizeOf<INPUT>();
            var batch = new INPUT[count];
            Array.Copy(inputs, batch, count);
            var sent = SendInput((uint)count, batch, size);
            if (sent != count)
            {
                var win32Error = Marshal.GetLastWin32Error();
                Nectar.Services.AppLogger.Log(
                    "Voice typing input synthesis was blocked: sent " + sent + " of " + count +
                    ", INPUT size " + size + ", Win32 error " + win32Error);
            }

            Nectar.Services.AppLogger.Log("Voice typing input synthesis sent " + sent + " of " + count);
            return sent == count;
        }
        catch (Exception ex)
        {
            Nectar.Services.AppLogger.Log("Voice typing input synthesis failed", ex);
            Nectar.Services.AppLogger.Log("Voice typing input synthesis returned false");
            return false;
        }
    }

    private static int AddKeyDown(INPUT[] inputs, int index, uint vk)
    {
        inputs[index] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new INPUTUNION
            {
                ki = new KEYBDINPUT(vk, KEYEVENTF_KEYDOWN),
            },
        };
        return index + 1;
    }

    private static int AddKeyUp(INPUT[] inputs, int index, uint vk)
    {
        inputs[index] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new INPUTUNION
            {
                ki = new KEYBDINPUT(vk, KEYEVENTF_KEYUP),
            },
        };
        return index + 1;
    }

    private static uint GetVirtualKey(string key)
    {
        if (key is "H" or "h")
        {
            return 0x48; // VK_H
        }

        return 0;
    }

    private const uint INPUT_KEYBOARD = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    // Present so the union is the same size as the Win32 INPUT union (40 bytes on x64).
    // Without it Marshal.SizeOf(INPUT) is smaller than the native struct and SendInput
    // rejects the whole batch with cbSize mismatch, returning 0.
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;

        // KEYBDINPUT.wVk is a ushort in the SDK headers; the Win32 VirtualKey values fit.
        public KEYBDINPUT(uint vk, uint flags)
        {
            wVk = (ushort)vk;
            wScan = 0;
            dwFlags = flags;
            time = 0;
            dwExtraInfo = IntPtr.Zero;
        }

        public KEYBDINPUT() { wVk = 0; wScan = 0; dwFlags = 0; time = 0; dwExtraInfo = IntPtr.Zero; }
    }

    private const uint KEYEVENTF_KEYDOWN = 0x0000;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private const uint VK_LWIN = 0x5B;
    private const uint VK_RWIN = 0x5C;
    private const uint VK_CONTROL = 0x11;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint cInputs,
        INPUT[] pInputs,
        int cbSize);
}

