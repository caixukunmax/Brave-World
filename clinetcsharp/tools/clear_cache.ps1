# Clear Godot Cache
Write-Host "Clearing Godot cache..." -ForegroundColor Cyan

$projectDir = Split-Path -Parent $PSScriptRoot
$godotDir = Join-Path $projectDir ".godot"

Write-Host "Project directory: $projectDir"
Write-Host "Cache directory: $godotDir"
Write-Host ""

if (Test-Path $godotDir) {
    Write-Host "Deleting .godot/ cache folder..." -ForegroundColor Yellow
    try {
        Remove-Item -Recurse -Force $godotDir
        Write-Host "Cache cleared successfully!" -ForegroundColor Green
    } catch {
        Write-Host "Failed to delete cache. Please close Godot and try again." -ForegroundColor Red
        Write-Host "Error: $_"
    }
} else {
    Write-Host ".godot/ folder does not exist, nothing to clear." -ForegroundColor Gray
}

Write-Host ""
Write-Host "Done! Please restart Godot editor." -ForegroundColor Green
pause
