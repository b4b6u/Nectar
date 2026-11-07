using Nectar.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Nectar.Data;

/// <summary>
/// All persistence for Nectar. The database lives in
/// %LOCALAPPDATA%\Nectar\nectar.db, and every operation is scoped to its own DbContext so
/// failures are contained and never leave a poisoned long-lived context behind.
/// </summary>
internal sealed class WorkspaceRepository
{
    private const string FolderName = "Nectar";
    private const string FileName = "nectar.db";

    private readonly DbContextOptions<NectarDbContext> _options;
    private readonly string? _setupError;

    internal WorkspaceRepository()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            FolderName);

        Directory.CreateDirectory(folder);
        DatabasePath = Path.Combine(folder, FileName);

        var builder = new DbContextOptionsBuilder<NectarDbContext>();

        // The key is loaded (or created) and any plaintext database is converted here, before the
        // first connection: opening a plaintext file with a password fails with "file is not a
        // database", so the order matters.
        var preparation = DatabaseEncryption.Prepare(DatabasePath);
        _setupError = preparation.Error;
        InitializationError = preparation.Error;

        if (preparation.Key is not null)
        {
            builder.UseSqlite(DatabaseEncryption.ConnectionString(DatabasePath, preparation.Key));
        }

        _options = builder.Options;
    }

    internal string DatabasePath { get; }

    /// <summary>Human readable message when initialization failed; null when healthy.</summary>
    internal string? InitializationError { get; private set; }

    /// <summary>Creates the schema on first run and guarantees the singleton note row exists.</summary>
    internal async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_setupError is not null)
        {
            InitializationError = _setupError;
            return false;
        }

        try
        {
            await using var db = new NectarDbContext(_options);
            await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

            // EnsureCreated only builds a missing database. These statements bring an existing
            // Nectar database up to the Insights schema without dropping user data.
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS FocusSessions (" +
                "Id TEXT NOT NULL CONSTRAINT PK_FocusSessions PRIMARY KEY, " +
                "DurationMinutes INTEGER NOT NULL, " +
                "CompletedAt TEXT NOT NULL)",
                cancellationToken).ConfigureAwait(false);

            await db.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS IX_FocusSessions_CompletedAt ON FocusSessions (CompletedAt)",
                cancellationToken).ConfigureAwait(false);

            if (!await TaskColumnExistsAsync(db, "CompletedAt", cancellationToken).ConfigureAwait(false))
            {
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE Tasks ADD COLUMN CompletedAt TEXT NULL",
                    cancellationToken).ConfigureAwait(false);
            }

            var noteExists = await db.Notes
                .AnyAsync(n => n.Id == EphemeralNote.SingletonId, cancellationToken)
                .ConfigureAwait(false);

            if (!noteExists)
            {
                db.Notes.Add(new EphemeralNote { Id = EphemeralNote.SingletonId });
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            InitializationError = null;
            return true;
        }
        catch (Exception ex)
        {
            InitializationError = $"The local database could not be opened: {ex.Message}";
            return false;
        }
    }

    internal async Task<IReadOnlyList<WorkspaceTask>> LoadTasksAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = new NectarDbContext(_options);
            return await db.Tasks
                .AsNoTracking()
                .OrderBy(t => t.IsCompleted)
                .ThenBy(t => t.Deadline ?? DateTime.MaxValue)
                .ThenBy(t => t.CreatedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new WorkspaceDataException("Tasks could not be loaded.", ex);
        }
    }

    internal async Task AddTaskAsync(WorkspaceTask task, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = new NectarDbContext(_options);
            db.Tasks.Add(task);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new WorkspaceDataException("The task could not be saved.", ex);
        }
    }

    internal async Task UpdateTaskAsync(WorkspaceTask task, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = new NectarDbContext(_options);
            db.Tasks.Update(task);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new WorkspaceDataException("The task could not be updated.", ex);
        }
    }

    internal async Task DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = new NectarDbContext(_options);
            var existing = await db.Tasks
                .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                return;
            }

            db.Tasks.Remove(existing);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new WorkspaceDataException("The task could not be deleted.", ex);
        }
    }

    /// <summary>
    /// Writes completion state and its timestamp together, so a row can never read "done"
    /// without a <c>CompletedAt</c> for the Insights chart to group by.
    /// </summary>
    internal async Task SetTaskCompletionAsync(
        Guid id,
        bool isCompleted,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = new NectarDbContext(_options);
            var existing = await db.Tasks
                .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                return;
            }

            existing.IsCompleted = isCompleted;
            existing.CompletedAt = isCompleted ? DateTime.Now : null;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new WorkspaceDataException("The task could not be updated.", ex);
        }
    }

    /// <summary>Records one finished focus countdown.</summary>
    internal async Task LogFocusSessionAsync(
        int durationMinutes,
        DateTime completedAt,
        CancellationToken cancellationToken = default)
    {
        if (durationMinutes <= 0)
        {
            return;
        }

        try
        {
            await using var db = new NectarDbContext(_options);
            db.FocusSessions.Add(new FocusSession
            {
                DurationMinutes = durationMinutes,
                CompletedAt = completedAt,
            });

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new WorkspaceDataException("The focus session could not be saved.", ex);
        }
    }

    /// <summary>
    /// Tasks touched since <paramref name="since"/>: created in the window, or finished in it.
    /// Older rows cannot affect a per-day count inside the window either way.
    /// </summary>
    internal async Task<IReadOnlyList<WorkspaceTask>> LoadTaskHistoryAsync(
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = new NectarDbContext(_options);
            return await db.Tasks
                .AsNoTracking()
                .Where(t => t.CreatedAt >= since
                    || (t.CompletedAt != null && t.CompletedAt >= since)
                    || t.CompletedAt == null)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new WorkspaceDataException("Recent activity could not be loaded.", ex);
        }
    }

    internal async Task<IReadOnlyList<FocusSession>> LoadFocusSessionsAsync(
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = new NectarDbContext(_options);
            return await db.FocusSessions
                .AsNoTracking()
                .Where(f => f.CompletedAt >= since)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new WorkspaceDataException("Focus time could not be loaded.", ex);
        }
    }

    /// <summary>True when <c>Tasks</c> already has the column (SQLite has no IF NOT EXISTS here).</summary>
    private static async Task<bool> TaskColumnExistsAsync(
        NectarDbContext db,
        string column,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Tasks)";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal async Task<string> LoadNoteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = new NectarDbContext(_options);
            var note = await db.Notes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == EphemeralNote.SingletonId, cancellationToken)
                .ConfigureAwait(false);

            return note?.Content ?? string.Empty;
        }
        catch (Exception ex)
        {
            throw new WorkspaceDataException("The note could not be loaded.", ex);
        }
    }

    internal async Task SaveNoteAsync(string content, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = new NectarDbContext(_options);
            var note = await db.Notes
                .FirstOrDefaultAsync(n => n.Id == EphemeralNote.SingletonId, cancellationToken)
                .ConfigureAwait(false);

            if (note is null)
            {
                db.Notes.Add(new EphemeralNote
                {
                    Id = EphemeralNote.SingletonId,
                    Content = content,
                    LastEdited = DateTime.Now,
                });
            }
            else
            {
                note.Content = content;
                note.LastEdited = DateTime.Now;
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new WorkspaceDataException("The note could not be saved.", ex);
        }
    }
}

/// <summary>Thrown when a SQLite operation fails; the message is safe to surface in the UI.</summary>
internal sealed class WorkspaceDataException : Exception
{
    internal WorkspaceDataException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
