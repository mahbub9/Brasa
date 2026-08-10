# Backup and restore

> **Status:** the mechanism is built and drilled (OPS-12). Nothing runs it on
> a schedule yet — there is no production deployment to schedule it in
> (OPS-11), so this is run on demand today, the same "ship the seam ahead of
> the trigger" shape as [OpenTelemetry](../product/status.md#infrastructure)
> (OPS-08) and [error tracking](../product/status.md#infrastructure) (OPS-14).

## Why "tested," not just "automated"

A backup nobody has ever restored is a hope, not a guarantee — the failure
mode that actually bites is a backup command that exits `0` while quietly
writing an empty or truncated file, which looks completely fine right up
until the day it's needed. `infra/scripts/restore-drill.ps1` exists
specifically to catch that: it doesn't just take a backup, it restores that
exact backup into a scratch database and compares row counts against the
source, table by table, across every schema.

A row-count match doesn't prove byte-for-byte fidelity, but it does prove
the whole mechanism end to end: `pg_dump` can read every schema, the dump
file is valid, `pg_restore` can rebuild the schema and reload every row, and
nothing silently dropped a table along the way.

## Scripts

All three live in `infra/scripts/` and are PowerShell (this project's
primary dev environment is Windows PowerShell 5.1 — see the root
`CLAUDE.md`). Run them from the repository root, or anywhere — each resolves
its own paths relative to `$PSScriptRoot`.

| Script | Purpose |
|---|---|
| `backup-database.ps1` | `pg_dump`s the live database (custom format) via `docker exec`, then `docker cp`s the result out to `infra/backups/` (gitignored) |
| `restore-database.ps1` | Restores a `.dump` file into a target database — **defaults to a `_restore_drill`-suffixed scratch database, never the real one**, unless explicitly overridden |
| `restore-drill.ps1` | Orchestrates all of the above: backup → restore into scratch → compare every table's row count → report PASS/FAIL → drop the scratch database → delete the backup file (unless `-KeepBackup`) |

```powershell
# The actual drill — this is the one to run.
.\infra\scripts\restore-drill.ps1

# Just a backup, kept on disk.
.\infra\scripts\backup-database.ps1

# Restore a specific backup into the real database (careful — this replaces
# today's data). restore-database.ps1 warns loudly when TargetDatabase is
# "brasa".
.\infra\scripts\restore-database.ps1 -BackupFile infra\backups\brasa-20260810-163955.dump -TargetDatabase brasa
```

## Why `docker cp`, never a PowerShell redirect

`pg_dump`'s custom format (`-F c`) is binary. Windows PowerShell 5.1's `>`
and `Out-File` default to UTF-16LE (or UTF-8-with-BOM, depending on host
config) for anything they treat as text — piping a binary `pg_dump` stream
through either would silently corrupt it. Both scripts instead write the
dump to a file *inside* the Postgres container's own filesystem via
`docker exec`, then move it to the host with `docker cp`, which copies bytes
exactly. The same reasoning is why the scripts themselves contain no
non-ASCII characters (not even a typographic em dash) — Windows PowerShell
5.1 reads a `.ps1` file with no BOM using the system codepage, not UTF-8;
this project's own first attempt at these scripts hit exactly that
corruption live (an em dash mis-decoded into three garbage characters, one
of which broke string parsing) before this rule was written down.

## Verified live

`restore-drill.ps1` has been run against the real dev database, more than
once, cleanly:

```
=== 3/4: Comparing row counts, source vs restored ===

Table                            Source Restored Status
-----                            ------ -------- ------
catalog.__ef_migrations_history       7        7 PASS
catalog.menu_categories               4        4 PASS
catalog.menu_items                  505      505 PASS
catalog.modifier_groups               3        3 PASS
catalog.modifiers                     7        7 PASS
floor.__ef_migrations_history         2        2 PASS
floor.rooms                           2        2 PASS
floor.tables                         16       16 PASS
ordering.__ef_migrations_history      7        7 PASS
ordering.order_line_modifiers      1295     1295 PASS
ordering.order_lines               4497     4497 PASS
ordering.orders                    3508     3508 PASS

RESTORE DRILL PASSED -- 12 tables checked, all row counts matched.
```

The failure paths were verified too, deliberately, not just assumed
correct by reading the code: two scratch databases seeded with a mismatched
row count, and a table present in one but not the other, were compared
using the same query shape the script uses — both were correctly flagged
(`FAIL` on the count, `MISSING` on the absent table) before this page was
written.

## What this doesn't cover yet

- **No schedule.** There is nothing to schedule it *in* — no production
  deployment (OPS-11) and no job runner (OPS-10, itself gated on having a
  real recurring job to run). Once either exists, `restore-drill.ps1` (or
  just `backup-database.ps1` for routine backups, with the drill run
  periodically rather than on every backup) is what a cron entry or
  Hangfire recurring job would call.
- **No off-host storage.** Backups land in `infra/backups/` on the same
  machine as the database they're backing up — fine for proving the
  mechanism works, not a real disaster-recovery posture. Off-host storage
  (object storage, a second machine) is a production-deployment concern,
  not a local-dev one.
- **No point-in-time recovery.** This is a full logical dump/restore, not
  WAL archiving — restoring recovers the database as of the moment the
  backup was taken, not to an arbitrary point in between.
- **Row counts, not row contents.** A count match doesn't rule out (for
  example) a column silently truncated during restore. Good enough to
  catch the realistic failure mode named above; not a substitute for
  restoring into a real environment and smoke-testing the app against it
  before trusting a specific backup under real pressure.
