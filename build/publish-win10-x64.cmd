@echo off
setlocal
cd /d "%~dp0.."
dotnet publish src\CanXe.Desktop\CanXe.Desktop.csproj -c Release -p:PublishProfile=Win10WeakPC
if errorlevel 1 exit /b 1
echo Publish completed: publish\win10-x64
