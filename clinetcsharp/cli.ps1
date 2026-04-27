#requires -Version 5.1
<#
.SYNOPSIS
    Client CLI -- Godot client toolbox
.DESCRIPTION
    Diagnose, clean, build, sync, run -- all in one.
    Supports human-friendly colored output and machine-friendly JSON mode.

USAGE
    .\cli.ps1 status              Quick status overview
    .\cli.ps1 check               Full diagnostics
    .\cli.ps1 check --quick       Quick diagnostics (skip slow checks)
    .\cli.ps1 clean               Clean .godot/mono cache
    .\cli.ps1 rebuild             Force rebuild C# project
    .\cli.ps1 sync-proto          Regenerate proto from source for both client and server
    .\cli.ps1 run                 Build and launch Godot editor
    .\cli.ps1 run --play          Build and play game (no editor)
    .\cli.ps1 test ping           Test server connectivity (no Godot)
    .\cli.ps1 test login          Auto login test
    .\cli.ps1 test enter          Full login -> enter game flow
    .\cli.ps1 test move --x 30 --y 30   Move character
    .\cli.ps1 test gm --cmd "additem 1001 10"   Execute GM command
    .\cli.ps1 test bot --duration 60    Run a random-move bot
    .\cli.ps1 help                Show this help

    .\cli.ps1 <cmd> --json        Output JSON (for AI parsing)
    .\cli.ps1 <cmd> --silent      Silent mode (errors only)
#>

# Manual argument parsing (PowerShell 5.1 compatible)
$Command = 'status'
$TestSub = 'ping'
$Quick = $false
$Json = $false
$Silent = $false
$Play = $false
$ServerHost = '127.0.0.1'
$ServerPort = 8889
$TestUsername = 'test'
$TestPassword = 'test'
$TestX = 25
$TestY = 25
$TestCmd = 'help'
$TestDuration = 60
$TestRoleName = ''

$validCommands = @('status','check','clean','rebuild','sync-proto','run','test','help')
$TestSubCommands = @('ping','login','enter','move','gm','bot')

for ($i = 0; $i -lt $args.Length; $i++) {
    $a = $args[$i]
    if ($a -eq '--quick')  { $Quick = $true }
    elseif ($a -eq '--json')   { $Json = $true }
    elseif ($a -eq '--silent') { $Silent = $true }
    elseif ($a -eq '--play')   { $Play = $true }
    elseif ($a -eq '-h' -or $a -eq '--help') { $Command = 'help' }
    elseif ($a -eq '--username' -and $i + 1 -lt $args.Length) { $TestUsername = $args[++$i] }
    elseif ($a -eq '--password' -and $i + 1 -lt $args.Length) { $TestPassword = $args[++$i] }
    elseif ($a -eq '--x' -and $i + 1 -lt $args.Length) { $TestX = [int]$args[++$i] }
    elseif ($a -eq '--y' -and $i + 1 -lt $args.Length) { $TestY = [int]$args[++$i] }
    elseif ($a -eq '--cmd' -and $i + 1 -lt $args.Length) { $TestCmd = $args[++$i] }
    elseif ($a -eq '--duration' -and $i + 1 -lt $args.Length) { $TestDuration = [int]$args[++$i] }
    elseif ($a -eq '--role-name' -and $i + 1 -lt $args.Length) { $TestRoleName = $args[++$i] }
    elseif ($validCommands -contains $a) { $Command = $a }
    elseif ($TestSubCommands -contains $a) { $TestSub = $a }
}

# If no command given, default to status
if (-not $validCommands -contains $Command) { $Command = 'status' }

# ========================================================================
# Config
# ========================================================================
$Script:ProjectRoot = (Resolve-Path "$PSScriptRoot").Path
$Script:ServerRoot  = [IO.Path]::Combine($Script:ProjectRoot, '..', 'servercsharp')
$Script:ProtoRoot   = [IO.Path]::Combine($Script:ProjectRoot, '..', 'protocols')
$Script:GodotExe    = 'D:\Program Files (x86)\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64.exe'

# ========================================================================
# Output helpers
# ========================================================================
function Write-Ok($msg)    { if (-not $Silent) { Write-Host "  [OK]    $msg" -ForegroundColor Green } }
function Write-Warn($msg)  { if (-not $Silent) { Write-Host "  [WARN]  $msg" -ForegroundColor Yellow } }
function Write-Err($msg)   { if (-not $Silent) { Write-Host "  [ERR]   $msg" -ForegroundColor Red } }
function Write-Info($msg)  { if (-not $Silent) { Write-Host "  [INFO]  $msg" -ForegroundColor Cyan } }
function Write-Dim($msg)   { if (-not $Silent) { Write-Host "          $msg" -ForegroundColor DarkGray } }
function Write-Title($title) {
    if ($Silent) { return }
    Write-Host ''
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  $title" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

# ========================================================================
# Result collector
# ========================================================================
$Script:Results = @{ timestamp = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'); checks = @{}; issues = @(); summary = '' }
function Add-Result($name, $status, $detail, [array]$hints = @()) {
    $Script:Results.checks[$name] = @{ status = $status; detail = $detail; hints = $hints }
    if ($status -in @('error','warn')) { $Script:Results.issues += "[$name] $detail" }
}

# ========================================================================
# Commands
# ========================================================================

function Invoke-Status {
    Write-Title 'Client Quick Status'

    # Godot
    $procs = Get-Process | Where-Object { $_.ProcessName -like '*godot*' }
    if ($procs) {
        Write-Ok "Godot running: $($procs.Count) instance(s)"
        foreach ($p in $procs) {
            Write-Dim "PID=$($p.Id) RAM=$([math]::Round($p.WorkingSet64/1MB,1))MB"
        }
    } else {
        Write-Warn 'Godot editor not running'
    }

    # Network
    try {
        $tcp = New-Object System.Net.Sockets.TcpClient
        $tcp.Connect($ServerHost, $ServerPort)
        if ($tcp.Connected) { Write-Ok "Server reachable: ${ServerHost}:${ServerPort}"; $tcp.Close() }
    } catch {
        Write-Err "Server unreachable: ${ServerHost}:${ServerPort}"
    }

    # Build
    $dll = [IO.Path]::Combine($Script:ProjectRoot, '.godot', 'mono', 'temp', 'bin', 'Debug', 'ClinetCSharp.dll')
    if (Test-Path $dll) {
        $ts = (Get-Item $dll).LastWriteTime.ToString('HH:mm:ss')
        Write-Ok "C# built at $ts"
    } else {
        Write-Warn 'C# project not built yet'
    }

    # Proto
    $mismatch = $false
    $protoFiles = @('Common.cs','Game.cs','Gateway.cs','Login.cs','MessageId.cs','Server.cs')
    $serverDir = [IO.Path]::Combine($Script:ServerRoot, 'src', 'GameServer.Proto', 'generated')
    foreach ($f in $protoFiles) {
        $cp = [IO.Path]::Combine($Script:ProjectRoot, 'Scripts', 'protos', $f)
        $sp = [IO.Path]::Combine($serverDir, $f)
        if ((Test-Path $cp) -and (Test-Path $sp)) {
            if ((Get-FileHash $cp -Algorithm SHA256).Hash -ne (Get-FileHash $sp -Algorithm SHA256).Hash) {
                $mismatch = $true; break
            }
        }
    }
    if ($mismatch) { Write-Err 'Proto out of sync with server' }
    else           { Write-Ok  'Proto in sync with server' }

    Write-Host ''
    Write-Dim "Tip: Run '.\cli.ps1 check' for full diagnostics"
    Write-Dim "     Run '.\cli.ps1 help'  for all commands"
}

function Invoke-Check {
    param([switch]$Quick)
    Write-Title 'Client Full Diagnostics'

    # -- 1. Project Structure --
    Write-Info 'Checking project structure...'
    $required = @(
        'project.godot','clinetcsharp.csproj',
        'scenes/login_scene.tscn','scenes/main.tscn','scenes/server_select_scene.tscn',
        'Scripts/NetworkManager.cs','Scripts/LoginScene.cs','Scripts/protos/MessageId.cs'
    )
    $missing = @()
    foreach ($r in $required) {
        if (-not (Test-Path ([IO.Path]::Combine($Script:ProjectRoot, $r)))) { $missing += $r }
    }
    if ($missing.Count -eq 0) { Write-Ok 'All required files present'; Add-Result 'project_structure' 'ok' 'All files present' }
    else { Write-Err "Missing: $($missing -join ', ')"; Add-Result 'project_structure' 'error' "Missing: $($missing -join ', ')" }

    # -- 2. Godot Version --
    if (-not $Quick) {
        Write-Info 'Checking Godot version...'
        $gp = Get-Process | Where-Object { $_.ProcessName -like '*godot*' } | Select-Object -First 1
        $gpath = if ($gp) { $gp.Path } elseif (Test-Path $Script:GodotExe) { $Script:GodotExe } else { $null }
        if ($gpath) {
            try {
                $ver = (& $gpath --version 2>$null) -join ' '
                Write-Ok "Godot $ver"
                Add-Result 'godot_version' 'ok' $ver
            } catch {
                Write-Warn 'Cannot detect Godot version'
                Add-Result 'godot_version' 'warn' 'Cannot detect version'
            }
        } else {
            Write-Warn 'Godot executable not found'
            Add-Result 'godot_version' 'warn' 'Not found'
        }
    }

    # -- 3. .NET SDK --
    if (-not $Quick) {
        Write-Info 'Checking .NET SDK...'
        $dotnet = Get-Command 'dotnet' -ErrorAction SilentlyContinue
        if ($dotnet) {
            $ver = (& dotnet --version 2>$null).Trim()
            Write-Ok ".NET SDK $ver"
            Add-Result 'dotnet_sdk' 'ok' ".NET SDK $ver"
        } else {
            Write-Err '.NET SDK not found'
            Add-Result 'dotnet_sdk' 'error' 'Not found'
        }
    }

    # -- 4. Proto Sync --
    Write-Info 'Checking proto consistency...'
    $protoFiles = @('Common.cs','Game.cs','Gateway.cs','Login.cs','MessageId.cs','Server.cs')
    $serverDir = [IO.Path]::Combine($Script:ServerRoot, 'src', 'GameServer.Proto', 'generated')
    $mismatches = @(); $missingClient = @(); $missingServer = @()
    foreach ($f in $protoFiles) {
        $cp = [IO.Path]::Combine($Script:ProjectRoot, 'Scripts', 'protos', $f)
        $sp = [IO.Path]::Combine($serverDir, $f)
        if (-not (Test-Path $cp)) { $missingClient += $f }
        elseif (-not (Test-Path $sp)) { $missingServer += $f }
        elseif ((Get-FileHash $cp -Algorithm SHA256).Hash -ne (Get-FileHash $sp -Algorithm SHA256).Hash) { $mismatches += $f }
    }
    $allOk = $mismatches.Count -eq 0 -and $missingClient.Count -eq 0 -and $missingServer.Count -eq 0
    if ($allOk) { Write-Ok 'All 6 proto files match'; Add-Result 'proto_sync' 'ok' 'All 6 files match' }
    else {
        $msg = @()
        if ($mismatches.Count -gt 0)   { $msg += "mismatch: $($mismatches -join ', ')" }
        if ($missingClient.Count -gt 0){ $msg += "missing client: $($missingClient -join ', ')" }
        if ($missingServer.Count -gt 0){ $msg += "missing server: $($missingServer -join ', ')" }
        Write-Err ($msg -join '; ')
        Add-Result 'proto_sync' 'error' ($msg -join '; ')
    }

    # -- 5. C# Build --
    Write-Info 'Checking C# build...'
    $dll = [IO.Path]::Combine($Script:ProjectRoot, '.godot', 'mono', 'temp', 'bin', 'Debug', 'ClinetCSharp.dll')
    if (Test-Path $dll) {
        $ts = (Get-Item $dll).LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')
        Write-Ok "Built at $ts"
        Add-Result 'csharp_build' 'ok' "Built at $ts"
    } else {
        Write-Warn 'Project not built yet'
        Add-Result 'csharp_build' 'warn' 'Not built'
    }

    # -- 6. Scenes --
    Write-Info 'Checking scenes...'
    $scenes = @('login_scene.tscn','main.tscn','server_select_scene.tscn','role_select_scene.tscn')
    $sceneDir = [IO.Path]::Combine($Script:ProjectRoot, 'scenes')
    $missingScenes = @()
    foreach ($s in $scenes) { if (-not (Test-Path ([IO.Path]::Combine($sceneDir, $s)))) { $missingScenes += $s } }
    if ($missingScenes.Count -eq 0) { Write-Ok 'All core scenes present'; Add-Result 'scenes' 'ok' 'All present' }
    else { Write-Warn "Missing: $($missingScenes -join ', ')"; Add-Result 'scenes' 'warn' "Missing: $($missingScenes -join ', ')" }

    # -- 7. Network --
    Write-Info "Checking connectivity to ${ServerHost}:${ServerPort}..."
    try {
        $tcp = New-Object System.Net.Sockets.TcpClient
        $tcp.Connect($ServerHost, $ServerPort)
        if ($tcp.Connected) {
            Write-Ok "Connected to ${ServerHost}:${ServerPort}"
            Add-Result 'network' 'ok' "Connected"
            $tcp.Close()
        }
    } catch {
        Write-Err "Cannot connect to ${ServerHost}:${ServerPort}"
        Add-Result 'network' 'error' "Cannot connect" @("Run servercsharp/restart.bat to start server")
    }

    # -- 8. Port Conflicts --
    if (-not $Quick) {
        Write-Info 'Checking port conflicts...'
        $listeners = @()
        try {
            $ns = netstat -ano | Select-String "TCP.*:${ServerPort}.*LISTENING"
            foreach ($line in $ns) {
                $pid = ($line -split '\s+')[-1]
                $proc = Get-Process -Id $pid -ErrorAction SilentlyContinue
                if ($proc) { $listeners += "$($proc.ProcessName)(PID $pid)" }
            }
        } catch {}
        if ($listeners.Count -gt 1) {
            Write-Warn "Multiple processes on port ${ServerPort}: $($listeners -join ', ')"
            Add-Result 'port_conflict' 'warn' "Multiple: $($listeners -join ', ')"
        } elseif ($listeners.Count -eq 1) {
            Write-Ok "Port ${ServerPort}: $($listeners[0])"
            Add-Result 'port_conflict' 'ok' $listeners[0]
        } else {
            Write-Warn "Port ${ServerPort} not listening"
            Add-Result 'port_conflict' 'warn' 'Not listening'
        }
    }

    # -- 9. Server Status --
    if (-not $Quick) {
        Write-Info 'Checking server status...'
        $sd = [IO.Path]::Combine($Script:ServerRoot, 'diagnose.ps1')
        if (Test-Path $sd) {
            try {
                $sj = & $sd -JsonOnly | Select-Object -Last 1
                $sr = $sj | ConvertFrom-Json
                if ($sr.issues.Count -eq 0) { Write-Ok 'Server healthy'; Add-Result 'server_status' 'ok' 'Healthy' }
                else { Write-Warn "$($sr.issues.Count) server issue(s)"; Add-Result 'server_status' 'warn' "$($sr.issues.Count) issues" }
            } catch {
                Write-Warn "Server diagnose failed"
                Add-Result 'server_status' 'warn' 'Check failed'
            }
        } else {
            Write-Info 'Server diagnose script not found'
            Add-Result 'server_status' 'info' 'N/A'
        }
    }

    # -- 10. project.godot --
    if (-not $Quick) {
        Write-Info 'Parsing project.godot...'
        $pg = [IO.Path]::Combine($Script:ProjectRoot, 'project.godot')
        if (Test-Path $pg) {
            $content = Get-Content $pg -Raw
            $mainScene = if ($content -match 'run/main_scene\s*=\s*"([^"]+)"') { $matches[1] } else { 'N/A' }
            $renderer  = if ($content -match 'rendering/renderer/rendering_method\s*=\s*"([^"]+)"') { $matches[1] } else { 'default' }
            Write-Ok "main_scene=$mainScene renderer=$renderer"
            Add-Result 'project_config' 'ok' "main_scene=$mainScene renderer=$renderer"
        }
    }

    # Summary
    $issueCount = $Script:Results.issues.Count
    $Script:Results.summary = if ($issueCount -eq 0) { 'All checks passed. Client healthy.' } else { "Found $issueCount issue(s)." }
    Write-Host ''
    $c = if ($issueCount -eq 0) { 'Green' } else { 'Yellow' }
    Write-Host "  $($Script:Results.summary)" -ForegroundColor $c
    Write-Host ''
}

function Invoke-Clean {
    Write-Title 'Clean Client Cache'
    $targets = @(
        @{ Path = '.godot/mono/temp'; Label = 'Mono build cache' },
        @{ Path = '.godot/imported'; Label = 'Imported assets cache' }
    )
    foreach ($t in $targets) {
        $full = [IO.Path]::Combine($Script:ProjectRoot, $t.Path)
        if (Test-Path $full) {
            try { Remove-Item $full -Recurse -Force; Write-Ok "Cleaned: $($t.Label)" }
            catch { Write-Warn "Failed: $($t.Label)" }
        } else {
            Write-Info "Already clean: $($t.Label)"
        }
    }
    Write-Host ''
    Write-Dim "Next: Run '.\cli.ps1 rebuild' to rebuild"
}

function Invoke-Rebuild {
    Write-Title 'Rebuild C# Project'
    Write-Info 'Building clinetcsharp.csproj...'
    $proc = Start-Process -FilePath 'dotnet' -ArgumentList 'build','clinetcsharp.csproj','-v','q','--nologo' `
        -WorkingDirectory $Script:ProjectRoot -PassThru -Wait -NoNewWindow
    if ($proc.ExitCode -eq 0) {
        Write-Ok 'Build succeeded'
        $dll = [IO.Path]::Combine($Script:ProjectRoot, '.godot', 'mono', 'temp', 'bin', 'Debug', 'ClinetCSharp.dll')
        if (Test-Path $dll) {
            $ts = (Get-Item $dll).LastWriteTime.ToString('HH:mm:ss')
            Write-Dim "Output: ClinetCSharp.dll (built at $ts)"
        }
    } else {
        Write-Err "Build failed (exit code $($proc.ExitCode))"
        Write-Dim "Check output above for compilation errors"
    }
}

function Invoke-SyncProto {
    Write-Title 'Sync Proto Files'
    $protoc = [IO.Path]::Combine($Script:ProtoRoot, 'bin', 'protoc.exe')
    $protoDir = [IO.Path]::Combine($Script:ProtoRoot, 'proto')
    $clientOut = [IO.Path]::Combine($Script:ProjectRoot, 'Scripts', 'protos')
    $serverOut = [IO.Path]::Combine($Script:ServerRoot, 'src', 'GameServer.Proto', 'generated')

    if (-not (Test-Path $protoc)) { Write-Err "protoc.exe not found at $protoc"; return }

    $protoFiles = @('common.proto','game.proto','gateway.proto','login.proto','message_id.proto','server.proto')

    Write-Info 'Generating client C# proto...'
    foreach ($f in $protoFiles) {
        $src = [IO.Path]::Combine($protoDir, $f)
        if (Test-Path $src) {
            & $protoc --proto_path=$protoDir --csharp_out=$clientOut $src 2>$null
            Write-Ok "  Generated $f -> client"
        } else {
            Write-Warn "  Missing: $f"
        }
    }

    Write-Info 'Generating server C# proto...'
    foreach ($f in $protoFiles) {
        $src = [IO.Path]::Combine($protoDir, $f)
        if (Test-Path $src) {
            & $protoc --proto_path=$protoDir --csharp_out=$serverOut $src 2>$null
            Write-Ok "  Generated $f -> server"
        }
    }

    Write-Info 'Verifying sync...'
    $allOk = $true
    $csFiles = @('Common.cs','Game.cs','Gateway.cs','Login.cs','MessageId.cs','Server.cs')
    foreach ($f in $csFiles) {
        $cp = [IO.Path]::Combine($clientOut, $f)
        $sp = [IO.Path]::Combine($serverOut, $f)
        if ((Test-Path $cp) -and (Test-Path $sp)) {
            $ch = (Get-FileHash $cp -Algorithm SHA256).Hash
            $sh = (Get-FileHash $sp -Algorithm SHA256).Hash
            if ($ch -eq $sh) { Write-Ok "  $f matched" }
            else { Write-Err "  $f MISMATCH"; $allOk = $false }
        } else {
            Write-Warn "  $f missing"
            $allOk = $false
        }
    }
    if ($allOk) { Write-Host ''; Write-Ok 'All proto files synchronized!' }
}

function Invoke-Run {
    param([switch]$Play)
    Invoke-Rebuild
    $dll = [IO.Path]::Combine($Script:ProjectRoot, '.godot', 'mono', 'temp', 'bin', 'Debug', 'ClinetCSharp.dll')
    if (-not (Test-Path $dll)) { Write-Err 'Build failed, cannot run'; return }

    $gpath = $Script:GodotExe
    if (-not (Test-Path $gpath)) {
        $gp = Get-Process | Where-Object { $_.ProcessName -like '*godot*' } | Select-Object -First 1
        if ($gp) { $gpath = $gp.Path }
    }
    if (-not (Test-Path $gpath)) { Write-Err 'Godot executable not found'; return }

    if ($Play) {
        Write-Info 'Launching game...'
        Start-Process $gpath "--path `"$Script:ProjectRoot`"" -WindowStyle Normal
    } else {
        Write-Info 'Launching Godot editor...'
        Start-Process $gpath "--editor --path `"$Script:ProjectRoot`"" -WindowStyle Normal
    }
}

function Invoke-Test {
    param([string]$SubCommand)
    $testClientDir = [IO.Path]::Combine($Script:ProjectRoot, 'tools', 'TestClient')
    $testClientExe = [IO.Path]::Combine($testClientDir, 'bin', 'Debug', 'net8.0', 'TestClient.exe')

    # Auto-build TestClient if missing
    if (-not (Test-Path $testClientExe)) {
        Write-Info 'Building TestClient...'
        $proc = Start-Process -FilePath 'dotnet' -ArgumentList 'build','-v','q','--nologo' `
            -WorkingDirectory $testClientDir -PassThru -Wait -NoNewWindow
        if ($proc.ExitCode -ne 0) { Write-Err 'TestClient build failed'; return }
    }

    $baseArgs = "--host $ServerHost --port $ServerPort"
    $cmdArgs = switch ($SubCommand) {
        'ping'   { 'ping' }
        'login'  { "login --username `"$TestUsername`" --password `"$TestPassword`"" }
        'enter'  {
            $a = "enter --username `"$TestUsername`" --password `"$TestPassword`""
            if ($TestRoleName) { $a += " --role-name `"$TestRoleName`"" }
            $a
        }
        'move'   { "move --username `"$TestUsername`" --password `"$TestPassword`" --x $TestX --y $TestY" }
        'gm'     { "gm --username `"$TestUsername`" --password `"$TestPassword`" --cmd `"$TestCmd`"" }
        'bot'    { "bot --username `"$TestUsername`" --password `"$TestPassword`" --duration $TestDuration" }
        default  { '--help' }
    }

    Write-Title "TestClient: $SubCommand"
    $argStr = "$cmdArgs $baseArgs"
    Write-Dim "Command: dotnet run -- $argStr"
    Write-Host ''

    # Run and stream output
    $proc = Start-Process -FilePath 'dotnet' -ArgumentList "run --project `"$testClientDir`" -- $argStr" `
        -WorkingDirectory $testClientDir -PassThru -NoNewWindow
    $proc.WaitForExit()
}

function Show-Help {
    Write-Host ''
    Write-Host '  Client CLI -- Godot Client Toolbox' -ForegroundColor Cyan
    Write-Host ''
    Write-Host '  PROJECT:' -ForegroundColor Yellow
    Write-Host '    status          Quick status overview' -ForegroundColor White
    Write-Host '    check           Full diagnostics' -ForegroundColor White
    Write-Host '    check --quick   Quick diagnostics' -ForegroundColor White
    Write-Host '    clean           Clean Mono cache' -ForegroundColor White
    Write-Host '    rebuild         Force rebuild C#' -ForegroundColor White
    Write-Host '    sync-proto      Regenerate proto (client + server)' -ForegroundColor White
    Write-Host '    run             Build and open editor' -ForegroundColor White
    Write-Host '    run --play      Build and play game' -ForegroundColor White
    Write-Host ''
    Write-Host '  AUTOMATION (no Godot UI):' -ForegroundColor Yellow
    Write-Host '    test ping                           Test server connectivity' -ForegroundColor White
    Write-Host '    test login                          Auto login test' -ForegroundColor White
    Write-Host '    test enter                          Full login -> enter/create flow' -ForegroundColor White
    Write-Host '    test move --x 30 --y 30             Move character' -ForegroundColor White
    Write-Host '    test gm --cmd "additem 1001 10"     Execute GM command' -ForegroundColor White
    Write-Host '    test bot --duration 60              Run a random-move bot' -ForegroundColor White
    Write-Host ''
    Write-Host '  GLOBAL OPTIONS:' -ForegroundColor Yellow
    Write-Host '    --host HOST     Server host (default: 127.0.0.1)' -ForegroundColor DarkGray
    Write-Host '    --port PORT     Server port (default: 8889)' -ForegroundColor DarkGray
    Write-Host '    --username U    Test account username' -ForegroundColor DarkGray
    Write-Host '    --password P    Test account password' -ForegroundColor DarkGray
    Write-Host '    --json          Output JSON for AI' -ForegroundColor DarkGray
    Write-Host '    --silent        Silent mode' -ForegroundColor DarkGray
    Write-Host ''
    Write-Host '  EXAMPLES:' -ForegroundColor Yellow
    Write-Host '    .\cli.ps1 test enter --username test --password test' -ForegroundColor DarkGray
    Write-Host '    .\cli.ps1 test bot --username bot1 --duration 120' -ForegroundColor DarkGray
    Write-Host '    .\cli.ps1 test gm --cmd "additem 1001 10"' -ForegroundColor DarkGray
    Write-Host ''
}

# ========================================================================
# Entry
# ========================================================================
switch ($Command) {
    'status'     { Invoke-Status }
    'check'      { Invoke-Check -Quick:$Quick }
    'clean'      { Invoke-Clean }
    'rebuild'    { Invoke-Rebuild }
    'sync-proto' { Invoke-SyncProto }
    'run'        { Invoke-Run -Play:$Play }
    'test'       { Invoke-Test -SubCommand $TestSub }
    'help'       { Show-Help }
    default      { Show-Help }
}

if ($Json -and $Command -eq 'check') {
    $Script:Results | ConvertTo-Json -Depth 5 -Compress | Write-Output
}
