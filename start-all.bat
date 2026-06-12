@echo off
echo ================================================
echo  Portfolio Manager - Full Stack Launcher
echo  Backend : http://localhost:5000
echo  Swagger  : http://localhost:5000/swagger
echo  Frontend : http://localhost:4200
echo ================================================
echo.

:: ── Kill existing backend ──────────────────────────────
echo [1/4] Stopping existing backend process...
taskkill /IM PortfolioManager.Api.exe /F >nul 2>&1

:: ── Free port 5000 ──────────────────────────────────────
echo [2/4] Freeing port 5000...
for /f "tokens=5" %%p in ('netstat -ano ^| findstr ":5000 " ^| findstr "LISTENING"') do (
    echo       Killing PID %%p on port 5000
    taskkill /PID %%p /F >nul 2>&1
)

:: ── Free port 4200 ──────────────────────────────────────
echo [3/4] Freeing port 4200...
for /f "tokens=5" %%p in ('netstat -ano ^| findstr ":4200 " ^| findstr "LISTENING"') do (
    echo       Killing PID %%p on port 4200
    taskkill /PID %%p /F >nul 2>&1
)

echo [4/4] Launching backend and frontend in separate windows...
echo.

:: ── Start backend in a new window ───────────────────────
start "Portfolio Manager Backend - http://localhost:5000" cmd /k "cd /d "%~dp0backend\PortfolioManager.Api" && dotnet run --launch-profile http"

:: Brief pause so the backend window appears first
timeout /t 2 /nobreak >nul

:: ── Start frontend in a new window ──────────────────────
start "Angular Frontend - http://localhost:4200" cmd /k "cd /d "%~dp0frontend\portfolio-manager-ui" && npx ng serve"

echo.
echo  Both services are starting in their own windows.
echo  Backend  : http://localhost:5000
echo  Swagger  : http://localhost:5000/swagger
echo  Frontend : http://localhost:4200  (ready in ~15s)
echo.
echo  NOTE: Make sure you have set your Finnhub API key:
echo    cd backend\PortfolioManager.Api
echo    dotnet user-secrets set "Finnhub:ApiKey" "YOUR_KEY"
echo.
echo  Close this window or press any key to exit.
pause >nul
