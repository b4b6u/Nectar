# Signing and shipping Nectar

Nectar ships unpackaged — a folder of files, no installer — so there is no Store identity or MSIX
pipeline to carry a signature. The thing that has to be signed is `Nectar.exe` itself, with an
Authenticode certificate, or Windows SmartScreen will treat the download as unknown software.
Everything here runs on your own machine with a certificate you already hold; the app never contacts
a signing service.

## 1. Publish

```powershell
dotnet publish Nectar/Nectar.csproj -p:PublishProfile=win-x64
```

This produces a **self-contained** folder in `Nectar/bin/publish/win-x64`: the .NET 8 runtime and
the Windows App SDK are copied in, so the target machine needs nothing pre-installed. It is not a
single file on purpose — WinUI resolves native DLLs by name from beside the executable.

## 2. Choose a certificate

Signing is optional and always local: no cloud signing account is involved in any path below.

| Option | Use it when | Notes |
| --- | --- | --- |
| **OV code-signing certificate** | You buy from a public CA (Sectigo, DigiCert, SSL.com…) | Identity-validated; the private key usually lives on a hardware token or in a cloud HSM. SmartScreen reputation accrues over downloads rather than existing on day one. |
| **EV code-signing certificate** | You need trust immediately | Extended validation, hardware token required; historically the fastest route past SmartScreen warnings, at a higher price. |
| **Self-signed certificate** | Local testing only | Fine to prove the pipeline works; **never** ship it. Windows shows "Unknown publisher" and the signature is untrusted on other machines. |

Never commit a `.pfx` or its password. Keep it outside the repository, or in the CI secret store.

### Self-signed certificate for a dry run

```powershell
$cert = New-SelfSignedCertificate `
  -Type CodeSigningCert `
  -Subject "CN=Nectar Development" `
  -CertStoreLocation Cert:\CurrentUser\My `
  -KeyUsage DigitalSignature `
  -NotAfter (Get-Date).AddYears(1)

$password = Read-Host -AsSecureString -Prompt "PFX password"
Export-PfxCertificate -Cert $cert -FilePath .\nectar-dev.pfx -Password $password
```

Trust it locally before verifying, or the script will reject its own output:

```powershell
Import-PfxCertificate -FilePath .\nectar-dev.pfx `
  -CertStoreLocation Cert:\CurrentUser\TrustedPeople -Password $password
```

A self-signed certificate is still untrusted after that, so a dry run of the pipeline needs
`-AllowUntrusted`, which signs, reports the status and warns instead of failing. Never attach such
a build to a release.

## 3. Sign

```powershell
# certificate file
.\scripts\sign-release.ps1 -PfxPath .\nectar-dev.pfx

# certificate already installed in Cert:\CurrentUser\My
.\scripts\sign-release.ps1 -Thumbprint 3F1A9C... -Zip
```

The script looks for `signtool.exe` in the Windows SDK if one is installed, and otherwise uses the
copy that ships inside the `Microsoft.Windows.SDK.BuildTools` NuGet package the project already
references — so a build machine needs no system-wide SDK. It signs with SHA-256, timestamps against
DigiCert by default (`-TimestampUrl` switches to Sectigo or another RFC 3161 authority), verifies
with `Get-AuthenticodeSignature`, and refuses to continue if the result is not `Valid`.

Timestamping matters: without it the signature expires the moment the certificate does, and every
copy already downloaded starts warning. With it, signatures stay valid for the life of the
timestamp.

Add `-Zip` to also get `dist\Nectar-<version>-win-x64.zip` plus its SHA-256, which is what you
attach to a GitHub Release and paste into the release notes.

## 4. Verify by hand

```powershell
Get-AuthenticodeSignature .\Nectar\bin\publish\win-x64\Nectar.exe |
  Format-List Status, StatusMessage, SignerCertificate

Get-FileHash .\dist\Nectar-2.0.0-win-x64.zip -Algorithm SHA256
```

`Status` must be `Valid`, the signer subject must be the name you expect, and the hash must match
the one published with the download.

## 5. What signing does and does not fix

- Signing proves the binary has not changed since it was signed. It does not make the app safe.
- SmartScreen reputation for a new OV certificate builds up with download volume; expect a warning
  on a brand-new certificate even when signed. EV certificates are the usual shortcut.
- Sign **after** publishing and **before** zipping. Never sign the debug output, and never edit
  files inside the folder after signing — every byte is covered.
- Re-sign for every version bump. The version lives in three places that must agree:
  `Nectar/Nectar.csproj` (`<Version>`), `Nectar/app.manifest` and `Nectar/Package.appxmanifest`.

## Signing in `.github/workflows/release-zip.yml`

The workflow expects exactly two secrets:

| Secret | Value |
| --- | --- |
| `BASE64_PFX` | the `.pfx` file, base64-encoded **without line breaks** |
| `PFX_PASSWORD` | the password used when exporting that `.pfx` |

Generate them from the certificate you exported in step 2:

```powershell
$b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes('.\nectar-dev.pfx'))
Set-Clipboard $b64      # paste into the BASE64_PFX secret
# PFX_PASSWORD is the SecureString you typed at export time — paste the same text.
```

Create them under **Settings → Secrets and variables → Actions → New repository secret**. Never
commit the `.pfx`; `.gitignore` already covers `*.pfx`, `*.cer` and `*.pvk`.

The workflow decodes to `$env:RUNNER_TEMP\signing.pfx`, signs, and then runs an `if: always()` step
that deletes the file, sweeps any stray `*.pfx` from the temp and workspace folders, and fails the
job if the key survives. That step is deliberate: without it a cancelled or failed run can leave
the private key on the runner.

A PFX password passed on a command line is visible to process inspection on the runner; the
thumbprint path (`-Thumbprint`) avoids that entirely when the key lives in a certificate store.
The PFX path covers the portable zip, which is the only release artifact; there is no cloud signing
service and no MSIX pipeline.
