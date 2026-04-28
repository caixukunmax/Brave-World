@echo off
chcp 65001 >nul
:: 快捷入口：构建全部 + 启动服务端
:: 实际逻辑在 scripts/build.ps1
cd /d "%~dp0\.."
powershell -ExecutionPolicy Bypass -File scripts\build.ps1 dev
