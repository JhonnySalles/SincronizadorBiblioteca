@echo off
chcp 65001 > nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run_tests.ps1"
exit /b %ERRORLEVEL%
