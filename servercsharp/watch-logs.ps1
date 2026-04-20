#requires -Version 5.1
<#
.SYNOPSIS
    实时追踪 GameServer 日志文件
.DESCRIPTION
    自动找到最新的 server-*.log 文件，实时输出新增内容。
    无论服务器是通过 restart.bat 启动还是后台启动，都能追踪。
    支持按日志级别过滤。

USAGE
    .\watch-logs.ps1              实时显示所有日志
    .\watch-logs.ps1 --error     只显示 ERR 和 FTL
    .\watch-logs.ps1 --warn      只显示 WRN、ERR、FTL
    .\watch-logs.ps1 --tail 50   先显示最后 50 行，再继续追踪
#>

# Manual argument parsing (PowerShell 5.1 compatible)
$ShowError = $false
$ShowWarn = $false
$Tail = 0

foreach ($a in $args) {
    if ($a -eq '--error') { $ShowError = $true }
    elseif ($a -eq '--warn') { $ShowWarn = $true }
    elseif ($a -eq '--tail' -and $i + 1 -lt $args.Length) { $Tail = [int]$args[++$i] }
}

$logDir = [IO.Path]::Combine($PSScriptRoot, 'src', 'GameServer', 'bin', 'Debug', 'net8.0', 'logs')

function Get-LatestLogFile {
    $files = Get-ChildItem -Path $logDir -Filter 'server-*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
    if ($files.Count -eq 0) { return $null }
    return $files[0].FullName
}

function Should-Show($line) {
    if ($ShowError) {
        return $line -match '\[(ERR|FTL)\]'
    }
    if ($ShowWarn) {
        return $line -match '\[(WRN|ERR|FTL)\]'
    }
    return $true
}

function Colorize-Line($line) {
    if ($line -match '\[ERR\]')   { Write-Host $line -ForegroundColor Red }
    elseif ($line -match '\[FTL\]')  { Write-Host $line -ForegroundColor DarkRed }
    elseif ($line -match '\[WRN\]')  { Write-Host $line -ForegroundColor Yellow }
    elseif ($line -match '\[INF\]')  { Write-Host $line -ForegroundColor White }
    elseif ($line -match '\[DBG\]')  { Write-Host $line -ForegroundColor DarkGray }
    else { Write-Host $line }
}

# Find latest log
$latestLog = Get-LatestLogFile
if (-not $latestLog) {
    Write-Host "[watch-logs] No log files found in $logDir" -ForegroundColor Yellow
    Write-Host "[watch-logs] Waiting for server to start..." -ForegroundColor DarkGray
    # Wait for log file to appear
    while (-not $latestLog) {
        Start-Sleep -Seconds 1
        $latestLog = Get-LatestLogFile
    }
}

Write-Host "[watch-logs] Watching: $latestLog" -ForegroundColor Cyan
Write-Host "[watch-logs] Press Ctrl+C to stop" -ForegroundColor DarkGray
Write-Host ""

# Show tail if requested
if ($Tail -gt 0) {
    $lines = Get-Content $latestLog -Tail $Tail
    foreach ($l in $lines) {
        if (Should-Show $l) { Colorize-Line $l }
    }
    Write-Host ""
    Write-Host "--- (now following real-time) ---" -ForegroundColor Cyan
    Write-Host ""
}

# Real-time follow using Get-Content -Wait
$lastFile = $latestLog
$lastPos = (Get-Item $lastFile).Length

try {
    while ($true) {
        # Check if a newer log file appeared (log rotation)
        $currentLatest = Get-LatestLogFile
        if ($currentLatest -ne $lastFile) {
            Write-Host ""
            Write-Host "[watch-logs] Switched to new log: $currentLatest" -ForegroundColor Cyan
            Write-Host ""
            $lastFile = $currentLatest
            $lastPos = 0
        }

        $fileInfo = Get-Item $lastFile
        if ($fileInfo.Length -gt $lastPos) {
            $stream = [System.IO.File]::Open($lastFile, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
            $stream.Position = $lastPos
            $reader = New-Object System.IO.StreamReader($stream)

            while ($null -ne ($line = $reader.ReadLine())) {
                if (Should-Show $line) { Colorize-Line $line }
            }

            $lastPos = $stream.Position
            $reader.Close()
            $stream.Close()
        }

        Start-Sleep -Milliseconds 200
    }
}
catch {
    # Ctrl+C or other interruption
    Write-Host ""
    Write-Host "[watch-logs] Stopped." -ForegroundColor DarkGray
}
