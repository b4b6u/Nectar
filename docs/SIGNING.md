# Signing and shipping Nectar

Nectar ships as a single **MSIX** package. Windows will not install an unsigned MSIX, so signing is
part of packaging rather than an optional extra: the certificate signs the package, and the package
identity (`Nectar.Desktop`) is bound to it through the `Publisher="CN=Nectar"` value in
`Nectar/Package.appxmanifest`.

Everything here runs on your own machine with a certificate you already hold; the app never contacts
a signing service.

## 1. Build and sign

```powershell
.\scripts\package-msix.ps1
```

The script runs the MSIX build, signs the result with SHA-256 and an RFC 3161 timestamp, verifies
the signature, copies the package to `dist\Nectar-<version>-x64.msix` and prints its SHA-256. It
looks for a code-signing certificate in `Cert:\CurrentUser\My`:

```powershell
# subject lookup (the default: CN=Nectar)
.\scripts\package-msix.ps1

# a specific certificate
.\scripts\package-msix.ps1 -Thumbprint 62ADE8CB0735EF316343D872680CBF4C7B30B05A

# a .pfx file, password from the NECTAR_PFX_PASSWORD environment variable
.\scripts\package-msix.ps1 -PfxPath .\nectar-signing.pfx
```

The package is self-contained: the .NET runtime and the Windows App SDK are inside it, so installing
it needs no separate runtime.

To build without signing (for inspection only — it will not install):

```powershell
dotnet msbuild Nectar/Nectar.csproj -p:Configuration=Release -p:Platform=x64 `
  -p:RuntimeIdentifier=win-x64 -p:WindowsPackageType=MSIX `
  -p:AppxPackage=true -p:GenerateAppxPackageOnBuild=true `
  -p:UapAppxPackageBuildMode=SideloadOnly -p:AppxBundle=Never `
  -p:AppxPackageSigningEnabled=false -p:AppxPackageDir=".\build\msix\"
```

`signtool.exe` comes from the `Microsoft.Windows.SDK.BuildTools` NuGet package the project already
references, so a build machine needs no system-wide Windows SDK.

## 2. Choose a certificate

Signing is optional only in the sense that you choose the certificate; it is always local.

| Option | Use it when | Notes |
| --- | --- | --- |
| **OV code-signing certificate** | You buy from a public CA (Sectigo, DigiCert, SSL.com…) | Identity-validated; the private key usually lives on a hardware token or in a cloud HSM. SmartScreen reputation accrues over downloads rather than existing on day one. |
| **EV code-signing certificate** | You need trust immediately | Extended validation, hardware token required; historically the fastest route past SmartScreen warnings, at a higher price. |
| **Self-signed certificate** | Local testing only | Fine to prove the pipeline works; **never** ship it. Other machines show "Unknown publisher" and the package will not install. |

The manifest's `Publisher` must match the certificate subject exactly. A different certificate
means editing `Nectar/Package.appxmanifest` and rebuilding — and a different package identity, so
Windows treats it as a different app.

Never commit a `.pfx` or its password. Keep it outside the repository, or in the CI secret store.

### Self-signed certificate for a dry run

```powershell
$cert = New-SelfSignedCertificate `
  -Type CodeSigningCert `
  -Subject "CN=Nectar" `
  -CertStoreLocation Cert:\CurrentUser\My `
  -KeyUsage DigitalSignature `
  -KeyExportPolicy Exportable `
  -NotAfter (Get-Date).AddYears(1)
```

`CN=Nectar` is what the manifest declares. For a different subject, change the manifest and the
`-Subject` argument together.

An MSIX installs only when its signing chain is trusted. For a self-signed certificate, trust it
machine-wide — this needs an elevated PowerShell session:

```powershell
Export-Certificate -Cert $cert -FilePath .\Nectar.cer
Import-Certificate -FilePath .\Nectar.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

`Cert:\CurrentUser\TrustedPeople` is **not** enough: `Add-AppxPackage` still fails with
`0x800B0109` (the certificate chain ends in an untrusted root). Only do this for packages you built
yourself, and remove the certificate when you are done:

```powershell
Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Subject -eq 'CN=Nectar' |
  Remove-Item
```

## 3. Install

```powershell
Add-AppxPackage -Path .\dist\Nectar-1.0.1-x64.msix
```

Windows adds Nectar to the Start menu and the first launch writes `Nectar.lnk` to the desktop,
pointing at `explorer.exe shell:AppsFolder\<PackageFamilyName>!Nectar` with
`Assets\nectar.ico` from the package as the icon. Remove the app with:

```powershell
Get-AppxPackage Nectar.Desktop | Remove-AppxPackage
```

## 4. Verify by hand

```powershell
Get-AuthenticodeSignature .\dist\Nectar-1.0.1-x64.msix |
  Format-List Status, StatusMessage, SignerCertificate

Get-FileHash .\dist\Nectar-1.0.1-x64.msix -Algorithm SHA256
Get-AppxPackage Nectar.Desktop | Format-List Name, Version, PackageFullName, Status
```

`Status` must be `Valid`, the signer subject must match the manifest's `Publisher`, and the hash
must match the one published with the download. In the app, Customize → General shows the version
and the encrypted database status; if the package launched at all, signing worked.

## 5. What signing does and does not fix

- Signing proves the package has not changed since it was signed. It does not make the app safe.
- SmartScreen reputation for a new OV certificate builds up with download volume; expect a warning
  on a brand-new certificate even when signed. EV certificates are the usual shortcut.
- Sign **after** building and **before** copying to `dist`. Never edit a file inside the package
  afterwards — every byte is covered.
- Re-sign for every version bump. The version lives in three places that must agree:
  `Nectar/Nectar.csproj` (`<Version>`), `Nectar/app.manifest` and `Nectar/Package.appxmanifest`.
- A self-signed package is only installable on machines that trust its certificate. It is a way to
  test the pipeline, not a way to distribute the app.

## Signing in CI: `.github/workflows/release-msix.yml`

Pushing a `v<version>` tag — or running the workflow by hand — builds and signs the MSIX and attaches
four files to the release: `Nectar-<version>-x64.msix`, its `.sha256`, `nectar.appinstaller` and its
`.sha256`. The website's installer feed reads that release, so these names are the interface.

What the job enforces before it uploads anything:

- **The version already agrees.** It reads `<Version>` from `Nectar/Nectar.csproj`, fails unless
  `Package.appxmanifest` declares the same value with a fourth part (`2.0.1` → `2.0.1.0`), and fails
  on a tag push whose tag is not `v<version>`. Nothing is stamped at build time, so a tag can never
  disagree with the package it publishes.
- **The package is signed and installable.** It signs through `scripts/package-msix.ps1 -PfxPath`,
  the same script used locally, then reopens the finished package, reads `AppxManifest.xml` and
  compares the identity `Publisher` with the signing certificate subject. `signtool` accepts a
  mismatch; `Add-AppxPackage` does not — it would fail on every customer machine instead of here.
- **The feed matches the build.** `nectar.appinstaller` is rendered from the packed identity and the
  certificate subject, never from a hard-coded publisher. `/downloads/nectar.appinstaller` parses
  that file for the publisher, which is why the download page needs no separate configuration
  (`MSIX_PUBLISHER` stays as a fallback for a release published without the asset).

The job deliberately does **not** install the package: trusting a self-signed certificate
machine-wide on the runner would prove nothing about a real machine, and there is no elevated step
in the workflow at all. Install verification stays local — see `docs/RELEASE-DRY-RUN.md`.

### Secrets

Create them under **Settings → Secrets and variables → Actions → New repository secret**:

| Secret | Value |
| --- | --- |
| `BASE64_PFX` | The `.pfx`, base64-encoded without line breaks (below) |
| `PFX_PASSWORD` | The `.pfx` password; passed to the script through the environment, never the command line |

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes('.\nectar-signing.pfx')) | Set-Clipboard
```

Never commit the `.pfx`; `.gitignore` covers `*.pfx`, `*.cer` and `*.pvk`. The workflow deletes the
decoded copy from the runner in an `if: always()` step, so a failed or cancelled run leaves no key
behind.
