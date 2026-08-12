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
| `catalog.category_not_found` | NotFound | 404 | `PUT /menu/categories/{id}/visibility`'s category id doesn't exist. |
| `catalog.combo_component_exists` | Conflict | 409 | `POST /combos/{id}/components`'s (CAT-10) `menuItemId` already has a component in this combo — remove it first, this endpoint never overwrites. |
| `catalog.combo_has_no_components` | Validation | 400 | `POST /orders/{id}/combo-lines`'s (CAT-10) combo has zero components — nothing to ring up. |
| `catalog.combo_not_found` | NotFound | 404 | The combo id in the request doesn't exist (CAT-10). |
| `catalog.combo_price_not_allocable` | Conflict | 409 | `POST /orders/{id}/combo-lines`'s (CAT-10) combo's components all price at zero, so `Combo.Price` cannot be proportionally allocated across them. |
| `catalog.import_empty` | Validation | 400 | `POST /menu/items/import`'s `csv` has no rows at all (not even a header). |
| `catalog.import_invalid_header` | Validation | 400 | `POST /menu/items/import`'s CSV header is missing a required column (`CategoryName`, `Name`, `Price`, `VatRate`). |
| `catalog.incomplete_schedule` | Validation | 400 | `PUT /menu/items/{id}/schedule`'s (CAT-11) days/start/end are only partly set — a schedule is all-or-nothing, not a partial update. |
| `catalog.incomplete_scheduled_price` | Validation | 400 | `PUT /menu/items/{id}/scheduled-price`'s (CAT-16) `price`/`effectiveFromUtc` are only partly set — both are required together, or both omitted to clear the pending change. |
| `catalog.invalid_allergen` | Validation | 400 | `PUT /menu/items/{id}/details`'s `allergens` contains a name that isn't a recognised `Allergen`. |
| `catalog.invalid_combo_name` | Validation | 400 | `POST /combos`'s (CAT-10) `name` is missing, empty or whitespace. |
| `catalog.invalid_course` | Validation | 400 | `PUT /menu/items/{id}/course`'s `course` isn't a recognised `Course` name. |
| `catalog.invalid_day_of_week` | Validation | 400 | `PUT /menu/items/{id}/schedule`'s (CAT-11) `daysOfWeek` contains a name that isn't a recognised day. |
| `catalog.invalid_schedule` | Validation | 400 | `PUT /menu/items/{id}/schedule`'s (CAT-11) window is empty or backwards (`startTime >= endTime`). |
| `catalog.invalid_scheduled_price_date` | Validation | 400 | `PUT /menu/items/{id}/scheduled-price`'s (CAT-16) `effectiveFromUtc` isn't a valid instant. |
| `catalog.invalid_station` | Validation | 400 | `PUT /menu/items/{id}/station`'s `station` isn't a recognised `KitchenStation` name. |
| `catalog.invalid_price` | Validation | 400 | `PUT /menu/items/{id}/price`'s `price` is negative, or `PUT /menu/items/{id}/takeaway-price`'s (CAT-06) `price` is negative, or `POST /price-lists/{id}/entries`'s (CAT-05) `price` is negative, or `POST /combos`'s (CAT-10) `price` is negative. |
| `catalog.invalid_price_list_name` | Validation | 400 | `POST /price-lists`'s (CAT-05) `name` is missing, empty or whitespace. |
| `catalog.invalid_time` | Validation | 400 | `PUT /menu/items/{id}/schedule`'s (CAT-11) `startTime`/`endTime` isn't a valid `"HH:mm"` time. |
| `catalog.item_not_found` | NotFound | 404 | The menu item id in the request doesn't exist (or is soft-deleted — CAT-18), incl. as referenced by `POST /price-lists/{id}/entries`/`GET /price-lists/{id}/effective-price/{menuItemId}` (CAT-05) or `POST /combos/{id}/components`/`POST /orders/{id}/combo-lines` (CAT-10). |
| `catalog.item_unavailable` | Conflict | 409 | The menu item exists but is currently 86'd (`IsAvailable = false`), incl. as referenced by a combo component (CAT-10). |
| `catalog.modifier_not_found` | NotFound | 404 | A `selectedModifierIds` entry doesn't belong to any of the item's modifier groups. |
| `catalog.modifier_selection_invalid` | Validation | 400 | A modifier group's selection count is outside its `MinSelect`/`MaxSelect` range. |
| `catalog.price_list_entry_exists` | Conflict | 409 | `POST /price-lists/{id}/entries`'s (CAT-05) `menuItemId` already has an entry in this price list — remove it first, this endpoint never overwrites. |
| `catalog.price_list_not_found` | NotFound | 404 | The price list id in the request doesn't exist (CAT-05). |
| `catalog.scheduled_price_not_future` | Validation | 400 | `PUT /menu/items/{id}/scheduled-price`'s (CAT-16) `effectiveFromUtc` is not strictly after the current instant. |
| `client.header_required` | Validation | 400 | `GET /client-requirements` was called with no (or a malformed) `X-Brasa-Client` header. |
| `client.unknown_client_id` | NotFound | 404 | `GET /client-requirements`'s `X-Brasa-Client` header names a client id with no configured version policy. |
| `fiscal.no_lines` | Validation | 400 | `IssueSimplifiedInvoiceAsync` was called with an empty line list. |
| `identity.invalid_organization_name` | Validation | 400 | `POST /organizations`'s (IDN-01) `name` is missing, empty or whitespace. |
| `identity.invalid_region` | Validation | 400 | `POST /organizations/{id}/sites`'s (IDN-01) `region` isn't a recognised `PortugueseRegion` name. |
| `identity.invalid_site_name` | Validation | 400 | `POST /organizations/{id}/sites`'s (IDN-01) `name` is missing, empty or whitespace. |
| `identity.invalid_terminal_label` | Validation | 400 | `POST /sites/{id}/terminals`'s (IDN-01) `label` is missing, empty or whitespace. |
| `identity.organization_not_found` | NotFound | 404 | The organization id in the request doesn't exist. |
| `identity.site_not_found` | NotFound | 404 | The site id in the request doesn't exist. |
| `floor.invalid_label` | Validation | 400 | `POST /rooms/{id}/tables` or `PUT /tables/{id}`'s (FLR-03) `label` is missing, empty or whitespace. |
| `floor.invalid_room_name` | Validation | 400 | `POST /rooms` or `PUT /rooms/{id}`'s (FLR-03) `name` is missing, empty or whitespace. |
| `floor.invalid_seats` | Validation | 400 | `POST /rooms/{id}/tables` or `PUT /tables/{id}`'s (FLR-03) `seats` is less than 1. |
| `floor.invalid_shape` | Validation | 400 | `POST /rooms/{id}/tables` or `PUT /tables/{id}`'s (FLR-03) `shape` isn't a recognised `TableShape` name. |
| `floor.room_not_empty` | Conflict | 409 | `DELETE /rooms/{id}`'s (FLR-03) target room still has at least one table. |
| `floor.room_not_found` | NotFound | 404 | `POST /rooms/{id}/tables`, `PUT /rooms/{id}` or `DELETE /rooms/{id}`'s (FLR-03) room id doesn't exist. |
| `floor.table_concurrently_modified` | Conflict | 409 | `PUT /tables/{id}`'s (FLR-03) `xmin` concurrency token was stale — someone else edited the same table between the read and the write. Distinct from `floor.table_not_free`: editing has no `TableState` precondition to re-affirm, so this is a genuine lost-update race, not a stale-state check. |
| `floor.table_not_dirty` | Conflict | 409 | `POST /tables/{id}/clear` was called on a table that isn't `Dirty`. |
| `floor.table_not_free` | Conflict | 409 | `POST /orders` or `POST /orders/{id}/transfer` targeted a table that isn't `Free` — including the `xmin` concurrency-token case where two requests raced for it; or `DELETE /tables/{id}` (FLR-03) targeted a table that isn't `Free`, initially or via the same concurrency race. |
| `floor.table_not_found` | NotFound | 404 | The table id in the request doesn't exist. |
| `floor.table_not_occupied` | Conflict | 409 | `POST /tables/{id}/request-bill` (FLR-04) or `MarkDirty`/`Release` (both domain-only, no endpoint) was called on a table that isn't `Occupied`. |
| `order.already_closed` | Conflict | 409 | `Close()` was called on an order that is already `Closed`. |
| `order.empty` | Validation | 400 | `Close()` or `EnsureCanGeneratePreBill()` was called on an order with zero lines. |
| `order.invalid_cover_count` | Validation | 400 | `POST /orders`'s `coverCount` is less than 1. |
| `order.invalid_cursor` | Validation | 400 | `GET /orders`'s `cursor` query parameter isn't a token `X-Next-Cursor` (API-09) produced. |
| `order.invalid_discount` | Validation | 400 | `PUT /orders/{id}/discount` or `PUT /orders/{id}/lines/{lineId}/discount` (ORD-11): `type` isn't a recognised discount type; only one of `type`/`value` was given; a percentage is outside (0, 100]; or a fixed amount isn't positive or exceeds the total it would be applied to. |
| `order.invalid_merge_target` | Validation | 400 | `POST /orders/{id}/merge`'s `secondaryOrderId` is the same as the primary order. |
| `order.invalid_quantity` | Validation | 400 | An order line's `quantity` is less than 1, whether ringing one up or changing an existing line's via `SetLineQuantity()` (ORD-03). |
| `order.invalid_split` | Validation | 400 | `SplitEvenly`'s `parts` is less than 1; `SplitByItem()`'s groups are empty, a group has no lines, or a line's quantity isn't allocated exactly once across the groups; or `SplitByCover()`'s cover groups are empty, contain a group below 1 cover, or don't sum to the order's `CoverCount`. |
| `order.invalid_status_filter` | Validation | 400 | `GET /orders`'s `status` query parameter isn't a recognised `OrderStatus` value. |
| `order.invalid_take` | Validation | 400 | `GET /orders`'s `take` query parameter is outside 1–200. |
| `order.invalid_transfer_target` | Validation | 400 | `POST /orders/{id}/lines/{lineId}/transfer`'s `destinationOrderId` is the same as the source order. |
| `order.line_already_voided` | Conflict | 409 | `POST /orders/{id}/lines/{lineId}/void`'s (ORD-10) target line has already been voided. |
| `order.line_not_found` | NotFound | 404 | `SetLineNotes()`, `SetLineQuantity()`, `SetLineDiscount()`, `VoidLine()`, `DetachLine()` or `SplitByItem()`'s `lineId` doesn't belong to the order. |
| `order.line_voided` | Conflict | 409 | `SetLineQuantity()`'s (ORD-03) target line has already been voided — a voided line's `Quantity` stays frozen as the audit record of what was actually rung up (see `order.line_already_voided`, `VoidLine`'s own way of rejecting the reverse case). |
| `order.not_empty` | Validation | 400 | `MarkMerged()` was called on an order that still has lines. |
| `order.not_found` | NotFound | 404 | The order id in the request doesn't exist. |
| `order.not_open` | Conflict | 409 | `AddLine()`, `EnsureCanGeneratePreBill()`, `SetLineNotes()`, `SetLineQuantity()`, `SetLineDiscount()`, `SetDiscount()`, `VoidLine()`, `TransferToTable()`, `DetachLine()`, `ReceiveLine()` or `MarkMerged()` was called on an order that isn't `Open`. |
| `order.notes_too_long` | Validation | 400 | `SetLineNotes()`'s `notes` is over 300 characters. |
| `order.void_reason_required` | Validation | 400 | `POST /orders/{id}/lines/{lineId}/void`'s (ORD-10) `reason` is missing, empty or whitespace. |
| `request.idempotency_key_required` | Validation | 400 | A mutating `/api` request had no `Idempotency-Key` header. |
| `request.rate_limited` | RateLimited | 429 | The calling `(tenant, X-Brasa-Client client id)` pair exceeded `RateLimiting`'s configured requests-per-window (API-12). The response carries a `Retry-After` header. |

## Retired codes

None yet. When a code is genuinely retired (the operation it described no
longer exists), move its row here with the date and why, rather than
deleting it outright — a code disappearing from git history silently is
exactly the kind of drift this registry exists to prevent.
