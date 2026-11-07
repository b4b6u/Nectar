namespace Nectar.Data.Entities;

/// <summary>
/// One finished focus countdown. Sessions are only written when the timer runs all the way out,
/// so the sum of <see cref="DurationMinutes"/> is real focused time rather than time with the
/// overlay open.
/// </summary>
public sealed class FocusSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int DurationMinutes { get; set; }

    public DateTime CompletedAt { get; set; } = DateTime.Now;
}
