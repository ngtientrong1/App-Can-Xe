@echo off
setlocal
cd /d "%~dp0.."

set PUBLISH=publish\win10-x64
set FAILED=%PUBLISH%.failed

echo [1/11] Clean publish folder
if exist "%PUBLISH%" (
  rmdir /s /q "%PUBLISH%" 2>nul
  if exist "%PUBLISH%" (
    echo ERROR: Could not clean publish folder
    exit /b 1
  )
)
if exist "%FAILED%" rmdir /s /q "%FAILED%" 2>nul
mkdir "%PUBLISH%"

echo [2/11] Restore
dotnet restore CanXe.sln
if errorlevel 1 goto :fail

echo [3/11] Build Release
dotnet build CanXe.sln -c Release --no-restore
if errorlevel 1 goto :fail

echo [4/11] Run tests
dotnet test CanXe.sln -c Release --no-build
if errorlevel 1 goto :fail

echo [5/11] Skipped separate integration pass (included in step 4)

echo [6/11] Publish desktop
dotnet publish src\CanXe.Desktop\CanXe.Desktop.csproj -c Release -p:PublishProfile=Win10WeakPC
if errorlevel 1 goto :fail

echo [7/11] Publish diagnostics CLI
dotnet publish src\CanXe.Diagnostics\CanXe.Diagnostics.csproj -c Release -r win-x64 --self-contained true -o "%PUBLISH%"
if errorlevel 1 goto :fail

echo [8/11] Copy production config
copy /y src\CanXe.Desktop\appsettings.Production.json "%PUBLISH%\appsettings.json" >nul
if errorlevel 1 goto :fail
findstr /i "Simulation" "%PUBLISH%\appsettings.json" >nul
if not errorlevel 1 (
  echo ERROR: Production appsettings contains Simulation
  goto :fail
)

echo [9/11] Copy FFmpeg
if not exist third-party\ffmpeg\win-x64\ffmpeg.exe (
  echo ERROR: third-party\ffmpeg\win-x64\ffmpeg.exe not found
  goto :fail
)
if not exist "%PUBLISH%\ffmpeg" mkdir "%PUBLISH%\ffmpeg"
xcopy /y /i /q third-party\ffmpeg\win-x64\* "%PUBLISH%\ffmpeg\"
if errorlevel 1 goto :fail

echo [10/11] Copy test assets for verification
if exist tests\TestAssets\camera-loop.mp4 (
  if not exist "%PUBLISH%\TestAssets" mkdir "%PUBLISH%\TestAssets"
  copy /y tests\TestAssets\camera-loop.mp4 "%PUBLISH%\TestAssets\" >nul
)

echo [11/11] Run publish verification
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
