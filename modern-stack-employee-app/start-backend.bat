@echo off
echo ================================================
echo  Starting .NET Backend - EmployeeApp.Api
echo  API : http://localhost:5000
echo  Swagger: http://localhost:5000/swagger
echo ================================================
echo.

:: Kill any existing instance to avoid file lock
taskkill /IM EmployeeApp.Api.exe /F >nul 2>&1

cd /d "%~dp0backend\EmployeeApp.Api"
dotnet run --urls "http://localhost:5000"

pause
