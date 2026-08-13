# Menu item photos, description and allergens

> **Status:** ✅ built
> **Module:** Catalog
> **Roadmap:** I1 (pulled forward)

## What it is

A menu item can carry a free-text description, a declared allergen set,
and a real photo — the three pieces of this backlog row's own title
("Menu items — name, description, image, allergens") beyond the bare
name every item already had since I0.

## Why it works this way

**Allergens are a closed, EU-wide taxonomy, not a Portugal-specific
figure.** Modelled as an `Allergen` enum over the 14 categories EU food-
information law requires disclosing (Regulation (EU) No 1169/2011, Annex
II) — fixed since 2014, so deliberately *not* treated with the same
"needs an accountant to confirm" caution `docs/fiscal/README.md` puts on
`VatRate`, which really is a Portugal-specific figure in flux.

**Local disk storage is an honest placeholder, not a real backend.**
`MenuItemImageStorage` writes uploaded photos under
`{ContentRoot}/uploads/menu-items/` on whichever machine the API happens
to run on, served back via `UseStaticFiles`. There is no S3/Azure Blob
credential available in this development environment — the same reason
[deployment](../product/backlog.md) (OPS-11) is skipped rather than
faked. This is fine for a single dev instance; it is **not** multi-
tenant-safe (every tenant's images share one flat directory with no
tenant prefix) and **not** durable across a redeploy or a second
instance. Both are named gaps to close before a real multi-restaurant
cloud deployment, not oversights.

**GUID filenames, never the caller's own filename.** `SaveAsync` names
every file `{Guid.CreateVersion7()}{extension}`, ignoring whatever name
the browser sent. `Delete` strips any directory component from a stored
URL via `Path.GetFileName()` before rejoining it under `RootPath`, so
even a maliciously crafted `imageUrl` can only ever resolve to a file
directly under the upload root — the standard defence against a
path-traversal delete.

**Save the new file before deleting the old one.** `UploadMenuItemImageAsync`
writes the new file, persists the new `ImageUrl` to the database, and
only *then* deletes whichever file the item pointed at before. A failed
upload (a disk error mid-write, a DB save that never commits) never
destroys a working image — the ordering exists specifically so a partial
failure degrades to "still has the old photo," never "has no photo."

**`imageUrl` is relative to the API's own origin, not `/api/v1`.**
Every JSON field on `MenuItemDto` is API data; `imageUrl` is a path
`UseStaticFiles` serves directly off the host root
(`/uploads/menu-items/{file}`), so both `admin` and `pos` export a
separate `apiOrigin` (the API base URL's origin, stripped of the
`/api/v1` suffix) specifically to build a fetchable `<img src>` — using
`API_BASE_URL` directly would 404.

**`IFormFile` binding needs `.DisableAntiforgery()`, even in a
cookie-less API.** ASP.NET Core auto-attaches antiforgery request-
validation metadata to any Minimal API endpoint that binds an
`IFormFile`/form body, as a default CSRF mitigation for form posts —
regardless of whether the app registers antiforgery services at all.
This app never does (hard rule 7: no cookie auth), so without
`.DisableAntiforgery()` the endpoint throws
`InvalidOperationException` at request time, not at startup — the gap
only surfaces once a real multipart request actually hits the route,
which is exactly what caught it here, live, via the E2E suite.

## Behaviour

1. Staff sets a description and allergen set:
   `PUT /menu/items/{itemId}/details` with
   `{ "description": "...", "allergens": ["Gluten", "Lactose"] }`.
2. Staff uploads a photo: `POST /menu/items/{itemId}/image`, multipart
   form data, field name `file`. JPEG/PNG/WebP only, 5MB max. Uploading
   again **replaces** the existing photo — there is no separate "replace"
   endpoint, the same verb does both.
3. `GET /menu`/`GET /menu/all` return `description`, `allergens` and
   `imageUrl` (`null` until a photo is set) on every item.
4. `pos`'s menu grid renders the description, allergen tags (full
   `--ink` contrast, never dimmed — allergen information is
   safety-relevant, the same reasoning [QA-14](../product/status.md#accessibility--first-scan-five-real-fixes)
   already established for never using CSS `opacity` for hierarchy) and
   the photo thumbnail when set.
5. `admin`'s menu editor shows a photo thumbnail with a remove button
   when one is set, or an upload control (a styled label wrapping a
   hidden native `<input type="file">`) when none is.
6. `DELETE /menu/items/{itemId}/image` clears the photo, deleting the
   stored file and setting `imageUrl` back to `null`. Calling it when no
   photo is set is a no-op, not an error.

## Offline behaviour

Not applicable — cloud API endpoint, no offline path today.

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| No file attached, or an empty one | Rejected, nothing changed | `400 catalog.image_required` |
| File larger than 5MB | Rejected, nothing changed | `400 catalog.image_too_large` |
| Content type isn't JPEG/PNG/WebP | Rejected, nothing changed | `400 catalog.invalid_image_type` |
| Unrecognised allergen name | Rejected, nothing changed | `400 catalog.invalid_allergen` |
| Unknown item id (any of the three endpoints) | Rejected | `404 catalog.item_not_found` |

## Data

`MenuItem.Description` (nullable `string`), `MenuItem.Allergens` (a
comma-joined string column with a `ValueComparer`, the same convention
`VatRate`/`TableState`/`OrderStatus` already use for an enum column,
rather than a provider-specific array type just for this one property).
`MenuItem.ImageUrl` (nullable `string`, max length 500) — a path, never
the file's bytes; the bytes live on disk under `MenuItemImageStorage`,
outside the database entirely.

## API

| Method | Route | Purpose |
|---|---|---|
| `PUT` | `/menu/items/{itemId}/details` | Set a menu item's description and declared allergens (replaces the full allergen set) |
| `POST` | `/menu/items/{itemId}/image` | Upload (or replace) a menu item's photo — `multipart/form-data`, field `file` |
| `DELETE` | `/menu/items/{itemId}/image` | Remove a menu item's photo, if one is set |

All three take `Idempotency-Key` like every other mutation and return
the full `MenuItemDto`. The image routes carry `.DisableAntiforgery()` —
see "Why it works this way" above.

## Integration events

None. Modules don't publish integration events yet at all — see
[module-boundaries.md](../architecture/module-boundaries.md).

## Fiscal impact

None. Purely descriptive/presentational data — never referenced by
`BuildFiscalLines` or `IFiscalProvider`.

## Permissions

None enforced yet — same "ships ahead of manager authorisation" shape as
repricing (CAT-19), 86-ing (CAT-13) and category visibility (CAT-01).
IDN-11 is the eventual real gate for menu-editing actions in general.

## Testing

`menu-item-details.spec.ts` — description/allergens set, persist,
clear; an unrecognised allergen name and an unknown item both rejected;
both render correctly on a real `pos` menu button.

`menu-item-image.spec.ts` — a real uploaded PNG's URL is genuinely
fetchable through `UseStaticFiles` (not just present in the DTO);
replacing a photo deletes the old file from disk, not just overwrites
the database field; an empty file, an oversized file, a disallowed
content type and an unknown item are each rejected with their own error
code; removing a never-set photo is a no-op; the `admin` UI round-trips
an upload and a removal through the real browser, confirmed via a
follow-up API call rather than trusting the UI alone.

## Open questions

- Real object storage (S3/Azure Blob) before a multi-tenant production
  deployment — local disk has no tenant isolation and isn't durable
  across a redeploy or a second API instance.
- No image resizing/thumbnailing — `pos`/`admin` both request the
  original upload at full size for a small grid thumbnail.
- No moderation or dimension/aspect-ratio validation beyond size and
  content type.
