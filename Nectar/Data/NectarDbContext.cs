using Nectar.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Nectar.Data;

/// <summary>
/// SQLite-backed context for the workspace. Instances are short-lived: the repository creates
/// one per operation so a background refresh can never collide with a UI save.
/// </summary>
internal sealed class NectarDbContext : DbContext
{
    public NectarDbContext(DbContextOptions<NectarDbContext> options)
        : base(options)
    {
    }

    public DbSet<WorkspaceTask> Tasks => Set<WorkspaceTask>();

    public DbSet<EphemeralNote> Notes => Set<EphemeralNote>();

    public DbSet<FocusSession> FocusSessions => Set<FocusSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var task = modelBuilder.Entity<WorkspaceTask>();
        task.ToTable("Tasks");
        task.HasKey(t => t.Id);
        task.Property(t => t.Title).IsRequired().HasMaxLength(512);
        task.Property(t => t.IsCompleted).IsRequired();
        task.Property(t => t.CreatedAt).IsRequired();
        task.HasIndex(t => t.IsCompleted);
        task.HasIndex(t => t.Deadline);
        task.HasIndex(t => t.CreatedAt);
        task.HasIndex(t => t.CompletedAt);

        var note = modelBuilder.Entity<EphemeralNote>();
        note.ToTable("Notes");
        note.HasKey(n => n.Id);
        note.Property(n => n.Content).IsRequired();
        note.Property(n => n.LastEdited).IsRequired();

        var focus = modelBuilder.Entity<FocusSession>();
        focus.ToTable("FocusSessions");
        focus.HasKey(f => f.Id);
        focus.Property(f => f.DurationMinutes).IsRequired();
        focus.Property(f => f.CompletedAt).IsRequired();
        focus.HasIndex(f => f.CompletedAt);
    }
}
