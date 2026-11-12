#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$DotnetPath,
    [string]$PackageVersion,
    [string]$ReleaseTag
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($DotnetPath)) {
    $localDotnet = Join-Path $repoRoot '.tools/dotnet/dotnet.exe'
    if (Test-Path -LiteralPath $localDotnet -PathType Leaf) { $DotnetPath = $localDotnet }
    else { $DotnetPath = (Get-Command dotnet -ErrorAction Stop).Source }
}
$dotnetExecutable = (Get-Command $DotnetPath -ErrorAction Stop).Source
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.tools/cli'
$env:NUGET_PACKAGES = Join-Path $repoRoot '.tools/nuget'
$env:DOTNET_ROOT = Split-Path -Parent $dotnetExecutable
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:MSBUILDDISABLENODEREUSE = '1'

function Invoke-Dotnet {
    param([string[]]$CommandArguments)
    & $dotnetExecutable @CommandArguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet command failed (exit $LASTEXITCODE): $CommandArguments" }
}

function Get-NativeImports {
    param([string]$Path, [switch]$AllowX86)
    $peBytes = [IO.File]::ReadAllBytes($Path)
    $peOffset = [BitConverter]::ToInt32($peBytes, 0x3c)
    if ([BitConverter]::ToUInt32($peBytes, $peOffset) -ne 0x4550) { throw "Invalid PE: $Path" }
    $machine = [BitConverter]::ToUInt16($peBytes, $peOffset + 4)
    if ($machine -ne 0x8664 -and -not ($AllowX86 -and $machine -eq 0x14c)) { throw "Unexpected PE architecture: $Path" }
    $optional = $peOffset + 24
    $directory = $optional + 112 # PE32+ (x64); Google's Windows ADB is PE32 (x86).
    if ($machine -eq 0x14c) { $directory = $optional + 96 }
    $sectionStart = $optional + [BitConverter]::ToUInt16($peBytes, $peOffset + 20)
    $sectionCount = [BitConverter]::ToUInt16($peBytes, $peOffset + 6)
    function Convert-Rva {
        param([uint32]$Rva)
        for ($sectionIndex = 0; $sectionIndex -lt $sectionCount; $sectionIndex++) {
            $section = $sectionStart + $sectionIndex * 40
            $virtualSize = [BitConverter]::ToUInt32($peBytes, $section + 8)
            $virtualAddress = [BitConverter]::ToUInt32($peBytes, $section + 12)
            $rawSize = [BitConverter]::ToUInt32($peBytes, $section + 16)
            $rawOffset = [BitConverter]::ToUInt32($peBytes, $section + 20)
            if ($Rva -ge $virtualAddress -and $Rva -lt $virtualAddress + [Math]::Max($virtualSize, $rawSize)) {
                return [int]($rawOffset + $Rva - $virtualAddress)
            }
        }
        return [int]$Rva
    }
    $importNames = New-Object 'System.Collections.Generic.List[string]'
    foreach ($entry in @(@{ Index = 1; Size = 20; NameOffset = 12 }, @{ Index = 13; Size = 32; NameOffset = 4 })) {
        $importRva = [BitConverter]::ToUInt32($peBytes, $directory + $entry.Index * 8)
        if ($importRva -eq 0) { continue }
        $descriptor = Convert-Rva $importRva
        while ([BitConverter]::ToUInt32($peBytes, $descriptor + $entry.NameOffset) -ne 0) {
            if ($entry.Index -eq 13 -and ([BitConverter]::ToUInt32($peBytes, $descriptor) -band 1) -eq 0) {
                throw "Unsupported VA-based delay import: $Path"
            }
            $nameOffset = Convert-Rva ([BitConverter]::ToUInt32($peBytes, $descriptor + $entry.NameOffset))
            $nameEnd = $nameOffset
            while ($peBytes[$nameEnd] -ne 0) { $nameEnd++ }
            $importNames.Add([Text.Encoding]::ASCII.GetString($peBytes, $nameOffset, $nameEnd - $nameOffset))
            $descriptor += $entry.Size
        }
    }
    return $importNames.ToArray()
}

function Test-PortableExe {
    param([string]$Folder, [string]$Report)
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = Join-Path $Folder 'NyangShinobi.App.exe'
    $start.Arguments = '--verify-portable'
    $start.WorkingDirectory = $repoRoot # Deliberately different from the publish folder.
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $start
    try {
        [void]$process.Start()
        $standardOutput = $process.StandardOutput.ReadToEndAsync()
        $standardError = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(45000)) {
            $process.Kill() # Only this self-check process; never an emulator or shared ADB server.
            $process.WaitForExit()
            throw 'Portable self-check timed out.'
        }
        $resultText = $standardOutput.GetAwaiter().GetResult()
        $errorText = $standardError.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "Portable self-check failed: $errorText" }
        $result = $resultText | ConvertFrom-Json
        if (-not $result.LocalRuntime -or $result.Architecture -ne 'X64' -or $result.Templates -le 0 -or $result.MatchScore -lt 0.999 -or $result.Version -ne $publishedVersion) {
            throw "Invalid portable self-check report: $resultText"
        }
        $resultText | Set-Content -LiteralPath $Report -Encoding UTF8
        return $result
    }
    finally { $process.Dispose() }
}

function Test-BundledAdb {
    param([string]$Folder, [string]$Report)
    $adbManifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'dependencies/platform-tools.windows.json') -Encoding UTF8 -Raw | ConvertFrom-Json
    foreach ($file in $adbManifest.files) {
        if ((Get-FileHash -LiteralPath (Join-Path $Folder $file.destination) -Algorithm SHA256).Hash -ne $file.sha256) {
            throw "Bundled ADB file mismatch: $($file.destination)"
        }
    }
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = Join-Path $Folder 'adb.exe'
    $start.Arguments = 'version' # Offline only: never starts/contacts the shared ADB server.
    $start.WorkingDirectory = $repoRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $start
    try {
        [void]$process.Start()
        $output = $process.StandardOutput.ReadToEndAsync()
        $errors = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(15000)) {
            $process.Kill()
            $process.WaitForExit()
            throw 'Bundled ADB version check timed out.'
        }
        $versionOutput = $output.GetAwaiter().GetResult()
        $errorOutput = $errors.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0 -or $versionOutput -notmatch ('(?m)^Version ' + [regex]::Escape($adbManifest.version) + '[-\s]')) {
            throw "Bundled ADB version check failed: $versionOutput $errorOutput"
        }
        $versionOutput | Set-Content -LiteralPath $Report -Encoding UTF8
    }
    finally { $process.Dispose() }
}

Push-Location -LiteralPath $repoRoot
try {
    [xml]$buildProperties = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props')
    $version = [string]$buildProperties.Project.PropertyGroup.Version
    if ($ReleaseTag -and $PackageVersion) { throw 'Use either ReleaseTag or PackageVersion, not both.' }
    if ($ReleaseTag) {
        if ($ReleaseTag -cnotmatch '^v(\d+\.\d+(?:\.\d+)?)(-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?\z') {
            throw 'ReleaseTag must look like v0.1, v0.1.0 or v0.1.0-rc.1.'
        }
        $tagCore = $Matches[1]
        if ($tagCore.Split('.').Count -eq 2) { $tagCore += '.0' }
        if ($tagCore -ne $version) { throw "Tag version $tagCore differs from Directory.Build.props version $version." }
        $PackageVersion = $ReleaseTag.Substring(1)
    }
    elseif (-not $PackageVersion) { $PackageVersion = $version }
    if ($PackageVersion -cnotmatch '\A\d+\.\d+(?:\.\d+)?(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?\z') {
        throw 'Invalid PackageVersion: only a numeric version and optional prerelease label are allowed.'
    }
    $packageName = "NyangShinobi-v$PackageVersion-win-x64"
    $publishedVersion = $PackageVersion
    if ($publishedVersion -match '^(\d+\.\d+)(-.*)?$') { $publishedVersion = $Matches[1] + '.0' + $Matches[2] }
    if ($publishedVersion -cnotmatch '\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\z') {
        throw 'Package version must be valid SemVer.'
    }
    $pre = $Matches[4]
    if ($pre -and @($pre.Split('.') | Where-Object { $_ -match '^0[0-9]+$' }).Count -gt 0) { throw 'Numeric prerelease identifiers cannot have leading zeroes.' }
    # Each run uses a fresh directory: no recursive delete and no stale publish files.
    $runRoot = Join-Path $repoRoot ('artifacts/portable/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    $packageFolder = Join-Path $runRoot $packageName
    $publishFolder = Join-Path $packageFolder 'app'
    [void](New-Item -ItemType Directory -Path $publishFolder)
    Invoke-Dotnet -CommandArguments @('restore', 'NyangShinobi.slnx', '--configfile', 'NuGet.Config', '-m:1')
    Invoke-Dotnet -CommandArguments @('build', 'NyangShinobi.slnx', '-c', 'Release', '--no-restore', '-m:1')
    Invoke-Dotnet -CommandArguments @('test', 'NyangShinobi.slnx', '-c', 'Release', '--no-build', '--no-restore', '-m:1', '--logger', 'trx', '--results-directory', (Join-Path $runRoot 'tests'))
    Invoke-Dotnet -CommandArguments @('publish', 'src/NyangShinobi.App/NyangShinobi.App.csproj', '-c', 'Release', '-r', 'win-x64',
        '--self-contained', 'true', '-p:PublishProfile=PortableWinX64', '-o', $publishFolder, '-m:1')
    & (Join-Path $PSScriptRoot 'Copy-PortableAdb.ps1') -Destination $publishFolder
    $sourceCommit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot determine source commit.' }
    [ordered]@{ SchemaVersion = 2; AppId = 'NyangShinobi'; Version = $publishedVersion; Runtime = 'win-x64'; Commit = $sourceCommit; ReleaseTag = $(if ($ReleaseTag) { $ReleaseTag } else { $null }) } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $publishFolder 'version.json') -Encoding UTF8

    $required = @('NyangShinobi.App.exe', 'NyangShinobi.App.dll', 'NyangShinobi.App.runtimeconfig.json',
        'hostfxr.dll', 'hostpolicy.dll', 'coreclr.dll', 'System.Private.CoreLib.dll',
        'PresentationFramework.dll', 'PresentationCore.dll', 'WindowsBase.dll', 'OpenCvSharp.dll',
        'OpenCvSharpExtern.dll', 'opencv_videoio_ffmpeg500_64.dll',
        'data/settings.default.v0.1.json', 'data/asset_manifest.v0.1.json', 'data/screen_catalog.v0.1.json')
    foreach ($relative in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $publishFolder $relative) -PathType Leaf)) { throw "Missing published file: $relative" }
    }
    foreach ($relative in @('data/settings.default.v0.1.json', 'data/asset_manifest.v0.1.json', 'data/screen_catalog.v0.1.json')) {
        if ((Get-FileHash -LiteralPath (Join-Path $repoRoot $relative)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $publishFolder $relative)).Hash) {
            throw "Published JSON mismatch: $relative"
        }
    }
    $manifest = Get-Content -LiteralPath (Join-Path $publishFolder 'data/asset_manifest.v0.1.json') -Encoding UTF8 -Raw | ConvertFrom-Json
    foreach ($asset in $manifest.assets) {
        if (-not $asset.included) { continue }
        $assetPath = Join-Path $publishFolder $asset.path
        if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) { throw "Missing asset: $($asset.path)" }
        if ((Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash -ne $asset.sha256) { throw "Asset hash mismatch: $($asset.path)" }
    }
    $nativeReport = foreach ($nativeName in @('OpenCvSharpExtern.dll', 'opencv_videoio_ffmpeg500_64.dll', 'adb.exe', 'AdbWinApi.dll', 'AdbWinUsbApi.dll')) {
        $nativeImports = @(Get-NativeImports (Join-Path $publishFolder $nativeName) -AllowX86:($nativeName -in @('adb.exe', 'AdbWinApi.dll', 'AdbWinUsbApi.dll')))
        $externalVc = @($nativeImports | Where-Object { $_ -match '^(msvcp\d|vcruntime\d|concrt\d)' -and -not (Test-Path -LiteralPath (Join-Path $publishFolder $_)) })
        if ($externalVc.Count -ne 0) { throw "Unbundled VC++ runtime dependency in ${nativeName}: $externalVc" }
        [pscustomobject]@{ File = $nativeName; Imports = $nativeImports; ExternalVcRuntime = $externalVc }
    }
    $nativeReport | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runRoot 'native-dependencies.json') -Encoding UTF8
    [void][IO.Directory]::CreateDirectory((Join-Path $publishFolder 'docs'))
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/PORTABLE_DISTRIBUTION.md') -Destination (Join-Path $publishFolder 'docs/PORTABLE_README.md')
    # No absolute shortcut target, PowerShell execution-policy requirement or second runtime.
    [IO.File]::WriteAllText((Join-Path $packageFolder '냥냥시노비 실행.cmd'), "@echo off`r`nsetlocal DisableDelayedExpansion`r`nif not exist `"%~dp0app\NyangShinobi.App.exe`" exit /b 1`r`nstart `"`" /D `"%~dp0app`" `"%~dp0app\NyangShinobi.App.exe`"`r`n", [Text.Encoding]::ASCII)
    $guide = @'
냥냥시노비 Windows x64 포터블

1. ZIP 전체를 새 폴더에 압축 해제하세요.
2. '냥냥시노비 실행.cmd'를 더블 클릭하세요. 잠깐 명령 창이 보일 수 있습니다.
   직접 실행하려면 app\NyangShinobi.App.exe를 사용하세요.
3. app 폴더를 포함한 전체 폴더를 함께 이동하세요. .NET/ADB 별도 설치는 필요 없습니다.
4. 앱플레이어는 직접 실행하고 개발자 진단 탭에서 ADB 장치를 직접 선택하세요.
5. 업데이트 탭: Stable은 정식만, Pre-release (테스트)는 RC와 정식을 확인합니다.
6. 구형 평면 배포에 덮어쓰지 마세요. 구형→새 구조 자동 전환은 지원하지 않습니다.
   사용자 설정은 %LocalAppData%\NyangShinobi에 유지됩니다.
   기존 폴더의 개인 스크린샷/추가 파일은 별도로 보관하세요.

상세 안내: app\docs\PORTABLE_README.md
앱/ADB/게임의 자동 복구·재시작과 Task 03 게임 진행은 포함하지 않습니다.
'@
    [IO.File]::WriteAllText((Join-Path $packageFolder '사용 설명서.txt'), $guide, (New-Object Text.UTF8Encoding($true)))
    $managedFiles = @(Get-ChildItem -LiteralPath $packageFolder -Recurse -File | Sort-Object FullName | ForEach-Object {
        [ordered]@{ Path = $_.FullName.Substring($packageFolder.Length + 1).Replace('\', '/'); Length = $_.Length; Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    [ordered]@{ SchemaVersion = 2; AppId = 'NyangShinobi'; Files = $managedFiles } | ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath (Join-Path $publishFolder 'install-manifest.json') -Encoding UTF8
    [void](Test-PortableExe $publishFolder (Join-Path $runRoot 'portable-check.json'))
    Test-BundledAdb $publishFolder (Join-Path $runRoot 'adb-check.txt')

    $zipPath = Join-Path $runRoot ($packageName + '.zip')
    Compress-Archive -LiteralPath $packageFolder -DestinationPath $zipPath -CompressionLevel Optimal
    $relocated = Join-Path $runRoot '다른 경로 & ZIP 이동 (검증)!'
    [void][IO.Directory]::CreateDirectory($relocated)
    $relocatedZip = Join-Path $relocated ($packageName + '.zip')
    Copy-Item -LiteralPath $zipPath -Destination $relocatedZip
    $unpackedRoot = Join-Path $relocated '압축 해제'
    Expand-Archive -LiteralPath $relocatedZip -DestinationPath $unpackedRoot
    [void](Test-PortableExe (Join-Path (Join-Path $unpackedRoot $packageName) 'app') (Join-Path $runRoot 'zip-check.json'))
    Test-BundledAdb (Join-Path (Join-Path $unpackedRoot $packageName) 'app') (Join-Path $runRoot 'zip-adb-check.txt')
    & (Join-Path $PSScriptRoot 'Test-PortableLayout.ps1') -PublishedFolder $packageFolder -Report (Join-Path $runRoot 'layout-check.json')
    & (Join-Path $PSScriptRoot 'Test-PortableLayout.ps1') -PublishedFolder (Join-Path $unpackedRoot $packageName) -Report (Join-Path $runRoot 'zip-layout-check.json')
    $zipHash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
    $zipHash.Hash | Set-Content -LiteralPath ($zipPath + '.sha256') -Encoding ASCII
    # Expose paths only after every check has passed. Local use does not require GitHub.
    if ($env:GITHUB_OUTPUT) {
        @("zip_path=$zipPath", "sha_path=$zipPath.sha256", "run_root=$runRoot", "package_name=$packageName", "package_version=$PackageVersion") |
            Out-File -LiteralPath $env:GITHUB_OUTPUT -Encoding utf8 -Append
    }
    Write-Output "Portable root: $packageFolder"
    Write-Output "Internal app folder: $publishFolder"
    Write-Output "ZIP: $zipPath"
    Write-Output "SHA256: $($zipHash.Hash)"
}
finally { Pop-Location }
