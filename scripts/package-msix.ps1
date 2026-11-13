<#
.SYNOPSIS
    Builds the Nectar MSIX, signs it with a local code-signing certificate and stages it in dist\.

.DESCRIPTION
    Produces one self-contained .msix: the .NET runtime and the Windows App SDK travel inside the
    package, so installing it needs no separate runtime download.

    Signing is mandatory for an MSIX — Windows refuses to install an unsigned package. The
    certificate is looked up in Cert:\CurrentUser\My by thumbprint or by subject, and can also be
    given as a .pfx file. A self-signed certificate must be trusted in
    Cert:\LocalMachine\TrustedPeople before the package will install.

.PARAMETER Thumbprint
    Thumbprint of a code-signing certificate already in Cert:\CurrentUser\My.

.PARAMETER Subject
    Subject to look up instead of a thumbprint. Defaults to CN=Nectar.

.PARAMETER PfxPath
    A .pfx file to sign with. Ignored when -Thumbprint or -Subject finds a certificate.

.PARAMETER TimestampUrl
    RFC 3161 timestamp authority. Defaults to DigiCert.

.EXAMPLE
    .\scripts\package-msix.ps1

.EXAMPLE
    .\scripts\package-msix.ps1 -Thumbprint 62ADE8CB0735EF316343D872680CBF4C7B30B05A
#>
[CmdletBinding()]
param(
    [string]$Thumbprint,
    [string]$Subject = 'CN=Nectar',
    [string]$PfxPath,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Nectar\Nectar.csproj'
$outDir = Join-Path $root 'build\msix'

$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "Could not read <Version> from $project" }

Write-Output "building Nectar $version (MSIX, x64, self-contained)"

& dotnet msbuild $project `
    -p:Configuration=Release `
    -p:Platform=x64 `
    -p:RuntimeIdentifier=win-x64 `
    -p:WindowsPackageType=MSIX `
    -p:AppxPackage=true `
    -p:GenerateAppxPackageOnBuild=true `
    -p:UapAppxPackageBuildMode=SideloadOnly `
    -p:AppxBundle=Never `
    -p:AppxPackageSigningEnabled=false `
    -p:AppxPackageDir="$outDir\"
if ($LASTEXITCODE -ne 0) { throw "msbuild failed with $LASTEXITCODE" }

$built = Get-ChildItem $outDir -Recurse -Filter '*.msix' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $built) { throw "no .msix was produced under $outDir" }

# signtool ships inside the SDK BuildTools package the project already references.
$signtool = Get-ChildItem (Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools') `
        -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\' } |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $signtool) {
    $signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue
}
if (-not $signtool) { throw 'signtool.exe not found; install the Windows SDK or restore the project first' }

$signArgs = @('sign', '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256')
if ($Thumbprint) {
    $signArgs += @('/sha1', $Thumbprint, '/s', 'My')
}
elseif ($PfxPath) {
    $signArgs += @('/f', (Resolve-Path $PfxPath).Path)
    if ($env:NECTAR_PFX_PASSWORD) { $signArgs += @('/p', $env:NECTAR_PFX_PASSWORD) }
}
else {
    $cert = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $Subject -and $_.HasPrivateKey } |
        Select-Object -First 1
    if (-not $cert) { throw "No certificate '$Subject' in Cert:\CurrentUser\My. Create one, or pass -PfxPath." }
    $signArgs += @('/sha1', $cert.Thumbprint, '/s', 'My')
}

& $signtool.FullName @signArgs $built.FullName
if ($LASTEXITCODE -ne 0) { throw "signtool failed with $LASTEXITCODE" }

$sig = Get-AuthenticodeSignature $built.FullName
if ($sig.Status -ne 'Valid') { throw "signature status is $($sig.Status)" }

$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$staged = Join-Path $dist "Nectar-$version-x64.msix"
Copy-Item $built.FullName $staged -Force
$hash = Get-FileHash $staged -Algorithm SHA256

Write-Output ''
Write-Output "package : $staged"
Write-Output "size    : $((Get-Item $staged).Length) bytes"
Write-Output "signer  : $($sig.SignerCertificate.Subject)"
Write-Output "sha256  : $($hash.Hash)"
Write-Output ''
Write-Output "Install with: Add-AppxPackage -Path `"$staged`""
