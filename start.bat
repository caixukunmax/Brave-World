@echo off
cd /d "%~dp0"

set "MAIN_CMD=%~1"
set "SUB_CMD=%~2"

if "%MAIN_CMD%"=="" goto show_help
if "%MAIN_CMD%"=="help" goto show_help
if "%MAIN_CMD%"=="/" goto show_help
if "%MAIN_CMD%"=="-h" goto show_help
if "%MAIN_CMD%"=="--help" goto show_help

if "%MAIN_CMD%"=="build" goto do_build
if "%MAIN_CMD%"=="up" goto do_up
if "%MAIN_CMD%"=="down" goto do_down
if "%MAIN_CMD%"=="restart" goto do_restart
if "%MAIN_CMD%"=="logs" goto do_logs
if "%MAIN_CMD%"=="ps" goto do_ps
if "%MAIN_CMD%"=="clean" goto do_clean
if "%MAIN_CMD%"=="backup" goto do_backup
if "%MAIN_CMD%"=="table" goto do_table
if "%MAIN_CMD%"=="proto" goto do_proto
if "%MAIN_CMD%"=="gen" goto do_gen

echo [ERROR] Unknown command: %MAIN_CMD%
goto show_help

:show_help
echo.
echo ==========================================
echo      TS-Lua2 Game Server Launcher
echo ==========================================
echo.
echo Usage: start.bat ^<command^>
echo.
echo Commands:
echo   start.bat up         Build TS + Start services
echo   start.bat down       Stop services
echo   start.bat restart    Build TS + Restart services
echo   start.bat build      Compile TypeScript only
echo   start.bat logs       View service logs
echo   start.bat ps         View container status
echo.
echo Resource Commands:
echo   start.bat table      Export config tables
echo   start.bat proto      Export protocols
echo   start.bat gen        Export tables + protocols
echo.
echo Other Commands:
echo   start.bat backup     Backup for offline deploy
echo   start.bat clean      Clean and rebuild
echo   start.bat help       Show this help
echo.
echo Examples:
echo   start.bat up         First time start
echo   start.bat restart    After code changes
echo   start.bat build      Compile only
echo.
goto end

:check_env
echo [Check] Checking environment...
call node --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Node.js not found
    exit /b 1
)
call docker --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Docker not found
    exit /b 1
)
echo [OK] Environment check passed
goto :eof

:do_build
call :check_env
call npx tstl -p tsconfig.json
if %errorlevel% neq 0 (
    echo [ERROR] Build failed
    pause
    exit /b 1
)
echo [OK] Build complete: docker/tslua/
pause
goto end

:do_up
call :check_env
echo [INFO] Building TypeScript...
call npx tstl -p tsconfig.json
if %errorlevel% neq 0 (
    echo [ERROR] Build failed
    pause
    exit /b 1
)
echo [INFO] Starting services...
cd docker
call :check_image
docker compose up -d
cd ..
echo [OK] Services started on port 8889
pause
goto end

:do_down
echo [INFO] Stopping services...
cd docker
docker compose down
cd ..
echo [OK] Services stopped
pause
goto end

:do_restart
call :check_env
echo [INFO] Stopping services...
cd docker
docker compose down
cd ..
echo [INFO] Building TypeScript...
call npx tstl -p tsconfig.json
if %errorlevel% neq 0 (
    echo [ERROR] Build failed
    pause
    exit /b 1
)
echo [INFO] Starting services...
cd docker
call :check_image
docker compose up -d --force-recreate
cd ..
echo [OK] Services restarted (logs cleared)
echo [TIP] Client can connect to localhost:8889
pause
goto end

:do_logs
cd docker
docker compose logs -f
cd ..
goto end

:do_ps
cd docker
docker compose ps
cd ..
pause
goto end

:check_image
echo [Check] Checking Docker image...
for /f "tokens=*" %%i in ('docker images -q docker-game-server 2^>nul') do set "IMAGE_ID=%%i"
if "%IMAGE_ID%"=="" (
    echo [INFO] Image not found, building...
    docker compose build
    echo [OK] Image built
) else (
    echo [OK] Image exists: %IMAGE_ID:~0,12%
)
goto :eof

:do_clean
call :check_env
call npx tstl -p tsconfig.json
if %errorlevel% neq 0 (
    echo [ERROR] Clean failed
    pause
    exit /b 1
)
echo [OK] Clean complete
pause
goto end

:do_table
cd tables
call npm install >nul 2>&1
call npm run build
if %errorlevel% neq 0 (
    echo [ERROR] Export tables failed
    cd ..
    pause
    exit /b 1
)
cd ..
echo [OK] Tables exported to docker/tables/
pause
goto end

:do_proto
cd protocols
call npm install >nul 2>&1
call npm run build
if %errorlevel% neq 0 (
    echo [ERROR] Export protocols failed
    cd ..
    pause
    exit /b 1
)
cd ..
echo [OK] Protocols exported to docker/protos/
pause
goto end

:do_gen
call :do_table
if %errorlevel% neq 0 goto end
call :do_proto
if %errorlevel% neq 0 goto end
echo [OK] All resources exported
pause
goto end

:do_backup
call :check_env
echo ==========================================
echo      Backup for Offline Deployment
echo ==========================================
echo.

for /f "tokens=*" %%a in ('git rev-parse --short HEAD 2^>nul') do set "GIT_COMMIT=%%a"
if "%GIT_COMMIT%"=="" set "GIT_COMMIT=unknown"

set "BACKUP_DIR=docker\backup\%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%%time:~6,2%_%GIT_COMMIT%"
set "BACKUP_DIR=%BACKUP_DIR: =0%"
if not exist "docker\backup" mkdir "docker\backup"
if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"

echo Backup directory: %BACKUP_DIR%
echo Git commit: %GIT_COMMIT%
echo.

:: 检查镜像
docker images docker-game-server --format "{{.Repository}}" | findstr "docker-game-server" >nul
if %errorlevel% neq 0 (
    echo [ERROR] docker-game-server image not found
    echo Please run 'start.bat up' first
    pause
    exit /b 1
)

echo [1/3] Backing up game server image...
docker save -o "%BACKUP_DIR%\docker-game-server.tar" docker-game-server:latest
echo [OK] docker-game-server.tar

echo [2/3] Backing up MongoDB image...
docker save -o "%BACKUP_DIR%\mongo-7.tar" mongo:7
echo [OK] mongo-7.tar

echo [3/3] Copying deployment files...
xcopy "docker\*" "%BACKUP_DIR%\" /E /I /Q /Y >nul
echo [OK] docker/ folder copied

echo Backup Time: %date:~0,4%-%date:~5,2%-%date:~8,2% %time:~0,8% > "%BACKUP_DIR%\VERSION.txt"
echo Git Commit: %GIT_COMMIT% >> "%BACKUP_DIR%\VERSION.txt"
echo [OK] VERSION.txt

echo.
echo ==========================================
echo       Backup Complete!
echo ==========================================
echo.
echo To deploy offline:
echo   1. Copy backup folder to target server
echo   2. cd %%BACKUP_DIR%%
echo   3. docker load -i docker-game-server.tar
echo   4. docker load -i mongo-7.tar
echo   5. docker compose up -d
echo.
pause
goto end

:end
