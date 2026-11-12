@echo off
title ScreenInk GitHub Login
echo ============================================
echo  GitHub CLI login for ScreenInk
echo  A one-time code will appear BELOW.
echo  Copy it, then paste it at:
echo  https://github.com/login/device
echo ============================================
echo.
"C:\Program Files\GitHub CLI\gh.exe" auth login --hostname github.com --git-protocol https --web
echo.
echo Exit code: %ERRORLEVEL%
echo If login succeeded, close this window and tell the assistant.
pause
