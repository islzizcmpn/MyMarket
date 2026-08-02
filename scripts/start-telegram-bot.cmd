@echo off
REM Double-clickable wrapper for start-telegram-bot.ps1.
REM
REM Two reasons this exists rather than running the .ps1 directly:
REM   1. PowerShell's default execution policy on Windows client is Restricted, so a plain
REM      double-click (or "Run with PowerShell") refuses the script and the window vanishes before
REM      the error is readable. -ExecutionPolicy Bypass applies to this process only; it does not
REM      change any machine setting.
REM   2. `pause` keeps the window open afterwards so the result stays on screen either way.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-telegram-bot.ps1" %*
set "RC=%ERRORLEVEL%"

echo.
if "%RC%"=="0" (
    echo Finished successfully.
) else (
    echo Script failed with exit code %RC%. The error is above.
)

echo.
pause
