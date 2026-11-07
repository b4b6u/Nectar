$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Keys2 {
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
}
"@

function Send-AltN {
    [Keys2]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 150
    [Keys2]::keybd_event(0x4E, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 90
    [Keys2]::keybd_event(0x4E, 0, 2, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [Keys2]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
}

$exe = "D:\canopy\Nectar\bin\Debug\net8.0-windows10.0.19041.0\win-x64\Nectar.exe"
$log = Join-Path $env:LOCALAPPDATA "Nectar\nectar.log"
Get-Process Nectar -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 4
Send-AltN
Start-Sleep -Seconds 2

$root = [System.Windows.Automation.AutomationElement]::RootElement
$pc = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
$windows = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $pc)
Write-Output "windows for pid: $($windows.Count)"
foreach ($w in $windows) {
    $r = $w.Current.BoundingRectangle
    Write-Output ("  window '{0}' class='{1}' rect={2},{3} {4}x{5} offscreen={6}" -f `
        $w.Current.Name, $w.Current.ClassName, $r.X, $r.Y, $r.Width, $r.Height, $w.Current.IsOffscreen)
}

$window = if ($windows.Count -gt 0) { $windows[0] } else { $null }
if ($null -eq $window) {
    Get-Content $log -Tail 15
    Stop-Process -Id $p.Id -Force
    exit 1
}

$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
function Dump($element, $depth) {
    if ($null -eq $element -or $depth -gt 4) { return }
    $indent = "  " * $depth
    Write-Output ("{0}{1} '{2}' auto='{3}'" -f $indent, $element.Current.ControlType.ProgrammaticName, $element.Current.Name, $element.Current.AutomationId)
    $child = $walker.GetFirstChild($element)
    while ($null -ne $child) {
        Dump $child ($depth + 1)
        $child = $walker.GetNextSibling($child)
    }
}

Write-Output "TREE:"
Dump $window 0

$buttons = $window.FindAll(
    [System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)))
Write-Output "BUTTONS: $($buttons.Count)"
foreach ($b in $buttons) { Write-Output "  button: '$($b.Current.Name)'" }

Write-Output "LOG (last 25):"
Get-Content $log -Tail 25

Stop-Process -Id $p.Id -Force
