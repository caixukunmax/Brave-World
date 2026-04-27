@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo ========================================
echo   Build Tables + Protos
echo ========================================

echo.
echo [1/2] Building Luban tables...
cd /d "%~dp0\..\tables"
call npm run build
if %errorlevel% neq 0 (
    echo [ERROR] Luban tables build failed
    pause
    exit /b 1
)

echo.
echo [2/2] Building protos...
cd /d "%~dp0\..\protocols"
call npm run build
if %errorlevel% neq 0 (
    echo [ERROR] Proto build failed
    pause
    exit /b 1
)

echo.
echo ========================================
echo   Done!
echo ========================================
pause
