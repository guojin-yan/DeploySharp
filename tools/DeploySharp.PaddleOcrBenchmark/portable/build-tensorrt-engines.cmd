@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-TensorRtEngines.ps1" %*
exit /b %ERRORLEVEL%
