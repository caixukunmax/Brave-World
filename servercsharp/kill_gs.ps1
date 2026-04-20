Get-Process GameServer -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Killing PID $($_.Id)..."
    Stop-Process -Id $_.Id -Force
}
Start-Sleep -Seconds 1
$procs = Get-Process GameServer -ErrorAction SilentlyContinue
if ($procs) {
    $procs | ForEach-Object {
        Write-Host "Force terminating PID $($_.Id) via WMI..."
        (Get-WmiObject Win32_Process -Filter "ProcessId = $($_.Id)").Terminate() | Out-Null
    }
}
Write-Host "Done."
