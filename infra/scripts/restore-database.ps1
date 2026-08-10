<#
.SYNOPSIS
    Restores a Brasa PostgreSQL backup into a (by default, scratch) database (OPS-12).

.DESCRIPTION
    Copies a backup file made by backup-database.ps1 into the Postgres
    container via `docker cp` (binary-safe, same reasoning as that
    script's own doc comment), (re)creates the target database, and runs
    pg_restore into it.

    Defaults to a `_restore_drill` suffixed database name rather than the
    real `brasa` database on purpose -- restoring over the live database
    by default would make this script one typo away from destroying
    today's data. Restoring over the real database is still possible
    (pass -TargetDatabase brasa), but it is an explicit, deliberate choice
    every time, not the default.

.PARAMETER BackupFile
    Path to a .dump file produced by backup-database.ps1.

.PARAMETER Container
    The Postgres container name. Defaults to `brasa-postgres`.

.PARAMETER TargetDatabase
    The database to restore into. Created fresh (dropped first if it
    already exists). Defaults to `brasa_restore_drill`, never the real
    `brasa` database, unless explicitly overridden.

.PARAMETER User
    The Postgres role to connect as. Defaults to `brasa` (superuser --
    needed to CREATE/DROP DATABASE).

.EXAMPLE
    ./restore-database.ps1 -BackupFile infra/backups/brasa-20260810-120000.dump
    Restores into the scratch database brasa_restore_drill.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,

    [string]$Container = 'brasa-postgres',
    [string]$TargetDatabase = 'brasa_restore_drill',
    [string]$User = 'brasa'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $BackupFile)) {
    throw "Backup file not found: $BackupFile"
}

if ($TargetDatabase -eq 'brasa') {
    Write-Warning 'Restoring over the real "brasa" database -- this replaces today''s data.'
}

$fileName = Split-Path $BackupFile -Leaf
$containerPath = "/tmp/$fileName"

Write-Host "Copying $BackupFile into container '$Container'..."
docker cp $BackupFile "${Container}:${containerPath}"
if ($LASTEXITCODE -ne 0) {
    throw "docker cp failed with exit code $LASTEXITCODE"
}

Write-Host "Recreating database '$TargetDatabase'..."
docker exec $Container psql -U $User -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS $TargetDatabase" | Out-Null
docker exec $Container psql -U $User -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE $TargetDatabase" | Out-Null

Write-Host "Restoring into '$TargetDatabase'..."
# --no-owner/--no-privileges: the dump's role-ownership statements name
# roles (brasa_app, migration-time grants) that may not exist yet in a
# brand-new target -- irrelevant to whether the DATA came back intact,
# which is what a restore drill is actually checking.
docker exec $Container pg_restore -U $User -d $TargetDatabase --no-owner --no-privileges $containerPath
$restoreExitCode = $LASTEXITCODE

docker exec $Container rm $containerPath | Out-Null

# pg_restore exits non-zero on some genuinely harmless warnings (e.g. a
# skipped ownership statement) as well as on real failures -- restore-drill.ps1
# is the actual pass/fail judge (it verifies row counts came back), not this
# exit code alone. Still surfaced so a caller running this script standalone
# sees it.
if ($restoreExitCode -ne 0) {
    Write-Warning "pg_restore exited with code $restoreExitCode -- this can be harmless (skipped ownership statements) or a real failure. Verify the restored data before trusting it."
}

Write-Host "Restore into '$TargetDatabase' complete."
