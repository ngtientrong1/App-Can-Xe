# Creates acceptance ZIP from publish output (flat contents, no TestAssets)
param(
    [string]$ZipName = "CanXe-0.3.0-rc10-Phase3C-DiagnosticsAllOrchestrationFix.zip"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "publish\win10-x64"
$zipPath = Join-Path $root $ZipName

if (-not (Test-Path (Join-Path $publishDir "CanXe.Desktop.exe"))) {
    throw "Publish folder not ready: $publishDir"
}

$forbidden = @("appsettings.Simulation.json", "appsettings.Test.json")
foreach ($name in $forbidden) {
    if (Test-Path (Join-Path $publishDir $name)) {
        throw "Publish contains forbidden config: $name"
    }
}

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem $publishDir -Recurse -File | ForEach-Object {
        if ($_.FullName -like "*\TestAssets\*") { return }
        $relative = $_.FullName.Substring($publishDir.Length + 1)
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $relative) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

Write-Host "Created: $zipPath"
Write-Host "Size: $([math]::Round((Get-Item $zipPath).Length / 1MB, 2)) MB"
