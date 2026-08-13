# Menu bulk import (CSV / Excel)

> **Status:** ✅ built
> **Module:** Catalog
> **Roadmap:** I0 (pulled forward)

## What it is

Staff can create many menu items at once from a spreadsheet — a CSV file
or a real Excel (`.xlsx`) workbook — instead of one item at a time. This
is also the *only* way to create a menu item at all today: no
"create item" form or endpoint exists, admin included.

## Why it works this way

**One row-processing pipeline, two file formats.** `POST /menu/items/import`
(CSV) and `POST /menu/items/import/excel` (`.xlsx`) both turn their file
into the identical `IReadOnlyList<IReadOnlyList<string>>` row shape —
`CsvParser` for CSV, `ExcelImportParser` (backed by `ExcelDataReader`,
reading only the first worksheet) for Excel — then hand off to one
shared `ImportRowsAsync`. Every validation rule, every error code, and
the per-row-independence guarantee live in exactly one place; the two
endpoints differ only in how they get from "uploaded file" to "rows."

**Rows import independently.** An unknown category or an unparsable
price is reported against that row and skipped — it doesn't fail the
whole file. A restaurant's real menu spreadsheet, hand-edited over
years, will have typos; failing the entire import over one bad row would
make this feature actively hostile to the exact data it exists to
import.

**Create-only, not upsert.** Importing the same file twice creates
duplicates rather than updating existing items by name. Matching an
import row to an existing item would need a stable identity column this
format doesn't have (a name isn't safe — two categories can share an
item name), so this was left as a known, named gap rather than guessed
at.

**`ExcelDataReader`, not ClosedXML or NPOI.** The first attempt used
ClosedXML, which pulls in `SSH.NET` 2025.1.0 transitively — a high-
severity CVE (GHSA-q939-rpr3-3284) the zero-warning build gate correctly
refused. `ExcelDataReader` was chosen specifically because this feature
only ever *reads* Excel, never writes it, so a read-only library with a
much smaller dependency surface is the right tool, not a workaround.

**A blank row is skipped, never reported as an error.** Real spreadsheets
routinely carry trailing blank rows a user never notices. `ExcelImportParser`
skips a row where every cell is blank, the same convention `CsvParser`
already applied to blank CSV lines — treating it as invalid would
surface a confusing error for something the user never intentionally
put there.

## Behaviour

1. Staff picks a `.csv` or `.xlsx` file in `admin`'s single import
   control. The file extension (not the browser's declared MIME type,
   which varies for `.xlsx`) decides which endpoint it goes to.
2. The header row is matched against `CategoryName`, `Name`, `Price`,
   `VatRate` (required) and optionally `Description`, `IsAlcoholic`.
   Missing a required column rejects the whole file before any row is
   processed.
3. Each data row either creates a `MenuItem` or is skipped with a
   per-row error naming the bad value (`"not-a-number" is not a valid
   price`, `Unknown category "..."`).
4. The response reports how many were created and lists every skipped
   row's number (1-indexed against the data rows, header excluded) and
   message.
5. `admin`'s menu screen refetches and shows the new items immediately,
   no reload.

## Offline behaviour

Not applicable — cloud API endpoint, no offline path today.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| CSV/Excel file has no rows at all | Rejected, nothing created | `400 catalog.import_empty` |
| Header is missing a required column | Rejected, nothing created | `400 catalog.import_invalid_header` |
| Uploaded file isn't `.xlsx`, or is a corrupt/non-Excel file | Rejected, nothing created | `400 catalog.import_invalid_file` |
| A data row names an unknown category | That row skipped, rest continue | Reported in `errors` with the row number |
| A data row's price/VAT rate doesn't parse | That row skipped, rest continue | Reported in `errors` with the row number |

## Data

No new persistent state — every successfully-imported row is an
ordinary `MenuItem` (Catalog module), created the same way any other
`MenuItem` is. Nothing tracks that a given item came from an import.

## API

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/menu/items/import` | Bulk-create menu items from a CSV file (`{ "csv": "..." }`) |
| `POST` | `/menu/items/import/excel` | Bulk-create menu items from an uploaded `.xlsx` file (`multipart/form-data`) |

Both take `Idempotency-Key` like every other mutation and return the
same `ImportMenuItemsResponse` (`created`, `errors[]`). The Excel route
carries `.DisableAntiforgery()` — see
[menu item photos & details](menu-item-photos-and-details.md)'s own
remarks on why any `IFormFile`-binding endpoint here needs it.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

None. Purely catalog data creation, no fiscal document involved.

## Permissions

None enforced yet — same "ships ahead of manager authorisation" shape as
repricing (CAT-19), 86-ing (CAT-13) and category visibility (CAT-01).

## Testing

`menu-import.spec.ts` — a 4-row CSV with 2 valid and 2 invalid rows
creates exactly 2 items (confirmed on the real menu afterward, not just
the import receipt) and reports the other 2 by row number with the bad
value named; an empty CSV and a header missing a required column both
`400`.

`menu-import-excel.spec.ts` — the same 2-valid/2-invalid-row proof
against a real `.xlsx` built at test time via `exceljs` (a devDependency
of the E2E suite only, never a committed binary fixture — the file can
never go stale), plus a wholly-blank row confirmed skipped rather than
reported; an empty file, a non-`.xlsx` file, a genuinely corrupt
`.xlsx`, and a missing-header-column file each rejected with their own
code; the `admin` UI imports a real `.xlsx` end to end.

## Open questions

- No upsert — a second import of the same file (or a corrected version
  of one) creates duplicates rather than updating in place. Matching
  rows to existing items needs a stable identity column this format
  doesn't have today.
- No column for `Course`, `Station`, `Schedule`, `IsCouvert`, image, or
  any of the other per-item attributes CAT-11/12/14/15 and the photo
  upload feature later added — those are all set one item at a time
  after import.
