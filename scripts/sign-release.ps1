<#
.SYNOPSIS
    Authenticode-signs the published Nectar build and optionally packs a release zip.

.DESCRIPTION
    Picks signtool.exe from the newest installed Windows SDK (or PATH), signs Nectar.exe with
    SHA-256 and an RFC 3161 timestamp, then verifies the result. Use either a PFX file or a
    certificate that already lives in CurrentUser\My:

        .\scripts\sign-release.ps1 -PfxPath .\nectar-signing.pfx
        .\scripts\sign-release.ps1 -Thumbprint 3F1A9C... -Zip

    The script stores no credentials. A PFX password is read from a prompt unless one is
    supplied, and it is handed to signtool for that one invocation only (as signtool has no
    other way to read a protected key; prefer the -Thumbprint path in automation).

.PARAMETER PublishDir
    Folder produced by the win-x64 publish profile.

.PARAMETER TimestampUrl
    RFC 3161 timestamp authority. DigiCert is the default; Sectigo's is
    http://timestamp.sectigo.com.

.PARAMETER Zip
    Also produce dist\Nectar-<Version>-win-x64.zip and print its SHA-256.
#>
[CmdletBinding(DefaultParameterSetName = 'Pfx')]
param(
    [string]$PublishDir,

    [Parameter(ParameterSetName = 'Pfx', Mandatory = $true)]
    [string]$PfxPath,

    [Parameter(ParameterSetName = 'Pfx')]
    [SecureString]$PfxPassword,

    [Parameter(ParameterSetName = 'Store', Mandatory = $true)]
    [string]$Thumbprint,

    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [string]$Version = '2.0.0',
    [switch]$AllowUntrusted,
    [switch]$Zip
)

$ErrorActionPreference = 'Stop'

# $PSScriptRoot is not populated while parameter defaults are bound when the script is launched
# with `powershell -File`, which left $PublishDir empty. Resolve the script root here instead.
$scriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }

if (-not $PublishDir) {
    $PublishDir = Join-Path $scriptRoot '..\Nectar\bin\publish\win-x64'
}

if (-not (Test-Path $PublishDir)) {
    throw "Publish folder not found: $PublishDir. Run: dotnet publish Nectar/Nectar.csproj -p:PublishProfile=win-x64"
}

$PublishDir = (Resolve-Path $PublishDir).Path
$exe = Join-Path $PublishDir 'Nectar.exe'
if (-not (Test-Path $exe)) {
    throw "Nectar.exe is not in $PublishDir. Publish before signing."
}

function Find-SignTool {
    $roots = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'),
        (Join-Path $env:ProgramFiles 'Windows Kits\10\bin')
    )

    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }
        $candidate = Get-ChildItem $root -Directory |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
            Where-Object { Test-Path $_ } |
            Select-Object -First 1
        if ($candidate) { return $candidate }
    }

    # The project pins Microsoft.Windows.SDK.BuildTools, which ships signtool in the NuGet
    # cache. That means a build machine needs no system-wide Windows SDK to sign.
    $nuget = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools'
    if (Test-Path $nuget) {
        $candidate = Get-ChildItem $nuget -Directory |
            Sort-Object Name -Descending |
            ForEach-Object { Get-ChildItem (Join-Path $_.FullName 'bin') -Directory -ErrorAction SilentlyContinue } |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
            Where-Object { Test-Path $_ } |
            Select-Object -First 1
        if ($candidate) { return $candidate }
    }

    $onPath = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    throw 'signtool.exe not found. Restore the project (Microsoft.Windows.SDK.BuildTools ships it) or install the Windows SDK signing tools.'
}

$signTool = Find-SignTool
$signArgs = @('sign', '/fd', 'SHA256', '/td', 'SHA256', '/tr', $TimestampUrl, '/v')

if ($PSCmdlet.ParameterSetName -eq 'Pfx') {
    if (-not (Test-Path $PfxPath)) { throw "PFX not found: $PfxPath" }
    if (-not $PfxPassword) {
        $PfxPassword = Read-Host -AsSecureString -Prompt 'PFX password'
    }

    $plain = [System.Net.NetworkCredential]::new('', $PfxPassword).Password
    $signArgs += @('/f', (Resolve-Path $PfxPath).Path, '/p', $plain)
}
else {
    $signArgs += @('/sha1', ($Thumbprint -replace '\s', ''))
}

Write-Host "Signing $exe" -ForegroundColor Cyan
& $signTool @signArgs $exe
if ($LASTEXITCODE -ne 0) {
    throw "signtool failed (exit $LASTEXITCODE)."
}

$signature = Get-AuthenticodeSignature -FilePath $exe
Write-Host ("Signature: {0} - {1}" -f $signature.Status, $signature.SignerCertificate.Subject)

if ($signature.Status -ne 'Valid') {
    if ($AllowUntrusted) {
        Write-Warning @"
The signature is not trusted on this machine ($($signature.Status): $($signature.StatusMessage)).
-AllowUntrusted is set, so the signing step is accepted; do not ship a build in this state. Trust
the certificate, or sign with one from a public CA, before publishing the file.
"@
    }
    else {
        throw @"
The signature is not trusted on this machine ($($signature.Status): $($signature.StatusMessage)).
That is expected with a self-signed test certificate: trust it first, or sign with a certificate
from a trusted CA. Pass -AllowUntrusted only to prove the pipeline locally.
"@
    }
}

if ($Zip) {
    $dist = Join-Path (Resolve-Path (Join-Path $scriptRoot '..')).Path 'dist'
    New-Item -ItemType Directory -Force -Path $dist | Out-Null
    $zipPath = Join-Path $dist "Nectar-$Version-win-x64.zip"

    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

    Compress-Archive -Path (Join-Path $PublishDir '*') -DestinationPath $zipPath -CompressionLevel Optimal

    $hash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash
    Write-Host ''
    Write-Host "Package: $zipPath"
    Write-Host "SHA256:  $hash"
}
