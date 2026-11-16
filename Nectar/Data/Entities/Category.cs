namespace Nectar.Data.Entities;

/// <summary>
/// A bucket a task can be filed under. Three of these are free; a licence lifts the cap. Kept
/// deliberately small - a name and a colour index - because a category is a label, not a folder.
/// </summary>
public sealed class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>Index into the app's chip tones, so the colour follows the theme rather than the data.</summary>
    public int Colour { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
