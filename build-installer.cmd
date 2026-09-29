@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build-installer.ps1" %*
set EXITCODE=%ERRORLEVEL%
if not "%EXITCODE%"=="0" (
  echo.
  echo Installer build failed with exit code %EXITCODE%.
  exit /b %EXITCODE%
)
echo.
echo Installer is ready in: installer\output\Win7BookManagement-Offline-Setup.exe
endlocal
