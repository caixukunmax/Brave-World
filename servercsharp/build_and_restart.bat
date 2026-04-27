@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo ========================================
echo   Build Tables + Protos + Restart
echo ========================================

echo.
echo [1/5] Stopping GameServer...
taskkill /f /im GameServer.exe >nul 2>&1
timeout /t 1 /nobreak >nul

echo.
echo [2/5] Building Luban tables...
cd /d "%~dp0\..\tables"
call npm run build
if %errorlevel% neq 0 (
    echo [ERROR] Luban tables build failed
    pause
    exit /b 1
)

echo.
echo [3/5] Building protos...
cd /d "%~dp0\..\protocols"
call npm run build
if %errorlevel% neq 0 (
    echo [ERROR] Proto build failed
    pause
    exit /b 1
)

echo.
echo [4/5] Building server...
cd /d "%~dp0"
dotnet build src\GameServer.GameLogic -v q --nologo
if %errorlevel% neq 0 (
    echo [ERROR] GameServer.GameLogic build failed
    pause
    exit /b 1
)
dotnet build src\GameServer -v q --nologo
if %errorlevel% neq 0 (
    echo [ERROR] GameServer build failed
    pause
    exit /b 1
)

echo.
echo [5/5] Starting GameServer...
cd /d "%~dp0\src\GameServer\bin\Debug\net8.0"
GameServer.exe
