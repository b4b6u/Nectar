namespace Nectar.ViewModels;

/// <summary>Row in the Customize list: icon, label, visibility toggle.</summary>
public sealed class CardSettingRow : ObservableObject
{
    private bool _isVisible;

    public CardSettingRow(string id, string title, string glyph, bool isVisible)
    {
        Id = id;
        Title = title;
        Glyph = glyph;
        _isVisible = isVisible;
    }

    /// <summary>Raised when the toggle flips, so the owner can persist and re-lay the cards.</summary>
    public event EventHandler? VisibilityChanged;

    public string Id { get; }

    public string Title { get; }

    public string Glyph { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetProperty(ref _isVisible, value))
            {
                VisibilityChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
