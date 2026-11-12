#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$PublishedFolder)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourceRoot = [IO.Path]::GetFullPath($PublishedFolder).TrimEnd([IO.Path]::DirectorySeparatorChar)
$resultRoot = Join-Path $repoRoot ('artifacts/update-install/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$targetRoot = [IO.Path]::GetFullPath((Join-Path $resultRoot 'app'))
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts/update-install')) + [IO.Path]::DirectorySeparatorChar
if (-not $targetRoot.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test target.' }
[void][IO.Directory]::CreateDirectory($resultRoot)

function Invoke-App {
    param([string]$Folder, [string]$Arguments, [int]$ExpectedExit = 0)
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = Join-Path $Folder 'NyangShinobi.App.exe'
    $start.Arguments = $Arguments
    $start.WorkingDirectory = $Folder
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $start
    try {
        [void]$process.Start()
        $processId = $process.Id
        $started = $process.StartTime.ToUniversalTime().Ticks
        $output = $process.StandardOutput.ReadToEndAsync()
        $errors = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(120000)) {
            $process.Kill() # Only the headless process created by this isolated test.
            $process.WaitForExit()
            throw 'Headless update test timed out.'
        }
        $stdout = $output.GetAwaiter().GetResult()
        $stderr = $errors.GetAwaiter().GetResult()
        if ($process.ExitCode -ne $ExpectedExit) {
            $errorFile = Join-Path $Folder 'helper-error.txt'
            if (Test-Path -LiteralPath $errorFile) { $stderr += [IO.File]::ReadAllText($errorFile) }
            throw "Unexpected update test exit $($process.ExitCode): $stderr"
        }
        return [pscustomobject]@{ Id = $processId; Started = $started; Output = $stdout; Error = $stderr }
    }
    finally { $process.Dispose() }
}

function Copy-Managed {
    param([string]$Source, [string]$Destination, [switch]$TopOnly)
    [void][IO.Directory]::CreateDirectory($Destination)
    $manifest = Get-Content -LiteralPath (Join-Path $Source 'install-manifest.json') -Encoding UTF8 -Raw | ConvertFrom-Json
    foreach ($file in $manifest.Files) {
        if ($TopOnly -and ($file.Path.Contains('/') -or $file.Path -notmatch '\.(dll|exe|json)$')) { continue }
        $relative = $file.Path.Replace('/', [IO.Path]::DirectorySeparatorChar)
        $sourceFile = [IO.Path]::GetFullPath((Join-Path $Source $relative))
        $destinationFile = [IO.Path]::GetFullPath((Join-Path $Destination $relative))
        if (-not $sourceFile.StartsWith($Source + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            -not $destinationFile.StartsWith($Destination + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe managed path.' }
        [void][IO.Directory]::CreateDirectory((Split-Path -Parent $destinationFile))
        [IO.File]::Copy($sourceFile, $destinationFile, $false)
    }
    Copy-Item -LiteralPath (Join-Path $Source 'install-manifest.json') -Destination (Join-Path $Destination 'install-manifest.json')
}

function Set-FixtureVersion {
    param([string]$Folder, [string]$Version, [switch]$BreakExe)
    $metadataFile = Join-Path $Folder 'version.json'
    $metadata = Get-Content -LiteralPath $metadataFile -Encoding UTF8 -Raw | ConvertFrom-Json
    $metadata.Version = $Version; $metadata.ReleaseTag = 'v' + $Version
    $metadata | ConvertTo-Json | Set-Content -LiteralPath $metadataFile -Encoding UTF8
    if ($BreakExe) { [IO.File]::WriteAllText((Join-Path $Folder 'NyangShinobi.App.exe'), 'invalid executable for rollback test') }
    $manifestFile = Join-Path $Folder 'install-manifest.json'
    $manifest = Get-Content -LiteralPath $manifestFile -Encoding UTF8 -Raw | ConvertFrom-Json
    foreach ($file in $manifest.Files) {
        $path = Join-Path $Folder $file.Path
        $file.Length = (Get-Item -LiteralPath $path).Length
        $file.Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestFile -Encoding UTF8
}

function New-FixturePlan {
    param([string]$Version, [switch]$BreakExe)
    $parent = Invoke-App $targetRoot '--verify-portable'
    $nonce = [Guid]::NewGuid().ToString('N')
    $transaction = Join-Path (Join-Path $targetRoot '.updates') $nonce
    $package = Join-Path $transaction 'package'
    $helper = Join-Path $transaction 'runner'
    Copy-Managed $sourceRoot $package
    Set-FixtureVersion $package $Version -BreakExe:$BreakExe
    Copy-Managed $targetRoot $helper -TopOnly
    $plan = [ordered]@{ SchemaVersion = 1; TargetRoot = $targetRoot; TransactionRoot = $transaction; ExpectedVersion = $Version;
        ParentId = $parent.Id; ParentStartTicks = $parent.Started; Nonce = $nonce; VerifyRestartOnly = $true }
    $planPath = Join-Path $transaction 'plan.json'
    $plan | ConvertTo-Json | Set-Content -LiteralPath $planPath -Encoding UTF8
    return [pscustomobject]@{ Path = $planPath; Transaction = $transaction; Helper = $helper }
}

# Verify the original published files before copying anything. The source is never replaced.
[void](Invoke-App $sourceRoot '--verify-portable')
Copy-Managed $sourceRoot $targetRoot
Set-FixtureVersion $targetRoot '0.1.0-rc.9001'
[IO.File]::WriteAllText((Join-Path $targetRoot 'settings.v0.1.json'), 'personal settings preserved')
[void][IO.Directory]::CreateDirectory((Join-Path $targetRoot 'screenshots'))
[IO.File]::WriteAllText((Join-Path $targetRoot 'screenshots/personal.txt'), 'personal capture preserved')

$success = New-FixturePlan '0.1.0-rc.9002'
[void](Invoke-App $success.Helper ('--update-helper "' + $success.Path + '"'))
$successResult = Get-Content -LiteralPath (Join-Path $success.Transaction 'result.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if (-not $successResult.Success) { throw "Install/restart failed: $($successResult.Message)" }
$installed = Get-Content -LiteralPath (Join-Path $targetRoot 'version.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if ($installed.Version -ne '0.1.0-rc.9002') { throw 'Installed metadata version mismatch.' }

$failure = New-FixturePlan '0.1.0-rc.9003' -BreakExe
[void](Invoke-App $failure.Helper ('--update-helper "' + $failure.Path + '"') -ExpectedExit 1)
$failureResult = Get-Content -LiteralPath (Join-Path $failure.Transaction 'result.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if ($failureResult.Success -or -not $failureResult.RolledBack) { throw "Restart failure did not roll back: $($failureResult.Message)" }
$verification = (Invoke-App $targetRoot '--verify-portable').Output | ConvertFrom-Json
if ($verification.Version -ne '0.1.0-rc.9002') { throw 'Rollback did not restore the previous version.' }
if ([IO.File]::ReadAllText((Join-Path $targetRoot 'settings.v0.1.json')) -ne 'personal settings preserved' -or
    [IO.File]::ReadAllText((Join-Path $targetRoot 'screenshots/personal.txt')) -ne 'personal capture preserved') { throw 'Personal data changed.' }

$report = [ordered]@{ Success = $true; RealHelperProcess = $true; HeadlessRestartVerified = $true;
    InvalidExeRollbackVerified = $true; PersonalDataPreserved = $true; SourceFolder = $sourceRoot; TargetFolder = $targetRoot;
    SuccessTransaction = $success.Transaction; RollbackTransaction = $failure.Transaction }
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $resultRoot 'report.json') -Encoding UTF8
Write-Output "PASS: real portable helper install/restart, failed EXE rollback, personal data preservation. Report: $resultRoot/report.json"
