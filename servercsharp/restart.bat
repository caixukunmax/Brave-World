@echo off
chcp 65001 >nul
rem Build all generated assets and start the game server.
rem Actual logic lives in scripts\build.ps1.
cd /d "%~dp0\.."
powershell -ExecutionPolicy Bypass -File scripts\build.ps1 dev
