@echo off
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Worldex.ps1" -SeekSeconds 67.25 -Paused %*
set "worldex_exit=%errorlevel%"
if not "%worldex_exit%"=="0" (
  echo.
  echo WORLDEX failed to start. The error above and .runtime logs explain why.
  pause
)
exit /b %worldex_exit%
