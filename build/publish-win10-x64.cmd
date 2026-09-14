@echo off
setlocal
cd /d "%~dp0.."

set PUBLISH=publish\win10-x64
set FAILED=%PUBLISH%.failed

echo [1/10] Clean publish folder
if exist "%PUBLISH%" (
  rmdir /s /q "%PUBLISH%" 2>nul
  if exist "%PUBLISH%" (
    echo ERROR: Could not clean publish folder
    exit /b 1
  )
)
if exist "%FAILED%" rmdir /s /q "%FAILED%" 2>nul
mkdir "%PUBLISH%"

echo [2/10] Restore
dotnet restore CanXe.sln
if errorlevel 1 goto :fail

echo [3/10] Build Release
dotnet build CanXe.sln -c Release --no-restore
if errorlevel 1 goto :fail

echo [4/10] Run tests
dotnet test CanXe.sln -c Release --no-build
if errorlevel 1 goto :fail

echo [5/10] Publish desktop
dotnet publish src\CanXe.Desktop\CanXe.Desktop.csproj -c Release -p:PublishProfile=Win10WeakPC
if errorlevel 1 goto :fail

echo [6/10] Publish diagnostics CLI
dotnet publish src\CanXe.Diagnostics\CanXe.Diagnostics.csproj -c Release -r win-x64 --self-contained true -o "%PUBLISH%"
if errorlevel 1 goto :fail

echo [6b/10] Publish COM reset helper (elevated Scheduled Task target — see installer)
dotnet publish src\CanXe.ComResetHelper\CanXe.ComResetHelper.csproj -c Release -r win-x64 --self-contained true -o "%PUBLISH%"
if errorlevel 1 goto :fail

echo [7/10] Copy production config
copy /y src\CanXe.Desktop\appsettings.Production.json "%PUBLISH%\appsettings.json" >nul
if errorlevel 1 goto :fail
findstr /i "Simulation" "%PUBLISH%\appsettings.json" >nul
if not errorlevel 1 (
  echo ERROR: Production appsettings contains Simulation
  goto :fail
)

echo [8/10] Skip FFmpeg packaging (Phase 4)
if exist "%PUBLISH%\ffmpeg" (
  echo ERROR: ffmpeg folder must not exist in Phase 4 publish
  goto :fail
)

echo [9/10] Skipped camera test assets

echo [10/10] Run publish verification
powershell -NoProfile -ExecutionPolicy Bypass -File build\verify-publish-win10-x64.ps1 -PublishDir "%CD%\%PUBLISH%"
if errorlevel 1 goto :fail

echo Publish completed: %PUBLISH%
exit /b 0

:fail
echo PUBLISH FAILED — marking output as failed
if exist "%PUBLISH%" (
  move /y "%PUBLISH%" "%FAILED%" >nul 2>&1
  if exist "%PUBLISH%" rmdir /s /q "%PUBLISH%" 2>nul
)
exit /b 1
