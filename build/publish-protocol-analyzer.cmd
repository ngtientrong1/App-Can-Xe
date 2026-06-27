@echo off
setlocal
cd /d "%~dp0.."
dotnet publish src\CanXe.ProtocolAnalyzer\CanXe.ProtocolAnalyzer.csproj -c Release -r win-x64 --self-contained false /p:PublishProfile=ProtocolAnalyzerWin10
if errorlevel 1 exit /b 1
echo Publish completed: publish\protocol-analyzer-win10-x64
