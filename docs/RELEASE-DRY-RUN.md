# Building and verifying a release locally

Nectar is a local program: the database, settings and key live under `%LOCALAPPDATA%\Nectar`, and
nothing in the app talks to a server. A release is therefore a local build you run yourself — there
is no cloud account, no CI signing service and no upload step in this procedure.

## 1. Build the debug app

```bash
dotnet build Nectar/Nectar.csproj -c Debug
Nectar/bin/Debug/net8.0-windows10.0.19041.0/win-x64/Nectar.exe
```

Check, in this order:

1. The app starts hidden (no window, no taskbar entry).
2. `Alt + N` opens the workspace and `Esc` collapses it.
3. Customize → General shows **Database: Encrypted — SQLCipher 256-bit** and the data folder opens
   at `%LOCALAPPDATA%\Nectar`.
4. Adding a task, closing the app and reopening it keeps the task.
5. With no calendar linked, the Events card shows its empty state and the app makes no network
   request. Linking a calendar is the only action that leaves the machine.

## 2. Publish the folder build

```powershell
dotnet publish Nectar/Nectar.csproj -p:PublishProfile=win-x64
```

This writes a self-contained `Nectar/bin/publish/win-x64`. Copy that folder somewhere clean (or to
another machine) and start `Nectar.exe`: it must run without installing .NET or the Windows App SDK.

Verify the copy in place:

```powershell
Get-ChildItem Nectar\bin\publish\win-x64 | Measure-Object -Property Length -Sum
```

The database is created on first write, not at startup. An empty `%LOCALAPPDATA%\Nectar` after a
first launch is expected; `nectar.db` and `db.key` appear together after the first task or note.

## 3. Signing is optional and local

Signing only matters when you hand the zip to someone else. Use a certificate you control — a
public OV/EV certificate, or a self-signed one for a local dry run. The procedure, including
`-AllowUntrusted` for a self-signed certificate, is in [`SIGNING.md`](SIGNING.md). No signing
service account is needed for either path.

```powershell
.\scripts\sign-release.ps1 -PfxPath .\nectar-dev.pfx -Zip
Get-AuthenticodeSignature .\Nectar\bin\publish\win-x64\Nectar.exe | Format-List Status, SignerCertificate
Get-FileHash .\dist\Nectar-*-win-x64.zip -Algorithm SHA256
```

## 4. Confirm the offline claim

With the app running, the only connections it may open are the calendar provider's. To check:

```powershell
Get-NetTCPConnection -OwningProcess (Get-Process Nectar).Id -ErrorAction SilentlyContinue |
  Select-Object RemoteAddress, RemotePort, State
```

Expect no rows until you link a calendar. After linking, expect the Google or Microsoft endpoints
only; the app does not call the marketing site, GitHub, or any update feed.

## 5. What to keep out of the repository

- `*.pfx`, `*.cer`, `*.pvk` — already covered by `.gitignore`.
- `%LOCALAPPDATA%\Nectar\db.key` — the user's key, never part of a build.
- The encrypted database itself — personal data, not an artifact.
