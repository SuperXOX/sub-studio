@echo off
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build.ps1"
if errorlevel 1 (
  echo Build failed. Check the error above.
  pause
  exit /b 1
)
echo Build finished. Open dist\SUB Studio.
pause
