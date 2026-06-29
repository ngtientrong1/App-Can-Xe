# Requires FFmpeg on PATH or third-party\ffmpeg\win-x64\ffmpeg.exe
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$assetsDir = Join-Path $root "tests\TestAssets"
$outFile = Join-Path $assetsDir "camera-loop.mp4"
$audioFirstFile = Join-Path $assetsDir "camera-audio-first.mp4"
$durationSeconds = 4

New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null

$ffmpeg = Join-Path $root "third-party\ffmpeg\win-x64\ffmpeg.exe"
$ffprobe = Join-Path $root "third-party\ffmpeg\win-x64\ffprobe.exe"
if (-not (Test-Path $ffmpeg)) {
    $ffmpegCmd = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if ($ffmpegCmd) { $ffmpeg = $ffmpegCmd.Source; $ffprobe = (Get-Command ffprobe -ErrorAction SilentlyContinue).Source }
    else { throw "FFmpeg not found. Place binary at third-party\ffmpeg\win-x64\ffmpeg.exe" }
}

Write-Host "Generating test pattern video: $outFile ($durationSeconds s, 1280x720)"
& $ffmpeg -y -hide_banner -loglevel error `
    -f lavfi -i "testsrc=size=1280x720:rate=12:duration=$durationSeconds" `
    -f lavfi -i "sine=frequency=440:duration=$durationSeconds" `
    -c:v libx264 -pix_fmt yuv420p -c:a aac `
    -shortest $outFile
if ($LASTEXITCODE -ne 0) { throw "Failed to generate camera-loop.mp4" }

Write-Host "Generating audio-first video: $audioFirstFile ($durationSeconds s, 1280x720, stream0=audio stream1=video)"
& $ffmpeg -y -hide_banner -loglevel error `
    -f lavfi -i "sine=frequency=880:duration=$durationSeconds" `
    -f lavfi -i "testsrc=size=1280x720:rate=12:duration=$durationSeconds" `
    -map 0:a -map 1:v `
    -c:a aac -c:v libx264 -pix_fmt yuv420p -shortest $audioFirstFile
if ($LASTEXITCODE -ne 0) { throw "Failed to generate camera-audio-first.mp4" }

function Show-MediaInfo([string]$label, [string]$path) {
    Write-Host "--- $label ---"
    $probeExe = if (Test-Path $ffprobe) { $ffprobe } else { $ffmpeg }
    cmd /c "`"$probeExe`" -hide_banner -i `"$path`" 2>&1" |
        Select-String -Pattern "Stream #|Duration" |
        ForEach-Object { Write-Host $_.Line.Trim() }
    $sizeKb = [math]::Round((Get-Item $path).Length / 1KB, 1)
    Write-Host "File size: ${sizeKb} KB"
}

Show-MediaInfo "camera-loop.mp4" $outFile
Show-MediaInfo "camera-audio-first.mp4" $audioFirstFile
Write-Host "Test assets ready."
