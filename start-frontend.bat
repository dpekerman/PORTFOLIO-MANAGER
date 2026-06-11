@echo off
echo ================================================
echo  Starting Angular Frontend - Portfolio Manager
echo  App: http://localhost:4200
echo ================================================
echo.
echo  NOTE: Make sure the backend is already running
echo  on http://localhost:5000 before opening the app.
echo.

:: Kill any process holding port 4200
echo Freeing port 4200...
for /f "tokens=5" %%p in ('netstat -ano ^| findstr ":4200 " ^| findstr "LISTENING"') do (
    echo Killing PID %%p on port 4200
    taskkill /PID %%p /F >nul 2>&1
)

cd /d "%~dp0frontend\portfolio-manager-ui"
npx ng serve

pause
