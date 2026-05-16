@echo off
chcp 65001 >nul
:: Shortcut: build tables + proto
:: Logic in scripts/build.ps1
cd /d "%~dp0\.."
powershell -ExecutionPolicy Bypass -File scripts\build.ps1 all
pause