@echo off
title Phone Screen Mirror (Full Display, No Bars)
cls
echo ========================================================
echo   Phone Screen Mirror - Clean Full Display Mode
echo ========================================================
echo.
echo 1. Hiding phone status bar and navigation bar...
"%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe" shell settings put global policy_control immersive.full=*

echo 2. Launching full display video without borders...
echo    - Press Alt+F or F11 anytime to toggle Fullscreen!
echo    - Double-click window to fill screen!
echo.

"%LOCALAPPDATA%\Microsoft\WinGet\Packages\Genymobile.scrcpy_Microsoft.Winget.Source_8wekyb3d8bbwe\scrcpy-win64-v4.1\scrcpy.exe" --no-control --max-size=1600 --max-fps=60 --video-bit-rate=8M --video-buffer=0 --window-borderless --serial="QV716QHQ1L"

pause
