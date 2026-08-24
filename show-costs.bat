@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\show-monthly-costs.ps1" %*
pause
