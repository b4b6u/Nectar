param(
    [string]$Exe = "D:\canopy\Nectar\bin\Debug\net8.0-windows10.0.19041.0\win-x64\Nectar.exe"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NativeKeys {
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

function Send-AltN {
    [NativeKeys]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 150
    [NativeKeys]::keybd_event(0x4E, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 90
    [NativeKeys]::keybd_event(0x4E, 0, 2, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [NativeKeys]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
}

function Find-Button($scope, [string]$name) {
    $nameCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $typeCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $condition = New-Object System.Windows.Automation.AndCondition($nameCondition, $typeCondition)
    return $scope.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Invoke-Button($scope, [string]$name) {
    $button = Find-Button $scope $name
    if ($null -eq $button) {
        Write-Output "MISSING button: $name"
        return $false
    }
    $pattern = $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
    Write-Output "CLICKED button: $name"
    return $true
}

$log = Join-Path $env:LOCALAPPDATA "Nectar\nectar.log"
$before = if (Test-Path $log) { (Get-Item $log).Length } else { 0 }

Get-Process Nectar -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$process = Start-Process -FilePath $Exe -PassThru
Start-Sleep -Seconds 4
Write-Output "started pid=$($process.Id)"

Send-AltN
Start-Sleep -Seconds 2

$root = [System.Windows.Automation.AutomationElement]::RootElement
$pidCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)
$window = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $pidCondition)

if ($null -eq $window) {
    Write-Output "NO top-level window for pid; enumerating root:"
    foreach ($child in $root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)) {
        if ($child.Current.ProcessId -eq $process.Id) {
            Write-Output ("  window: '{0}' class='{1}'" -f $child.Current.Name, $child.Current.ClassName)
        }
    }
    Write-Output "LOG AFTER OPEN:"
    if (Test-Path $log) { Get-Content $log | Select-Object -Skip 0 | Select-Object -Last 20 }
    Stop-Process -Id $process.Id -Force
    exit 1
}

Write-Output ("WINDOW: '{0}' class='{1}'" -f $window.Current.Name, $window.Current.ClassName)

# --- Settings screen: statuses and buttons -----------------------------------------------
Invoke-Button $window "Customize" | Out-Null
Start-Sleep -Seconds 2

$textCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::Text)
$texts = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $textCondition)
Write-Output "SETTINGS TEXT:"
foreach ($text in $texts) {
    $name = $text.Current.Name
    if ($name -match "Microphone|Database|Encrypted|Plaintext|Allowed|Off for desktop|Blocked|Win \+ H|Checking") {
        Write-Output "  text: $name"
    }
}

Write-Output "SETTINGS BUTTONS:"
foreach ($name in @("Open microphone privacy settings", "Show the Nectar data folder")) {
    $button = Find-Button $window $name
    if ($null -eq $button) { Write-Output "  MISSING: $name" } else { Write-Output "  present: $name" }
}

# --- Dictation: press the mic chip and observe ------------------------------------------
Invoke-Button $window "Workspace" | Out-Null
Start-Sleep -Seconds 1
Invoke-Button $window "Dictate a task" | Out-Null
Start-Sleep -Seconds 8

Write-Output "NEW LOG LINES:"
if (Test-Path $log) {
    $stream = [System.IO.File]::Open($log, "Open", "Read", "ReadWrite")
    try {
        $stream.Position = [Math]::Min($before, $stream.Length)
        $reader = New-Object System.IO.StreamReader($stream)
        $reader.ReadToEnd()
    } finally {
        $stream.Dispose()
    }
}

Write-Output "PROMPT WINDOWS:"
foreach ($child in $root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)) {
    if ($child.Current.Name -match "microphone|voice|dictat|privacy|Nectar") {
        Write-Output ("  '{0}' pid={1} class='{2}'" -f $child.Current.Name, $child.Current.ProcessId, $child.Current.ClassName)
    }
}

# --- Capture banner text if the fallback ran --------------------------------------------
$texts = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $textCondition)
$banner = $texts | ForEach-Object { $_.Current.Name } | Where-Object { $_ -match "Win \+ H|organisation|Privacy" }
if ($banner) {
    Write-Output "BANNER:"
    $banner | ForEach-Object { Write-Output "  $_" }
} else {
    Write-Output "BANNER: (none found)"
}

Stop-Process -Id $process.Id -Force
Write-Output "app stopped"
