# Error code registry

> **This file is the contract.** `Error.Code` is public API — every mobile
> and web client branches on these strings (`docs/architecture/api-contract.md`,
> hard rule 11 in `docs/ai/README.md`). Once a code ships, its **meaning**
> (what triggered it) and its **type** (which decides the HTTP status) never
> change. A genuine breaking change is a new code, never an edit to an old
> one's row here.

`tests/Brasa.Shared.Tests/ErrorCodeRegistryTests.cs` (API-04) enforces this
mechanically: it scans every `Error.Validation/NotFound/Conflict/Forbidden/Failure(...)`
call site under `src/` and fails if the scanned codes and this table ever
disagree — a code removed or renamed in source, a code that changed `Type`
(and therefore HTTP status), or a new code added here that doesn't fail
without a matching source change. Update this table in the **same commit**
as any change to an error code, the same rule as everything else in
`docs/development/documentation.md`.

| Code | Type | HTTP | Meaning |
|---|---|---|---|
| `catalog.invalid_allergen` | Validation | 400 | `PUT /menu/items/{id}/details`'s `allergens` contains a name that isn't a recognised `Allergen`. |
| `catalog.item_not_found` | NotFound | 404 | The menu item id in the request doesn't exist (or is soft-deleted — CAT-18). |
| `catalog.item_unavailable` | Conflict | 409 | The menu item exists but is currently 86'd (`IsAvailable = false`). |
| `catalog.modifier_not_found` | NotFound | 404 | A `selectedModifierIds` entry doesn't belong to any of the item's modifier groups. |
| `catalog.modifier_selection_invalid` | Validation | 400 | A modifier group's selection count is outside its `MinSelect`/`MaxSelect` range. |
| `client.header_required` | Validation | 400 | `GET /client-requirements` was called with no (or a malformed) `X-Brasa-Client` header. |
| `client.unknown_client_id` | NotFound | 404 | `GET /client-requirements`'s `X-Brasa-Client` header names a client id with no configured version policy. |
| `fiscal.no_lines` | Validation | 400 | `IssueSimplifiedInvoiceAsync` was called with an empty line list. |
| `floor.table_not_dirty` | Conflict | 409 | `POST /tables/{id}/clear` was called on a table that isn't `Dirty`. |
| `floor.table_not_free` | Conflict | 409 | `POST /orders` or `POST /orders/{id}/transfer` targeted a table that isn't `Free` — including the `xmin` concurrency-token case where two requests raced for it. |
| `floor.table_not_found` | NotFound | 404 | The table id in the request doesn't exist. |
| `floor.table_not_occupied` | Conflict | 409 | `RequestBill`/`MarkDirty`/`Release` was called on a table that isn't `Occupied` (`RequestBill` has no endpoint yet — domain-only). |
| `order.already_closed` | Conflict | 409 | `Close()` was called on an order that is already `Closed`. |
| `order.empty` | Validation | 400 | `Close()` or `EnsureCanGeneratePreBill()` was called on an order with zero lines. |
| `order.invalid_cover_count` | Validation | 400 | `POST /orders`'s `coverCount` is less than 1. |
| `order.invalid_cursor` | Validation | 400 | `GET /orders`'s `cursor` query parameter isn't a token `X-Next-Cursor` (API-09) produced. |
| `order.invalid_merge_target` | Validation | 400 | `POST /orders/{id}/merge`'s `secondaryOrderId` is the same as the primary order. |
| `order.invalid_quantity` | Validation | 400 | An order line's `quantity` is less than 1. |
| `order.invalid_split` | Validation | 400 | `SplitEvenly`'s `parts` is less than 1; `SplitByItem()`'s groups are empty, a group has no lines, or a line's quantity isn't allocated exactly once across the groups; or `SplitByCover()`'s cover groups are empty, contain a group below 1 cover, or don't sum to the order's `CoverCount`. |
| `order.invalid_status_filter` | Validation | 400 | `GET /orders`'s `status` query parameter isn't a recognised `OrderStatus` value. |
| `order.invalid_take` | Validation | 400 | `GET /orders`'s `take` query parameter is outside 1–200. |
| `order.invalid_transfer_target` | Validation | 400 | `POST /orders/{id}/lines/{lineId}/transfer`'s `destinationOrderId` is the same as the source order. |
| `order.line_not_found` | NotFound | 404 | `SetLineNotes()`, `DetachLine()` or `SplitByItem()`'s `lineId` doesn't belong to the order. |
| `order.not_empty` | Validation | 400 | `MarkMerged()` was called on an order that still has lines. |
| `order.not_found` | NotFound | 404 | The order id in the request doesn't exist. |
| `order.not_open` | Conflict | 409 | `AddLine()`, `EnsureCanGeneratePreBill()`, `SetLineNotes()`, `TransferToTable()`, `DetachLine()`, `ReceiveLine()` or `MarkMerged()` was called on an order that isn't `Open`. |
| `order.notes_too_long` | Validation | 400 | `SetLineNotes()`'s `notes` is over 300 characters. |
| `request.idempotency_key_required` | Validation | 400 | A mutating `/api` request had no `Idempotency-Key` header. |

## Retired codes

None yet. When a code is genuinely retired (the operation it described no
longer exists), move its row here with the date and why, rather than
deleting it outright — a code disappearing from git history silently is
exactly the kind of drift this registry exists to prevent.
