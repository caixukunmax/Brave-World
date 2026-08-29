@echo off
chcp 65001 >nul
rem 强制带上 .NET SDK 环境启动 Godot 编辑器（绕开旧的 VS Code/终端环境快照）。
rem 用途：当编辑器报 “...cs is not compiling / Failed to create an autoload” 时，
rem       用这个脚本打开编辑器，C# 就能正常编译、地图配置插件面板也会出现。
set "DOTNET_ROOT=C:\Program Files\dotnet"
set "PATH=%PATH%;C:\Program Files\dotnet"
set "GODOT=D:\Program Files (x86)\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe"
start "" "%GODOT%" --editor --path "%~dp0"
