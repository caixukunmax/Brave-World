#requires -Version 5.1
<#
.SYNOPSIS
    tslua2 统一构建与验证脚本
.DESCRIPTION
    提供构建、测试、验证、启动等命令，替代分散的 .bat 脚本。
    所有命令从仓库根目录运行。

USAGE
    .\scripts\build.ps1 tables          构建 Luban 配置表
    .\scripts\build.ps1 proto           构建 Protobuf 协议
    .\scripts\build.ps1 server          构建服务端
    .\scripts\build.ps1 all             构建全部（tables + proto + server）
    .\scripts\build.ps1 test            运行测试
    .\scripts\build.ps1 verify          构建全部 + 运行测试
    .\scripts\build.ps1 dev             构建 + 启动服务端
#>

param(
    [Parameter(Position=0)]
    [ValidateSet('tables', 'proto', 'server', 'all', 'test', 'verify', 'dev')]
    [string]$Command = 'all'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

function Step-Tables {
    Write-Host "`n[1] Building Luban tables..." -ForegroundColor Cyan
    Push-Location (Join-Path $RepoRoot 'tables')
    try {
        npm run build
        if ($LASTEXITCODE -ne 0) { throw "Tables build failed" }
        Write-Host "[OK] Tables built" -ForegroundColor Green
    }
    finally { Pop-Location }
}

function Step-Proto {
    Write-Host "`n[2] Building Protobuf protocols..." -ForegroundColor Cyan
    Push-Location (Join-Path $RepoRoot 'protocols')
    try {
        npm run build
        if ($LASTEXITCODE -ne 0) { throw "Proto build failed" }
        Write-Host "[OK] Protocols built" -ForegroundColor Green
    }
    finally { Pop-Location }
}

function Step-Server {
    Write-Host "`n[3] Building server..." -ForegroundColor Cyan
    $sln = Join-Path $RepoRoot 'servercsharp\GameServer.sln'

    # GameLogic 需要先构建（ALC 热加载）
    dotnet build (Join-Path $RepoRoot 'servercsharp\src\GameServer.GameLogic') -v q --nologo
    if ($LASTEXITCODE -ne 0) { throw "GameLogic build failed" }

    dotnet build $sln -v q --nologo
    if ($LASTEXITCODE -ne 0) { throw "Server build failed" }
    Write-Host "[OK] Server built" -ForegroundColor Green
}

function Step-Test {
    Write-Host "`n[4] Running tests..." -ForegroundColor Cyan
    $testProj = Join-Path $RepoRoot 'servercsharp\src\GameServer.Tests'
    dotnet test $testProj --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
    Write-Host "[OK] All tests passed" -ForegroundColor Green
}

function Step-KillServer {
    $procs = Get-Process GameServer -ErrorAction SilentlyContinue
    if ($procs) {
        Write-Host "Stopping GameServer..." -ForegroundColor Yellow
        $procs | Stop-Process -Force
        Start-Sleep -Seconds 1
    }
}

function Step-Dev {
    Step-KillServer
    Step-Tables
    Step-Proto
    Step-Server
    Write-Host "`n[5] Starting GameServer..." -ForegroundColor Cyan
    $exe = Join-Path $RepoRoot 'servercsharp\src\GameServer\bin\Debug\net8.0\GameServer.exe'
    if (-not (Test-Path $exe)) { throw "GameServer.exe not found at $exe" }
    & $exe
}

# ---- Main ----
$sw = [System.Diagnostics.Stopwatch]::StartNew()

try {
    switch ($Command) {
        'tables'  { Step-Tables }
        'proto'   { Step-Proto }
        'server'  { Step-Server }
        'all'     { Step-Tables; Step-Proto; Step-Server }
        'test'    { Step-Test }
        'verify'  { Step-Tables; Step-Proto; Step-Server; Step-Test }
        'dev'     { Step-Dev }
    }

    $sw.Stop()
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  Done! ($($sw.Elapsed.ToString('mm\:ss')))" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
}
catch {
    $sw.Stop()
    Write-Host "`n[FAILED] $_" -ForegroundColor Red
    Write-Host "  Elapsed: $($sw.Elapsed.ToString('mm\:ss'))" -ForegroundColor DarkGray
    exit 1
}
