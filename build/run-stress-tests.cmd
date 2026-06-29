@echo off
setlocal
cd /d "%~dp0.."

set RUNS=10
if not "%~1"=="" set RUNS=%~1

echo Running full test suite %RUNS% times...
set FAIL=0

for /L %%i in (1,1,%RUNS%) do (
  echo.
  echo === Stress run %%i / %RUNS% ===
  dotnet test CanXe.sln -c Release --no-build
  if errorlevel 1 (
    echo STRESS RUN %%i FAILED
    set FAIL=1
    goto :done
  )
  echo STRESS RUN %%i PASSED
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
