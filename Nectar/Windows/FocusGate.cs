using Nectar.Interop;

namespace Nectar.Windows;

/// <summary>
/// Owns the "never steal focus" contract.
///
/// The window carries WS_EX_NOACTIVATE at all times, so hovering it, clicking its buttons and
/// reading it never touches the foreground window. Clicking a *text field* is the one explicit
/// opt-in: the field's PointerPressed handler calls <see cref="BeginInteractive"/>, which drops
/// the style for exactly as long as the overlay keeps activation. Losing activation restores it.
/// </summary>
internal sealed class FocusGate
{
    private readonly IntPtr _hwnd;
    private bool _interactive;
    private bool _nonActivatingInForce;

    internal FocusGate(IntPtr hwnd)
    {
        _hwnd = hwnd;
        ApplyNonActivating(true);
    }

    /// <summary>True while the overlay owns keyboard focus (text editing).</summary>
    internal bool IsInteractive => _interactive;

    internal bool NonActivatingActive => _nonActivatingInForce;

    internal event EventHandler<bool>? InteractiveChanged;

    /// <summary>
    /// Called from WM_MOUSEACTIVATE. Clicks never activate the window implicitly; explicit text
    /// entry goes through <see cref="BeginInteractive"/> instead.
    /// </summary>
    internal int EvaluateMouseActivate() => NativeMethods.MA_NOACTIVATE;

    /// <summary>Opt in to keyboard focus for a text field.</summary>
    internal void BeginInteractive()
    {
        ApplyNonActivating(false);
        NativeMethods.SetForegroundWindow(_hwnd);

        if (_interactive)
        {
            return;
        }

        _interactive = true;
        InteractiveChanged?.Invoke(this, true);
    }

    internal void EndInteractive()
    {
        if (!_interactive)
        {
            return;
        }

        _interactive = false;
        ApplyNonActivating(true);
        InteractiveChanged?.Invoke(this, false);
    }

    private void ApplyNonActivating(bool noActivate)
    {
        if (WindowStyle.SetExStyle(_hwnd, NativeMethods.WS_EX_NOACTIVATE, noActivate))
        {
            _nonActivatingInForce = noActivate;
        }
    }
}
