@echo off
chcp 65001 >nul

echo Clearing Godot cache...
echo.

REM Get the project root directory (parent of tools folder)
set "PROJECT_DIR=%~dp0.."
set "GODOT_DIR=%PROJECT_DIR%\.godot"

REM Normalize the path
for %%F in ("%GODOT_DIR%") do set "GODOT_DIR=%%~fF"

echo Project directory: %PROJECT_DIR%
echo Cache directory: %GODOT_DIR%
echo.

if exist "%GODOT_DIR%" (
    echo Deleting .godot/ cache folder...
    rmdir /s /q "%GODOT_DIR%"
    if exist "%GODOT_DIR%" (
        echo Failed to delete cache. Please close Godot and try again.
    ) else (
        echo Cache cleared successfully!
    )
) else (
    echo .godot/ folder does not exist, nothing to clear.
)

echo.
echo Done! Please restart Godot editor.
pause
