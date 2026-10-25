@echo off
call "%~dp0start.cmd" -SeekSeconds 36.9 -PreviewSeconds 10 %*
exit /b %errorlevel%
