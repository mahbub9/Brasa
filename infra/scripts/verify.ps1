<#
.SYNOPSIS
  Runs the same checks .github/workflows/ci.yml runs, locally, before a
  commit -- so a build, test, or OpenAPI-drift failure is caught here,
  not discovered later on GitHub Actions.

.DESCRIPTION
  CAT-02, then ORD-07/08/09, then CAT-17 all landed with docs/openapi/v1.json
  left stale -- the "regenerate by hand" step docs/openapi/README.md
  documents was skipped three commits in a row, and CI's own openapi-drift
  job (API-14) has been red since. This script exists so that check runs
  locally before a commit, not only after a push.

  Mirrors three of ci.yml's four jobs as closely as a single developer
  machine reasonably can:
    - Build and test      dotnet build/test, Release, matching CI exactly
    - OpenAPI drift        regenerate docs/openapi/v1.json from the live
                            API (API-14's own check) and diff against the
                            committed copy
    - Vulnerable packages  dotnet list package --vulnerable --include-transitive

  E2E (Playwright) is NOT run by default -- it takes a few minutes and
  needs Docker plus two dev servers up. Pass -IncludeE2E to run it too.

.PARAMETER IncludeE2E
  Also runs the full Playwright suite (src/web/e2e), same as CI's e2e job.

.EXAMPLE
  infra/scripts/verify.ps1
  infra/scripts/verify.ps1 -IncludeE2E
#>
param(
    [switch]$IncludeE2E
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $repoRoot

$failed = $false
$apiProcess = $null

function Invoke-Step {
    param([string]$Name, [scriptblock]$Action)

    Write-Host ""
    Write-Host "=== $Name ===" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $Name" -ForegroundColor Red
        $script:failed = $true
    }
}

function Stop-ApiIfRunning {
    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    }
}

try {
    Invoke-Step 'Restore' { dotnet restore Brasa.slnx }

    # TreatWarningsAsErrors is on solution-wide -- this is also the
    # analyzer gate and the transitive-vulnerability gate, same as CI.
    Invoke-Step 'Build (Release)' { dotnet build Brasa.slnx --no-restore --configuration Release }

    Invoke-Step 'Test' { dotnet test Brasa.slnx --no-build --configuration Release }

    if (-not $failed) {
        Write-Host ""
        Write-Host "=== OpenAPI drift (API-14) ===" -ForegroundColor Cyan

        dotnet build Brasa.slnx --configuration Debug | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Host "FAILED: Build (Debug, for the OpenAPI check)" -ForegroundColor Red
            $failed = $true
        }
        else {
            $env:ASPNETCORE_ENVIRONMENT = 'Development'
            $apiProcess = Start-Process -FilePath 'dotnet' `
                -ArgumentList 'run', '--project', 'src/backend/Brasa.Api', '--no-build' `
                -PassThru -WindowStyle Hidden
            Remove-Item Env:\ASPNETCORE_ENVIRONMENT

            $ready = $false
            for ($i = 0; $i -lt 30; $i++) {
                Start-Sleep -Seconds 1
                try {
                    $response = Invoke-WebRequest -Uri 'http://localhost:5216/health' -UseBasicParsing -TimeoutSec 2
                    if ($response.StatusCode -eq 200) { $ready = $true; break }
                }
                catch { }
            }

            if (-not $ready) {
                Write-Host "FAILED: API never became healthy within 30s." -ForegroundColor Red
                $failed = $true
            }
            else {
                $doc = Invoke-RestMethod http://localhost:5216/openapi/v1.json
                $doc.PSObject.Properties.Remove('servers')
                $liveJson = $doc | ConvertTo-Json -Depth 100
                $tempFile = Join-Path $env:TEMP 'brasa-openapi-live.json'
                [System.IO.File]::WriteAllText($tempFile, $liveJson, (New-Object System.Text.UTF8Encoding $false))

                $committed = Get-Content 'docs/openapi/v1.json' -Raw
                $live = Get-Content $tempFile -Raw
                if ($committed -ne $live) {
                    Write-Host "FAILED: docs/openapi/v1.json is out of date." -ForegroundColor Red
                    Write-Host "Regenerate it (see docs/openapi/README.md) and commit the result in the same commit as the endpoint change that caused this drift." -ForegroundColor Yellow
                    $failed = $true
                }
                else {
                    Write-Host "OK: docs/openapi/v1.json matches the live API." -ForegroundColor Green
                }
            }

            Stop-ApiIfRunning
        }
    }

    Invoke-Step 'Vulnerable package scan' { dotnet list Brasa.slnx package --vulnerable --include-transitive }

    if ($IncludeE2E) {
        Invoke-Step 'E2E (Playwright)' {
            Push-Location 'src/web/e2e'
            try { npx playwright test } finally { Pop-Location }
        }
    }
}
finally {
    Stop-ApiIfRunning
}

Write-Host ""
if ($failed) {
    Write-Host "Verification FAILED -- see above." -ForegroundColor Red
    exit 1
}
else {
    Write-Host "Verification passed." -ForegroundColor Green
    exit 0
}
