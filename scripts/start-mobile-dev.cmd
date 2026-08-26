@echo off
REM Double-clickable wrapper for start-mobile-dev.ps1.
REM
REM Same two reasons as start-dev-services.cmd: PowerShell's default execution policy on Windows
REM client is Restricted, so a plain double-click refuses the script and the window vanishes before
REM the error is readable (-ExecutionPolicy Bypass applies to this process only, it changes no
REM machine setting); and `pause` keeps the result on screen either way.
REM
REM Arguments pass straight through, so this works too:
REM   start-mobile-dev.cmd -WithStorefront -Launch

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-mobile-dev.ps1" %*
set "RC=%ERRORLEVEL%"

echo.
if "%RC%"=="0" (
    echo Finished successfully.
) else (
    echo Script failed with exit code %RC%. The error is above.
)

echo.
pause
