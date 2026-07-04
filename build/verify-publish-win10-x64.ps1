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
if (-not (Test-Path $appSettings)) { Fail "appsettings.json missing" }
if (Test-Path $ffmpegExe) { Fail "ffmpeg/ffmpeg.exe must not be packaged in Phase 4 production" }

$configJson = Get-Content $appSettings -Raw
if ($configJson -match '"DeviceMode"\s*:\s*"Simulation"') { Fail "appsettings.json has DeviceMode=Simulation" }
if ($configJson -match '"DeveloperMode"\s*:\s*true') { Fail "appsettings.json has DeveloperMode=true" }
if ($configJson -match '"ShowDeveloperPanel"\s*:\s*true') { Fail "appsettings.json has ShowDeveloperPanel=true" }
if ($configJson -notmatch '"DeviceMode"\s*:\s*"Hardware"') { Fail "appsettings.json is not Hardware mode" }
if ($configJson -match 'CameraPreviewEnabled|RtspHost|CameraEnabled') { Fail "appsettings.json still contains camera keys" }

$forbiddenPublishPatterns = @('ffmpeg.exe', 'camera-loop.mp4', 'rtsp', 'snapshot')
foreach ($pattern in $forbiddenPublishPatterns) {
    $hits = Get-ChildItem -Path $PublishDir -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match $pattern }
    if ($hits) {
        Fail "Forbidden publish artifact: $($hits[0].FullName)"
    }
}

$requiredDlls = @("CanXe.Application.dll", "CanXe.Infrastructure.dll", "CanXe.Domain.dll")
foreach ($dll in $requiredDlls) {
    if (-not (Test-Path (Join-Path $PublishDir $dll))) { Fail "Missing $dll" }
}

Push-Location $PublishDir
try {
    & .\CanXe.Diagnostics.exe --publish-check --skip-scale
    if ($LASTEXITCODE -ne 0) { Fail "Diagnostics --publish-check failed (exit $LASTEXITCODE)" }
}
finally {
    Pop-Location
}

Write-Host "PUBLISH VERIFICATION PASSED" -ForegroundColor Green
