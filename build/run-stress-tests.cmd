@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0.."

set RUNS=10
if not "%~1"=="" set RUNS=%~1

rem Stabilize MSBuild / CLI for long repeated stress (avoids MSB4166 node reuse crashes).
set MSBUILDDISABLENODEREUSE=1
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set DOTNET_NOLOGO=1

echo [stress] Shutting down build servers...
dotnet build-server shutdown >nul 2>&1

echo [stress] Restore + single Release build (nodeReuse off, /m:1)...
dotnet restore CanXe.sln
if errorlevel 1 (
  echo STRESS PREBUILD RESTORE FAILED
  exit /b 1
)
dotnet build CanXe.sln -c Release --no-restore /nr:false /m:1 -v q
if errorlevel 1 (
  echo STRESS PREBUILD BUILD FAILED
  exit /b 1
)

dotnet build-server shutdown >nul 2>&1

echo Running full test suite %RUNS% times (no-build, MaxCpuCount=1, isolated logs, 20min/run)...
set FAIL=0

for /L %%i in (1,1,%RUNS%) do (
  echo.
  echo === Stress run %%i / %RUNS% ===
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-one-stress-iteration.ps1" -RunIndex %%i -RepoRoot "%CD%"
  if errorlevel 1 (
    echo STRESS RUN %%i FAILED
    set FAIL=1
    goto :done
  )
  echo STRESS RUN %%i PASSED
  dotnet build-server shutdown >nul 2>&1
)

:done
if %FAIL% equ 0 (
  echo.
  echo STRESS TEST PASSED: %RUNS%/%RUNS% full suite runs
  exit /b 0
)

echo.
echo STRESS TEST FAILED
exit /b 1
