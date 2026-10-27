@echo off
rem Publishes the debt web app self-contained (no .NET install needed on the target) to publish\debt-win-x64.
setlocal
set ROOT=%~dp0..
if exist "%ROOT%\publish\debt-win-x64" rmdir /s /q "%ROOT%\publish\debt-win-x64"
dotnet publish "%ROOT%\src\CanXe.Debt.Web\CanXe.Debt.Web.csproj" -c Release -r win-x64 --self-contained true -o "%ROOT%\publish\debt-win-x64"
exit /b %ERRORLEVEL%
