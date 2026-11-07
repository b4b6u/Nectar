namespace Nectar.Data.Entities;

/// <summary>A single actionable item in the Tasks tab.</summary>
public sealed class WorkspaceTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Set when the task is checked off, cleared when it is reopened.</summary>
    public DateTime? CompletedAt { get; set; }

    public DateTime? Deadline { get; set; }
}
