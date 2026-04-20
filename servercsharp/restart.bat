@echo off
chcp 65001 >nul
cd /d "%~dp0"

:: Kill existing GameServer process
for /f "tokens=2" %%i in ('tasklist /fi "imagename eq GameServer.exe" /nh 2^>nul ^| findstr /i "GameServer"') do (
    echo [INFO] Killing existing GameServer ^(PID %%i^)...
    taskkill /pid %%i /f >nul 2>&1
    timeout /t 1 /nobreak >nul
)

:: Build GameLogic first (it is loaded via ALC, not referenced directly)
echo [INFO] Building GameServer.GameLogic...
dotnet build src\GameServer.GameLogic -v q --nologo
if %errorlevel% neq 0 (
    echo [ERROR] GameServer.GameLogic build failed
    pause
    exit /b 1
)

:: Build main server
echo [INFO] Building GameServer...
dotnet build src\GameServer -v q --nologo
if %errorlevel% neq 0 (
    echo [ERROR] GameServer build failed
    pause
    exit /b 1
)

echo [INFO] Starting GameServer...
cd /d %~dp0\src\GameServer\bin\Debug\net8.0
GameServer.exe
