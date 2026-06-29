param(
    [string]$PublishDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $root "publish\win10-x64"
}

function Fail([string]$Message) {
    Write-Host "FAIL: $Message" -ForegroundColor Red
    exit 1
}

Write-Host "Verifying publish: $PublishDir"

$desktopExe = Join-Path $PublishDir "CanXe.Desktop.exe"
$diagExe = Join-Path $PublishDir "CanXe.Diagnostics.exe"
$ffmpegExe = Join-Path $PublishDir "ffmpeg\ffmpeg.exe"
$appSettings = Join-Path $PublishDir "appsettings.json"

if (-not (Test-Path $desktopExe)) { Fail "CanXe.Desktop.exe missing" }
if (-not (Test-Path $diagExe)) { Fail "CanXe.Diagnostics.exe missing" }
if (-not (Test-Path $ffmpegExe)) { Fail "ffmpeg/ffmpeg.exe missing" }
if (-not (Test-Path $appSettings)) { Fail "appsettings.json missing" }

& $ffmpegExe -version | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "ffmpeg -version failed" }

$configJson = Get-Content $appSettings -Raw
if ($configJson -match '"DeviceMode"\s*:\s*"Simulation"') { Fail "appsettings.json has DeviceMode=Simulation" }
if ($configJson -match '"DeveloperMode"\s*:\s*true') { Fail "appsettings.json has DeveloperMode=true" }
if ($configJson -match '"ShowDeveloperPanel"\s*:\s*true') { Fail "appsettings.json has ShowDeveloperPanel=true" }
if ($configJson -notmatch '"DeviceMode"\s*:\s*"Hardware"') { Fail "appsettings.json is not Hardware mode" }

$ffmpegSourceFiles = @(
    (Join-Path $root "src\CanXe.Infrastructure\Camera\FfmpegRtspDecoder.cs"),
    (Join-Path $root "src\CanXe.Infrastructure\Camera\FfmpegCapabilityProbe.cs")
)
foreach ($file in $ffmpegSourceFiles) {
    if (-not (Test-Path $file)) { continue }
    $content = Get-Content $file -Raw
    if ($content -match '-stimeout') {
        Fail "FFmpeg source contains -stimeout: $file"
    }
}

$requiredDlls = @("CanXe.Application.dll", "CanXe.Infrastructure.dll", "CanXe.Domain.dll")
foreach ($dll in $requiredDlls) {
    if (-not (Test-Path (Join-Path $PublishDir $dll))) { Fail "Missing $dll" }
}

$testAssets = Join-Path $root "tests\TestAssets\camera-loop.mp4"
if (Test-Path $testAssets) {
    $destDir = Join-Path $PublishDir "TestAssets"
    New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    Copy-Item $testAssets (Join-Path $destDir "camera-loop.mp4") -Force
}

Push-Location $PublishDir
try {
    & .\CanXe.Diagnostics.exe --publish-check --skip-camera --skip-scale
    if ($LASTEXITCODE -ne 0) { Fail "Diagnostics --publish-check failed (exit $LASTEXITCODE)" }
}
finally {
    Pop-Location
}

Write-Host "PUBLISH VERIFICATION PASSED" -ForegroundColor Green
