<#
.SYNOPSIS
    Builds the easy installer: a self-contained publish folder, a portable zip, and Nectar-Setup.exe.

.DESCRIPTION
    The MSIX needs a trusted certificate before Windows will install it. This script produces the
    two artifacts that do not: a setup .exe (NSIS, per-user install, desktop and Start-menu
    shortcuts, uninstaller) and a portable zip (extract, run Nectar.exe).

    Both contain the same self-contained build: the .NET runtime and the Windows App SDK travel
    inside, so a target machine needs nothing pre-installed.

.PARAMETER MakensisPath
    Path to makensis.exe. Defaults to build\nsis\nsis-3.11\makensis.exe, then PATH.

.PARAMETER SkipZip
    Skip the portable zip (the setup exe is always built).

.PARAMETER SkipPublish
    Reuse build\installer\payload instead of publishing again — handy when only the .nsi changed.

.EXAMPLE
    .\scripts\package-installer.ps1
#>
[CmdletBinding()]
param(
    [string]$MakensisPath,
    [switch]$SkipZip,
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Nectar\Nectar.csproj'
$publish = Join-Path $root 'Nectar\bin\publish\win-x64'
$payload = Join-Path $root 'build\installer\payload'
$dist = Join-Path $root 'dist'
$nsi = Join-Path $root 'installer\nectar.nsi'

$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "Could not read <Version> from $project" }
Write-Output "building Nectar $version (self-contained publish, no certificate needed)"

if (-not $SkipPublish) {
    & dotnet publish $project -c Release -p:PublishProfile=win-x64
    if ($LASTEXITCODE -ne 0) { throw "publish failed with $LASTEXITCODE" }
    if (-not (Test-Path (Join-Path $publish 'Nectar.exe'))) {
        throw "Publish did not produce Nectar.exe under $publish"
    }

    # Stage a clean copy: the publish folder can hold leftovers from earlier configurations.
    if (Test-Path $payload) { Remove-Item $payload -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $payload | Out-Null
    Copy-Item "$publish\*" $payload -Recurse -Force
}
elseif (-not (Test-Path (Join-Path $payload 'Nectar.exe'))) {
    throw "-SkipPublish was used but $payload has no Nectar.exe"
}

$publishBytes = (Get-ChildItem $payload -Recurse -File | Measure-Object -Property Length -Sum).Sum
Write-Output ("payload   : {0:N1} MB in {1} files" -f ($publishBytes / 1MB), (Get-ChildItem $payload -Recurse -File).Count)

New-Item -ItemType Directory -Force -Path $dist | Out-Null

if (-not $SkipZip) {
    $zip = Join-Path $dist "Nectar-$version-win-x64.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path "$payload\*" -DestinationPath $zip -CompressionLevel Optimal
    Write-Output ("portable  : {0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
}

if (-not $MakensisPath) {
    $candidates = @(
        (Join-Path $root 'build\nsis\nsis-3.11\makensis.exe'),
        (Join-Path $root 'build\nsis-3.11\makensis.exe'),
        (Join-Path $root 'build\nsis\makensis.exe')
    )
    $MakensisPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $MakensisPath) {
        $onPath = Get-Command makensis.exe -ErrorAction SilentlyContinue
        if ($onPath) { $MakensisPath = $onPath.Source }
    }
}
if (-not $MakensisPath -or -not (Test-Path $MakensisPath)) {
    throw 'makensis.exe not found. Download the NSIS zip into build\nsis, or pass -MakensisPath.'
}

& $MakensisPath `
    "/DAPP_VERSION=$version" `
    "/DAPP_SOURCE=$payload" `
    "/DOUT_DIR=$dist" `
    "/DICON_FILE=$(Join-Path $root 'Nectar\Assets\nectar.ico')" `
    $nsi
if ($LASTEXITCODE -ne 0) { throw "makensis failed with $LASTEXITCODE" }

$setup = Join-Path $dist "Nectar-Setup-$version.exe"
if (-not (Test-Path $setup)) { throw "Expected $setup to exist" }

Write-Output ''
Write-Output ("setup     : {0}" -f $setup)
Write-Output ("size      : {0:N1} MB" -f ((Get-Item $setup).Length / 1MB))
Write-Output ("sha256    : {0}" -f (Get-FileHash $setup -Algorithm SHA256).Hash)
Write-Output ''
Write-Output "Installs per user to %LOCALAPPDATA%\Programs\Nectar — no administrator rights, no certificate."
