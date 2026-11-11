#requires -Version 5.1
[CmdletBinding()]
param([string]$DotnetPath)

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
    param([string]$Path)
    $peBytes = [IO.File]::ReadAllBytes($Path)
    $peOffset = [BitConverter]::ToInt32($peBytes, 0x3c)
    if ([BitConverter]::ToUInt32($peBytes, $peOffset) -ne 0x4550) { throw "Invalid PE: $Path" }
    if ([BitConverter]::ToUInt16($peBytes, $peOffset + 4) -ne 0x8664) { throw "Not x64: $Path" }
    $optional = $peOffset + 24
    $directory = $optional + 112 # PE32+ (x64)
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
        if (-not $result.LocalRuntime -or $result.Architecture -ne 'X64' -or $result.Templates -le 0 -or $result.MatchScore -lt 0.999) {
            throw "Invalid portable self-check report: $resultText"
        }
        $resultText | Set-Content -LiteralPath $Report -Encoding UTF8
        return $result
    }
    finally { $process.Dispose() }
}

Push-Location -LiteralPath $repoRoot
try {
    [xml]$buildProperties = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props')
    $version = [string]$buildProperties.Project.PropertyGroup.Version
    $packageName = "NyangShinobi-v$version-win-x64"
    # Each run uses a fresh directory: no recursive delete and no stale publish files.
    $runRoot = Join-Path $repoRoot ('artifacts/portable/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    $publishFolder = Join-Path $runRoot $packageName
    [void](New-Item -ItemType Directory -Path $publishFolder)
    Invoke-Dotnet -CommandArguments @('restore', 'NyangShinobi.slnx', '--configfile', 'NuGet.Config', '-m:1')
    Invoke-Dotnet -CommandArguments @('build', 'NyangShinobi.slnx', '-c', 'Release', '--no-restore', '-m:1')
    Invoke-Dotnet -CommandArguments @('test', 'NyangShinobi.slnx', '-c', 'Release', '--no-build', '--no-restore', '-m:1', '--logger', 'trx', '--results-directory', (Join-Path $runRoot 'tests'))
    Invoke-Dotnet -CommandArguments @('publish', 'src/NyangShinobi.App/NyangShinobi.App.csproj', '-c', 'Release', '-r', 'win-x64',
        '--self-contained', 'true', '-p:PublishProfile=PortableWinX64', '-o', $publishFolder, '-m:1')

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
    $nativeReport = foreach ($nativeName in @('OpenCvSharpExtern.dll', 'opencv_videoio_ffmpeg500_64.dll')) {
        $nativeImports = @(Get-NativeImports (Join-Path $publishFolder $nativeName))
        $externalVc = @($nativeImports | Where-Object { $_ -match '^(msvcp\d|vcruntime\d|concrt\d)' -and -not (Test-Path -LiteralPath (Join-Path $publishFolder $_)) })
        if ($externalVc.Count -ne 0) { throw "Unbundled VC++ runtime dependency in ${nativeName}: $externalVc" }
        [pscustomobject]@{ File = $nativeName; Imports = $nativeImports; ExternalVcRuntime = $externalVc }
    }
    $nativeReport | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runRoot 'native-dependencies.json') -Encoding UTF8
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/PORTABLE_DISTRIBUTION.md') -Destination (Join-Path $publishFolder 'PORTABLE_README.md')
    [void](Test-PortableExe $publishFolder (Join-Path $runRoot 'portable-check.json'))

    $zipPath = Join-Path $runRoot ($packageName + '.zip')
    Compress-Archive -LiteralPath $publishFolder -DestinationPath $zipPath -CompressionLevel Optimal
    $unpackedRoot = Join-Path $runRoot 'zip-verification'
    Expand-Archive -LiteralPath $zipPath -DestinationPath $unpackedRoot
    [void](Test-PortableExe (Join-Path $unpackedRoot $packageName) (Join-Path $runRoot 'zip-check.json'))
    $zipHash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
    $zipHash.Hash | Set-Content -LiteralPath ($zipPath + '.sha256') -Encoding ASCII
    Write-Output "Publish folder: $publishFolder"
    Write-Output "ZIP: $zipPath"
    Write-Output "SHA256: $($zipHash.Hash)"
}
finally { Pop-Location }
