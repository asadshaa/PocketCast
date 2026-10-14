@echo off
title Start Phone Mirroring
cls
echo ========================================================
echo   Launching Phone Screen Mirror (Sony Xperia XZ3)
echo ========================================================
echo.
echo Device: QV716QHQ1L
echo Resolution: 1600p, 60 FPS cap, 0ms buffer
echo.
"%LOCALAPPDATA%\Microsoft\WinGet\Packages\Genymobile.scrcpy_Microsoft.Winget.Source_8wekyb3d8bbwe\scrcpy-win64-v4.1\scrcpy.exe" --no-control --max-size=1600 --max-fps=60 --video-bit-rate=8M --video-buffer=0 --print-fps --serial="QV716QHQ1L"
pause
