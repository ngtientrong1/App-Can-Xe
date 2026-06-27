@echo off
setlocal
cd /d "%~dp0.."
dotnet publish src\CanXe.DeviceTester\CanXe.DeviceTester.csproj -c Release -r win-x64 --self-contained false /p:PublishProfile=DeviceTesterWin10
if errorlevel 1 exit /b 1
echo Publish completed: publish\device-tester-win10-x64
