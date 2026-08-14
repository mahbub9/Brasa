# Backup and restore

> **Status:** the mechanism is built, drilled, and now scheduled (OPS-12,
> OPS-10). `DatabaseBackupJob` (`src/backend/Brasa.Api/Jobs/`) wraps
> `backup-database.ps1`/`restore-drill.ps1` as two Hangfire recurring jobs —
> a nightly backup, a weekly full drill — visible and manually triggerable
> from `/hangfire` outside Production. Both scripts still also work run by
> hand, exactly as documented below.

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
its own paths relative to `$PSScriptRoot`, **except** when a script is
launched via `-File` from a redirected, non-interactive process (exactly how
`DatabaseBackupJob` invokes them, and reproducible with a plain
`powershell.exe -File ... 2>&1` from any non-interactive shell, no Hangfire
involved) — Windows PowerShell 5.1 leaves `$PSScriptRoot` empty specifically
inside that script's own `param()` block in that one launch style, though it
resolves correctly everywhere else (the script body, and any script it goes
on to invoke via `&`). `backup-database.ps1`'s own `-OutputDir` default hit
this; `DatabaseBackupJob` works around it by passing `-OutputDir` explicitly
rather than relying on the default, so the script itself needed no change.

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

**`hangfire.*` is excluded from the row-count comparison, on purpose.**
Once OPS-10 wired Hangfire in, its own tables (`hangfire.lock`,
`hangfire.server`, and friends) became the first source of writes in this
database that happen continuously, independent of the drill's own
timing — the job scheduler, distributed locks, and server heartbeats keep
mutating them every few seconds regardless of any tenant traffic. A
row-count comparison assumes the source is quiescent between "back it up"
and "count it again for comparison"; true for every tenant-data table
(this drill runs at 3am UTC via its own recurring job, when there's no
tenant traffic to race against), never true for Hangfire's own operational
state. That's fine to exclude, not just convenient: a lost lock row or
stale heartbeat after a real restore isn't a disaster-recovery failure the
way losing tenant data would be — it gets re-acquired or re-sent on its
own. Confirmed live: `hangfire.lock` genuinely mismatched (0 vs. 1 row) on
one real drill run before this exclusion existed, while all 34 other
tables matched exactly — proof the mechanism itself was never broken, only
the comparison's assumption that nothing else in the database moves.

**Scheduled via Hangfire now, both jobs manually triggered end to end
through the running server** (`RecurringJob.TriggerJob`, not just the
underlying scripts run by hand) — a real ~1MB backup file produced by
`nightly-database-backup`, a real drill passing clean via
`weekly-restore-drill`.

## What this doesn't cover yet

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
