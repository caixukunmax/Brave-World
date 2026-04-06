@echo off
cd /d "%~dp0"

set "MAIN_CMD=%~1"
set "SUB_CMD=%~2"

if "%MAIN_CMD%"=="" goto show_help
if "%MAIN_CMD%"=="help" goto show_help
if "%MAIN_CMD%"=="/?" goto show_help
if "%MAIN_CMD%"=="-h" goto show_help
if "%MAIN_CMD%"=="--help" goto show_help

if "%MAIN_CMD%"=="build" goto do_build
if "%MAIN_CMD%"=="dev" goto do_dev
if "%MAIN_CMD%"=="clean" goto do_clean
if "%MAIN_CMD%"=="backup" goto do_backup
if "%MAIN_CMD%"=="table" goto do_table
if "%MAIN_CMD%"=="proto" goto do_proto
if "%MAIN_CMD%"=="gen" goto do_gen
if "%MAIN_CMD%"=="docker" goto handle_docker

echo [ERROR] Unknown command: %MAIN_CMD%
goto show_help

:show_help
echo.
echo ==========================================
echo      TS-Lua2 Game Server Launcher
echo ==========================================
echo.
echo Usage: start.bat ^<command^> [subcommand]
echo.
echo Docker Commands:
echo   start.bat docker up       Start Docker services
echo   start.bat docker down     Stop Docker services
echo   start.bat docker restart  Restart Docker services
echo   start.bat docker logs     View Docker logs
echo   start.bat docker run      Build and start (default)
echo.
echo Dev Commands:
echo   start.bat build           Compile TypeScript to Lua
echo   start.bat dev             Dev mode (compile+start+watch)
echo   start.bat clean           Clean and rebuild
echo.
echo Resource Commands:
echo   start.bat table           Export tables (config)
echo   start.bat proto           Export protocols
echo   start.bat gen             Export both tables and protocols
echo.
echo Other Commands:
echo   start.bat backup          Backup Docker images (offline)
echo   start.bat help            Show this help
echo.
echo Examples:
echo   start.bat docker up       Start service
echo   start.bat build           Compile only
echo   start.bat gen             Export tables + protocols
echo   start.bat backup          Backup images
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
echo [OK] Build complete
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
echo [OK] Tables exported
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
echo [OK] Protocols exported
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

:handle_docker
if "%SUB_CMD%"=="" set "SUB_CMD=run"
if "%SUB_CMD%"=="up" goto docker_up
if "%SUB_CMD%"=="down" goto docker_down
if "%SUB_CMD%"=="restart" goto docker_restart
if "%SUB_CMD%"=="logs" goto docker_logs
if "%SUB_CMD%"=="run" goto docker_run
echo [ERROR] Unknown docker subcommand: %SUB_CMD%
goto end

:docker_up
call :check_env
cd docker
docker compose up -d
cd ..
echo [OK] Services started
goto end

:docker_down
cd docker
docker compose down
cd ..
echo [OK] Services stopped
pause
goto end

:docker_restart
call :check_env
call :docker_down
call npx tstl -p tsconfig.json
call :docker_up
goto end

:docker_logs
cd docker
docker compose logs -f
cd ..
goto end

:docker_run
call :check_env
call npx tstl -p tsconfig.json
if %errorlevel% neq 0 (
    echo [ERROR] Build failed
    pause
    exit /b 1
)
call :docker_up
goto end

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

:do_dev
call :check_env
call npx tstl -p tsconfig.json
if %errorlevel% neq 0 (
    echo [ERROR] Build failed
    pause
    exit /b 1
)
call :docker_up
call npx tstl -p tsconfig.json --watch
goto end

:do_backup
echo ==========================================
echo      Backup Docker Images (Offline)
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

docker images docker-game-server --format "{{.Repository}}" | findstr "docker-game-server" >nul
if %errorlevel% neq 0 (
    echo [ERROR] docker-game-server image not found
    echo Please run 'start.bat docker up' first
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
copy "docker\config.lua" "%BACKUP_DIR%\" >nul
copy "docker\main.lua" "%BACKUP_DIR%\" >nul
copy "docker\preload.lua" "%BACKUP_DIR%\" >nul
copy "docker\start.sh" "%BACKUP_DIR%\" >nul
copy "docker\gateway_service.lua" "%BACKUP_DIR%\" >nul
xcopy "docker\tslua" "%BACKUP_DIR%\tslua\" /E /I /Q >nul
xcopy "docker\tables" "%BACKUP_DIR%\tables\" /E /I /Q >nul
xcopy "docker\protos" "%BACKUP_DIR%\protos\" /E /I /Q >nul
echo [OK] Files copied

echo Backup Time: %date:~0,4%-%date:~5,2%-%date:~8,2% %time:~0,8% > "%BACKUP_DIR%\VERSION.txt"
echo Git Commit: %GIT_COMMIT% >> "%BACKUP_DIR%\VERSION.txt"
echo [OK] VERSION.txt

echo.
echo ==========================================
echo     Backup Complete! Ready for offline
echo ==========================================
echo.
echo Backup contents:
echo   - docker-game-server.tar
echo   - mongo-7.tar
echo   - Configuration files
echo.
echo To deploy offline:
echo   1. Copy %BACKUP_DIR% to offline server
echo   2. docker load -i docker-game-server.tar
echo   3. docker load -i mongo-7.tar
echo   4. docker compose -f docker-compose.offline.yml up -d
echo.
pause
goto end

:end
