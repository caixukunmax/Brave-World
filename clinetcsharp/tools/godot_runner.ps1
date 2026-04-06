# Godot 自动化运行脚本
# 功能：启动 Godot 并捕获所有输出到日志文件

param(
    [string]$Mode = "run",  # "run" = 运行项目, "check" = 只检查不运行
    [int]$TimeoutSeconds = 30
)

$godotPath = "C:/Program Files (x86)/Godot_v4.6.1-stable_win64.exe/Godot_v4.6.1-stable_win64.exe"
$projectPath = Join-Path $PSScriptRoot ".."
$logFile = Join-Path $projectPath ".godot/auto_run_log.txt"
$errorLog = Join-Path $projectPath ".godot/auto_run_errors.txt"

# 确保 .godot 目录存在
$godotDir = Join-Path $projectPath ".godot"
if (-not (Test-Path $godotDir)) {
    New-Item -ItemType Directory -Path $godotDir -Force | Out-Null
}

# 清空旧日志
"" | Out-File -FilePath $logFile -Encoding UTF8
"" | Out-File -FilePath $errorLog -Encoding UTF8

Write-Host "[Godot Runner] 启动 Godot..." -ForegroundColor Cyan
Write-Host "[Godot Runner] 模式: $Mode" -ForegroundColor Cyan
Write-Host "[Godot Runner] 日志: $logFile" -ForegroundColor Gray

# 构建启动参数
$arguments = @("--path", $projectPath)
if ($Mode -eq "run") {
    $arguments += "--editor"
}

# 启动进程并捕获输出
$process = Start-Process -FilePath $godotPath -ArgumentList $arguments -RedirectStandardOutput $logFile -RedirectStandardError $errorLog -PassThru -WindowStyle Hidden

# 等待进程或超时
$completed = $process.WaitForExit($TimeoutSeconds * 1000)

if (-not $completed) {
    Write-Host "[Godot Runner] 超时 (${TimeoutSeconds}s)，强制终止..." -ForegroundColor Yellow
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
}

# 分析错误
$errors = @()
$warnings = @()

if (Test-Path $logFile) {
    $content = Get-Content $logFile -Raw -Encoding UTF8
    
    # 匹配错误模式
    $errorPatterns = @(
        @{ Pattern = 'ERROR:.*'; Type = 'ERROR' },
        @{ Pattern = 'SCRIPT ERROR:.*'; Type = 'SCRIPT_ERROR' },
        @{ Pattern = 'Failed to load.*'; Type = 'LOAD_ERROR' },
        @{ Pattern = 'Parse Error.*'; Type = 'PARSE_ERROR' },
        @{ Pattern = 'Cannot open file.*'; Type = 'FILE_ERROR' },
        @{ Pattern = 'Node not found.*'; Type = 'NODE_ERROR' }
    )
    
    foreach ($pattern in $errorPatterns) {
        $matches = [regex]::Matches($content, $pattern.Pattern)
        foreach ($match in $matches) {
            $errors += @{
                Type = $pattern.Type
                Message = $match.Value
                FullLine = $match.Value
            }
        }
    }
    
    # 匹配警告
    $warningMatches = [regex]::Matches($content, 'WARNING:.*')
    foreach ($match in $warningMatches) {
        $warnings += $match.Value
    }
}

# 输出结果
Write-Host ""
Write-Host "[Godot Runner] 运行完成" -ForegroundColor Green
Write-Host "  错误数: $($errors.Count)" -ForegroundColor $(if ($errors.Count -gt 0) { "Red" } else { "Green" })
Write-Host "  警告数: $($warnings.Count)" -ForegroundColor $(if ($warnings.Count -gt 0) { "Yellow" } else { "Green" })

if ($errors.Count -gt 0) {
    Write-Host ""
    Write-Host "[Godot Runner] 检测到的错误:" -ForegroundColor Red
    $errors | Select-Object -First 10 | ForEach-Object {
        Write-Host "  [$($_.Type)] $($_.Message)" -ForegroundColor Red
    }
}

# 返回结果对象
@{
    Success = $errors.Count -eq 0
    ErrorCount = $errors.Count
    WarningCount = $warnings.Count
    Errors = $errors
    Warnings = $warnings
    LogFile = $logFile
    ErrorLog = $errorLog
} | ConvertTo-Json -Depth 3
