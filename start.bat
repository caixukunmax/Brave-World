@echo off
cd /d "%~dp0"

echo [tslua2] Building TypeScript to Lua...
call npx tstl -p tsconfig.json
if %errorlevel% neq 0 (
    echo [tslua2] tstl build failed!
    pause
    exit /b 1
)

echo [tslua2] Starting Docker container...
cd docker
docker compose up -d --build %*
if %errorlevel% neq 0 (
    echo [tslua2] Docker start failed!
    pause
    exit /b 1
)

echo.
echo [tslua2] Server started. Showing logs (Ctrl+C to exit)...
docker compose logs -f
