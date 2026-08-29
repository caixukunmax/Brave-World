@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo [INFO] Building C# project...
dotnet build clinetcsharp.csproj -v q --nologo
if %errorlevel% neq 0 (
    echo [ERROR] Build failed
    pause
    exit /b 1
)

echo [INFO] Opening Godot editor...
start "" "D:\Program Files (x86)\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe" --editor --path "%~dp0"
