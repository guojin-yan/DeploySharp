@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-PaddleOcrBenchmark.ps1" %*
exit /b %ERRORLEVEL%
