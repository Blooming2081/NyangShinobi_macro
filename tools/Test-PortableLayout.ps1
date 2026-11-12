param(
    [Parameter(Mandatory = $true)][string]$PublishedFolder,
    [Parameter(Mandatory = $true)][string]$Report,
    [switch]$VerifyGui
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$portableRoot = (Resolve-Path -LiteralPath $PublishedFolder).Path
$packageFolder = Join-Path $portableRoot 'app'
$launcher = Join-Path $portableRoot '냥냥시노비 실행.cmd'
$expectedNames = @('app', '냥냥시노비 실행.cmd', '사용 설명서.txt')
if (@(Get-ChildItem -LiteralPath $portableRoot -Force).Count -ne 3 -or
    @(Get-ChildItem -LiteralPath $portableRoot -Force | Where-Object { $_.Name -notin $expectedNames }).Count -ne 0) {
    throw 'Portable root must contain only the launcher, guide and app directory.'
}
if (-not (Test-Path -LiteralPath $packageFolder -PathType Container)) { throw 'Missing app directory.' }
$launchText = [IO.File]::ReadAllText($launcher)
if ($launchText -notmatch [regex]::Escape('%~dp0app\NyangShinobi.App.exe') -or
    $launchText -match '[A-Za-z]:\\|%\*|powershell|runas') { throw 'Launcher must use its own relative path, without elevation or forwarded shell arguments.' }
$metadata = Get-Content -LiteralPath (Join-Path $packageFolder 'version.json') -Encoding UTF8 -Raw | ConvertFrom-Json
$manifest = Get-Content -LiteralPath (Join-Path $packageFolder 'install-manifest.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if ($metadata.SchemaVersion -ne 2 -or $manifest.SchemaVersion -ne 2 -or
    'app/NyangShinobi.App.exe' -notin $manifest.Files.Path -or
    '냥냥시노비 실행.cmd' -notin $manifest.Files.Path -or '사용 설명서.txt' -notin $manifest.Files.Path) { throw 'Invalid v2 portable manifest.' }
$reportPath = [IO.Path]::GetFullPath($Report)
if ($reportPath.StartsWith($portableRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Verification report must stay outside the managed package.' }
$result = [ordered]@{ PortableRoot = $portableRoot; Schema = 2; RootEntries = 3; RelativeLauncher = $true; WpfGuiVerified = $false }
if (-not $VerifyGui) {
    $result | ConvertTo-Json | Set-Content -LiteralPath $reportPath -Encoding UTF8
    Write-Output 'PASS: portable app/ layout and relative launcher (GUI not requested).'
    return
}
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class PortableTestWindows {
    private delegate bool Callback(IntPtr window, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Callback callback, IntPtr state);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
    public static IntPtr Find(int processId) {
        IntPtr result = IntPtr.Zero;
        EnumWindows((window, state) => {
            uint owner; GetWindowThreadProcessId(window, out owner);
            if (owner != processId) return true;
            var title = new StringBuilder(512); GetWindowText(window, title, title.Capacity);
            if (!title.ToString().EndsWith(" v0.1", StringComparison.Ordinal)) return true;
            result = window; return false;
        }, IntPtr.Zero);
        return result;
    }
}
'@
$exe = Join-Path $packageFolder 'NyangShinobi.App.exe'
function Find-PackageProcess {
    foreach ($candidate in [Diagnostics.Process]::GetProcessesByName('NyangShinobi.App')) {
        try {
            $module = $candidate.MainModule
            if ($null -ne $module -and [string]::Equals($module.FileName, $exe, [StringComparison]::OrdinalIgnoreCase)) { return $candidate }
        }
        catch [ComponentModel.Win32Exception] { }
        catch [InvalidOperationException] { }
        $candidate.Dispose()
    }
    return $null
}
if ($null -ne (Find-PackageProcess)) { throw 'This package already has an active process; refusing to interfere with it.' }
$launchTime = [DateTime]::UtcNow
# /d disables cmd AutoRun; quotes protect spaces, &, parentheses and Korean paths.
$shell = Start-Process -FilePath $env:ComSpec -ArgumentList ('/d /v:off /s /c ""' + $launcher + '""') -WorkingDirectory ([IO.Path]::GetTempPath()) -WindowStyle Hidden -PassThru
$process = $null
$window = $null
try {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $windowHandle = [IntPtr]::Zero
    while ($timer.Elapsed.TotalSeconds -lt 20 -and $windowHandle -eq [IntPtr]::Zero) {
        if ($null -eq $process) { $process = Find-PackageProcess }
        if ($null -ne $process) {
            if ($process.StartTime.ToUniversalTime() -lt $launchTime -or $process.HasExited) { throw 'Launcher did not create a new live package process.' }
            $windowHandle = [PortableTestWindows]::Find($process.Id)
        }
        if ($windowHandle -eq [IntPtr]::Zero) { Start-Sleep -Milliseconds 100 }
    }
    if ($windowHandle -eq [IntPtr]::Zero) { throw 'Published WPF window was not created.' }
    $window = [Windows.Automation.AutomationElement]::FromHandle($windowHandle)
    function Find-Id([string]$id) {
        # SelectionItem.Select returns before WPF has necessarily materialized the tab content.
        $ready = [Diagnostics.Stopwatch]::StartNew()
        while ($ready.Elapsed.TotalSeconds -lt 10) {
            if ($process.HasExited) { throw "WPF process exited before control became ready: $id" }
            $element = $window.FindFirst([Windows.Automation.TreeScope]::Descendants,
                (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::AutomationIdProperty, $id)))
            if ($null -ne $element) { return $element }
            Start-Sleep -Milliseconds 50
        }
        throw "WPF control readiness timed out: $id"
    }
    # The existing startup update check selects UpdatesTab when it finds a release.
    # Observe its bounded completion before selecting diagnostics; do not change app behavior.
    (Find-Id 'UpdatesTab').GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $check = Find-Id 'CheckUpdate'
    $startup = [Diagnostics.Stopwatch]::StartNew()
    $startupDone = $false
    while ($startup.Elapsed.TotalSeconds -lt 45 -and -not $startupDone) {
        $messages = $window.FindAll([Windows.Automation.TreeScope]::Descendants,
            (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Text)))
        $finishedMessage = @($messages | Where-Object { $_.Current.Name -match '^(새 버전이 있습니다\.|선택한 채널에|업데이트 확인 (실패|취소))' }).Count -gt 0
        $startupDone = $finishedMessage -and $check.Current.IsEnabled
        if (-not $startupDone) { Start-Sleep -Milliseconds 50 }
    }
    if (-not $startupDone) { throw 'Startup update check did not reach a completed UI state within its budget.' }
    $tab = Find-Id 'DiagnosticsTab'
    if ($null -eq $tab) { throw 'Diagnostics tab missing from published WPF application.' }
    $tab.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $adbPath = (Find-Id 'DiagnosticAdbPath').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.Value
    if (-not $adbPath.Equals((Join-Path $packageFolder 'adb.exe'), [StringComparison]::OrdinalIgnoreCase)) { throw "Bundled ADB path mismatch: $adbPath" }
    $tabControls = $window.FindAll([Windows.Automation.TreeScope]::Descendants,
        (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Tab)))
    if ($tabControls.Count -ne 1) { throw 'Diagnostic content must contain a single preview without nested tabs.' }
    $assetSelector = Find-Id 'DiagnosticAssetSelector'
    if ($null -eq $assetSelector) { throw 'Diagnostic asset selector missing.' }
    $assetsButton = [Windows.Automation.TreeWalker]::ControlViewWalker.GetPreviousSibling($assetSelector)
    if ($assetsButton.Current.ControlType -ne [Windows.Automation.ControlType]::Button) { throw 'Asset loading button not found.' }
    $assetsButton.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
    $timer.Restart()
    $statusText = $null
    while ($timer.Elapsed.TotalSeconds -lt 20 -and $null -eq $statusText) {
        $texts = $window.FindAll([Windows.Automation.TreeScope]::Descendants,
            (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Text)))
        foreach ($element in $texts) {
            if ($element.Current.Name -match '(^|\D)79(\D|$)') { $statusText = $element.Current.Name; break }
        }
        if ($null -eq $statusText) { Start-Sleep -Milliseconds 100 }
    }
    if ($null -eq $statusText) { throw 'Published diagnostic UI did not load 79 asset templates.' }
    $devices = Find-Id 'DiagnosticDeviceSelector'
    $selected = $devices.GetCurrentPattern([Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()
    if ($selected.Count -ne 0) { throw 'A diagnostic device was unexpectedly selected.' }
    if ((Find-Id 'DiagnosticCapture').Current.IsEnabled) { throw 'Capture must remain disabled without explicit device selection.' }
    $process.Refresh()
    foreach ($moduleName in @('coreclr.dll', 'OpenCvSharpExtern.dll')) {
        $modules = @($process.Modules | Where-Object { $_.ModuleName -eq $moduleName })
        if ($modules.Count -ne 1 -or -not $modules[0].FileName.StartsWith($packageFolder + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Native module was not loaded from package: $moduleName"
        }
    }
    $result.WpfGuiVerified = $true
    $result.WpfWindow = $window.Current.Name; $result.DiagnosticsTab = $true
    $result.AssetTemplates = 79; $result.DeviceSelected = $false; $result.LocalNativeModules = $true; $result.BundledAdbPath = $adbPath
    $result | ConvertTo-Json | Set-Content -LiteralPath $reportPath -Encoding UTF8
    $result | ConvertTo-Json
}
catch {
    if ($null -ne $window) {
        $tree = @($window.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition) | ForEach-Object {
            [pscustomobject]@{ Id = $_.Current.AutomationId; Name = $_.Current.Name; Type = $_.Current.ControlType.ProgrammaticName; Offscreen = $_.Current.IsOffscreen }
        })
        $tree | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath ($reportPath + '.failure.json') -Encoding UTF8
    }
    throw
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        if ($null -ne $window) { $window.GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern).Close() }
        else { [void]$process.CloseMainWindow() }
        if (-not $process.WaitForExit(10000)) { $process.Kill(); $process.WaitForExit() }
    }
    if ($null -ne $process) { $process.Dispose() }
    $shell.Dispose()
}
