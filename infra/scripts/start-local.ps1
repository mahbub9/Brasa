<#
.SYNOPSIS
  One-click local startup: API (in-memory database, ADR 0012) + both web
  clients (pos, admin), no Docker/PostgreSQL required.

.DESCRIPTION
  Starts three long-running processes -- the API under the "beta" launch
  profile (Properties/launchSettings.json: Database__Provider=InMemory,
  Database__SeedOnStartup=true, ASPNETCORE_ENVIRONMENT=Development), and the
  pos/admin Vite dev servers -- waits for each to answer before moving on,
  optionally opens a browser tab per client, then blocks until you press
  Enter, at which point all three are stopped together.

  This is the local-machine equivalent of docs/development/deployment.md's
  "Path A -- beta pilot": zero external infrastructure, data lives in an
  in-process SQLite :memory: store and is lost the moment the API process
  stops. That is the point -- a throwaway sandbox to click through the app
  in a real browser, not a place to leave real data.

  Re-running this script is safe: if something is already answering on a
  given port (e.g. a previous run you never stopped), that service is left
  alone rather than started a second time.

.PARAMETER NoBrowser
  Don't automatically open browser tabs for pos/admin once they're up.

.PARAMETER SkipInstall
  Don't run `npm install` even if node_modules is missing for pos/admin --
  fails fast with whatever error npm run dev itself produces instead.

.PARAMETER NoWait
  Start everything, print the summary, then exit immediately instead of
  blocking on Enter -- the three processes keep running unattended. Use
  this if you're driving the script from another tool, or you'd rather stop
  things later with -Stop. Without it, closing/interrupting this script
  stops everything it started.

.PARAMETER Stop
  Don't start anything -- stop whatever is currently listening on the
  api/pos/admin ports (5216/5173/5174) and exit. Works regardless of which
  invocation of this script (or `dotnet run`/`npm run dev` run by hand)
  started them, since it identifies processes by port, not by PID this
  script happens to remember.

.EXAMPLE
  infra/scripts/start-local.ps1
  infra/scripts/start-local.ps1 -NoBrowser
  infra/scripts/start-local.ps1 -NoWait
  infra/scripts/start-local.ps1 -Stop
#>
param(
    [switch]$NoBrowser,
    [switch]$SkipInstall,
    [switch]$NoWait,
    [switch]$Stop
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $repoRoot

$logDir = Join-Path $env:TEMP 'brasa-local'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

$apiPort = 5216
$posPort = 5173
$adminPort = 5174
$apiUrl = "http://localhost:$apiPort"
$posUrl = "http://localhost:$posPort"
$adminUrl = "http://localhost:$adminPort"

$tracked = @()   # {Name, Port} this run actually started -- only these get stopped on exit

function Test-Endpoint {
    param([string]$Url)
    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 2
        return $response.StatusCode -ge 200 -and $response.StatusCode -lt 400
    }
    catch {
        return $false
    }
}

function Wait-ForEndpoint {
    param([string]$Name, [string]$Url, [int]$TimeoutSeconds, [System.Diagnostics.Process]$Process)

    $attempts = [Math]::Ceiling($TimeoutSeconds / 2)
    for ($i = 0; $i -lt $attempts; $i++) {
        if ($null -ne $Process -and $Process.HasExited) {
            throw "$Name exited before it ever answered on $Url (exit code $($Process.ExitCode)) -- check the logs in $logDir."
        }
        if (Test-Endpoint $Url) {
            return
        }
        Start-Sleep -Seconds 2
    }
    throw "$Name never answered on $Url within ${TimeoutSeconds}s -- check the logs in $logDir."
}

function Start-TrackedProcess {
    param([string]$Name, [int]$Port, [string]$FilePath, [string[]]$ArgumentList, [string]$WorkingDirectory)

    $outLog = Join-Path $logDir "$Name.out.log"
    $errLog = Join-Path $logDir "$Name.err.log"
    $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList `
        -WorkingDirectory $WorkingDirectory -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput $outLog -RedirectStandardError $errLog
    $script:tracked += [PSCustomObject]@{ Name = $Name; Port = $Port }
    return $process
}

function Ensure-NodeModules {
    param([string]$Path, [string]$Label)

    if ($SkipInstall) { return }
    if (Test-Path (Join-Path $Path 'node_modules')) { return }

    Write-Host "Installing $Label dependencies (first run only)..." -ForegroundColor Cyan
    Push-Location $Path
    try {
        npm install
        if ($LASTEXITCODE -ne 0) {
            throw "npm install failed for $Label -- see output above."
        }
    }
    finally {
        Pop-Location
    }
}

function Stop-ByPort {
    param([int]$Port, [string]$Label)

    # Identifies the real listener by port, not a remembered PID -- `dotnet
    # run` and `npm run dev` (via npm.cmd -> a further node.exe child on
    # Windows) both spawn at least one process below whatever this script's
    # own Start-Process call returns, so the tracked parent PID is never the
    # one actually bound to the port. Walking the process tree to find it
    # was tried first and was unreliable (a multi-hop npm.cmd -> node.exe
    # chain, plus a race against the parent already having exited by the
    # time its children are enumerated); asking Windows directly who holds
    # the port is both simpler and correct by construction.
    $connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if (-not $connections) {
        return
    }
    foreach ($processId in ($connections | Select-Object -ExpandProperty OwningProcess -Unique)) {
        Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
    }
    if ($Label) {
        Write-Host "Stopped $Label (port $Port)."
    }
}

if ($Stop) {
    Write-Host 'Stopping anything on the api/pos/admin ports...' -ForegroundColor Cyan
    Stop-ByPort -Port $apiPort -Label 'api'
    Stop-ByPort -Port $posPort -Label 'pos'
    Stop-ByPort -Port $adminPort -Label 'admin'
    Write-Host 'Done.' -ForegroundColor Green
    exit 0
}

try {
    foreach ($tool in 'dotnet', 'node', 'npm') {
        if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
            throw "'$tool' isn't on PATH -- see docs/development/getting-started.md's Prerequisites."
        }
    }

    Write-Host ''
    Write-Host '=== Backend API (in-memory database) ===' -ForegroundColor Cyan
    if (Test-Endpoint "$apiUrl/health") {
        Write-Host "Already running at $apiUrl -- leaving it alone." -ForegroundColor Yellow
    }
    else {
        $apiProcess = Start-TrackedProcess -Name 'api' -Port $apiPort -FilePath 'dotnet' `
            -ArgumentList @('run', '--project', 'src/backend/Brasa.Api', '--launch-profile', 'beta') `
            -WorkingDirectory $repoRoot
        Write-Host "Starting (first build can take a minute)... logs: $logDir\api.out.log"
        Wait-ForEndpoint -Name 'API' -Url "$apiUrl/health" -TimeoutSeconds 180 -Process $apiProcess
        Write-Host "Up at $apiUrl" -ForegroundColor Green
    }

    Write-Host ''
    Write-Host '=== pos (POS terminal) ===' -ForegroundColor Cyan
    if (Test-Endpoint $posUrl) {
        Write-Host "Already running at $posUrl -- leaving it alone." -ForegroundColor Yellow
    }
    else {
        Ensure-NodeModules -Path (Join-Path $repoRoot 'src\web\pos') -Label 'pos'
        $posProcess = Start-TrackedProcess -Name 'pos' -Port $posPort -FilePath 'npm.cmd' `
            -ArgumentList @('run', 'dev', '--', '--port', "$posPort", '--strictPort') `
            -WorkingDirectory (Join-Path $repoRoot 'src\web\pos')
        Wait-ForEndpoint -Name 'pos' -Url $posUrl -TimeoutSeconds 60 -Process $posProcess
        Write-Host "Up at $posUrl" -ForegroundColor Green
    }

    Write-Host ''
    Write-Host '=== admin (back office) ===' -ForegroundColor Cyan
    if (Test-Endpoint $adminUrl) {
        Write-Host "Already running at $adminUrl -- leaving it alone." -ForegroundColor Yellow
    }
    else {
        Ensure-NodeModules -Path (Join-Path $repoRoot 'src\web\admin') -Label 'admin'
        $adminProcess = Start-TrackedProcess -Name 'admin' -Port $adminPort -FilePath 'npm.cmd' `
            -ArgumentList @('run', 'dev', '--', '--port', "$adminPort", '--strictPort') `
            -WorkingDirectory (Join-Path $repoRoot 'src\web\admin')
        Wait-ForEndpoint -Name 'admin' -Url $adminUrl -TimeoutSeconds 60 -Process $adminProcess
        Write-Host "Up at $adminUrl" -ForegroundColor Green
    }

    if (-not $NoBrowser) {
        Start-Process $posUrl
        Start-Process $adminUrl
    }

    Write-Host ''
    Write-Host '=================================================' -ForegroundColor Green
    Write-Host ' Brasa is running locally (in-memory database).'
    Write-Host " pos:   $posUrl"
    Write-Host " admin: $adminUrl"
    Write-Host " api:   $apiUrl"
    Write-Host " logs:  $logDir"
    Write-Host ''
    Write-Host ' All data lives in memory and is lost when the API' -ForegroundColor Yellow
    Write-Host ' process is stopped -- see ADR 0012.' -ForegroundColor Yellow
    Write-Host '================================================='
    Write-Host ''

    if ($NoWait) {
        if ($tracked.Count -gt 0) {
            Write-Host "Running unattended (-NoWait). Stop later with:"
            Write-Host '  infra/scripts/start-local.ps1 -Stop'
        }
        exit 0
    }

    Read-Host 'Press Enter to stop everything this script started'
}
finally {
    if (-not $NoWait -and $tracked.Count -gt 0) {
        Write-Host ''
        Write-Host 'Stopping...' -ForegroundColor Cyan
        foreach ($entry in $tracked) {
            Stop-ByPort -Port $entry.Port -Label $entry.Name
        }
    }
}
