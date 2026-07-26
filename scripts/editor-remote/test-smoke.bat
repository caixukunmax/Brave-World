@echo off
REM Editor Remote 烟雾测试 - 验证插件 API 是否正常工作
REM 使用前请确保 Godot 编辑器已打开且 Editor Remote 插件已启用

setlocal
set "PORT=7788"
set "BASE=http://localhost:%PORT%"

echo === Editor Remote Smoke Test ===
echo.

echo [1/5] 检查服务是否启动...
curl -s -o nul -w "HTTP %%{http_code}\n" %BASE%/api/editor/info
if errorlevel 1 (
    echo 失败：无法连接到 %BASE%
    echo 请确认 Godot 编辑器已启动并启用了 Editor Remote 插件
    pause
    exit /b 1
)
echo.

echo [2/5] 获取编辑器信息...
curl -s %BASE%/api/editor/info | findstr /c:"godot_version" /c:"project"
echo.

echo [3/5] 获取当前场景...
curl -s %BASE%/api/scene/current
echo.

echo [4/5] 获取场景树（根节点）...
curl -s %BASE%/api/scene/tree | findstr /c:"\"name\"" /c:"\"type\""
echo.

echo [5/5] 列出根目录...
curl -s "%BASE%/api/filesystem/list?path=res://" | findstr /c:"directories" /c:"files"
echo.

echo === 测试完成 ===
echo 如果以上都返回了数据，说明插件工作正常。
pause
