using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Nectar.Data;
using Xunit;

namespace Nectar.Tests;

/// <summary>
/// Round-trips through the real SQLCipher migration: a plaintext database becomes an encrypted one
/// that still holds every row, and the key is the only way back in. The app itself cannot be
/// referenced (WinUI executable), so these tests compile the same source file it uses.
/// </summary>
public sealed class DatabaseEncryptionTests : IDisposable
{
    private readonly string _folder;
    private readonly string _databasePath;
    private readonly string _keyPath;

    public DatabaseEncryptionTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "nectar-db-tests-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_folder);
        _databasePath = Path.Combine(_folder, "nectar.db");
        _keyPath = Path.Combine(_folder, "db.key");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A pooled SQLite handle can outlive the test briefly; the temp folder is disposable.
        }
    }

    [Fact]
    public void Prepare_EncryptsPlaintextDatabase_AndPreservesEveryRow()
    {
        CreatePlaintextDatabase(("a", "First"), ("b", "Second"), ("c", "Third"));

        var preparation = DatabaseEncryption.Prepare(_databasePath, _keyPath);

        Assert.Null(preparation.Error);
        Assert.NotNull(preparation.Key);
        Assert.Equal(32, preparation.Key!.Length);
        Assert.False(DatabaseEncryption.LooksPlaintext(_databasePath), "the plaintext header must be gone");
        Assert.Equal(3, CountRows(_databasePath, preparation.Key));
        Assert.Equal("Second", ReadTitle(_databasePath, preparation.Key, "b"));
    }

    [Fact]
    public void Prepare_IsIdempotent_AndKeepsTheSameKey()
    {
        CreatePlaintextDatabase(("a", "First"));

        var first = DatabaseEncryption.Prepare(_databasePath, _keyPath);
        var second = DatabaseEncryption.Prepare(_databasePath, _keyPath);

        Assert.Null(first.Error);
        Assert.Null(second.Error);
        Assert.Equal(first.Key, second.Key);
        Assert.Equal(1, CountRows(_databasePath, second.Key!));
    }

    [Fact]
    public void EncryptedDatabase_RejectsTheWrongKey()
    {
        CreatePlaintextDatabase(("a", "First"));
        var preparation = DatabaseEncryption.Prepare(_databasePath, _keyPath);

        Assert.True(DatabaseEncryption.CanOpen(_databasePath, preparation.Key!));
        Assert.False(DatabaseEncryption.CanOpen(_databasePath, RandomNumberGenerator.GetBytes(32)));
    }

    [Fact]
    public void Prepare_RefusesToStartWhenTheStoredKeyIsCorrupt()
    {
        CreatePlaintextDatabase(("a", "First"));
        var preparation = DatabaseEncryption.Prepare(_databasePath, _keyPath);
        Assert.NotNull(preparation.Key);

        // A truncated or foreign db.key must fail closed, not silently create a new key that would
        // make the existing database unreadable.
        File.WriteAllBytes(_keyPath, RandomNumberGenerator.GetBytes(48));

        var retry = DatabaseEncryption.Prepare(_databasePath, _keyPath);

        Assert.Null(retry.Key);
        Assert.False(string.IsNullOrWhiteSpace(retry.Error));
        Assert.True(
            DatabaseEncryption.CanOpen(_databasePath, preparation.Key!),
            "the database itself must be left untouched");
    }

    [Fact]
    public void Prepare_SealsTheKeyInsteadOfStoringItRaw()
    {
        CreatePlaintextDatabase(("a", "First"));

        var preparation = DatabaseEncryption.Prepare(_databasePath, _keyPath);
        var sealedBytes = File.ReadAllBytes(_keyPath);

        Assert.True(sealedBytes.Length > preparation.Key!.Length);
        Assert.NotEqual(preparation.Key, sealedBytes);
        Assert.Equal(preparation.Key, DatabaseEncryption.TryLoadKey(_keyPath));
    }

    [Fact]
    public void Describe_ReportsStateWithoutOpeningTheLockedDatabase()
    {
        Assert.Equal("Not created yet", DatabaseEncryption.Describe(_databasePath, _keyPath));

        CreatePlaintextDatabase(("a", "First"));
        Assert.StartsWith("Plaintext", DatabaseEncryption.Describe(_databasePath, _keyPath));

        DatabaseEncryption.Prepare(_databasePath, _keyPath);
        Assert.StartsWith("Encrypted", DatabaseEncryption.Describe(_databasePath, _keyPath));

        // A held-open handle (what the running app has) must not change the answer or log.
        using var held = new FileStream(
            _databasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        Assert.StartsWith("Encrypted", DatabaseEncryption.Describe(_databasePath, _keyPath));
    }

    [Fact]
    public void Prepare_CheckpointsTheWal_SoNoPlaintextPagesLeakIntoTheEncryptedCopy()
    {
        CreatePlaintextDatabase(("a", "First"), ("b", "Second"));
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Pooling = false
        }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL; INSERT INTO Tasks VALUES ('c', 'Third');";
            command.ExecuteNonQuery();
        }

        var preparation = DatabaseEncryption.Prepare(_databasePath, _keyPath);

        Assert.Null(preparation.Error);
        Assert.Equal(3, CountRows(_databasePath, preparation.Key!));
        Assert.False(File.Exists(_databasePath + "-wal") && DatabaseEncryption.LooksPlaintext(_databasePath + "-wal"));
    }

    private void CreatePlaintextDatabase(params (string Id, string Title)[] rows)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Pooling = false
        }.ToString());
        connection.Open();

        using var create = connection.CreateCommand();
        create.CommandText = "CREATE TABLE Tasks (Id TEXT PRIMARY KEY, Title TEXT NOT NULL);";
        create.ExecuteNonQuery();

        foreach (var (id, title) in rows)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO Tasks (Id, Title) VALUES ($id, $title);";
            insert.Parameters.AddWithValue("$id", id);
            insert.Parameters.AddWithValue("$title", title);
            insert.ExecuteNonQuery();
        }
    }

    private static int CountRows(string path, byte[] key)
    {
        using var connection = new SqliteConnection(
            DatabaseEncryption.ConnectionString(path, key, SqliteOpenMode.ReadOnly, pooling: false));
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Tasks;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static string? ReadTitle(string path, byte[] key, string id)
    {
        using var connection = new SqliteConnection(
            DatabaseEncryption.ConnectionString(path, key, SqliteOpenMode.ReadOnly, pooling: false));
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Title FROM Tasks WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteScalar() as string;
    }
}
