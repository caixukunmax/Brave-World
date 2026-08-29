@echo off
chcp 65001 >nul
setlocal
rem ============================================================
rem  一键开发：确保服务器在【可见窗口】运行 + 打开 Godot 编辑器
rem  - 若已有 GameServer 进程 → 不重复起（编辑器内 DevServerLauncher 会附加到它）
rem  - 若没有 → 在独立可见窗口里启动服务器，方便你盯日志/调试
rem  - DevServerLauncher 检测到 8889 已监听 → 走附加模式，不会再起一个
rem  提示：首次使用请先跑一次 servercsharp\restart.bat 构建出 GameServer.exe
rem ============================================================

set "CLIENT_DIR=%~dp0"
for %%i in ("%CLIENT_DIR%.") do set "REPO=%%~fi"
set "SERVER_EXE=%REPO%\servercsharp\src\GameServer\bin\Debug\net8.0\GameServer.exe"
set "GODOT_EXE=D:\Program Files (x86)\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe"

if not exist "%SERVER_EXE%" (
    echo [WARN] 未找到 GameServer.exe：%SERVER_EXE%
    echo        请先运行 servercsharp\restart.bat 构建服务器。
)

echo [INFO] 检查服务器进程...
powershell -NoProfile -Command "if (-not (Get-Process GameServer -ErrorAction SilentlyContinue)) { if (Test-Path '%SERVER_EXE%') { Start-Process -FilePath '%SERVER_EXE%' -WorkingDirectory '%REPO%'; Write-Host '[INFO] 已在可见窗口启动 GameServer' } else { Write-Host '[INFO] 跳过：服务器未构建' } } else { Write-Host '[INFO] GameServer 已在运行，附加到现有实例' }"

echo [INFO] 打开 Godot 编辑器...
start "" "%GODOT_EXE%" --editor --path "%CLIENT_DIR%"
endlocal
