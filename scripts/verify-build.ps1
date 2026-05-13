# 编译验证脚本：客户端 + 服务器
param()

Write-Host "========================================"
Write-Host "  编译验证"
Write-Host "========================================"

# 1. 杀掉服务器进程（如果正在跑）
$procs = Get-Process GameServer -ErrorAction SilentlyContinue
if ($procs) {
    Write-Host "[KILL] 停止服务器..."
    $procs | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

# 2. 编译客户端
Write-Host ""
Write-Host "[BUILD] 客户端..."
$clientResult = dotnet build "$PSScriptRoot/../clinetcsharp/clinetcsharp.csproj" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "客户端编译失败!" -ForegroundColor Red
    $clientResult
    exit 1
}
Write-Host "客户端 OK" -ForegroundColor Green

# 3. 编译服务器
Write-Host ""
Write-Host "[BUILD] 服务器..."
$serverResult = dotnet build "$PSScriptRoot/../servercsharp/GameServer.sln" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "服务器编译失败!" -ForegroundColor Red
    $serverResult
    exit 1
}
Write-Host "服务器 OK" -ForegroundColor Green

Write-Host ""
Write-Host "========================================"
Write-Host "  全部通过" -ForegroundColor Green
Write-Host "========================================"
