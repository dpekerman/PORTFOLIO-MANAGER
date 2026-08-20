@echo off
echo ============================================================
echo  Portfolio Manager - Full Data Migration: Local to Azure SQL
echo ============================================================
echo.
echo This will:
echo   1. Fix any missing columns in Azure SQL
echo   2. DELETE all existing data from Azure SQL business tables
echo   3. Import all data from local SQL Server
echo.
echo Press Ctrl+C to cancel, or any key to continue...
pause > nul

PowerShell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0migrate-full.ps1"

echo.
if %ERRORLEVEL% EQU 0 (
    echo Migration completed successfully.
) else (
    echo Migration failed. See error above.
)
echo.
pause
