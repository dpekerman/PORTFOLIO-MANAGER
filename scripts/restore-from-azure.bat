@echo off
echo ============================================================
echo  Portfolio Manager - Restore: Azure SQL to Local
echo ============================================================
echo.
echo This will:
echo   1. Back up your local database (local-backups\*.bak)
echo   2. Fix any missing columns/tables in local SQL
echo   3. DELETE all existing data from local SQL business tables
echo   4. Import all data from Azure SQL
echo.
echo Press Ctrl+C to cancel, or any key to continue...
pause > nul

PowerShell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0restore-from-azure.ps1"

echo.
if %ERRORLEVEL% EQU 0 (
    echo Restore completed successfully.
) else (
    echo Restore failed. See error above.
)
echo.
pause
