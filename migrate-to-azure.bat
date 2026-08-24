@echo off
echo ============================================================
echo Portfolio Manager - Migrate Local Data to Azure SQL
echo ============================================================
echo.
echo This will:
echo   1. Apply all EF migrations to Azure SQL (creates/updates tables)
echo   2. Export all business data from local SQL Server
echo   3. Clear existing data on Azure SQL
echo   4. Import local data to Azure SQL
echo.
echo WARNING: All existing Azure data will be replaced!
echo.
set /p CONFIRM=Type YES to continue: 
if /i not "%CONFIRM%"=="YES" (
    echo Cancelled.
    pause
    exit /b 0
)

set AZURE_CONN=Server=tcp:portfolio-sql-dpekerman.database.windows.net,1433;Initial Catalog=PortfolioManagerDb;User ID=portfolioadmin;Password=@Fang1970;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;

echo.
echo Step 1: Applying EF migrations to Azure SQL...
echo (This creates any missing tables before data import)
echo.
cd "%~dp0backend\PortfolioManager.Api"
dotnet ef database update --connection "%AZURE_CONN%"
if errorlevel 1 (
    echo.
    echo WARNING: EF migration had issues - check output above.
    echo Continuing with data migration...
)
cd "%~dp0"

echo.
echo Step 2: Migrating data...
echo.
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\migrate-local-to-azure.ps1" -ImportToAzure -CleanFirst -AzureConnectionString "%AZURE_CONN%"

echo.
echo Migration complete. Check output above for any errors.
echo NOTE: Change Azure SQL password after migration is confirmed successful.
pause
