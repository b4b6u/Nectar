using Nectar.Data.Entities;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.Text;

namespace Nectar.ViewModels;

/// <summary>
/// Observable projection of a <see cref="WorkspaceTask"/>. Completion state is pushed straight
/// through to the entity so "toggle the ring" and "persist" can never disagree. The brush and
/// opacity members exist so the row can be styled declaratively without value converters.
/// </summary>
public sealed class TaskRow : ObservableObject
{
    private static readonly SolidColorBrush RingBrush =
        new(Color.FromArgb(0x8A, 0x4C, 0x5C, 0x50));

    private static readonly SolidColorBrush DoneBrush =
        new(Color.FromArgb(0xFF, 0x2E, 0x6B, 0x4A));

    private static readonly SolidColorBrush ClearBrush = new(Colors.Transparent);

    private bool _isCompleted;

    public TaskRow(WorkspaceTask model)
    {
        Model = model;
        _isCompleted = model.IsCompleted;
    }

    /// <summary>Raised when the user flips completion, so the owner can persist and re-sort.</summary>
    public event EventHandler? CompletionChanged;

    public WorkspaceTask Model { get; }

    public Guid Id => Model.Id;

    public string Title => Model.Title;

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (!SetProperty(ref _isCompleted, value))
            {
                return;
            }

            Model.IsCompleted = value;
            Model.CompletedAt = value ? DateTime.Now : null;
            OnPropertyChanged(nameof(ContentOpacity));
            OnPropertyChanged(nameof(TitleDecorations));
            OnPropertyChanged(nameof(MetaText));
            OnPropertyChanged(nameof(DialFill));
            OnPropertyChanged(nameof(DialStroke));
            OnPropertyChanged(nameof(CheckGlyphOpacity));
            CompletionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Dims completed rows without needing a converter.</summary>
    public double ContentOpacity => _isCompleted ? 0.5 : 1.0;

    public TextDecorations TitleDecorations =>
        _isCompleted ? TextDecorations.Strikethrough : TextDecorations.None;

    public string MetaText
    {
        get
        {
            if (_isCompleted)
            {
                return "Done";
            }

            var meta = Model.Deadline is { } deadline
                ? $"Due {deadline:ddd d MMM} · {deadline:HH:mm}"
                : $"Added {Model.CreatedAt:ddd d MMM}";

            // The category rides along on the same line, so filing a task never makes the row taller.
            return CategoryName is null ? meta : $"{meta} · {CategoryName}";
        }
    }

    public Visibility MetaVisibility => Visibility.Visible;

    /// <summary>The category chip's label, or null when the task is unfiled.</summary>
    public string? CategoryName { get; private set; }

    /// <summary>Index into the chip tones; -1 when unfiled.</summary>
    public int CategoryColour { get; private set; } = -1;

    public string CategoryBadge => CategoryName ?? string.Empty;

    public Visibility CategoryVisibility =>
        CategoryName is null ? Visibility.Collapsed : Visibility.Visible;

    public void ApplyCategory(Guid? id, string? name, int colour)
    {
        Model.CategoryId = id;
        CategoryName = name;
        CategoryColour = colour;
        OnPropertyChanged(nameof(CategoryName));
        OnPropertyChanged(nameof(CategoryBadge));
        OnPropertyChanged(nameof(CategoryVisibility));
        OnPropertyChanged(nameof(MetaText));
    }

    public Brush DialFill => _isCompleted ? DoneBrush : ClearBrush;

    public Brush DialStroke => _isCompleted ? DoneBrush : RingBrush;

    public double CheckGlyphOpacity => _isCompleted ? 1 : 0;

    public bool IsOverdue => Model.Deadline is { } deadline && deadline < DateTime.Now && !_isCompleted;
}
