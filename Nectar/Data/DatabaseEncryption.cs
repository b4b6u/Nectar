using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Nectar.Interop;
using Nectar.Services;

namespace Nectar.Data;

/// <summary>
/// Owns the SQLCipher key and the one-time conversion of an existing plaintext database.
///
/// The key is 32 random bytes sealed with DPAPI at <c>%LOCALAPPDATA%\Nectar\db.key</c>, so another
/// Windows account cannot unseal it and copying the folder to another machine yields an unreadable
/// key. The database itself is opened with that key through <see cref="Microsoft.Data.Sqlite"/>'s
/// <c>Password</c>, which issues <c>PRAGMA key</c> for SQLCipher builds.
///
/// Conversion is deliberately paranoid: the plaintext file is kept as <c>.plain.bak</c> until the
/// encrypted copy passes <c>PRAGMA integrity_check</c>, and restored if it does not. A failure here
/// must never cost the user their tasks.
/// </summary>
internal static class DatabaseEncryption
{
    private const int KeyBytes = 32;

    private static readonly byte[] PlaintextMagic = "SQLite format 3\0"u8.ToArray();

    private static bool _providerReady;

    internal static string KeyPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nectar",
            "db.key");

    /// <summary>SQLCipher passphrase for the raw key; hex keeps it quote-safe in PRAGMA/ATTACH.</summary>
    internal static string Passphrase(byte[] key) => Convert.ToHexString(key);

    internal static string ConnectionString(
        string path,
        byte[] key,
        SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate,
        bool pooling = true) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Password = Passphrase(key),
            Mode = mode,
            Pooling = pooling
        }.ToString();

    /// <summary>
    /// Loads (or creates) the key and converts a plaintext database in place. Never throws: the
    /// caller surfaces the returned error in the UI banner instead of crashing at startup.
    /// </summary>
    internal static DatabasePreparation Prepare(string databasePath, string? keyPath = null)
    {
        try
        {
            EnsureProvider();
            var keyFile = keyPath ?? KeyPath;

            // A zero-byte file is what a half-finished earlier attempt leaves behind; SQLite would
            // otherwise treat it as a valid empty database and skip the key check.
            if (File.Exists(databasePath) && new FileInfo(databasePath).Length == 0)
            {
                File.Delete(databasePath);
            }

            var key = LoadOrCreateKey(keyFile, out var keyError);
            if (key is null)
            {
                return new DatabasePreparation(null, keyError);
            }

            if (File.Exists(databasePath) && LooksPlaintext(databasePath))
            {
                Migrate(databasePath, key);
            }

            if (File.Exists(databasePath) && !CanOpen(databasePath, key))
            {
                return new DatabasePreparation(
                    null,
                    "Nectar's database exists but cannot be opened with the stored key. Restore " +
                    "db.key from a backup, or rename nectar.db to start with an empty workspace.");
            }

            return new DatabasePreparation(key, null);
        }
        catch (Exception ex)
        {
            AppLogger.Log("Database encryption preparation failed", ex);
            return new DatabasePreparation(
                null,
                $"The encrypted database could not be prepared: {ex.Message}");
        }
    }

    /// <summary>True when the file still begins with the plaintext SQLite header.</summary>
    internal static bool LooksPlaintext(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var header = new byte[PlaintextMagic.Length];
            var read = stream.Read(header, 0, header.Length);
            return read == header.Length && header.AsSpan().SequenceEqual(PlaintextMagic);
        }
        catch (Exception ex)
        {
            AppLogger.Log("Could not read the database header", ex);
            return false;
        }
    }

    /// <summary>Opens an encrypted database read-only and reports whether it is structurally sound.</summary>
    internal static bool CanOpen(string path, byte[] key)
    {
        try
        {
            using var connection = new SqliteConnection(
                ConnectionString(path, key, SqliteOpenMode.ReadOnly, pooling: false));
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            return command.ExecuteScalar() is string result &&
                   result.Equals("ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            AppLogger.Log("Encrypted database did not open with the stored key", ex);
            return false;
        }
    }

    /// <summary>The sealed key, for diagnostics and tests; null when absent or unreadable.</summary>
    internal static byte[]? TryLoadKey(string? keyPath = null)
    {
        try
        {
            var keyFile = keyPath ?? KeyPath;
            return File.Exists(keyFile) ? DataProtection.Unprotect(File.ReadAllBytes(keyFile)) : null;
        }
        catch (Exception ex)
        {
            AppLogger.Log("Could not read the database key", ex);
            return null;
        }
    }

    /// <summary>
    /// One line for the Settings screen. Deliberately does not open the database: SQLite holds the
    /// file exclusively while the app runs, and a status label must not log or fail because of it.
    /// The sealed key only exists once <see cref="Prepare"/> has migrated (or will create) an
    /// encrypted database, so its presence is the honest signal.
    /// </summary>
    internal static string Describe(string databasePath, string? keyPath = null)
    {
        try
        {
            var keyFile = keyPath ?? KeyPath;
            if (File.Exists(keyFile))
            {
                return "Encrypted — SQLCipher 256-bit";
            }

            return File.Exists(databasePath) ? "Plaintext — encryption pending" : "Not created yet";
        }
        catch (Exception ex)
        {
            AppLogger.Log("Could not read the database encryption state", ex);
            return "Unknown";
        }
    }

    private static byte[]? LoadOrCreateKey(string keyPath, out string? error)
    {
        error = null;

        try
        {
            if (File.Exists(keyPath))
            {
                var sealedBytes = File.ReadAllBytes(keyPath);
                var plain = DataProtection.Unprotect(sealedBytes);
                if (plain is { Length: KeyBytes })
                {
                    return plain;
                }

                error = "The stored database key is unreadable. Restore db.key from a backup, " +
                        "or rename nectar.db to start with an empty workspace.";
                return null;
            }

            var key = RandomNumberGenerator.GetBytes(KeyBytes);
            var sealedKey = DataProtection.Protect(key, "Nectar database key");
            if (sealedKey is null)
            {
                error = "The database key could not be sealed with Windows DPAPI, so the encrypted " +
                        "database cannot be opened safely.";
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
            AtomicFile.WriteAllBytes(keyPath, sealedKey);
            AppLogger.Log("Created the Nectar database key (DPAPI, current user).");
            return key;
        }
        catch (Exception ex)
        {
            AppLogger.Log("Database key setup failed", ex);
            error = $"The database key could not be prepared: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Copies a plaintext database into a new encrypted one with <c>sqlcipher_export</c>, verifies
    /// the result, and only then removes the plaintext copy.
    /// </summary>
    private static void Migrate(string path, byte[] key)
    {
        var target = path + ".enc";
        var backup = path + ".plain.bak";

        TryDelete(target);
        TryDelete(backup);

        // ReadWriteCreate is required: ATTACH inherits the main connection's open flags, so a
        // ReadWrite source cannot create the encrypted target and fails with SQLITE_CANTOPEN (14).
        using (var source = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            }.ToString()))
        {
            source.Open();

            // Fold the WAL into the main file first; its pages are still plaintext and would be
            // replayed into the encrypted database as garbage.
            using (var checkpoint = source.CreateCommand())
            {
                checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                try
                {
                    checkpoint.ExecuteNonQuery();
                }
                catch (SqliteException ex)
                {
                    AppLogger.Log("Plaintext WAL checkpoint failed; continuing", ex);
                }
            }

            using var command = source.CreateCommand();
            command.CommandText =
                $"ATTACH DATABASE '{Escape(target)}' AS encrypted KEY '{Passphrase(key)}';" +
                "SELECT sqlcipher_export('encrypted');" +
                "DETACH DATABASE encrypted;";
            command.ExecuteNonQuery();
        }

        // The plaintext sidecars hold readable pages; the encrypted copy already has them folded in.
        DeleteSidecars(path);

        File.Move(path, backup, overwrite: true);
        File.Move(target, path, overwrite: true);

        if (!CanOpen(path, key))
        {
            TryDelete(path);
            File.Move(backup, path, overwrite: true);
            throw new InvalidOperationException(
                "The encrypted copy failed verification, so the original database was restored.");
        }

        // Verified. The plaintext copy is now the thing that must not survive.
        TryDelete(backup);
        DeleteSidecars(backup);
        AppLogger.Log($"Workspace database encrypted with SQLCipher ({key.Length * 8}-bit key).");
    }

    private static void EnsureProvider()
    {
        if (_providerReady)
        {
            return;
        }

        try
        {
            // Registered explicitly so the cipher build is the only SQLite provider in the process,
            // whatever order EF Core and the migration reach the native library.
            SQLitePCL.Batteries_V2.Init();
        }
        catch (Exception ex)
        {
            AppLogger.Log("SQLite provider initialisation failed", ex);
        }

        _providerReady = true;
    }

    private static string Escape(string path) => path.Replace("'", "''");

    private static void DeleteSidecars(string path)
    {
        TryDelete(path + "-wal");
        TryDelete(path + "-shm");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log($"Could not delete {Path.GetFileName(path)}", ex);
        }
    }
}

internal sealed record DatabasePreparation(byte[]? Key, string? Error);
