# Encrypted local database (SQLCipher)

Nectar's tasks, note and focus sessions live in `%LOCALAPPDATA%\Nectar\nectar.db`. That file is now
a **SQLCipher** database: opened with a 32-byte key that Windows DPAPI seals to the signed-in user.
Copying the folder to another account or machine yields an unreadable ciphertext, and no plaintext
copy is left behind.

## What is in place

| Piece | Where |
| --- | --- |
| Packages | `Nectar.csproj`: `Microsoft.EntityFrameworkCore.Sqlite.Core` + `SQLitePCLRaw.bundle_e_sqlcipher` |
| Key storage | `%LOCALAPPDATA%\Nectar\db.key`, sealed with `CryptProtectData` (current user) |
| Key + migration | `Nectar/Data/DatabaseEncryption.cs` |
| Connection | `WorkspaceRepository` passes `Password` through `SqliteConnectionStringBuilder` |
| Provider init | `SQLitePCL.Batteries_V2.Init()` once, before the first connection |

`.Core` replaces the meta-package on purpose: `Microsoft.EntityFrameworkCore.Sqlite` drags in
`bundle_e_sqlite3`, and two native providers in one process means whichever DLL loads first wins —
usually the plaintext one, which then ignores `PRAGMA key`. If a future dependency reintroduces
`SQLitePCLRaw.bundle_e_sqlite3`, remove it or the app will silently write plaintext again.

## First launch after the upgrade

1. `DatabaseEncryption.Prepare` runs inside the repository constructor, before EF Core connects.
2. If `db.key` is missing it generates 32 random bytes, seals them with DPAPI and writes the file.
3. If `nectar.db` starts with `SQLite format 3\0` it is plaintext:
   - `PRAGMA wal_checkpoint(TRUNCATE)` folds the WAL into the main file;
   - `ATTACH DATABASE 'nectar.db.enc' ... KEY '<hex>'` + `sqlcipher_export('encrypted')` copies it;
   - the plaintext `-wal`/`-shm` are deleted, the original becomes `nectar.db.plain.bak`;
   - the encrypted file is opened read-only and must pass `PRAGMA integrity_check` = `ok`;
   - only then is `nectar.db.plain.bak` deleted. If verification fails, the backup is restored and
     the app shows the reason in its banner instead of starting on a half-converted database.
4. If `nectar.db` is already encrypted, it is opened with the key and checked; a missing or wrong
   `db.key` produces a banner explaining how to restore it — the file is never overwritten.

A failed migration is not fatal: the app runs with `InitializationError` set and shows the banner.
The original data is intact either way.

## Verifying by hand

There is no `sqlcipher` CLI on a stock Windows box, so check the two properties that matter:

```powershell
# 1. The header is no longer the plaintext SQLite magic.
$bytes = [IO.File]::ReadAllBytes("$env:LOCALAPPDATA\Nectar\nectar.db")[0..15]
[Text.Encoding]::ASCII.GetString($bytes)   # expect gibberish, not "SQLite format 3"

# 2. A plain SQLite reader cannot open it.
python -c "import sqlite3;c=sqlite3.connect(r'%LOCALAPPDATA%\Nectar\nectar.db');print(c.execute('select count(*) from Tasks').fetchone())"
# expect: sqlite3.DatabaseError: file is not a database
```

The app itself proves the positive case on every launch: `PRAGMA integrity_check` runs against the
encrypted file, and the workspace loads the same rows it did before the upgrade, so the migration is
verified by the data being there.

## Operational consequences

- **Key loss is data loss.** No escrow, no recovery code. Restoring `db.key` from a backup is the
  only way back; otherwise the encrypted file is unrecoverable by design.
- **Backups now need two files.** `nectar.db` alone is useless without `db.key`; copy both, and keep
  the pair somewhere the user trusts. A backup of `db.key` is as sensitive as the database.
- **`*.plain.bak` is transient.** It exists only between the copy and the integrity check, and is
  deleted on success. If one is ever found on disk, the migration was interrupted and the next
  launch retries.
- **Performance**: page-level AES costs roughly 5–10% on this workload; the Insights aggregate over
  a few thousand rows is unaffected in practice.
- **Uninstall/delete**: removing `nectar.db` and `db.key` removes everything; the DPAPI key cannot be
  unsealed by another account even if the file survives.
