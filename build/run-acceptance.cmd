@echo off
setlocal
cd /d "%~dp0.."

set REQUIRE_CAMERA=--require-camera
set REQUIRE_SCALE=--require-scale
:parse
if "%~1"=="" goto done_parse
if /I "%~1"=="--require-scale" set REQUIRE_SCALE=--require-scale
if /I "%~1"=="--skip-scale" set REQUIRE_SCALE=--skip-scale
if /I "%~1"=="--require-camera" set REQUIRE_CAMERA=--require-camera
if /I "%~1"=="--skip-camera" set REQUIRE_CAMERA=--skip-camera
shift
goto parse
:done_parse

set "PUBLISH=%CD%\publish\win10-x64"
if not exist "%PUBLISH%\CanXe.Diagnostics.exe" (
  echo ERROR: Run publish-win10-x64.cmd first
  exit /b 1
)

pushd "%PUBLISH%"
CanXe.Diagnostics.exe --all %REQUIRE_CAMERA% %REQUIRE_SCALE%
if errorlevel 1 (
  popd
  echo CANXE ACCEPTANCE FAILED
  exit /b 1
)
popd

echo CANXE ACCEPTANCE PASSED
exit /b 0
