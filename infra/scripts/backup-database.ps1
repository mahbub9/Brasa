<#
.SYNOPSIS
    Backs up the Brasa PostgreSQL database (OPS-12).

.DESCRIPTION
    Runs pg_dump inside the running `brasa-postgres` container (custom
    format -- compressed, and the only format pg_restore can selectively
    restore from later) and copies the resulting file out to the host via
    `docker cp`. Deliberately never pipes the dump through a PowerShell
    text redirection: pg_dump's custom format is binary, and Windows
    PowerShell's `>`/Out-File default to UTF-16LE (or UTF-8-with-BOM,
    depending on host config) for anything it treats as text, which would
    silently corrupt a binary stream. `docker cp` moves bytes exactly, so
    the dump is round-tripped through the container's own filesystem
    instead of ever touching a PowerShell pipe.

    This is the mechanism a scheduled backup would call once real
    production infrastructure exists (OPS-11/OPS-10) -- the "automated" in
    this task's own title describes the trigger that would invoke this
    script on a timer, not something this script does for itself. Today
    it's run on demand, same as restore-drill.ps1 already does every time
    it's invoked.

.PARAMETER Container
    The Postgres container name. Defaults to `brasa-postgres`, matching
    infra/docker-compose.yml.

.PARAMETER Database
    The database to back up. Defaults to `brasa`.

.PARAMETER User
    The Postgres role to connect as. Defaults to `brasa` (the migration
    /superuser role -- pg_dump needs to read every schema, which
    `brasa_app`'s RLS-restricted access does not guarantee it can do).

.PARAMETER OutputDir
    Where to write the backup file on the host. Defaults to
    infra/backups (gitignored -- these are data, not source).

.OUTPUTS
    The full path to the backup file written, so callers (restore-drill.ps1)
    can chain off it directly.

.EXAMPLE
    ./backup-database.ps1
    Backs up brasa-postgres:brasa to infra/backups/brasa-<timestamp>.dump
#>
[CmdletBinding()]
param(
    [string]$Container = 'brasa-postgres',
    [string]$Database = 'brasa',
    [string]$User = 'brasa',
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\backups')
)

$ErrorActionPreference = 'Stop'

$resolvedOutputDir = New-Item -ItemType Directory -Force -Path $OutputDir
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$fileName = "$Database-$timestamp.dump"
$containerPath = "/tmp/$fileName"
$hostPath = Join-Path $resolvedOutputDir.FullName $fileName

Write-Host "Backing up '$Database' from container '$Container' (as '$User')..."
docker exec $Container pg_dump -U $User -d $Database -F c -f $containerPath
if ($LASTEXITCODE -ne 0) {
    throw "pg_dump failed with exit code $LASTEXITCODE"
}

docker cp "${Container}:${containerPath}" $hostPath
if ($LASTEXITCODE -ne 0) {
    throw "docker cp failed with exit code $LASTEXITCODE"
}

docker exec $Container rm $containerPath | Out-Null

$sizeKb = [math]::Round((Get-Item $hostPath).Length / 1KB, 1)
Write-Host "Backup written to $hostPath ($sizeKb KB)"

return $hostPath
