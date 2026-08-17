@echo off
echo ================================================
echo  Starting .NET Backend - PortfolioManager.Api
echo  API    : http://localhost:5000
echo  Swagger: http://localhost:5000/swagger
echo ================================================
echo.

:: Kill any existing instance to avoid file lock
taskkill /IM PortfolioManager.Api.exe /F >nul 2>&1

:: Free port 5000
for /f "tokens=5" %%p in ('netstat -ano ^| findstr ":5000 " ^| findstr "LISTENING"') do (
    taskkill /PID %%p /F >nul 2>&1
)

cd /d "%~dp0backend\PortfolioManager.Api"
dotnet run --launch-profile http

pause
