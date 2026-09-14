# One stress iteration with 20-minute wall timeout. Used by run-stress-tests.cmd only.
param(
    [Parameter(Mandatory = $true)][int]$RunIndex,
    [string]$RepoRoot = "",
    [int]$TimeoutMs = 1200000
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        $RepoRoot = Split-Path -Parent $PSScriptRoot
    }
    elseif (-not [string]::IsNullOrWhiteSpace($PSCommandPath)) {
        $RepoRoot = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
    }
    else {
        $RepoRoot = (Get-Location).Path
    }
}

Set-Location $RepoRoot

$env:MSBUILDDISABLENODEREUSE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"

$logDir = Join-Path $env:TEMP "CanXeStressLogs\run-$RunIndex"
if (Test-Path $logDir) {
    Remove-Item $logDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$env:CANXE_LOG_DIR = $logDir

$args = @(
    "test", "CanXe.sln",
    "-c", "Release",
    "--no-build",
    "--no-restore",
    "/nr:false",
    "/m:1",
    "--logger", "console;verbosity=minimal",
    "--",
    "RunConfiguration.MaxCpuCount=1"
)

$p = Start-Process -FilePath "dotnet" -ArgumentList $args -WorkingDirectory $RepoRoot -NoNewWindow -PassThru
if (-not $p.WaitForExit($TimeoutMs)) {
    Write-Host "STRESS RUN $RunIndex TIMED OUT after $($TimeoutMs / 60000) minutes"
    try {
        Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
        Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
            Where-Object { $_.ParentProcessId -eq $p.Id } |
            ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    } catch {}
    exit 2
}

exit $p.ExitCode
