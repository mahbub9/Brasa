<#
.SYNOPSIS
    Runs a full backup-and-restore drill against the real dev database and
    verifies it actually worked (OPS-12).

.DESCRIPTION
    "Automated backup" only earns the word "tested" if something actually
    restores the backup and checks the data came back -- a backup nobody
    has ever restored is a hope, not a guarantee. This script:

      1. Backs up the source database (backup-database.ps1).
      2. Restores that backup into a scratch database (restore-database.ps1),
         never touching the real database.
      3. Enumerates every table in the source across every schema and
         compares row counts against the restored copy.
      4. Reports PASS/FAIL per table and an overall summary.
      5. Drops the scratch database and, unless -KeepBackup is passed,
         deletes the backup file -- a drill's artifacts shouldn't silently
         accumulate on disk every time this runs.

    A row-count match does not prove byte-for-byte fidelity, but it does
    prove the mechanism end to end: pg_dump can read every schema, the
    dump file is valid, pg_restore can rebuild the schema and reload every
    row, and nothing silently dropped a table along the way. That is
    exactly the failure mode a restore drill exists to catch -- a backup
    command that "succeeds" (exit code 0) while quietly writing an empty
    or truncated file is the realistic way backups go unnoticed as broken
    until the day they're actually needed.

.PARAMETER Container
    The Postgres container name. Defaults to `brasa-postgres`.

.PARAMETER Database
    The source database to drill against. Defaults to `brasa`.

.PARAMETER User
    The Postgres role to connect as. Defaults to `brasa` (superuser).

.PARAMETER KeepBackup
    Keep the backup file this drill produced instead of deleting it
    afterward.

.EXAMPLE
    ./restore-drill.ps1
#>
[CmdletBinding()]
param(
    [string]$Container = 'brasa-postgres',
    [string]$Database = 'brasa',
    [string]$User = 'brasa',
    [switch]$KeepBackup
)

$ErrorActionPreference = 'Stop'
$scratchDatabase = "${Database}_restore_drill"

function Get-TableList {
    param([string]$TargetDatabase)
    $query = "SELECT table_schema || '.' || table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema') ORDER BY 1"
    $output = docker exec $Container psql -U $User -d $TargetDatabase -tAc $query
    if ($LASTEXITCODE -ne 0) { throw "Listing tables in '$TargetDatabase' failed with exit code $LASTEXITCODE" }
    return @($output | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
}

function Get-RowCount {
    param([string]$TargetDatabase, [string]$Table)
    $output = docker exec $Container psql -U $User -d $TargetDatabase -tAc "SELECT count(*) FROM $Table"
    if ($LASTEXITCODE -ne 0) { throw "Counting rows in '$Table' on '$TargetDatabase' failed with exit code $LASTEXITCODE" }
    return [int]($output.Trim())
}

Write-Host "=== 1/4: Backing up '$Database' ==="
$backupFile = & (Join-Path $PSScriptRoot 'backup-database.ps1') -Container $Container -Database $Database -User $User

Write-Host "`n=== 2/4: Restoring into scratch database '$scratchDatabase' ==="
& (Join-Path $PSScriptRoot 'restore-database.ps1') -BackupFile $backupFile -Container $Container -TargetDatabase $scratchDatabase -User $User

Write-Host "`n=== 3/4: Comparing row counts, source vs restored ==="
$sourceTables = Get-TableList -TargetDatabase $Database
$restoredTables = Get-TableList -TargetDatabase $scratchDatabase

$missing = $sourceTables | Where-Object { $_ -notin $restoredTables }
$results = @()
$allPassed = $true

foreach ($table in $sourceTables) {
    if ($table -in $missing) {
        $results += [pscustomobject]@{ Table = $table; Source = 'n/a'; Restored = 'MISSING'; Status = 'FAIL' }
        $allPassed = $false
        continue
    }

    $sourceCount = Get-RowCount -TargetDatabase $Database -Table $table
    $restoredCount = Get-RowCount -TargetDatabase $scratchDatabase -Table $table
    $status = if ($sourceCount -eq $restoredCount) { 'PASS' } else { 'FAIL' }
    if ($status -eq 'FAIL') { $allPassed = $false }

    $results += [pscustomobject]@{ Table = $table; Source = $sourceCount; Restored = $restoredCount; Status = $status }
}

$results | Format-Table -AutoSize

Write-Host "`n=== 4/4: Cleaning up ==="
docker exec $Container psql -U $User -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS $scratchDatabase" | Out-Null
Write-Host "Dropped scratch database '$scratchDatabase'."

if ($KeepBackup) {
    Write-Host "Kept backup file: $backupFile"
} else {
    Remove-Item $backupFile -Force
    Write-Host "Deleted backup file: $backupFile"
}

if ($allPassed) {
    Write-Host "`nRESTORE DRILL PASSED -- $($results.Count) tables checked, all row counts matched." -ForegroundColor Green
    exit 0
} else {
    $failedCount = ($results | Where-Object { $_.Status -eq 'FAIL' }).Count
    Write-Host "`nRESTORE DRILL FAILED -- $failedCount of $($results.Count) tables did not match." -ForegroundColor Red
    exit 1
}
