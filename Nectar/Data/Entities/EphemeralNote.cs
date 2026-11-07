namespace Nectar.Data.Entities;

/// <summary>
/// The scratch note. Nectar keeps exactly one row (a fixed id) so the note behaves like a
/// single ephemeral page rather than a collection of documents. The surrogate key exists only
/// because EF Core requires a primary key; the meaningful fields are Content and LastEdited.
/// </summary>
internal sealed class EphemeralNote
{
    /// <summary>Fixed key of the single note row.</summary>
    public static readonly Guid SingletonId = new("3f1c1a52-9a2e-4d8e-9d7a-1e0d3e6a51c7");

    public Guid Id { get; set; } = SingletonId;

    public string Content { get; set; } = string.Empty;

    public DateTime LastEdited { get; set; } = DateTime.Now;
}
