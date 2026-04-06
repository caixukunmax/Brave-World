@echo off
chcp 65001 >nul
echo ==========================================
echo Godot 自动检测和修复工具
echo ==========================================
echo.

set PYTHON=%LOCALAPPDATA%\Programs\Python\Python311\python.exe
if not exist "%PYTHON%" (
    echo [错误] 未找到 Python，请安装 Python 3.11+
    pause
    exit /b 1
)

echo [1/2] 运行 Godot 检查...
echo.

"%PYTHON%" "%~dp0auto_fix.py"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [提示] 检测到错误，部分已自动修复
    echo [提示] 请查看上方输出了解详情
) else (
    echo.
    echo [成功] 检查完成，无错误！
)

echo.
echo ==========================================
pause
