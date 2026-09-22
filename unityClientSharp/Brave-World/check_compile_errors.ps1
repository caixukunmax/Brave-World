<#
.SYNOPSIS
  Tuanjie engine one-shot compile check: trigger script compilation via batchmode,
  parse the log and extract errors/warnings.
.DESCRIPTION
  For CI / git hook / scheduled task / manual run, to automatically detect Unity
  project compile errors.
  NOTE: close the Tuanjie editor before running. The script also kills any leftover
  Tuanjie batch process so a previous run does not hold the Library lock.
  In this Tuanjie build the -logFile argument only reliably accepts the 8.3 Temp
  path ($env:TEMP); we pass that and read back via the canonical GetFullPath path.
.EXAMPLE
  .\check_compile_errors.ps1
#>
param(
  [string]$ProjectPath = "c:/code/codeBrave-World-node2/unityClientSharp/Brave-World",
  [string]$EditorPath  = "D:/Program Files (x86)/unity_tuanjie_editor/2022.3.62t8/Editor/Tuanjie.exe"
)

$ErrorActionPreference = "Stop"

# Tuanjie's -logFile only accepts the 8.3 Temp path in this build; read back canonical.
$LogFile = [System.IO.Path]::Combine($env:TEMP, "tuanjie_compile.log")
$realLog = [System.IO.Path]::GetFullPath($LogFile)

if (-not (Test-Path $EditorPath)) {
  Write-Error "Editor not found: $EditorPath"
  exit 2
}
if (-not (Test-Path $ProjectPath)) {
  Write-Error "Project not found: $ProjectPath"
  exit 2
}

# Kill any leftover Tuanjie process so a previous batch compile does not hold the Library lock
$leftover = Get-Process -Name "Tuanjie" -ErrorAction SilentlyContinue
if ($leftover) {
  Write-Host "==> Killing leftover Tuanjie process(es)..." -ForegroundColor Yellow
  $leftover | Stop-Process -Force
  for ($i = 0; $i -lt 30; $i++) {
    if (-not (Get-Process -Name "Tuanjie" -ErrorAction SilentlyContinue)) { break }
    Start-Sleep -Seconds 1
  }
}

# NOTE: do NOT delete the log file beforehand; in this Tuanjie build the engine
# does not recreate a -logFile path that an external process removed, so we let
# it create/overwrite the file itself.

Write-Host "==> Launching Tuanjie compile ($ProjectPath) ..." -ForegroundColor Cyan
$sw = [System.Diagnostics.Stopwatch]::StartNew()
& $EditorPath -batchmode -nographics -quit -logFile $LogFile -projectPath $ProjectPath
$exitCode = $LASTEXITCODE
$sw.Stop()
Write-Host "==> Editor exited, exit=$exitCode, elapsed $($sw.Elapsed.TotalSeconds.ToString('F1'))s" -ForegroundColor Cyan

if (-not [System.IO.File]::Exists($realLog)) {
  Write-Error "No log generated: $realLog"
  exit 4
}

$log = [System.IO.File]::ReadAllText($realLog)

# Extract compile errors/warnings: Assets/xxx.cs(line,col): error CSxxxx: message
$errLines  = @([regex]::Matches($log, '[^\r\n]*\.cs\(\d+,\d+\): error CS[^\r\n]*'))   | ForEach-Object { $_.Value.Trim() }
$warnLines = @([regex]::Matches($log, '[^\r\n]*\.cs\(\d+,\d+\): warning CS[^\r\n]*')) | ForEach-Object { $_.Value.Trim() }

Write-Host ""
Write-Host ("=" * 64)
$color = if ($errLines.Count -gt 0) { 'Red' } else { 'Green' }
Write-Host "Compile errors: $($errLines.Count)    Warnings: $($warnLines.Count)" -ForegroundColor $color
Write-Host ("=" * 64)

if ($errLines.Count -gt 0) {
  Write-Host "`n--- ERRORS ---" -ForegroundColor Red
  $errLines | Sort-Object -Unique | ForEach-Object { Write-Host $_ }
}
if ($warnLines.Count -gt 0) {
  Write-Host "`n--- WARNINGS (first 30) ---" -ForegroundColor Yellow
  $warnLines | Sort-Object -Unique | Select-Object -First 30 | ForEach-Object { Write-Host $_ }
}

if ($errLines.Count -gt 0 -or $exitCode -ne 0) {
  Write-Host "`n[RESULT] Compile errors found, fix them before continuing." -ForegroundColor Red
  exit 1
}
Write-Host "`n[RESULT] Compilation passed, no errors." -ForegroundColor Green
exit 0
