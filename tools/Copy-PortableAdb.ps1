#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Destination,
    [string]$ArchivePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$manifestPath = Join-Path $PSScriptRoot 'dependencies/platform-tools.windows.json'
$manifest = Get-Content -LiteralPath $manifestPath -Encoding UTF8 -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($ArchivePath)) {
    $ArchivePath = Join-Path $repoRoot ('.tools/' + $manifest.archive)
    if (-not (Test-Path -LiteralPath $ArchivePath -PathType Leaf)) {
        [void][IO.Directory]::CreateDirectory((Split-Path -Parent $ArchivePath))
        $downloadPath = $ArchivePath + '.' + [Guid]::NewGuid().ToString('N') + '.partial'
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $manifest.url -OutFile $downloadPath -UseBasicParsing -TimeoutSec 180
        if ((Get-FileHash -LiteralPath $downloadPath -Algorithm SHA256).Hash -ne $manifest.sha256) {
            throw 'Downloaded Platform-Tools archive SHA256 mismatch; not installing.'
        }
        Move-Item -LiteralPath $downloadPath -Destination $ArchivePath
    }
}
if ((Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash -ne $manifest.sha256) {
    throw 'Platform-Tools archive SHA256 mismatch; not installing. Check or replace the local cache.'
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($ArchivePath))
try {
    foreach ($file in $manifest.files) {
        # Extract only fixed, reviewed entries. Never extract arbitrary archive paths.
        $entry = $archive.GetEntry($file.source)
        if ($null -eq $entry) { throw "Missing Platform-Tools entry: $($file.source)" }
        $target = Join-Path $Destination $file.destination
        [void][IO.Directory]::CreateDirectory((Split-Path -Parent $target))
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $false)
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $file.sha256) {
            throw "Bundled ADB file SHA256 mismatch: $($file.destination)"
        }
    }
}
finally { $archive.Dispose() }
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $Destination 'licenses/android-platform-tools/distribution.json')
