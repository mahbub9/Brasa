# AI session brief — start here

> **You are picking up development on this repository. Read this file
> completely before reading any source.** It is written so you can be productive
> without scanning the tree. It is maintained deliberately; if it is wrong, fix
> it in the same commit as whatever proved it wrong.

**Last verified:** 2026-08-17 · **Phase:** I0 complete except deployment (OPS-11); I1's floor plan and menu modifiers proven live end-to-end, plus menu item description/allergens (CAT-02, still 🚧 — image upload not built), course assignment per item (CAT-14) and kitchen station routing per item (CAT-15, independent tags on the same greenfield shape) and a second web client — the `admin` back-office shell (WEB-09, its own pt/en toggle) with its first real editor, menu management (WEB-10, still 🚧 — floor-plan editing not built); I2's pre-bill preview (ORD-18/19), order history/search (ORD-22), kitchen notes (ORD-06), line and order discounts (ORD-11, percentage or fixed, composing, no manager-authorisation gate yet), voiding a line (ORD-10, still 🚧 — same no-authorisation-gate-yet shape, its own row title names manager authorisation as in scope), table transfer (ORD-12), line transfer (ORD-13), order merge (ORD-14), split by item/cover (ORD-16/17) and takeaway orders (ORD-20) pulled forward and done; I3's `ETag`/304 caching on `GET /menu` (API-10), client version negotiation (`X-Brasa-Client` parsing + `GET /client-requirements` — API-06/07), RFC 8594 `Deprecation`/`Sunset` headers (API-08, a no-op until a real `/api/v2` exists), per-tenant-and-client rate limiting (API-12, a sixth `ErrorType.RateLimited` → 429), cursor pagination on `GET /orders` (API-09), Brotli/gzip response compression (API-11) and a committed OpenAPI document (API-13) pulled forward and done; the idempotency replay guarantee (API-05) now has an automated test harness (QA-11); menu bulk CSV import (CAT-17, still 🚧 — Excel not built) pulled forward from I1; every request now logs with `TenantId` attached (OPS-07, still 🚧 at this point in the project's history — the HTTP completion-summary line got the same enrichment later, see the tail of this same sentence); the two deep-link verification documents exist too (API-18, honestly empty — no bundle id/package name exists to put in either until a native app does); real distributed traces and metrics now exist too (OPS-08, OTLP-exported to Seq), after finding Seq itself had been silently crash-looping (fixed, see §7); `SplitByItem` (ORD-16) was made discount/void-aware, closing a gap ORD-11/ORD-10 had each explicitly left open; the first feature-docs pages exist (DOC-10 — `docs/features/{discounts,void-a-line,menu-item-classification}.md`); and `pos`'s server error messages are now localized by error code (closes the "Server-sent error text" gap ADR 0011 named), not just the raw English `ProblemDetails.title`; and editing an order line's quantity (ORD-03) is built — `PUT /orders/{id}/lines/{lineId}/quantity` plus a +/− stepper in `pos` — deliberately not how a wrong line is undone, which stays void (ORD-10); channel pricing for dine-in/takeaway (CAT-06, delivery out of scope — no delivery order path exists at all yet) is built too, with real UI on both `pos` (the menu button shows whichever price the order in progress would charge) and `admin` (inline add/edit/clear next to the dine-in price); and `admin`'s "Plano de sala" is live for the first time too — FLR-03's table CRUD via plain add/edit/delete forms, then extended the same day to room CRUD too (create/rename/delete, guarded to zero tables), not yet the drag-and-drop canvas that row's own title names; both web clients also got client-side error tracking (OPS-14, `@sentry/react`) — neither had any error boundary before, so a render-phase throw took the whole screen to blank white — with `Sentry.init()` config-bound and empty by default (same "no real collector yet" shape as OPS-08), a translated fallback screen instead of a blank one, and a dev-only crash trigger (confirmed stripped from both production bundles) proving the boundary genuinely catches a thrown error, not just that the component exists in the tree; and a database backup + restore drill exists too (OPS-12, still 🚧 — the mechanism and the drill are both proven live, "automated" scheduling is the open half since there's nothing to schedule it in yet), which is also where a real `.ps1`-encoding trap got found and written down (see §7); and a load test exists too (QA-13, still 🚧 and deliberately scoped down from its own title's "50 sites" — no multi-tenant seed data exists to drive that, and `Reporting` is empty so there's nothing to test isolation against), which is where a real ~40x `Debug`-logging latency trap got found and written down (see §7); and a shared `web/ui` component library exists too (WEB-02) — `formatMoney`, the `brasa.lang` cookie store and `LanguageToggle` deduplicated out of `pos`/`admin`'s own identical copies, consumed by a Vite `resolve.alias` + matching TS `paths` entry in each app (never through `node_modules`) plus a new npm workspace root (`src/web/package.json`) that exists solely to hoist the shared package's own `react`/`react-i18next` imports to a reachable `node_modules`; both apps' duplicate files deleted, both re-verified clean (`tsc -b`, `vite build`, `oxlint`), full suite green (64 backend, 109 E2E, one confirmed pre-existing flake); and a *prato do dia* schedule exists too (CAT-11) — `MenuItem.Schedule` (a `[Flags] ScheduleDays` mask + start/end `TimeOnly`, no overnight wraparound) via `PUT /menu/items/{id}/schedule`, all-or-nothing; `GET /menu` now filters a scheduled item out entirely outside its own window (mainland `Europe/Lisbon` time — no per-tenant region exists yet to pick a real one, an honest gap not a silent one) while `GET /menu/all` never filters on it, same split as CAT-01/13; `admin` gets a new day-checkboxes + time-inputs editor, `pos` needed no changes at all since an out-of-window item is simply absent from `GET /menu`'s response. **Verified live**: `menu-item-schedule.spec.ts`, self-scheduling off the real `Europe/Lisbon` date so it never goes stale; and *couvert* handling exists too (CAT-12) — `MenuItem.IsCouvert`, a plain tag via `PUT /menu/items/{id}/couvert` that (unlike CAT-11's schedule) never filters `GET /menu`, since "charged only when consumed" was already true of every item — `AddLine` needs no changes at all. What was missing was the workflow: `pos`'s new `CouvertBar` rings a couvert item up at the order's own cover count in one tap instead of one tap per guest, hidden for takeaway orders (no cover count to ring up against); `admin` gets a mark/unmark toggle next to the availability one. **Verified live**: `menu-item-couvert.spec.ts` — a real 3-cover table, one tap, a `3×` line, not `1×`; the bar absent for takeaway while the item stays orderable the normal way; and a first Organization/Site/Terminal slice exists too (IDN-01) — `Brasa.Modules.Identity`, previously an empty stub, now owns a real `identity` schema (`Organization`, `Site` with a real `PortugueseRegion` from day one, `Terminal`), create + list only via `POST`/`GET /organizations`, `/organizations/{id}/sites`, `/sites/{id}/terminals`, no update/delete, no auth/pairing (IDN-06/07 untouched); exists to give `Site` a stable id CAT-05 (price lists per site) and FLR-06 (waiter sections) can key by; `DevIdentitySeeder` seeds one demo chain the same way `DevFloorSeeder` seeds the floor plan. Caught the same RLS-forgetting trap ADR 0010 named, in a new form — `dotnet ef migrations add` never emits `RowLevelSecurity.EnableFor` calls, they're hand-added every time a new schema lands (see §7). **Verified live**: `identity-organization-site-terminal.spec.ts` against the real API and real Postgres, confirming the RLS grant/policy pair actually works, not just that the migration ran; and price lists per site exist too (CAT-05), unblocked by IDN-01 the same session — `PriceList`/`PriceListEntry` (Catalog module, `SiteId` a plain opaque reference to Identity's `Site`, same pattern `Order.TableId` uses for Floor) via `POST`/`GET /price-lists`, `GET /sites/{id}/price-lists`, `POST /price-lists/{id}/entries` (one price per item per list, a domain guard plus a DB unique index) and `GET /price-lists/{id}/effective-price/{menuItemId}` — the actual override-or-fallback resolution, not just storage. Create/read/add-entry only; nothing in `AddLine` or either web client resolves an effective price through this yet, since neither `pos` nor `admin` has a site-selection concept at all today — the same "mechanism before the trigger" shape CAT-14/15 already established. **Verified live**: `price-lists.spec.ts` — a fresh item resolves to its own price with `isOverridden: false` before any entry exists, then to the list's own price with `isOverridden: true` once one is added, persisting across a refetch; duplicate-entry, negative-price, unknown-item and unknown-list rejections all verified. Full suite green (64 backend, 126 E2E); and combos (*menu do dia*) exist too (CAT-10) — deliberately never a new fiscal concept: `POST /orders/{id}/combo-lines` (`OrderEndpoints.cs`) resolves a `Combo`'s components, allocates its fixed price across them via `Money.Allocate` weighted by each component's own standalone price (the exact proration ORD-11 already uses for an order-level discount), then adds each as an ordinary `OrderLine` through the existing `Order.AddLine` at its own real VAT rate — so a combo mixing VAT bands (13% food + 23% wine) itemizes correctly and reconciles to the cent by construction, reusing fiscal machinery already proven rather than inventing new logic. `Combo`/`ComboComponent` (always exactly one unit per component — guest choice and quantity>1 both deferred) mirror `PriceList`/`PriceListEntry`'s own ownership shape; nothing in `pos`/`admin` offers a "ring up this combo" UI yet, verified at the API level only. **Verified live**: `combos.spec.ts` — €6.00 split by weight 4:3 across a 13%-band and a 23%-band component allocates to €3.43/€2.57 exactly, the pre-bill's VAT breakdown shows both bands separately and reconciles to the same €6.00; iterating on this suite hit the QA-02 table-pool limitation again (recovered via the documented runbook, not a regression — see §7). Full suite green (64 backend, 132 E2E); and scheduled price changes exist too (CAT-16, scoped down from "menu versioning" — order lines already solved the per-sale history half, this covers the other half, activating a change automatically in the future) — `MenuItem.EffectivePrice(nowUtc)` resolves a pending `ScheduledNewPrice`/`ScheduledPriceEffectiveFromUtc` lazily on every read, no background job (none exists yet, Hangfire is OPS-10), the same "computed, never promoted" shape CAT-11's own schedule proved; `GetMenuAsync`/`GetMenuAllAsync`'s `Price` and `AddLine`/`AddComboLineAsync`'s snapshot all resolve through it now, so a guest is charged exactly what the menu just displayed. First attempt nested the pending change as one complex value object inside `MenuItem`'s own complex property and failed at migration time — EF Core cannot constructor-bind a complex type nested inside another; fixed by flattening to two sibling fields (see §7). **Verified live**: `menu-item-scheduled-price.spec.ts` schedules a change ~1.5s out, confirms it's not yet active, waits out a real ~2s window (no clock-injection seam exists for a live API, QA-04), then confirms both `GET /menu` and a real `AddLine` reflect the new price with zero manual step. Full suite green (64 backend, 134 E2E); and floor-plan seating groups exist too (FLR-05, scoped to the "push tables together" reading of "table merge/split" — full order-merge already existed separately as `ORD-14`) — `Table.GroupId` (a plain `Guid?`, no FK, same opaque-reference convention `OrderLine.MenuItemId` uses) set/cleared via `POST`/`DELETE /table-groups`, requiring every member table `Free` first. Given real teeth rather than staying cosmetic: `Table.Occupy()` itself refuses a grouped table (`floor.table_grouped`) — a purely additive tag would have shown a grouped table as `Free` with nothing stopping it being seated individually, actively contradicting the feature's own purpose. Cascading `Occupy`/`Clear`/`Release` across a group's siblings deliberately not built (a materially larger change touching every already-shipped table-state endpoint) — a named, deferred gap. No client UI yet — no floor-plan multi-select exists in either web client today — the same "mechanism before the trigger" shape CAT-05/CAT-10/CAT-16 already established. **Verified live**: `table-groups.spec.ts` — grouping blocks `POST /orders` on every member table, ungrouping restores ordinary seating (proven with a real order open+close, not just a `GET /floor` re-read); too-few-tables, an unknown table, a non-free table, an already-grouped table, and deleting an unknown group are all rejected with their own codes. Full suite green (64 backend, 137 E2E); and effective-dated tax rules exist too (CAT-07/08) — `TaxRule(isAlcoholic, isTakeaway, region, rate, effectiveFromUtc, effectiveToUtc)`, replacing the hardcoded-constant instinct `VatRate`'s own doc comment always named as temporary. "Item" in CAT-07's own title is the alcohol band, not a per-`MenuItemId` key — VAT law taxes categories of goods, never one named product, the same reason `MenuItem.IsAlcoholic` (CAT-09) exists at all. `POST /tax-rules` creates only — no update/delete, a correction is a new later-effective row, never an edit to one on file, the same "never mutate, only add" instinct fiscal documents themselves follow even though this isn't one. `GET /tax-rules/resolve` (CAT-08) is `TaxRule.Resolve`: picks the rule in force for a combination at an instant (defaulting to now), and if two rules' ranges wrongly overlap, the most recently-*started* one wins. Deliberately **not** wired into `AddLine`/`AddComboLineAsync`/the fiscal document builder — all three still read `MenuItem.VatRate` directly; rewiring the actual VAT computation path is the most fiscal-sensitive change this codebase could make and deserves its own dedicated pass, not a side effect of shipping the model, the same "mechanism before the trigger" shape CAT-05/CAT-10/CAT-16/FLR-05 already used. Dev-seeded: mainland dine-in/takeaway rows for both bands, effective from a fixed 2024-01-01 anchor so "today" always resolves regardless of which day the seeder runs. Caught one real bug before it shipped: the seeder's first version placed the new seeding call *after* the existing `if (categories already exist) return`, so on this session's own long-lived dev database it silently never ran — fixed by moving it above that early return with its own idempotency check (see §7). **Verified live**: `tax-rules.spec.ts` — seeded mainland rates resolve correctly for both bands/channels; on a region only this spec touches, a later rule supersedes an earlier one exactly within its window and nothing resolves before the earliest rule starts; an unrecognised region, an out-of-range percentage, an unparsable date and a backwards range are all rejected on create; an uncovered combination 404s on resolve. Full suite green (64 backend, 141 E2E, one confirmed pre-existing QA-02 table-pool flake — a different unrelated spec each run, clean in isolation both times); and multi-floor support exists too (FLR-07) — `Room.FloorLevel: int`, default `0`, so no existing/seeded room needed a data migration. Deliberately not a separate `Floor` entity — everything a floor needs is computable from the rooms that carry its level, the same "no entity where a plain field says the same thing" call FLR-05's `Table.GroupId` already made over a `TableGroup` row. Framed as a **display** concern only, never an access one: a "Floor N" badge in `admin`'s room editor and a floor-heading grouping in `pos`'s table picker both stay entirely invisible until a tenant's own rooms actually span more than one level, so every seeded (single-storey) restaurant renders byte-for-byte as before; once ambiguity exists, *every* room gets labelled, not just the odd one out, since a bare unbadged room would otherwise be a second, silent way to mean "ground floor." **Verified live**: `floor-multi-level.spec.ts` — `floorLevel` round-trips through create/`GET /floor`/update, defaults to `0` when omitted on create; `admin`'s badge appears on every room once a second floor exists, editing a room's floor through the real UI round-trips to the API; `pos`'s table picker renders both floor headings with rooms grouped correctly under each. Full suite green (64 backend, 146 E2E); and this codebase's first realtime channel exists too (API-16/17) — `FloorHub` at `/hubs/floor` (outside `/api/v1`, since a connection isn't a versioned resource) broadcasts one payload-less `FloorChanged` signal whenever any table's state changes; the six call sites that actually change it (`ClearTableAsync`/`RequestBillAsync` in `FloorEndpoints.cs`, `OpenOrderAsync`/`TransferOrderAsync`/`MergeOrdersAsync`/`CloseOrderAsync` in `OrderEndpoints.cs`) each call the same `NotifyFloorChangedAsync` helper right after their own successful save, composing `IHubContext<FloorHub>` the same way these handlers already compose Floor and Ordering. API-17's "REST equivalent" requirement holds by construction, not a bolted-on check: the message carries no data, so a client always re-fetches `GET /floor` — nothing in the push could ever disagree with the REST response, because there is no payload to disagree with. `pos` is the first subscriber (`connectFloorHub`, `@microsoft/signalr`, `withCredentials: false` — no cookie auth exists to carry, hard rule 7), re-fetching on the signal and again on `onreconnected`. Installing the new npm dependency from inside `pos/` rather than the workspace root left a stale Vite dependency-scan cache pointing at a pre-hoist `node_modules` layout — `pos` rendered nothing at all until `node_modules/.vite` was cleared and the dev server restarted, a real trap for the next task that adds a frontend dependency from a workspace member instead of the root (see §7). No per-tenant/per-terminal targeting yet — every connection sits in one `Clients.All` group, waiting on IDN-06/07 to key a group by. **Verified live**: `floor-realtime.spec.ts` — two real, separate browser tabs, neither ever calling `reload()`: opening a table from the second tab flips it to `Occupied` in the first purely from the pushed signal, and clearing a dirty table flips it back to `Free` the same way. Full suite green (64 backend, 148 E2E); and fixed-clock control for E2E exists too (QA-04) — revisited once CAT-16/CAT-07-08 actually needed it, having each shipped by waiting out a real ~2s wall-clock window instead. `TestableClock : IClock` holds its override in a static `AsyncLocal<DateTimeOffset?>`, not a scoped service — `MockFiscalProvider` is deliberately singleton (its in-memory sequential document numbering must survive across requests), and a singleton can't consume a scoped `IClock`; ASP.NET Core's own DI validation refused to start the app on the first attempt, a real captive-dependency bug caught before it shipped, fixed the same way `TenantContextAccessor` already solves an identical problem for tenant context (see §7). `TestClockMiddleware` reads an optional `X-Brasa-Test-Clock` header and fixes the override for that request's own async call chain only — two Playwright workers overriding different instants against the one shared API instance never interfere. Mirrors `DevTenantMiddleware` exactly: registered unconditionally, throws on the very first request if `IsProduction()`. **Verified live**: `test-clock.spec.ts` proves the mechanism directly against `GET /ping`; `menu-item-scheduled-price.spec.ts` (CAT-16) is the real first consumer, retrofitted to fast-forward past the scheduled instant instead of a real wait — same assertions, now instant and deterministic. Full suite green (64 backend, 150 E2E); and staff PIN accounts exist too (IDN-08/09) — `Staff` (Identity, site-scoped like `Terminal`) closes "PIN hashing, lockout, and rotation policy" and the PIN-*verification* half of "staff PIN sign-in," not the "on a paired terminal" half (IDN-07 doesn't exist yet). `POST /staff/{id}/verify-pin` checks a PIN against a *known* staff id, deliberately not "identify me by PIN alone with no picker" — without knowing who's attempting, a failed PIN can't be attributed to the right person's own lockout counter. PBKDF2-HMAC-SHA256 via .NET's own `Rfc2898DeriveBytes` (no new dependency, 210k iterations, OWASP's 2023 minimum), 5-strike/15-minute lockout (even a *correct* PIN is refused while locked), `PUT /staff/{id}/pin` resets both together with no old-PIN check — an admin action, same "ships ahead of manager authorisation" shape every other admin mutation here already has; still not wired into ORD-10/ORD-11's own gate (IDN-11). `admin`'s new "Equipa" screen closes WEB-11's staff half and was the shell's last placeholder nav entry — assumes the first organization's first site, no site-selector exists anywhere yet. Building `TestableClock` (QA-04) had already found the "singleton can't consume scoped" DI trap; `Staff.PinHash` needed a smaller one — a `private` auto-property mapped via `builder.Property<string>("PinHash")` works the same way a true EF shadow property would (see §7). **Verified live**: `staff.spec.ts` — correct/incorrect PIN, lockout after 5 failures (even the correct PIN then refused), a reset clearing it with the new PIN working immediately, every validation rejection, and `admin`'s staff screen adding a member and resetting a PIN through the real UI, proven to actually take effect via a follow-up API call. Full suite green (64 backend, 153 E2E); and manager authorisation exists too (IDN-11) — the real gate ORD-10 (void)/ORD-11 (discount) both shipped ahead of, closed the same session `Staff` made it possible. `OrderEndpoints.AuthorizeManagerAsync` composes `IdentityDbContext` into Ordering at the API layer (module-boundaries.md rule 5, same Catalog+Identity composition `PriceListEndpoints` already uses for CAT-05) — checks the credential names a real `StaffRole.Manager` *before* ever calling `Staff.VerifyPin`, so a Staff-role id spends none of that person's own lockout budget, then reuses `VerifyPin` exactly as `POST /staff/{id}/verify-pin` does, same lockout persistence regardless of outcome. A per-call credential, not a session — nothing cached, no terminal pairing or OAuth session (IDN-03…07) exists to hang one off yet anyway. `identity.staff_not_manager` is the error registry's first real `ErrorType.Forbidden`/403 call site, a type that's existed since API-03's original five but had never been constructed until now. No pos/admin UI prompts for the credential yet (no staff-picker exists in either client) — ships the gate itself, the same "mechanism before the trigger" shape as everything from CAT-13/19 onward. `void-line.spec.ts`/`discounts.spec.ts` needed zero call-site changes — `support/api.ts`'s helpers default every void/discount to the seeded demo manager's credentials unless a caller overrides them, so both specs kept testing exactly what they always tested while now genuinely exercising the real gate. **Verified live**: `manager-authorization.spec.ts` — a non-manager credential rejected without touching the line, an unknown manager id and a wrong PIN both rejected then the correct one working, the same gate covering both discount endpoints, role checked before PIN, and 5 consecutive wrong attempts against a freshly-created isolated manager (never the shared seeded one, so parallel specs relying on her aren't disturbed) locking them out exactly like IDN-09's own lockout test. Full suite green (64 backend, 157 E2E, two confirmed pre-existing QA-02 table-pool flakes — a different unrelated spec each run, clean in isolation both times); and waiter section assignment exists too now (FLR-06), unblocked the same session by `Staff` — `PUT /rooms/{id}/section` assigns or clears which waiter is working a room. A room was already the "which area" granularity a real *secção* means, so this is one new nullable field, `Room.AssignedStaffId` (a plain opaque reference to Identity's `Staff`, the same pattern `Order.TableId` uses for a Floor `Table`), not a new entity — the same "no entity where a plain field says the same thing" call FLR-05's `Table.GroupId` and FLR-07's `Room.FloorLevel` already made. Turned out to key off `Staff` directly rather than `Site`, unlike what IDN-01's own row once expected — a room has no site relationship of its own to match against. `RoomDto.AssignedStaffName` resolves fresh from Identity on every `GET /floor`, one batched query across every room rather than N+1, never snapshotted onto `Room` itself. Any staff role works — unlike IDN-11 shipped the same session, this isn't a privileged action. `admin`'s room editor gets a section dropdown; `pos` shows nothing yet. **Verified live**: `floor-section-assignment.spec.ts` — a plain Staff-role member (not a manager) assigned and resolved correctly, clearing removing both fields together, an unknown staff id and an unknown room both 404ing with their own codes, and the admin UI assigning/clearing through the real section `<select>`, confirmed via a follow-up API call. Full suite green (64 backend, 160 E2E, two confirmed pre-existing QA-02 table-pool flakes — a different unrelated spec each run, clean in isolation both times); and a first slice of `web/sdk` exists too (API-15/WEB-03 — the same deliverable under two backlog rows), narrowly: `npm run generate` runs `openapi-typescript` against the committed `docs/openapi/v1.json`, producing every request body/path/query-param type, verified against real endpoints by a permanent guard file rather than trusting the generator blindly. Building it surfaced two real problems it didn't create: `v1.json` itself had drifted (this session's own IDN-11/FLR-06 changes never got the documented manual regeneration step — fixed, and re-verified), and, more consequentially, every response body in that document has always been undescribed, since API-13 first shipped it — `Brasa.Api`'s endpoints all return a bare `Results.Ok(...)`/`IResult`, which erases the concrete type OpenAPI reflection would need to describe a response, only a request. Closing that is its own bounded-but-broad follow-up (`.Produces<T>()` across ~68 route mappings), deliberately not attempted here. No consumer yet either — `pos`/`admin` keep their own hand-written `api/client.ts`. See `docs/openapi/README.md` and `src/web/sdk/README.md` for the full accounting; §7 below has the PowerShell-BOM trap this also caught along the way. Full suite green (64 backend, 160 E2E) — this task added no new runtime behaviour, so no new E2E coverage; verification was regenerate-and-typecheck, the same shape API-13 itself was verified by; and API-15's own gap got closed the same session, not left as a standing note — every one of the 68 route mappings across all eight endpoint files now carries an explicit `.Produces<T>(statusCode)` after its `.WithSummary(...)`, telling OpenAPI reflection what a success response actually contains (it could already infer a request body from a strongly-typed parameter, never a response from a bare `Results.Ok(...)`/`IResult`). `GET /floor`'s `200` now describes `RoomDto[]`, not `{"description": "OK"}`; `204`/`304` correctly still show no `content` key, since neither carries a body at all. Regenerated `docs/openapi/v1.json` and `web/sdk/src/schema.ts` and confirmed live: the SDK's own `content?: never` marker — its signal for "no schema known" — dropped from 71 occurrences to 9, and every remaining one is a genuinely bodyless `204`/`304` or the out-of-scope `/ping` endpoint, not a missed case. `schema.guard.ts` now checks a response shape the same way it already checked request shapes. Error responses stay undescribed — `.Produces<T>()` only names a success shape, and enumerating every distinct error code a handler can return (several have 3–6 different validation paths) is a separate, materially larger task, deliberately not attempted here. Pure metadata, zero handler-logic changes: full suite green (64 backend, 160 E2E) confirms it — the exact same 160 that were green before this pass, since nothing about what any endpoint actually does changed; and `pos` can sign a staff member in now too (WEB-07), unblocked the same session by IDN-08/09's `Staff` — a "Sign in" button in the header opens a staff-picker-then-PIN modal (`StaffLogin.tsx`), calling the same `POST /staff/{id}/verify-pin` IDN-11's own gate reuses, so this is that mechanism's second real caller. Deliberately non-blocking: signing in gates nothing else in `pos` — a real login gate would have meant touching every existing E2E spec that drives `pos` directly from a fresh `page.goto('/')`, the entire ordering flow's own suite, a materially larger and riskier change than "a real, working sign-in exists," the same "mechanism before the trigger" shape IDN-08/09 itself shipped in (nothing consumed its own endpoint at all until IDN-11). A locked-out staff member appears in the picker but its button is `disabled`, so the UI never lets a PIN even be attempted against it; a wrong PIN shows an inline error and stays on the PIN screen to retry; success shows "Hi, {name}" and a sign-out control. No persistence across a reload — plain React state, since no session/token concept exists yet (IDN-03…05). **Verified live**: `staff-login.spec.ts` — wrong-then-correct PIN, sign-out, cancelling the modal, and a freshly-locked staff member (created directly on the real seeded demo site, never the shared "Ana Ferreira"/"Tiago Costa" other specs depend on) showing disabled. Full suite green (64 backend, 163 E2E, two confirmed pre-existing QA-02 table-pool flakes — a different unrelated spec each run, clean in isolation both times) — the pre-existing 160 all still pass unchanged, confirming the non-blocking design really doesn't disturb anything that never signs in; and CAT-02's own long-open image-upload gap ("still 🚧 — image upload not built") is closed too — `POST`/`DELETE /menu/items/{id}/image`, local disk under a new `MenuItemImageStorage` (GUID-named files, `Path.GetFileName()`-sanitised deletes, no per-tenant isolation — an honest dev-only placeholder, the same "no real infra credentials, skip it like OPS-11" reasoning applied to storage), served back through a second `UseStaticFiles` mount at `/uploads/menu-items/…`, outside `/api/v1` since it's a file, not JSON — so both `pos` and `admin` gained a small `apiOrigin` export (the API base URL's own origin) to build a fetchable `<img src>`. Saves the new file and persists the new `ImageUrl` before ever deleting whichever file it replaces, so a failed upload degrades to "still has the old photo," never "has no photo." Building it caught a real gotcha live, via the E2E suite, not at compile time: ASP.NET Core auto-attaches antiforgery request-validation metadata to any Minimal API endpoint binding an `IFormFile`, even in an app that registers no antiforgery services at all (hard rule 7 — no cookies here) — every upload 500'd with `ThrowMissingAntiforgeryMiddlewareException` until `.DisableAntiforgery()` was added to the route mapping (see §7). `admin`'s menu editor gets a thumbnail + remove button when a photo is set, or a styled upload control (a clickable label wrapping a `display:none` native `<input type="file">`) when none is; `pos`'s menu grid renders the same thumbnail read-only. **Verified live**: `menu-item-image.spec.ts` — a real uploaded PNG's returned URL is genuinely fetchable through `UseStaticFiles`, not just present in the DTO; replacing a photo deletes the old file from disk; an empty file, an oversized (>5MB) file, a disallowed content type and an unknown item are each rejected with their own code; removing a never-set image is a no-op; the `admin` UI uploads and removes a real file through the real browser, confirmed via a follow-up API call. Full suite green (64 backend, 168 E2E, one confirmed pre-existing QA-02 table-pool flake, clean in isolation) — the pre-existing 163 all still pass unchanged, and course firing exists too now (ORD-07/08/09), unblocked by CAT-14's own `MenuItem.Course` — `Course.cs`'s doc comment literally named this task as its future reader before it was built. `OrderLine.Course` snapshots the catalog item's course at add-time (same convention as `ItemName`, never a live join — Ordering still never references Catalog's `Course` enum directly). `POST /orders/{id}/fire` (`Order.FireLines`) fires a named course (ORD-08's "partial" send) or, with `course: null`, everything still unfired (the "full" send); idempotent, skips an already-fired line rather than erroring. `OrderLine.IsFired`/`FiredAtUtc` (ORD-09) is deliberately the only line-status tracked — richer states need a KDS that doesn't exist yet (KIT-10…13, I4). No manager authorisation, since firing touches no money. `pos`'s `OrderSummary` gets a fire-controls bar (one button per course still pending, plus "Fire all," gone once nothing is left) and a "Sent" badge on a fired line. **Verified live**: `order-course-firing.spec.ts` — a named course fires only its own lines, "fire all" gets the rest, a voided line is never fired, a courseless line only ever fires with "fire all," an unrecognised course/closed order/unknown order all reject with their own codes, and the `pos` UI round-trips through a real browser. Full suite green (64 backend, 173 E2E, one confirmed pre-existing QA-02 table-pool flake — this time landing on this feature's own new UI test, same failure shape as every prior recurrence, clean in isolation) — the pre-existing 168 all still pass unchanged, and CAT-17's own remaining gap ("Excel not built") is closed too — `POST /menu/items/import/excel` accepts a real `.xlsx` (`ExcelDataReader`, chosen over ClosedXML/NPOI for its minimal, vulnerability-free footprint) and shares the exact same `ImportRowsAsync` pipeline the CSV endpoint already used, once `ExcelImportParser` turns the first worksheet into the identical row shape `CsvParser` produces — one implementation, not two. `admin`'s single import control now routes by file extension. Two dependency problems surfaced along the way, unrelated to the import logic itself: `ClosedXML` (tried first) pulls in the same vulnerable `SSH.NET` the build gate had just started refusing (see the next trap entry) — switched to `ExcelDataReader` instead; and `ExcelDataReader` itself throws on every `.xlsx`, not just legacy `.xls`, until `CodePagesEncodingProvider` is registered (see the trap after that one). **Verified live**: `menu-import-excel.spec.ts` — 2 valid + 2 invalid rows plus a wholly-blank row in a real `.xlsx` built at test time via `exceljs` (a devDependency of `e2e` only) → `created: 2`, the same two row-level errors the CSV spec already proves, the blank row silently skipped; an empty file, a non-`.xlsx` file, a corrupt `.xlsx`, and a missing-header-column file each `400` with their own code; the `admin` UI imports a real `.xlsx` end to end; the pre-existing CSV spec re-verified unchanged. Full suite green (64 backend, 176 E2E, no flakes this run) — the pre-existing 173 all still pass unchanged; and FLR-03's own remaining gap — the drag-and-drop canvas its own backlog title names — is closed too: `FloorCanvas`, a pointer-events (not HTML5 drag-and-drop, flaky under Playwright) grid canvas in `admin`'s floor editor, dragging a table to a grid-snapped position via the same `PUT /tables/{id}` the plain form already used, `aria-hidden` since it exposes no capability the accessible form doesn't already have. Writing its own E2E test (`floor-drag-drop.spec.ts`) found a real, severe pre-existing bug along the way: `IdempotencyMiddleware` crashed the whole API process on any `204` response (any DELETE endpoint) by calling `WriteAsync` unconditionally even for an empty buffered body, which Kestrel rejects outright for a status that forbids a body at all — fixed on both the cache-and-forward and cached-replay paths, and very likely the real explanation for this project's earlier run of unexplained CI-only `e2e` job failures (see §7). **Verified live**: `floor-drag-drop.spec.ts` — a real `page.mouse` drag past half a cell persists a grid-snapped position, one under half a cell fires no update; `idempotency.spec.ts` gained a regression case proving a replayed `204` DELETE no longer crashes the API. Full suite green (95 backend, 179 E2E, no flakes this run) — the pre-existing 176 all still pass unchanged; and per-tenant, per-platform feature flags exist too (IDN-16, an I3 item pulled forward, the same "scale decision to make on day one" shape this project's own build plan named explicitly) — `FeatureFlag` (Identity), `Platform` deliberately never `null` (`"all"` is the sentinel for every platform instead, since Postgres unique indexes never treat two `NULL`s as equal — a nullable column would have silently let two "all platforms" rows for the same key coexist, the one case a flag is most likely to actually be used in). `PUT /feature-flags/{key}` upserts, `GET /feature-flags` lists, `GET /feature-flags/{key}/resolve` is the shape a real consumer will actually call — a platform-specific row wins over the tenant's "all platforms" row, an unconfigured flag defaults to disabled, never enabled. No consumer anywhere in this codebase reads a flag yet — ships the mechanism only, the same "mechanism before the trigger" shape CAT-05/CAT-10/CAT-16/FLR-05 already established. `admin` gets a new "Feature flags" screen, the one screen in that shell needing no site id at all. **Verified live**: `feature-flags.spec.ts`. Full suite green (95 backend, 185 E2E, no flakes) — the pre-existing 179 all still pass unchanged; and OPS-07's own long-standing "known gap" is closed too — the HTTP completion-summary log line (`UseSerilogRequestLogging`) now carries `TenantId` (and `Site`/`Terminal`/`User` ids once auth populates them), not just lines logged during the request. The gap's own note assumed closing it meant reordering the pipeline; it didn't — `EnrichDiagnosticContext` runs at request-completion time reading `ITenantContext` straight from DI, sidestepping `TenantLoggingMiddleware`'s own `LogContext.PushProperty` scopes (already disposed by the time control returns to the completion-log call) entirely. See the trap entry (§7) for the mechanism. **Verified live**: a real `GET /floor` request's completion line shows `TenantId` directly. Full suite green (95 backend, still 185 E2E — a Serilog output-shape change, not app behaviour, so no new E2E coverage needed); and OPS-10/OPS-12 (Hangfire + the scheduled backup/drill jobs) landed too, picked up mid-session from a stalled concurrent session's own fully-coded-but-unverified WIP — `Hangfire.AspNetCore`/`Hangfire.PostgreSql` on the migrations connection, dashboard at `/hangfire` outside Production, `DatabaseBackupJob` (`Jobs/`) wrapping the already-verified `backup-database.ps1`/`restore-drill.ps1` as two recurring jobs. "Verify live" caught two real bugs the code-complete state hadn't yet surfaced: `backup-database.ps1`'s own `-OutputDir` default resolves via `$PSScriptRoot` inside a `param()` block, which Windows PowerShell 5.1 leaves empty specifically when that script is the direct `-File` target of a redirected, non-interactive process launch (exactly what `Process.Start` does, reproducible with zero Hangfire involved — see the trap below) — fixed by having `DatabaseBackupJob` pass `-OutputDir` explicitly rather than touching the already-verified script; and the restore drill's own row-count comparison raced against Hangfire's own continuously-mutating `hangfire.lock`/`hangfire.server` tables (the first source of writes in this database independent of any test/tenant activity), fixed by excluding `hangfire.*` from the comparison with the reasoning written down in `backup-and-restore.md` — losing that transient, self-healing operational state in a real restore was never a disaster-recovery failure the way losing tenant data would be. **Verified live**: both jobs manually triggered end to end through the running Hangfire server (`RecurringJob.TriggerJob`, not just the underlying scripts run by hand) — a real ~1MB backup file produced, a real drill passing clean (23 tables). Full suite green (95 backend, 185 E2E, one confirmed pre-existing QA-02 table-pool flake — `order-course-firing.spec.ts`'s own UI test, clean in isolation, recovered by clearing the 7 dirty tables the flaky run left behind); and QA-02's own interim mitigation was applied a second time too — `DevFloorSeeder`'s pool doubled again, 16→32 tables, since the same E2E suite that once passed twenty tests sharing 8 tables (motivating the first doubling) had grown to 185 sharing 16, a worse tests-per-table ratio than the one that first exhausted the pool. **Verified live**: three consecutive full E2E runs back-to-back, no pause between them (the exact load pattern that used to occasionally flake), 185/185 clean every time. Disposable-per-run is still the real fix, not superseded by this — a documented, deliberate stopgap, not a claim the underlying QA-02 gap is closed; and a real push (by the project owner, outside this session) finally exercised CI against this whole run of work, surfacing two genuine bugs no amount of local `infra/scripts/verify.ps1` runs could have caught, because both were bugs *in* the tooling those local checks trusted, not in the application code they were checking. First: `docs/openapi/v1.json`'s own regeneration command (`ConvertTo-Json -Depth 100`, documented in `docs/openapi/README.md` since API-14 first shipped) produces a "staircase" indentation Windows PowerShell 5.1 has no option to turn off, bloating a semantically unchanged ~120KB document to ~540KB and never matching CI's own `jq`-based regeneration byte-for-byte regardless of content — confirmed by deep-sorting both documents down to primitives and diffing that instead of raw text, which showed zero semantic drift on a run CI still failed. This had been failing on every push, including a docs-only commit that could not possibly have changed the API's contract. Fixed by moving regeneration to Node (`infra/scripts/regenerate-openapi.mjs`, `JSON.stringify(doc, null, 2)`, which matches jq's own default formatting), and pointing both the docs and `verify.ps1` at that one script instead of two independent hand-rolled PowerShell snippets — `verify.ps1`'s own version of this check had used the identical broken formatter to build its comparison copy, so it always agreed with whatever was already committed and could never have caught this on its own. Second: `vite@8` made `rolldown` a hard dependency, and the npm version `actions/setup-node@v5` bundles for `node-version: '24.x'` has repeatedly failed to install rolldown's Linux native binding correctly even though `package-lock.json` lists it — a real, documented, still-recurring npm bug (`npm/cli#4828`), confirmed by inspecting the lockfile directly rather than assumed. This crashed `pos`'s own Playwright `webServer` on startup, aborting the entire `e2e` CI job before a single test ran. Fixed with an explicit `npm install -g npm@latest` step before any dependency install in that job — see the two trap entries (§7) for the full reasoning behind both fixes, including why "a local check passed" was never actually evidence either of these specific things were fine; a beta UI refresh (WEB-02, grown out of its own original "shared library" scope) landed too — real design tokens (spacing/radius/elevation/type-scale/motion layered on the existing terracotta identity, 44px touch targets) plus seven components (`Button`, `Card`, `Badge`, `TextField`, `SelectField`, `Modal`/`ModalActions`, `Icon`) in `src/web/ui`; `pos` was wired through fully (`StaffLogin`, `TablePicker`, `OrderSummary`, `ModifierPicker`, `PreBill`, `TransferTablePicker`, `Receipt`, `ErrorBanner`, `MenuGrid`), `admin` initially left with only the CSS tokens loaded and no component actually used in its own markup — a named, deliberate gap; a swappable database provider landed the same day too ([ADR 0012](../architecture/decisions/0012-beta-in-memory-database.md)) — `Database:Provider` (`Postgres`/`InMemory`, SQLite-backed since EF Core's own InMemory provider crashes on this codebase's required `Money` complex-property mapping, a real `KeyNotFoundException` confirmed live) lets the beta pilot run with zero on-site Postgres install, RLS accepted as a real trade-down for that one-tenant path only, and `SeedOnStartup` separately gates the dev seeders so a beta restart doesn't reseed demo data over what a restaurant entered live; and `admin`'s own half of the beta refresh — the gap the previous sentence just named — is closed too, in a later session: `MenuManager`, `FloorManager`, `StaffManager` and `FeatureFlagManager` now render every button/badge/text-and-select input through the same shared components `pos` already adopted, not hand-rolled markup that merely inherited the shared color tokens; `FloorCanvas`'s pointer-driven drag grid and every click-to-rename tile (a table's or room's own label) deliberately stay outside this, the same "no shared component fits a custom tile or a hand-tracked grid" call `pos`'s own table/menu tiles already made; `App.css`'s now-dead badge-pill and input/select border overrides were deleted so the shared classes actually show through instead of losing the cascade to a same-specificity, later-loaded app rule. **Verified live**: the full existing admin E2E coverage re-run clean with zero spec changes, confirming a pure restyle rather than a behaviour change — 186/186 (one `order-course-firing.spec.ts` failure, the same pre-existing QA-02 table-pool shape named throughout this file, clean when re-run in isolation); and order concurrency protection exists too (ORD-21, unblocking QA-08 the same session) — `Order` carries the same `xmin` optimistic-concurrency token `Table` already had (`OrderConfiguration.cs`, the same deliberately-empty migration trap `AddTableXminConcurrencyToken` first worked through), closing a real gap: every order-mutating endpoint used to do a blind `UPDATE ... WHERE Id = @id`, so two terminals racing to mutate the same order silently let the second writer overwrite the first with no error and no trace. A shared `TrySaveOrderAsync` helper (`OrderEndpoints.cs`) now catches the resulting `DbUpdateConcurrencyException` and returns `409 order.concurrently_modified` from every call site except `CloseOrderAsync`, which deliberately does not share it — by the time its own save could lose this race, `IFiscalProvider.IssueSimplifiedInvoiceAsync` has already issued a real, un-un-issuable document (hard rule 3), so a generic "reload and retry" would risk a client double-issuing; it returns a distinct `order.close_conflict_after_fiscal_issuance` instead, whose message says explicitly not to retry — the same "I0's correctness floor, not I5's durable two-phase guarantee" boundary that endpoint's own pre-existing comment already named for the fiscal-*failure* case, now also covering the lost-race case honestly rather than silently. Trying to prove the race itself over real HTTP (`Promise.all`, 8 then 40 genuinely concurrent `POST /orders/{id}/lines` requests against the same order) never once triggered it in this environment — local Kestrel/Postgres round trips are fast enough that even 40 concurrent requests didn't reliably overlap at the `SaveChangesAsync` level — so `order-concurrency.spec.ts` asserts only what's always true (no lost or duplicated line, and a well-formed code whenever a conflict does land) while a new `OrderConcurrencyIntegrationTests.cs` proves the actual mechanism deterministically instead, by directly controlling two `OrderingDbContext`s against a real Testcontainers Postgres with no timing dependence at all. Writing that test found a real bug in the test *harness*, not the app: it and `TenantIsolationIntegrationTests` both migrate through the same process-wide `BRASA_MIGRATIONS_CONNECTION` environment variable, and xUnit running different test classes in parallel by default let one test's migration silently target the other's container the moment a second consumer of that pattern existed to race against — fixed with a shared `[Collection]` (`MigrationsEnvVarCollection.cs`) forcing the two to run sequentially. **Verified live**: `infra/scripts/verify.ps1` (Release build, full backend suite, OpenAPI drift — none, since this only added new *error* responses, which aren't documented there yet per API-15's own noted gap — and the vulnerable-package scan) plus the full E2E suite, 188/188 clean. Full suite (96 backend, 188 E2E); and the privileged system-context role exists too (DAT-07) — `brasa_system` (`infra/initdb/02-system-role.sql`), the exact "third, narrowly-scoped role" `docs/architecture/multi-tenancy.md`'s own system-context section had deferred to, not the tempting-but-wrong superuser/`BYPASSRLS` shortcut [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md) already named a mistake once. Read-only and cross-tenant by construction: `RowLevelSecurity.EnableSystemReadFor` grants a role-scoped `FOR SELECT ... USING (true)` policy per table (invisible to `brasa_app` connections — Postgres only evaluates a policy for the role it names, so this can never widen what an ordinary tenant-scoped request sees) plus a `GRANT` that never includes a write verb, redundant enforcement on purpose. Deliberately a second, separate call from `EnableFor`, not folded into it — a first attempt DID fold it in, and immediately failed a fresh Testcontainers run with "policy already exists," because `EnableFor`'s C# implementation is shared, not snapshotted per historical migration, so folding in silently changed what every already-committed migration does the next time it runs against a fresh database; fixed by keeping the two calls separate and adding one new migration per module (`AddSystemContextRole`) granting the existing tables explicitly. `ModulePersistenceExtensions` picks a physically separate connection string (`ConnectionStrings:PostgresSystem`) whenever a scope's `ITenantContext.IsSystemContext` is set — different Npgsql credentials mean a genuinely separate connection pool, so an ordinary request can never end up running on a connection still carrying `brasa_system`'s privileges, a structural guarantee rather than one resting on connection-pool reset-on-close behaving correctly. No real consumer yet (the SAF-T sweep, FIS-23, doesn't exist) — mechanism only, the same shape as everything else deferred this way. **Verified live**: `SystemContextIntegrationTests.cs` — `brasa_system` sees rows across multiple tenants with zero session variable set, `brasa_app` stays exactly as isolated as before, a write and a DDL statement both rejected with `insufficient_privilege`, and the real `ResolveAsSystem()` → EF path proven end to end, including the non-obvious discovery that the RLS policy admitting every row is only half the story: EF's own query filter still compiles to `TenantId == Guid.Empty` for a system context and still shows nothing until the caller calls `IgnoreQueryFilters()` explicitly — now written down in `multi-tenancy.md` since it's exactly the kind of thing that looks like a bug the first time someone hits it. Adding this test class hit the `BRASA_MIGRATIONS_CONNECTION` env-var race a third time (see the ORD-21 entry above) — fixed for good with a shared `TestRoles.EnsureAppAndSystemRolesExistAsync` helper, since two existing test classes had each been hand-rolling their own `brasa_app`-only role setup and both started failing with "role brasa_system does not exist" the moment real migrations began granting to it too. Full suite green (101 backend, 188 E2E, two confirmed pre-existing QA-02 table-pool flakes — `order-course-firing.spec.ts`/`transfer-table.spec.ts`, a different pair of unrelated UI specs than any prior recurrence, clean in isolation both times) — `infra/scripts/verify.ps1` also re-confirmed no OpenAPI drift, since this entire task never touched a request/response shape, only new possible error responses and pure persistence/DI plumbing; and API-14's own long-named gap ("not true breaking-change detection yet — it can't tell an additive change from a removed field") is closed too, deliberately as a conservative, high-confidence subset rather than an exhaustive OpenAPI-diff engine — `infra/scripts/check-breaking-changes.mjs` flags a removed operation, a newly-required parameter, a parameter that became required, a request body (or one of its properties) that became required, a removed 2xx/3xx response status, or a property removed from a 2xx/3xx response's own top-level schema, resolving at most one `$ref`/nullable-`oneOf` level rather than recursing — named, not hidden. Calibrated against this repo's own real commit history (every commit that ever touched `docs/openapi/v1.json`, each diffed against its own parent, not only hand-built fixtures): it stayed silent on every purely-additive commit and correctly flagged IDN-11's manager-authorisation gate — a newly-required `managerStaffId`/`managerPin` on three already-shipped endpoints — as the genuine breaking change it actually was. It also surfaced one honest false-positive class worth naming rather than hiding: API-15/API-13's own response-description commit made several DELETE/POST operations' *documented* status disappear (200 → the real 204/201, finally described accurately) — the tool can only diff what's written, not what the runtime actually returned before `.Produces<T>()` existed, so a doc *correction* reads identically to a real removal. Non-blocking everywhere it runs, on purpose: unlike drift (always a bug), a breaking change is sometimes the intended one, exactly what IDN-11 legitimately did — forcing every intentional case through a bypass flag would just train the habit of ignoring the report. Wired into both `verify.ps1` (diffing the last commit's own document against the freshly-regenerated live one) and CI's `openapi-drift` job (`fetch-depth: 2` so `HEAD^`'s committed document is reachable, diffing that against the just-verified-current one) — the CI half is written and proven-equivalent locally via `verify.ps1`, but, the same honesty this project already applies elsewhere to untested CI changes, not yet exercised by a real push. Verified live with Node's own built-in test runner (`node --test`, zero new dependency for one script) — 16 tests, including a regression case for a real bug found along the way: `git show ... > file` in PowerShell writes a UTF-8 BOM Node's `JSON.parse` chokes on, the same class of PowerShell-encoding trap this file already names more than once, fixed by stripping it defensively in `loadDoc` rather than trusting every caller to know not to use `>`; and cash payments exist too (PAY-01/02) — a new `Brasa.Modules.Payments` module, previously not even a stub project, now owns a real `payments` schema: `Payment` (`OrderId` a plain opaque reference to Ordering's `Order`, the same convention `Order.TableId` uses for a Floor `Table` — Payments never queries Ordering directly, module-boundaries.md rule 4), `Method` (`PaymentMethod.Cash`, the only case today), `AmountDue`/`AmountTendered` snapshotted at record time, a computed `Change`, `PaidAtUtc`. `POST /orders/{id}/payments` composes `PaymentsDbContext` and `OrderingDbContext` at the API layer (the same Catalog+Identity composition shape `PriceListEndpoints` already uses) and reads the order's own current total server-side rather than trusting a client-sent amount due, rejecting a tender that doesn't cover it (`payment.insufficient_tender`), an invalid amount (`payment.invalid_amount_tendered`), or an unsupported method (`payment.unsupported_method`); `GET /orders/{id}/payments` lists every payment recorded, sorted client-side since SQLite (ADR 0012) can't translate `ORDER BY` over `DateTimeOffset`, the same limitation `TaxRuleEndpoints.GetTaxRulesAsync` already works around. Deliberately purely additive — does not gate `Order.Close()` and doesn't care whether an order is already closed, the same "mechanism before the trigger" shape this codebase already used for `TaxRule`, price lists, and DAT-07's own role; full tender only, `Payment`'s own constructor throws if the amount tendered doesn't cover the amount due, since splitting a bill across several tenders or methods is a deliberately separate later task (PAY-04/05). `pos`'s receipt screen gets a new `CashPayment` panel — enter what the guest handed over, submit, see the change due; extracting its error-formatting helper (`describeError`) out of `App.tsx` into its own `lib/describeError.ts` module was needed to avoid a circular import (`App` → `Receipt` → `CashPayment` → `App`), the first time this codebase's component tree needed that kind of extraction. Caught one own cosmetic bug before it shipped: the `payment.insufficient_tender` message first built its two amounts via `Money.Format(CultureInfo.InvariantCulture)`, which renders a bare unlabelled "¤" symbol with no currency code for an API error string nobody localizes — switched to `Money.ToString()`'s plain diagnostic form ("300 EUR") instead. **Verified live**: `payments.spec.ts` — a tender covering the total with correct change, a tender below the total rejected, zero/negative amounts and an unsupported method and an unknown order all rejected with their own code, listing payments (empty, then populated, then 404 for an unknown order), recording a payment *after* close proving close isn't a precondition, and the `pos` UI round-tripping a real tender through a real browser. Full suite green (101 backend, 194 E2E, one confirmed pre-existing QA-02 table-pool flake — `transfer-table.spec.ts`'s own UI test, the same failure shape as every prior recurrence, clean in isolation) — the pre-existing 188 all still pass unchanged; `infra/scripts/verify.ps1` also confirmed no OpenAPI drift beyond the two new documented routes and no new vulnerable transitive package; and partial payment support landed the same session too (PAY-05) — `Payment`'s constructor no longer requires `AmountTendered` to cover `AmountDue`; instead `RecordPaymentAsync` sums every prior payment's own new `AmountApplied` computed property (the smaller of what was tendered and what was due) and subtracts that from `order.Total` server-side to get the *remaining* balance, passing that in as the new payment's own `AmountDue` — so `AmountDue` now means "what's still owed at this moment," not always the order's full total. `Change` is redefined as `AmountTendered - AmountApplied` (always zero unless this particular tender overpaid) and a new `RemainingBalance` computed property (`AmountDue - AmountApplied`) tells the caller whether the order is now settled. A tender smaller than what's owed is no longer an error — it's a valid partial payment, recorded as-is; a further payment once the balance is already zero is rejected with the new `400 payment.already_settled`, replacing the now-removed `payment.insufficient_tender` (whose old meaning — "this tender doesn't cover the total" — no longer describes an error case at all). This closes the "nothing stops a double payment" gap the feature's own docs had explicitly left open as a known trade-off when PAY-01/02 shipped. `pos`'s `CashPayment.tsx` now accumulates every tender it records in local state instead of replacing itself after one success, showing the running remaining balance and a small history list, and only renders the "done" confirmation once a tender brings the balance to zero — the one new UI wrinkle this needed was purely additive (no new circular-import risk, unlike PAY-01/02's own `describeError` extraction). **Verified live**: `payments.spec.ts` grew from 6 to 7 tests — a partial tender followed by a settling tender that itself overpays, proving `AmountApplied`/`Change`/`RemainingBalance` compose correctly across two payments rather than just working in isolation; a further tender against an already-settled order rejected with `payment.already_settled`; and a second real-browser UI test recording two tenders through the receipt screen, asserting the remaining-balance text and the history list appear between them. Full suite green (101 backend, 195 E2E) — `infra/scripts/verify.ps1` also re-confirmed no drift beyond the documented `PaymentDto` field additions (`amountApplied`, `remainingBalance`) and no new vulnerable package; and tips exist too (PAY-06) — `Payment` grows `TipAmount` (a real persisted column via a new `AddTipToPayment` migration, hand-edited so its default currency string deserializes existing rows correctly rather than throwing on next read) and `AttributedStaffId` (nullable, a plain opaque reference to Identity's `Staff`, same convention `Room.AssignedStaffId` uses). A tip rides along on the same `Payment` row as its tender rather than its own row, entirely separate from `AmountDue`/`AmountApplied`/`RemainingBalance`; attribution is optional even when a tip is given (an unattributed tip goes to a shared pool), but a *named* staff id must resolve to a real `Staff` (`404 identity.staff_not_found`) before the payment is ever constructed, composing three modules (Payments+Ordering+Identity) at the API layer for the first time in this endpoint. `GET /orders/{id}/payments` batch-resolves every distinct attributed name in one query, never N+1. `pos`'s `CashPayment.tsx` gets an optional tip field, auto-attributed to whoever is signed in (WEB-07) since there's no staff picker on the receipt screen. **Verified live**: `payments.spec.ts` grew to 10 tests — a tip attributed to a real staff member with the name resolved both on record and on list, an unattributed tip, a negative tip and an unknown staff id each rejected with their own code, and a UI test signing in then recording an auto-attributed tip. Full suite green (101 backend, 198 E2E); and card tenders exist too (PAY-03) — `PaymentMethod.Card`, needing no migration at all since `Method` was already a plain `character varying(20)` string column with room to spare. The one new domain rule: a card tender can never exceed the balance (`400 payment.card_tender_exceeds_balance`) since a standalone TPA has no change to give back, enforced twice on purpose — once in `Payment`'s own constructor (the real invariant) and mirrored in `RecordPaymentAsync` before ever constructing one, so the API returns a proper `Result`/`Error` (hard rule 6) instead of letting the domain `ArgumentException` bubble as an unhandled fault. A card tender smaller than the balance is still a valid partial payment, identical to cash (PAY-05). `pos`'s `CashPayment.tsx` gets a Cash/Card `<select>` (`@brasa/ui`'s `SelectField`, the same shared-component convention WEB-02 established), defaulting to Cash and persisting across however many tenders it takes to settle; the payment-history list now labels each tender with its method. Picking up this task also caught two documentation gaps the immediately-prior PAY-06 session had left behind — `docs/features/cash-payments.md` was never updated for PAY-06 at all (still described "cash only" with a stale Open Questions section), and this file had no PAY-06 entry either — both backfilled in the same commit as PAY-03 rather than left to compound further, the same "fix it in the same commit as whatever proved it wrong" instinct this file's own header names. **Verified live**: `payments.spec.ts` grew to 13 tests — a card tender covering the total exactly with zero change, a card partial payment tracked the same as cash, an overpaying card tender rejected then the exact remaining balance settling normally, plus the pre-existing "unsupported method" test switched off `'Card'` since it's a real method now; a UI test switching the method selector to Card and settling across a partial-then-exact pair, the history list's own localized "(Cartão)" label confirming the selected method actually reached the recorded payment. Full suite green (101 backend, 201 E2E, one confirmed pre-existing QA-02 table-pool flake — `transfer-table.spec.ts`, the same failure shape as every prior recurrence, clean in isolation) — the pre-existing 198 all still pass unchanged; `infra/scripts/verify.ps1` also confirmed no OpenAPI drift beyond the endpoint summary text (the wire shape for `method` was always a plain `string`, so PAY-03 needed no schema widening at all) and no new vulnerable package; and split tender across methods exists too (PAY-04), picked up the same session once PAY-03 made a second method to split across meaningful — `POST /orders/{id}/payments/split` records several tenders (any mix of `Cash`/`Card`) as one atomic action: each applies in order against what's left after the ones before it in the same batch, and if any tender is rejected, nothing in the batch is persisted. Deliberately not an explicit `BeginTransactionAsync` — nothing is added to `PaymentsDbContext` until every tender in the batch has validated in memory, so the endpoint's own single `SaveChangesAsync` call (already one implicit EF Core transaction the moment more than one entity is added) either writes every row or none, atomic by construction. Extracted a shared `BuildTenderAsync` helper (method/amount/tip/card-no-overpay/staff-attribution validation, returning `Result<(Payment, string?)>`) out of the single-tender endpoint's own body so both endpoints apply identical rules rather than two hand-maintained copies that could drift — the single-tender endpoint itself shrank to a thin wrapper around it. Deliberately no tip/staff fields on a split — attributing a tip to one portion of a split is ambiguous ("which tender earned it?"), left unresolved rather than guessed at. `pos`'s `CashPayment.tsx` gets a "Split between cash and card" toggle: two linked amount fields where typing one auto-fills the other with whatever's left of the balance, submitting both together in one click. Before writing new code, checked whether PAY-04 was actually still open at all — PAY-03+PAY-05 already let a guest pay cash-then-card across two *sequential* requests, so the literal "split across methods" capability technically already existed; built the atomic single-request version anyway since the sequential path has a real, if narrow, correctness gap an atomic one closes (a dropped connection between two sequential requests can leave a confusing half-settled state with no way to tell "interrupted split" from "genuine partial cash payment"). Picking this up also caught two documentation gaps the immediately-prior PAY-03 session's own commit message named it was fixing for PAY-06 — turned out it had, in turn, left an now-inaccurate claim behind in its own new prose ("today's model only lets a guest pay in several tenders of the *same* method," written when only `Cash` existed and never revisited once `Card` made it false) — corrected in the same commit as the code that finally made that sentence obsolete for real, the same "fix it in the same commit as whatever proved it wrong" instinct this file's own header names, now caught a second time by the same instinct that first triggered it. **Verified live**: `payments.spec.ts` grew to 16 tests — a genuine cash+card split settling an order exactly, both rows confirmed via a follow-up `GET /payments` (not just the split call's own response body); an invalid tender partway through a batch leaving zero rows persisted, proving the all-or-nothing atomicity; an empty `tenders` array rejected; and a UI test filling the split form's cash leg, watching the card leg auto-compute, and settling in one click. Full suite green (101 backend, 204 E2E, no flakes this run) — the pre-existing 201 all still pass unchanged; `infra/scripts/verify.ps1` also confirmed real additive OpenAPI drift for the genuinely new route (regenerated and typechecked through `web/sdk`) and no new vulnerable package; and cash sessions exist too (PAY-08) — `CashSession`, a new table in the existing `payments` schema (no new module), a staff member's starting float declaration against a `Terminal` (Identity), both `TerminalId`/`OpenedByStaffId` plain opaque references, the same convention `Payment.OrderId` already uses. Purely a record — no payment endpoint requires an open session to exist yet, the same "mechanism before the trigger" shape this codebase already uses everywhere. Only one open session per terminal at a time, enforced at the API layer (`409 cash_session.already_open`) before a second row is ever constructed, since the check needs to see every sibling session for the terminal — a query, not a domain invariant of one row. `POST /cash-sessions` opens one, `POST /cash-sessions/{id}/close` is a bare status flip (a blind count and variance report are PAY-10/11's own later task), `GET /terminals/{id}/cash-sessions/current` resolves the open one. `admin` gets a new "Cash sessions" nav entry, one row per terminal registered at the site, an open (staff picker + float) or close action depending on current status. Generated the migration for real via `dotnet ef migrations add` this time rather than hand-writing one from scratch, since a whole new table (not just new columns on an existing one, unlike PAY-06's own `AddTipToPayment`) is more surface area to get right by hand — caught two real bugs live doing it properly instead of assuming: the `EnableFor`/`EnableSystemReadFor` argument order is `(table, schema)`, backwards from what analogy to the existing same-named `payments.payments` table's own migration call suggested, surfaced immediately as `schema "cash_sessions" does not exist` the first time `dotnet run` actually applied it against real Postgres; and `Results.Ok<T>(null)` (the first attempt at `GET .../current` with nothing open) writes a zero-byte body, not the JSON literal `"null"`, caught by the E2E suite's own first real call throwing `Unexpected end of JSON input` — fixed by returning `204 No Content` instead, both now written up as their own trap entries (§7). **Verified live**: a real Postgres smoke test by hand (open → `GET current` → duplicate rejected 409 → close → `GET current` 204 → reopen → negative float/unknown terminal/unknown staff each rejected) before `cash-sessions.spec.ts` even existed, which is exactly how both traps above were caught before a commit; backend suite (101 tests) + `verify.ps1` clean; `cash-sessions.spec.ts` — 6 tests: open-then-current, duplicate-open rejected, close-then-204-then-reopen, three rejection cases, two 404 cases, and a real `admin` UI test opening and closing a session through the real browser, every test creating its own fresh terminal rather than sharing the seeded demo "Caixa 1" since only one session can be open per terminal and E2E workers run in parallel. Full suite green (101 backend, 210 E2E) — three pre-existing QA-02 table-pool flakes surfaced across two full runs, a different unrelated spec each time, all confirmed clean in isolation; the shared dev database's own table pool (32 seeded, now past 210 E2E tests sharing it) is visibly closer to its documented "doesn't scale indefinitely" limit than it's ever been — recovered by hand via the documented runbook (closed leftover open orders, cleared dirty tables) rather than by any code change, since this session's own new work never touches the floor/table pool at all (cash sessions key off `Terminal`, an entirely separate Identity concept). A new `docs/features/cash-sessions.md` page exists too, plus a small backfill: `cash-payments.md`'s own index row in `docs/features/README.md` still said "PAY-01, PAY-02" despite covering PAY-03/04/05/06 for several commits running — corrected in the same commit as the code that made the drift obvious yet again, the same repeating "fix it in the same commit as whatever proved it wrong" instinct this file's own header names, now caught a third time in as many sessions.

---

## 1. What this is, in sixty seconds

**Brasa** — Portuguese for the glowing embers of a grill, after *frango na
brasa*. Every assembly, namespace and container is named `Brasa.*`.

A multi-tenant restaurant management SaaS for **Portugal**, built by a **solo
developer**, targeting a first live restaurant in **~6 months**.

The dominant constraint is not product features — it is **Portuguese fiscal
law**. Invoicing software used in Portugal must be certified in advance by the
Autoridade Tributária (AT) under Portaria 363/2010. Fines for non-certified
software run €3,000–€18,750 per infraction. Certification is granted to the
software *producer*, so it gates the ability to **sell**, not to **build**.

Three requirements collide and shape everything:

1. A restaurant's internet **will** fail; service cannot stop.
2. Fiscal documents need a **chained RSA signature over a gapless per-series
   sequence**, so signing must work offline — but a browser cannot hold a key.
3. A PWA **cannot open raw TCP sockets**, so it cannot drive ESC/POS printers.

All three are solved by the **Site Agent**: a .NET worker running inside each
restaurant. This is the single most consequential design decision here. If you
understand only one thing, understand that.

## 2. Hard invariants — never violate these

| # | Rule | Why |
|---|---|---|
| 1 | Money is `Money` (integer minor units). Never `double`/`float`/bare `decimal` | Totals must reconcile to the cent against SAF-T and the Z report |
| 2 | Split money with `Allocate`, never division | Dividing €10 three ways loses a cent |
| 3 | Never call `DateTime.UtcNow`. Inject `IClock` | Fiscal `SystemEntryDate` must be monotonic per series; the chain must be testable |
| 4 | Never mutate an issued fiscal document | Corrections are credit notes. An invisible-alteration path is a **certification failure** |
| 5 | Modules never reference or query each other | Use integration events. This is what keeps later extraction from being a rewrite |
| 6 | Expected failures return `Result`, not exceptions | Exceptions are for genuine faults only |
| 7 | Never weaken `TreatWarningsAsErrors` | Suppress in `.editorconfig` **with a written reason**, or fix it |
| 8 | Signature chaining is **per-series**, never global | Two series advance independently |
| 9 | **No cookie auth. No web-only assumptions in the API** | Android and iOS ship soon after web and must need *zero* backend change |
| 10 | Every realtime message must have a **REST equivalent** | A platform with no usable SignalR client must still work, degraded but correct |
| 11 | Error codes are a **public contract** — once released, the meaning never changes | Mobile clients branch on them and cannot be patched quickly. Enforced, not just stated: [error-codes.md](../architecture/error-codes.md) + `ErrorCodeRegistryTests` (API-04) |

## 3. Where things stand

**Authoritative inventory: [../product/status.md](../product/status.md).** Read
it — it marks every empty project explicitly, because a scaffold makes empty
things look finished.

Condensed:

- ✅ **Built, tested, and proven live** (not just unit-tested — see §3a):
  `Money` (17 tests), `Result`/`Error` (18th test: the error-code registry,
  API-04 — see [error-codes.md](../architecture/error-codes.md)),
  `PortugueseTimeZone` (14 tests — IANA ids resolve on this runtime, Azores
  stays 1h behind the mainland year-round, the same instant can land on two
  different business days in different regions, the exact scenario the
  type's own doc comment warns about), `Result`/`Error`/`ErrorMapping`
  (23 tests across `Brasa.Shared.Tests` and `Brasa.Api.IntegrationTests` —
  `Value` on a failed `Result<T>` throws with the error code named, and all
  6 `ErrorType`→HTTP status mappings are pinned directly rather than only
  through whichever status each endpoint's own tests happen to trigger),
  tenancy +
  **real RLS** (DAT-01…06),
  `Catalog` (categories/items, seeded, soft delete — CAT-18, modifier groups
  — CAT-03/04, description + declared allergens — CAT-02, a fixed EU-wide
  taxonomy, not a rate awaiting an accountant's confirmation like `VatRate`,
  bulk CSV import — CAT-17, still 🚧, Excel not built, rows import
  independently so one bad row doesn't fail the file),
  `Ordering` (open against a real table/add-line-with-modifiers
  — ORD-05/per-line kitchen notes — ORD-06/line and order discounts,
  percentage or fixed, composing — ORD-11, no manager-authorisation gate
  yet (IDN-11)/transfer to a different table —
  ORD-12/transfer a single line to a different order — ORD-13/merge two
  orders — ORD-14, new `OrderStatus.Merged`, no migration needed/split
  evenly, by item or by cover — ORD-15/16/17/takeaway order with no table —
  ORD-20, `IsTakeaway`/pre-bill preview — ORD-18/19, provably non-fiscal,
  see §7/close/history-search — ORD-22), `Floor` (rooms, tables, full `Free ⇄ Occupied ⇄ Dirty ⇄ Free`
  lifecycle, `xmin` optimistic concurrency — FLR-01/02/04), `Fiscal` contract
  + `Fiscal.Mock`, API layer (versioning, ProblemDetails, idempotency, CORS,
  the full order flow composing all four modules), the `pos` web shell
  (React 19 + Vite + TS, table-picker → order incl. a modifier picker →
  receipt, WEB-01/05, pt-PT default / en toggle behind a mobile-portable
  cookie seam — WEB-13, ADR 0011), the `admin` back-office shell (WEB-09 —
  React 19 + Vite + TS on port 5174, own full pt/en toggle sharing `pos`'s
  `brasa.lang` cookie, genuinely English in English mode since not every
  staff member is a Portuguese speaker) with its first real editor
  (WEB-10's menu slice — toggle category visibility, 86/reprice/delete an
  item, bulk-import more via CAT-17's CSV pipeline, all backed by a new
  `GET /menu/all` that deliberately doesn't filter the way the guest-facing
  `GET /menu` does; floor-plan editing, FLR-03, wasn't built yet at this
  point in the project's history — it is now, CRUD and the drag-and-drop
  canvas both), a Playwright
  E2E harness driving the real
  UI (`src/web/e2e`, QA-01/03/05/14 incl. axe-core accessibility scans, 134
  tests green on a clean run — the seeded floor plan was doubled to 16
  tables after back-to-back full runs started exhausting the original 8, a
  QA-02 scaling limitation, not a product bug; see
  [e2e-testing.md](../development/e2e-testing.md)), an idempotency replay
  harness (QA-11 — a mutating request replayed 3× with the same
  `Idempotency-Key` is byte-identical and runs its side effect exactly
  once; specifically proves a retried `POST /orders/{id}/close` never
  issues a second fiscal document, the scenario `IdempotencyMiddleware`'s
  own doc comment names),
  `Brasa.Api.IntegrationTests` (DAT-11,
  QA-09/10 — real Testcontainers Postgres proving tenant isolation by
  automated test, not just manual psql anymore), a liveness/readiness split
  (`/health`, `/health/ready` — OPS-09, live-verified against a stopped and
  restarted PostgreSQL container), `ETag`/`If-None-Match` caching on
  `GET /menu` (API-10 — deliberately not `GET /floor`, whose state changes
  too continuously for caching to pay off; live-verified 200→304, and the
  repeated-run E2E discipline caught a real bug where the helper's own JSON
  serialization used PascalCase instead of the app's configured camelCase,
  see [status.md](../product/status.md)), client version negotiation
  (API-06/07 — best-effort `X-Brasa-Client` header parsing that enriches
  every log line for the request via Serilog's `LogContext`, plus
  `GET /client-requirements` looking up the caller's client id in a
  config-bound policy; ships ahead of any client that sends the header or
  calls the endpoint yet), RFC 8594 `Deprecation`/`Sunset` response headers
  (API-08 — config-bound under `Api:Deprecation`, empty by default, so a
  no-op until a real `/api/v2` gives it something to announce), rate
  limiting per `(tenant, X-Brasa-Client client id)` on `/api/**` (API-12 —
  a real, generous production default (1000 req/60s) plus a much higher
  dev-only override, because every dev/E2E client shares one bucket per
  tenant until a client actually sends the header; a sixth `ErrorType`,
  `RateLimited` → 429, joined the five `ErrorMappingTests` already pinned),
  cursor pagination on `GET /orders` (API-09 —
  the one genuinely unbounded collection today; additive via a new
  `X-Next-Cursor` response header, not a breaking change to the
  already-shipped body shape), Brotli/gzip response compression incl.
  `application/problem+json` error bodies (API-11 — safe over HTTPS here
  since there's no cookie-reflected secret for BREACH to exploit, ADR 0008),
  the `BillRequested` floor-plan signal (FLR-04 — `POST /tables/{id}/request-bill`
  plus a "Pedir conta" button; the CSS and i18n for it existed before the
  endpoint did, see §7), 86-ing a menu item (CAT-13 — `MarkAvailable`/
  `MarkUnavailable` and `AddLine`'s guard for both existed since I0 with no
  endpoint to reach them, the same shape as FLR-04) and menu item
  repricing (CAT-19, newly minted — `MenuItem.Reprice` and its
  negative-price guard existed since I0 too, a third instance of the same
  pattern; verified live that an already-open order's line survives a
  reprice unchanged, not just that the flag flips), and menu category
  visibility (CAT-01 — a fourth instance one level up: `MenuCategory.IsVisible`
  had no setter at all, despite the row's own title naming "visibility"
  and being marked done; hiding a category now removes it and every item
  under it from `GET /menu` in one call), a committed OpenAPI
  document ([docs/openapi/v1.json](../openapi/v1.json),
  API-13 — regenerated by hand for now; CI drift-checking is the separate,
  not-yet-built API-14), Docker Compose
  (PostgreSQL 18 + Seq), full docs tree, CI (including an `e2e` job —
  written, not yet run in CI).
- 📁 **Empty projects (structure only, zero logic):** `Modules.Payments`,
  `Modules.Reporting`, `Fiscal.Portugal`.
- 🚧 **Stub:** `SiteAgent` starts and stops; nothing else. `Modules.Identity`
  has its first real slice (IDN-01 — Organization/Site/Terminal registry,
  create + list only) but auth/staff PINs/pairing, the rest of the epic,
  is still unbuilt.
- ⬜ **Not started:** `kds`/`order` web clients, deployment.

**Delivery is incremental** — vertical slices, each ending in a runnable demo.
**[../product/roadmap.md](../product/roadmap.md) says what to build next**;
[../product/backlog.md](../product/backlog.md) holds the 292 tasks and their
status. Reference IDs in commits: `feat(identity): terminal pairing (IDN-07)`,
and update the status in the same commit.

**Current increment: I0 is done except deployment (OPS-11).** I1 ("Menu and
floor," see roadmap) is well underway — floor plan (FLR-01/02/04, WEB-05),
the floor-plan editor including its drag-and-drop canvas (FLR-03), modifiers
(CAT-03/04), price lists (CAT-05), the `admin` back-office shell (WEB-09) and
its menu editor (WEB-10) are done and proven; staff/reporting screens
(WEB-11) are only the staff half.

Backend/I0 tasks — **done**: DAT-01/03/04/**05**/06/**11**/10 · API-01/03/05 ·
CAT-**01**/02/03/04/07/**13**/**17**/18/**19** ·
ORD-01/02/03/04/**05**/**06**/**12**/**13**/**14**/15/**16**/**17**/**18**/**19**/**20**/**22** ·
FIS-01/02/03 · WEB-01/05/13 · QA-01/03/05/**09**/**10**/**11**/**14** · FLR-01/02/**04** ·
API-**04**/**06**/**07**/**08**/**09**/**10**/**11**/**12**/**13**/**18** · OPS-**09**. API-14 is 🚧 (drift detection, not semantic breaking-change detection).

**Not in I0:** auth, offline, printing, real fiscal, menu editing, KDS.

> RLS (DAT-05), idempotency (API-05) and `/api/v1` (API-01) were in I0 **on
> purpose**, and it was the right call: RLS in particular turned out to be
> silently broken (§3a) in a way that would have been far more expensive to
> discover after other modules copied the same pattern.

### 3a. Verified live — and what that caught

I0's backend was driven end-to-end against a real API process and a real
PostgreSQL container, not only against unit tests: open a table → mixed
food+alcohol order → even 3-way split → close → fiscal document, plus an
idempotency replay checked directly against the database. Full script and
numbers: [../product/status.md](../product/status.md#i0-demo-verified-live-not-just-unit-tested).

This surfaced three real bugs that `dotnet build` and the pre-existing unit
tests both missed:

1. **RLS was inert.** The bootstrap Postgres role is a superuser; superusers
   bypass RLS unconditionally, `FORCE ROW LEVEL SECURITY` notwithstanding. Now
   [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md).
2. **New order lines were tracked `Modified`, not `Added`.** EF Core's `Guid`
   key convention assumes a non-default key means "already exists." Fixed with
   `ValueGeneratedNever()` in `ApplyEntityConventions`.
3. **VAT was computed backwards.** Menu prices are VAT-inclusive under
   Portuguese law; the fiscal document must derive net/VAT from gross, not add
   VAT on top. See §7 and `docs/fiscal/README.md`.
4. **`Table.Occupy()` had no database-level concurrency guard.** Two
   concurrent "open this table" requests could both read `Free`, both
   transition in memory, and both successfully save — EF's default
   `SaveChangesAsync` is a blind `UPDATE ... WHERE Id = @id` with nothing to
   notice a second writer got there first. Found by E2E flakiness under
   Playwright's 2-worker parallelism, not by inspection. Fixed with an
   `xmin`-based concurrency token (`TableConfiguration.cs`) — see §7 and
   [status.md](../product/status.md#a-real-concurrency-bug-found-by-running-the-suite-enough-times).

**The lesson, not just the fix:** a clean build and green unit tests proved
nothing about whether tenant isolation actually worked, and a single passing
E2E run proved nothing about whether concurrent access was safe. If you build
something that depends on database-level behaviour (RLS, triggers,
constraints, concurrent writes), run it against the real database, under
real concurrent load, and try to break it before calling it done.

The `pos` web shell was first verified only at the wire level — `curl`
reproducing the exact browser request sequence (CORS preflight, `Origin`
header, `Idempotency-Key`) against the running API — because no
browser-automation tool was available in that session. That gap is now
closed: a Playwright harness (`src/web/e2e`, QA-01/03/05) drives the actual
rendered UI in a real Chromium instance and passes, verified both against
already-running dev servers and from a hard cold start (both processes
killed, Playwright's own `webServer` config launching them from nothing). See
[../development/e2e-testing.md](../development/e2e-testing.md). What's
**still** unverified: the new `e2e` CI job itself — written, mirrors what
passed locally, but no push has exercised it in actual GitHub Actions yet.

## 4. Repo map

Detailed file-by-file inventory: [repo-map.md](repo-map.md).

```
src/backend/
  Brasa.Api               Cloud API. Minimal APIs under /api/v1
  Brasa.Shared            Shared kernel — depends on no module
  Brasa.Modules.Identity  Users, roles, staff PINs, terminal pairing
  Brasa.Modules.Catalog   Menu, modifiers, price lists, tax rules
  Brasa.Modules.Ordering  Orders, courses, splits, transfers
  Brasa.Modules.Floor     Rooms, tables, table state
  Brasa.Modules.Fiscal    IFiscalProvider, document lifecycle, audit
  Brasa.Modules.Payments  Tenders, cash sessions, tips
  Brasa.Modules.Reporting Read models, X/Z reports, VAT summaries
  Brasa.Fiscal.Portugal   ATCUD, RSA chain, QR, SAF-T, AT webservices
  Brasa.Fiscal.Mock       Deterministic fake for dev and tests
src/agent/
  Brasa.SiteAgent         In-restaurant worker: signing, printing, LAN hub
src/web/
  ui                      Shared source for pos/admin, alias-consumed, no build step — WEB-02
  pos                     POS PWA (React + TS + Vite) — I0 shell, WEB-01
  admin                   Back-office SPA (React + TS + Vite) — shell + menu editor, WEB-09/10
  e2e                     Playwright E2E harness — QA-01/03/05
tests/                            Unit, fiscal golden-file, integration
docs/                             This documentation tree
infra/                            docker-compose (PostgreSQL 18, Seq)
```

## 5. Task → where to look

| Doing this | Read |
|---|---|
| Anything fiscal | [../fiscal/README.md](../fiscal/README.md) — legal constraints, not preferences |
| Money, totals, VAT, splitting | [../architecture/money.md](../architecture/money.md) |
| Adding or changing **any endpoint** | [../architecture/api-contract.md](../architecture/api-contract.md) — the mobile-readiness rules |
| Adding a module, or a cross-module call | [../architecture/module-boundaries.md](../architecture/module-boundaries.md) |
| Tenant-scoped data, RLS | [../architecture/multi-tenancy.md](../architecture/multi-tenancy.md) |
| Offline, printing, the agent | [../architecture/site-agent.md](../architecture/site-agent.md) |
| "Why is it built like this?" | [../architecture/decisions/](../architecture/decisions/) |
| Code style, analyzer policy | [../architecture/conventions.md](../architecture/conventions.md) |
| Testing expectations | [../development/testing.md](../development/testing.md) |
| A Portuguese term you don't know | [../glossary.md](../glossary.md) |
| **What to build next** | [../product/roadmap.md](../product/roadmap.md) — increments and demo scripts |
| Task status, or a task's ID | [../product/backlog.md](../product/backlog.md) |
| Why this product is worth building | [../product/differentiation.md](../product/differentiation.md) |
| End-to-end testing | [../development/e2e-testing.md](../development/e2e-testing.md) |
| The overall plan and roadmap | [../product/plan.md](../product/plan.md) |

## 6. Decisions already made — do not re-litigate

Each has an ADR with a **"Revisit when"** section. Reopen only if a trigger
there is actually met.

| ADR | Decision |
|---|---|
| [0001](../architecture/decisions/0001-modular-monolith.md) | Modular monolith, not microservices |
| [0002](../architecture/decisions/0002-own-fiscal-engine.md) | Build our own AT-certified engine, not a partner API |
| [0003](../architecture/decisions/0003-site-agent.md) | In-restaurant Site Agent |
| [0004](../architecture/decisions/0004-react-pwa-not-blazor.md) | React PWA clients, **not Blazor** (despite a C# backend) |
| [0005](../architecture/decisions/0005-plain-guid-ids.md) | Plain `Guid` ids; isolation enforced by RLS |
| [0006](../architecture/decisions/0006-no-mediatr.md) | Hand-rolled dispatcher; MediatR is now commercially licensed |
| [0007](../architecture/decisions/0007-client-agnostic-api.md) | One client-agnostic API for every platform — **no BFF** |
| [0008](../architecture/decisions/0008-token-auth-no-cookies.md) | Token auth with PKCE, device-bound refresh, **no cookies** |
| [0009](../architecture/decisions/0009-incremental-delivery.md) | Incremental delivery; walking skeleton first, demo script is done |
| [0010](../architecture/decisions/0010-rls-runtime-role-split.md) | Split the DB role: unprivileged at runtime, superuser only for migrations |
| [0011](../architecture/decisions/0011-i18n.md) | i18next, pt default, cookie preference behind a storage seam swappable for mobile |

## 7. Traps — things that look wrong but are intentional

- **Order lines copy the item name, price and VAT rate.** That is correctness,
  not denormalisation. A receipt must show what the item cost *when it was sold*.
- **The pre-bill given to a table is a *documento não fiscal*.** Issuing it as an
  invoice would fiscalise every table that merely asks to see the bill.
  `GET /orders/{id}/pre-bill` (ORD-18/19) enforces this by construction, not
  just by convention: `PreBillDto` has no document number, ATCUD or QR field
  at all, and the handler never calls `IFiscalProvider` — it reuses
  `FiscalDocumentLine`'s gross→net/VAT math purely as a calculator. Because
  nothing is persisted or numbered, requesting it any number of times against
  an unchanged order reproduces identical figures (the "reprint" requirement,
  ORD-19) for free — verified live in `pre-bill.spec.ts`, not just asserted by
  the type shape.
- **A discount (ORD-11) never touches `OrderLine.UnitPrice`.** It would be
  tempting to apply a discount by reducing the unit price handed to
  `FiscalDocumentLine`, but that price is a snapshot taken at add-time and
  must stay exactly what the guest was charged when the line was rung up —
  the same "order lines copy the price" rule above. Instead
  `OrderEndpoints.BuildFiscalLines` renders a discount as its own **separate
  negative** `FiscalDocumentLine` (`"Desconto: {ItemName}"`) at the
  discounted line's own VAT rate. This also sidesteps a real correctness
  trap: a line-level discount doesn't divide evenly across a multi-quantity
  line (`Money` deliberately has no division operator — see
  [money.md](../architecture/money.md)), so trying to fold it into a
  per-unit price for a `quantity > 1` line would either lose a cent or need
  `Allocate`'s own remainder logic for no reason. An order-level discount is
  prorated across lines by `Money.Allocate` (the same tool `SplitByCover`
  uses) before being folded into that same per-line discount entry — so
  `order.Total` and `document.GrossTotal` reconcile to the cent by
  construction, not because the two code paths happened to agree.
- **VAT rates are data with effective dates, not constants.** They are unconfirmed
  by an accountant and politically contested. Never hardcode them.
- **Menu prices are VAT-inclusive (gross), not net.** `MenuItem.Price` and
  `OrderLine.UnitPrice` are the final amount the guest pays. A fiscal document
  *derives* net and VAT from the gross price — computing it the other way
  round makes the running order total disagree with what the fiscal document
  charges. See `docs/fiscal/README.md`.
- **The Azores are an hour behind the mainland.** This moves the daily close and
  SAF-T period boundaries. `PortugueseRegion` exists for this.
- **`pos`'s `brasa.lang` cookie is not the ADR 0008 cookie.** ADR 0008 bans
  *authentication* cookies. `brasa.lang` is a client-only UI preference the
  API never sends or reads — setting it does not weaken that rule. Don't
  "fix" it into `localStorage` on sight; the user explicitly asked for a
  cookie. See [ADR 0011](../architecture/decisions/0011-i18n.md).
- **`formatMoney` and the receipt's issued date are hardcoded `'pt-PT'`,
  even in English mode.** Not a missed i18n string — a total or a fiscal
  timestamp must not change format because staff switched their own
  interface language. See ADR 0011.
- **Table labels ("Mesa 1") are the one seeded-data string that *does*
  translate, unlike menu item names.** Real staff here include people who
  don't read Portuguese, and "Mesa" is a generic word, not an identity-
  bearing name the way a dish's is — `src/lib/tableLabel.ts` renders it as
  "Table 1" in English (display-only; `Table.Label` itself is untouched).
  Don't assume every seeded string follows the menu-item precedent of
  staying untranslated — check whether it's actually content, or just a
  generic label wearing a Portuguese word. See ADR 0011.
- **`OpenOrderAsync`/`CloseOrderAsync` save two `DbContext`s sequentially, not
  in one transaction — and each saves them in the OPPOSITE order on purpose.**
  `OpenOrderAsync` saves Floor first: that's the row with the new `xmin`
  concurrency check, so a lost race is caught *before* an `Order` exists,
  leaving nothing to clean up. `CloseOrderAsync` saves Ordering first: the
  closed-and-fiscally-issued order is the part that must never be silently
  lost, so marking the table `Dirty` afterward is deliberately best-effort.
  Don't "fix" either into a `TransactionScope` across two Npgsql connections,
  and don't assume the other handler's ordering applies here too — read the
  comment at each call site, and see
  [module-boundaries.md](../architecture/module-boundaries.md) rule 5.
- **`Table` has an `xmin`-based optimistic concurrency token, and its
  migration's `Up()`/`Down()` are deliberately empty.** `xmin` is a
  PostgreSQL system column every row already has — `dotnet ef migrations add`
  scaffolds an `ADD COLUMN xmin`, which Postgres rejects outright (the name is
  reserved). The migration exists only so EF's model snapshot knows about the
  shadow property; there is no DDL to run. If you regenerate this migration,
  strip the `AddColumn`/`DropColumn` calls again.
- **`Order.TableId` is a bare `Guid`, not a navigation property, and
  Ordering never queries `floor.tables`.** Same pattern as
  `OrderLine.MenuItemId` — a cross-module reference is an opaque id an
  endpoint resolves, never a join. `TableLabel` is the part that gets
  snapshotted (for the receipt-history reason above); `TableId` deliberately
  isn't.
- **A takeaway order's `TableId` is `Guid.Empty` (ORD-20) — never check that
  directly.** Check `Order.IsTakeaway` instead. `Guid.Empty` is not a magic
  value anywhere else in this codebase; it means exactly one thing here
  ("this order was opened with `OpenTakeaway`, so there is no Floor table to
  look up"), and `TransferOrderAsync`'s existing "table might not exist"
  handling already treats it correctly by coincidence — don't add a special
  case that assumes otherwise. `TransferToTable` clears `IsTakeaway` back to
  `false` when a takeaway order lands on a real table; nothing ever sets it
  the other way.
- **The seeded floor plan has 32 tables (doubled twice: 8→16, then 16→32 —
  see below), and the dev database is not reset between E2E runs.** Every
  spec that opens a table
  (`src/web/e2e`) must close the order and clear the table before finishing,
  or repeated runs exhaust the free-table pool — and because table state is
  real contended state now (the `xmin` token above), specs pick a table via
  `openOrderOnAnyFreeTable` / `openAnyFreeTable`, which retry on a 409
  instead of assuming the first "free" table they see is still free by the
  time the request lands. See `tests/support/api.ts` and `tests/support/ui.ts`.
  Even at 16, back-to-back full runs with no pause can still occasionally
  exhaust the pool once the suite is large enough — a QA-02 scaling
  limitation (the dev database isn't disposable per run), not a product bug.
  If a run fails with "No free table available", check `GET
  /orders?status=Open` for a leftover order, close it, and `POST
  /tables/{id}/clear` any table stuck `Dirty`.
- **Any endpoint that serializes JSON itself (bypassing `Results.Ok`/
  `Results.Json`) must resolve `IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>`
  and use its `SerializerOptions` — never call `JsonSerializer` with no
  options.** `Results.Ok(dto)` uses the app's configured options, which are
  camelCase; `JsonSerializer.Serialize(dto)` with no options defaults to
  `System.Text.Json`'s own PascalCase. `ETagResults.OkWithETag` (API-10) got
  this wrong on the first pass — it built silently, passed every backend
  test, and only broke visibly when `pos`'s menu screen crashed on
  `category.items` being `undefined`. See `ETagResults.cs` and the API-10
  entry in [status.md](../product/status.md).
- **An E2E test that filters `GET /orders` by a time window (`openedFrom`)
  and asserts an exact result count is not safe under `fullyParallel`.**
  Other specs create orders concurrently on the other Playwright worker, so
  a window like "everything opened since I started this test" can pick up
  rows this test didn't create — a page can legitimately come back longer
  than expected. `order-history.spec.ts`'s cursor-pagination test (API-09)
  hit exactly this: its first version asserted page lengths of 2 then 1 and
  flaked. Fixed by walking the full `X-Next-Cursor` chain and asserting
  only facts that hold regardless of concurrent noise — each of this
  test's own created order ids appears exactly once across all pages, and
  the "full page has a cursor, short page doesn't" invariant holds on every
  page — never an exact total row count.
- **`pos` never dims text with CSS `opacity` for visual hierarchy.** It looks
  fine to a sighted reviewer and quietly fails WCAG contrast anyway —
  `opacity` blends the color toward whatever's behind it, so the *effective*
  contrast is lower than the raw foreground color suggests. `accessibility.spec.ts`
  (QA-14) caught five of these on its first run. Pick a genuinely-compliant
  color instead; see [status.md](../product/status.md#accessibility-first-scan-five-real-fixes).
- **`Money.Format(culture)` is not called `ToString`.** Deliberate — it forces
  callers to name the culture, and keeps `ToString()` unambiguously invariant.
- **Unused-looking code isn't necessarily dead — check for a domain method
  or guard with no caller before assuming something is finished.** Four
  instances found the same way (grepping domain classes for public methods,
  then checking whether any endpoint actually calls them): `TablePicker.tsx`'s
  `.floor-table-BillRequested` CSS and `floor.state.BillRequested` i18n
  strings existed for a state — `Table.RequestBill()` — with *no* producer
  anywhere, so the UI could render a state that could never occur; FLR-04
  added `POST /tables/{id}/request-bill` to finally reach it. Separately,
  `MenuItem.MarkAvailable`/`MarkUnavailable` and `AddLine`'s
  `catalog.item_unavailable` guard for both had existed since I0 with no
  endpoint ever able to set `IsAvailable` to `false` — CAT-13's
  `PUT /menu/items/{id}/availability` closed that one. A third:
  `MenuItem.Reprice` and its own negative-price guard, same story again —
  CAT-19's `PUT /menu/items/{id}/price` closed it, and specifically proved
  live that the pre-existing snapshot safety (`OrderLine.UnitPrice` copied
  at add-time) actually holds under a real reprice, not just in the type
  shape. A fourth, one level up: `MenuCategory.IsVisible` had no setter *at
  all* — not even an unreachable one — despite CAT-01's own backlog title
  naming "visibility" as in scope and the row already being marked done.
  `PUT /menu/categories/{id}/visibility` closed it; hiding a category
  removes it and every item under it from `GET /menu` in one call. Before
  deleting UI/CSS/i18n/a domain method that looks unreferenced, check
  whether it's scaffolding for a state or guard that already exists and is
  already enforced, just missing the one endpoint that would reach it — the
  same way CAT-02's fields shipped ahead of the UI that would set them.
- **`GET /menu` and `GET /menu/all` are not interchangeable — `pos` must
  never call the second one.** `GET /menu` filters to visible categories
  and available items on purpose: it's what a guest may actually order.
  `GET /menu/all` (WEB-10) deliberately skips both filters, because
  `admin`'s menu editor needs to *see* a hidden category or an 86'd item to
  turn it back on — but that only works because `pos` never reaches it. If
  `pos` ever needs a second endpoint, that is a sign something about this
  split needs rethinking, not a reason to point it at `/menu/all`.
- **A test asserting `GET /menu`'s `ETag` is stable between two calls can
  break for a completely legitimate reason: another spec mutated the menu
  in the gap between them.** `menu-etag.spec.ts` assumed nothing changes
  `GET /menu`'s content between its own two back-to-back requests, true
  when written but no longer true once CAT-01/13/19 landed sibling specs
  that legitimately change the menu (category visibility, item
  availability, item price) as part of what *they* test. Under real
  parallel workers, one of those landing in the gap turns the expected
  `304` into a genuine `200` — the caching mechanism working correctly on
  content that actually changed, not a bug. Fixed by retrying the whole
  round trip (fresh `ETag`, immediate reuse) up to 5× rather than weakening
  the assertion — the same shape as the API-09 pagination test's fix for
  concurrent-spec interference under `fullyParallel`. If you add a spec
  that mutates catalog state, expect this class of interaction with
  anything that reads `GET /menu` and assumes it's stable.
- **"Sobremesas" is the seeded category with no name/item dependency
  elsewhere — but that stops being true the moment a second spec claims it
  too, and nothing warns you.** `menu-category-visibility.spec.ts` (CAT-01)
  picked it specifically because nothing else references it; adding
  `admin-menu-management.spec.ts` (WEB-10)'s own category-visibility UI
  test against the same category, without checking that comment first,
  reintroduced exactly the kind of collision CAT-01 had deliberately
  avoided — two specs racing to hide/show the same category under real
  parallel workers, each assuming exclusive ownership. Every *other*
  seeded category has a real dependency (Bebidas has "Imperial", Pratos
  Principais has "Frango na Brasa", both looked up by exact name in
  several specs; Entradas is referenced by name in four more), so there
  is no free fourth fixture — and no endpoint to create one. Fixed by
  making both specs tolerate a shared resource instead of assuming
  exclusivity: the id lookup goes through `GET /menu/all` (never
  filtered, so it can't itself fail mid-race), the hide/show/verify round
  trip retries as a whole (same shape as the `ETag` trap above), and
  WEB-10's own second visibility test is read-only precisely so it needs
  no exclusivity at all. Before adding a spec that mutates a seeded
  fixture, grep for what else already uses it — "nothing else references
  this" is a claim that can go stale.
- **`Fiscal.Mock` must never run in Production.** It produces structurally valid
  but fiscally meaningless documents.
- **The `RateLimiting` default in `appsettings.json` is tuned for production
  traffic, not for this repo's own dev/E2E traffic — they are not the same
  thing today.** `ApiRateLimiting` partitions by `(tenant, X-Brasa-Client
  client id)`, but no client sends that header yet, so every request from
  `pos`, `admin` *and* the entire Playwright suite falls into one shared
  `unknown` bucket per tenant. A first, production-shaped default (300
  req/60s) throttled the E2E suite itself — 6 unrelated specs failed with a
  real `429`, not a flake. Fixed with a much higher
  `appsettings.Development.json` override, not by weakening the production
  default to match dev traffic. If you ever see spurious `429`s running
  the suite locally, check this before assuming it's QA-02 concurrency
  flakiness (§ above) — the two look similar (an unrelated spec fails, and
  it isn't reproducible in isolation with a normal request volume) but have
  different fixes.
- **The web client gets a refresh-token cookie, but the API is not
  cookie-authenticated.** That cookie is scoped to the token endpoint only; every
  API call carries a bearer token, which is what keeps native clients working
  against an identical API.
- **A staff PIN is not a password.** It is a fast identity switch on hardware
  that was already authenticated by terminal pairing. It must never be accepted
  as a primary credential over the internet.
- **The solution file is `Brasa.slnx`**, the .NET 10 XML format — not `.sln`.
- **PostgreSQL 18 mounts `/var/lib/postgresql`**, not `.../data`. Mounting
  `.../data` makes the container refuse to start.
- **A new web client's origin must be added to `Cors:AllowedOrigins`
  (`Brasa.Api/appsettings.Development.json`) before it can call the API at
  all.** `admin` (port 5174) hit this directly: every fetch failed silently
  from the app's own perspective — no server-side error, no visible
  exception, just a hung `fetch` — because the browser blocks the response
  at the CORS layer before JS ever sees it. Only visible in the browser's
  network console, not in API logs. The list is read once at startup, not
  live-reloaded, so a config edit needs the API restarted, not just saved.
- **A test that calls `Database.MigrateAsync()` must build its `DbContext`
  through that module's design-time factory (`new XyzDbContextFactory().CreateDbContext([])`),
  never a hand-rolled `DbContextOptionsBuilder`.** `dotnet ef migrations
  has-pending-model-changes` (the design-time check) always goes through
  the factory, so it stays clean no matter how a test builds its own
  context — but `MigrateAsync` throws `PendingModelChangesWarning` as a
  hard error the moment a test's *live* model disagrees with the last
  migration's committed snapshot, and a hand-rolled options builder can
  silently drift from the factory in ways that don't show up in a source
  diff. Found via `TenantIsolationIntegrationTests`: CAT-14 hit this, was
  "fixed" by matching one visible setting (`MigrationsHistoryTable`) the
  hand-rolled builder was missing — which genuinely fixed *that* instance,
  but CAT-15's migration hit the identical error again immediately after,
  with `has-pending-model-changes` still reporting clean throughout. The
  real fix was routing the test through `CatalogDbContextFactory` itself
  (with `BRASA_MIGRATIONS_CONNECTION` pointed at the test's Testcontainers
  instance), which removes the category of bug entirely — there is no
  second configuration left to keep in sync, whatever the exact trigger
  turns out to be for the next new column. If a second module (Ordering,
  Floor) ever gets a similar Testcontainers test, build its context via
  its own design-time factory from the start.
- **`Seq:latest` needs `SEQ_FIRSTRUN_NOAUTHENTICATION` or it crash-loops.**
  A newer Seq release started requiring an explicit first-run admin
  password (or this opt-out) and refuses to start without one.
  `docker compose ps` / `docker ps` shows it as `Up` for a few seconds
  after every restart, which reads as healthy at a glance — it isn't;
  check `docker logs brasa-seq` or actually query `http://localhost:5341/api`
  if logs/traces seem to be going nowhere. Fixed in `infra/docker-compose.yml`;
  if this container is ever recreated from a different compose file (or the
  env var gets dropped), it will silently start failing this way again.
- **OpenTelemetry's `AddOtlpExporter` does not append a per-signal OTLP
  path to `Endpoint` — you must.** `otlp.Endpoint = new Uri("http://host:5341/ingest/otlp")`
  posts to exactly that URL and 404s against Seq (which expects
  `/ingest/otlp/v1/traces`, `/ingest/otlp/v1/metrics`, etc.). The
  auto-append-per-signal-path behaviour only exists on the separate,
  newer unified `UseOtlpExporter()` helper (which reads
  `OTEL_EXPORTER_OTLP_ENDPOINT`), not on the per-signal
  `TracerProviderBuilder`/`MeterProviderBuilder.AddOtlpExporter(...)`
  extension `Program.cs` actually uses. A failed export throws **no
  exception anywhere in the app** — it's an OpenTelemetry SDK-internal
  concern by design — so this is invisible without either checking the
  destination's own ingestion logs or temporarily subscribing a raw
  `System.Diagnostics.ActivityListener` to inspect the exporter's own
  outbound `System.Net.Http.HttpRequestOut` spans directly (which is how
  this was actually found — `dotnet ef`-style "no error" is not the same
  as "it worked"). Fixed by appending `/v1/traces`/`/v1/metrics`
  explicitly in `Program.cs`.
- **`Order.Close()`'s "at least one line" guard counts voided lines too —
  the real "can this order actually close" answer lives one layer up, in
  Fiscal.** Voiding (ORD-10) every line on an order still satisfies
  `Order.Close()` (`_lines.Count` doesn't care whether a line is voided),
  so the domain call succeeds and flips `Status` to `Closed` in memory —
  but `BuildFiscalLines` (`OrderEndpoints.cs`) omits every voided line, so
  `IFiscalProvider.IssueSimplifiedInvoiceAsync` receives zero lines and
  its own pre-existing `fiscal.no_lines` guard (there for unrelated
  reasons, predating ORD-10 entirely) rejects the whole close. Because
  `CloseOrderAsync` doesn't call `SaveChangesAsync` until *after* the
  fiscal document is actually issued, that in-memory `Close()` never
  reaches the database — the order stays genuinely `Open`, not
  closed-with-nothing-issued. This is correct, but it means "can Close()
  succeed" is not answerable by reading `Order.Close()` alone; a
  first-drafted doc comment on it guessed wrong (assumed a zero-value
  document would be issued) until the E2E suite caught the real
  `fiscal.no_lines` outcome. If a similar guard is ever added elsewhere,
  check what happens at the Fiscal boundary too, not just the Ordering
  aggregate's own state machine.
- **A `.ps1` file containing a typographic character (em dash, curly
  quote) can silently corrupt itself the moment it's saved without a
  BOM.** Windows PowerShell 5.1 reads a script file with no byte-order
  mark using the system codepage, not UTF-8 — an em dash's 3-byte UTF-8
  sequence decodes into three wrong characters under that codepage, and
  one of them can land inside a string literal and break it, spilling the
  rest of the file out as literal printed text instead of running it.
  Found live writing `infra/scripts/restore-drill.ps1` (OPS-12): a
  `Write-Host "...— ..." -ForegroundColor Green` line corrupted into
  garbage output, and everything after it in the file printed instead of
  executing. Not a one-off — `pg_dump`'s *own* binary output has the
  identical risk from a different angle (a PowerShell text redirect
  mangles it the same way `>` would mangle a UTF-8 file it decides to
  re-encode), which is why those scripts round-trip dumps through
  `docker cp` instead of a shell redirect at all. Rule going forward:
  no non-ASCII characters in a `.ps1` file, full stop — use `--` for an
  em dash, plain straight quotes always.
- **A load test against this API measures Serilog, not the application,
  unless you override the log level.** `appsettings.Development.json`
  defaults `Serilog:MinimumLevel:Default` to `Debug` — every EF Core
  command, connection open/close and pipeline event, written to both the
  console and Seq. `GET /menu` under 10 concurrent connections measured
  **p50 = 2583ms** at that level and **p50 = 67ms** (~40× faster) with
  `Serilog__MinimumLevel__Default=Information` set instead — the exact
  level `appsettings.json`'s production default already uses. A single
  sequential `curl` at the `Debug` level was fast (16–50ms), which is what
  ruled out the query/serialization itself and pointed at logging I/O
  under concurrency instead. Set that environment variable (or otherwise
  run at `Information`) before trusting any latency number measured
  against a `dotnet run`-started instance of this API — see
  [load-testing.md](../development/load-testing.md).

- **`src/web/ui` (WEB-02) needs *both* a Vite/TS path alias *and* an npm
  workspace — either alone is insufficient, for two different reasons.**
  The alias (`resolve.alias` in each app's `vite.config.ts` + a matching
  `paths` entry in its `tsconfig.app.json`) makes `ui`'s own `.ts`/`.tsx`
  files resolve as that app's own source, so Vite transforms them directly
  instead of trying to pre-bundle them as an opaque `node_modules`
  dependency (which assumes pre-compiled JS and breaks on raw TSX). But
  the alias only resolves `ui`'s *own* files — it does nothing for *that
  package's own bare imports* (`ui/src/components/LanguageToggle.tsx`
  importing `react-i18next`), which Node/TypeScript resolution still walks
  up the directory tree from `ui/src/` to find, and `ui` has no
  `node_modules` of its own. Fixed with an npm workspace root
  (`src/web/package.json`, `"workspaces": ["pos", "admin", "e2e", "ui"]`)
  that hoists `react`/`react-i18next` to a shared ancestor `node_modules`.
  If a third file type ever moves into `ui` with a new bare dependency,
  add it to `ui/package.json`'s `peerDependencies` and re-run `npm
  install` from `src/web/` — adding the import alone will resolve at the
  alias level and still fail at the dependency level.
- **TypeScript 6+ deprecates `"baseUrl"` — use `"paths"` alone.** Pairing
  `"baseUrl": "."` with `"paths"` in a `tsconfig.app.json` (the pattern
  many older examples use) now errors with TS5101. `"paths"` alone
  resolves relative to the tsconfig file's own directory in modern
  TypeScript; no `baseUrl` needed.
- **`dotnet ef migrations add` never emits the `RowLevelSecurity.EnableFor`
  calls a new schema needs — add them to the generated migration by hand,
  every time.** EF diffs the C# model to generate `CreateTable`/
  `CreateIndex` calls, but `EnableFor` (the `ALTER TABLE ... ENABLE ROW
  LEVEL SECURITY` + policy + grant, all in one) is raw SQL invoked
  explicitly in a migration's own `Up()` — nothing in the model tells EF
  it needs to be there, so a freshly generated migration compiles, runs,
  and creates a fully reachable, completely unpoliced table. Same failure
  mode as the original RLS bug (§3a's "RLS was a complete no-op"), just
  triggered by a new schema instead of a superuser bootstrap role — and
  just as silent, since nothing about a missing policy shows up in
  `dotnet build`, `dotnet ef migrations add`'s own output, or even a
  successful `dotnet ef database update`. Caught here while building
  IDN-01 (`Brasa.Modules.Identity`'s first-ever migration): add
  `migrationBuilder.EnableFor(table, schema)` for every new table at the
  end of `Up()`, and the matching `DisableFor` calls at the *start* of
  `Down()` (before the `DropTable`s) — see any existing module's
  `InitialCreate` migration for the exact shape, and
  `RowLevelSecurity.cs`'s own remarks for why it's the real isolation
  boundary, not the EF query filter.
- **EF Core cannot constructor-bind a complex type nested inside another
  complex type — flatten it into two sibling properties instead.**
  `ComplexProperty(x => x.Foo).ComplexProperty(f => f.Bar, ...)` compiles
  and looks like it should work (`ComplexPropertyBuilder<T>` genuinely has
  a nested `ComplexProperty` overload), but fails at `dotnet ef migrations
  add` time with "no suitable constructor was found... Cannot bind 'bar'
  in 'Foo(Bar bar, ...)'" — EF's complex-type materialisation resolves a
  scalar constructor parameter to a mapped column fine, but can't resolve
  one that is itself another complex type. Found building CAT-16's
  `MenuItem.ScheduledPrice` (a `ScheduledPriceChange` record whose own
  `NewPrice` is a `Money` — itself a complex type via `MapMoney`): nesting
  it directly inside `MenuItem`'s own `ComplexProperty(i => i.ScheduledPrice)`
  failed exactly this way. Fixed by not nesting at the persistence layer at
  all — `MenuItem` instead carries two flat, independently-nullable
  properties (`ScheduledNewPrice: Money?`, mapped with the same
  `MapOptionalMoney` helper `TakeawayPrice` already uses, and
  `ScheduledPriceEffectiveFromUtc: DateTimeOffset?`), always written
  together by the one domain method that touches either
  (`SetScheduledPrice`), with the rich `ScheduledPriceChange?` staying a
  computed, `.Ignore()`d property that assembles the two for the domain
  API. If a future value object needs to nest another value object that
  is itself a complex type (not a plain scalar), flatten one level the
  same way rather than nesting `ComplexProperty` calls.
- **`DevCatalogSeeder.SeedAsync` has an early return partway through — new
  seed data added after it silently never runs on any already-seeded
  database.** `if (await db.Categories.AnyAsync(...)) return;` exists to
  make menu seeding idempotent, but this session's dev database has had
  categories since I0 — every subsequent run hits that return immediately.
  Adding CAT-07/08's tax-rule seeding *after* it (with its own, separate
  `TaxRules.AnyAsync` idempotency check) meant the new code path was
  provably unreachable: `GET /tax-rules` kept returning `[]` after a clean
  rebuild and restart, no exception anywhere. Fixed by moving the new
  seeding call *above* the categories early return, keeping its own
  independent guard. Any future addition to this seeder needs the same
  check: does it sit above or below that return, and is a stale local
  dev database (not a fresh one) actually going to reach it?
- **Installing a new npm dependency from inside a workspace member
  (`pos/`, `admin/`, …) instead of the workspace root can leave a stale
  Vite dependency-scan cache that makes the whole app render nothing, no
  console error pointing at the real cause.** `npm install
  @microsoft/signalr` from inside `src/web/pos` hoisted the package (and,
  incidentally, re-hoisted `i18next`) up to `src/web/node_modules` — normal
  npm workspace behaviour — but the *already-running* Vite dev server
  (started before that install) kept `pos/node_modules/.vite`'s optimizer
  cache pointing at the old layout. Playwright's `webServer` config
  reuses an already-running dev server by default
  (`reuseExistingServer: !CI`), so the stale cache survived across
  multiple `npx playwright test` runs, each one hanging on
  `waitForSelector('.table-picker')` for the full 30s timeout with no
  error in the test output at all — the actual failure (`ENOENT:
  ...i18next/dist/esm/i18next.js`) only showed up in the dev server's own
  `[WebServer]`-prefixed console output. Fixed by killing the dev server
  processes (so Playwright starts fresh ones against the settled
  dependency tree) and deleting `pos/node_modules/.vite`. If a frontend
  change makes a Playwright test hang on a selector that should obviously
  exist, check the `[WebServer]` log output before assuming the app code
  is wrong — and prefer `npm install <pkg>` from `src/web` (the workspace
  root), not from inside whichever app happens to need it.
- **A singleton cannot consume a scoped service — ASP.NET Core's DI
  validation refuses to start the app, and the fix is an `AsyncLocal`, not
  loosening either lifetime.** Building `TestableClock` (QA-04), the first
  attempt registered it `AddScoped<IClock>` so a per-request override
  wouldn't leak between Playwright's parallel workers. The app failed to
  start: `Cannot consume scoped service 'IClock' from singleton
  'IFiscalProvider'` — `MockFiscalProvider` is deliberately singleton (its
  in-memory sequential document numbering must survive across requests,
  not reset every time a fresh scope is created), and a singleton silently
  "capturing" a scoped dependency for its own lifetime is exactly the bug
  class this validation exists to catch. Loosening `MockFiscalProvider` to
  scoped would have broken real numbering; loosening `IClock` back to
  singleton would have broken the override's per-request isolation. Fixed
  by keeping `IClock` singleton and moving the override into a `static
  AsyncLocal<DateTimeOffset?>` instead — the same trick
  `TenantContextAccessor` already uses in this codebase, for the identical
  reason (EF Core's global query filter needs tenant state without DI
  scoping). If a future singleton needs request-scoped data, this is the
  pattern: an `AsyncLocal`-backed accessor, not a scope-lifetime change on
  either side.
- **EF Core can map a genuinely `private` auto-property by name, not just
  true shadow properties — useful for a hash/secret nothing outside the
  entity should ever read back out.** `Staff.PinHash` (IDN-08/09) is
  `private string PinHash { get; set; }` — a real CLR property, just
  inaccessible outside the class, unlike a shadow property (which has no
  backing CLR member on the entity at all). `builder.Property(s =>
  s.PinHash)` can't compile from a different assembly/namespace since the
  lambda can't reference a private member it can't see, but EF's
  string-keyed overload, `builder.Property<string>("PinHash")`, resolves
  the backing property via reflection regardless of its accessibility —
  confirmed by a clean `dotnet ef migrations add` producing the expected
  `pin_hash` column. No shadow property, no public getter anywhere in the
  codebase: the only code that ever reads or writes the raw hash is
  `Staff`'s own `VerifyPin`/`SetPin` methods.
- **Windows PowerShell 5.1's `Set-Content -Encoding utf8` always writes a
  UTF-8 BOM, unlike PowerShell Core's same-named encoding.** Regenerating
  `docs/openapi/v1.json` (API-15) by hand with `Set-Content -Encoding
  utf8` produced a file starting with a BOM, unlike every other JSON file
  already in this repo. `git diff` still showed it and `openapi-typescript`
  still parsed it fine (most JSON parsers skip a leading BOM), so nothing
  *broke* — but it was a real, avoidable inconsistency, not a stylistic
  nitpick. Fixed by writing with `[System.IO.File]::WriteAllText(path,
  content, (New-Object System.Text.UTF8Encoding $false))` instead, now
  the documented regeneration command in `docs/openapi/README.md`
  itself.
- **Any Minimal API endpoint binding `IFormFile` needs `.DisableAntiforgery()`,
  even in an app with no cookies and no antiforgery services registered at
  all.** ASP.NET Core auto-attaches antiforgery request-validation metadata
  to a route the moment it sees an `IFormFile`/form-body parameter — a
  default CSRF mitigation aimed at browser form posts, applied regardless
  of whether `AddAntiforgery()`/`UseAntiforgery()` are ever called. This app
  never calls either (hard rule 7: no cookie auth, so nothing to protect
  against CSRF in the first place). The image-upload endpoint (CAT-02,
  `POST /menu/items/{id}/image`) compiled and passed every type check —
  `.Accepts<IFormFile>("multipart/form-data")` is just metadata — but threw
  `InvalidOperationException: ... contains anti-forgery metadata, but a
  middleware was not found that supports anti-forgery` on the **first real
  multipart request**, a 500 no unit test or `dotnet build` could have
  caught, only a live call. Fixed with `.DisableAntiforgery()` chained onto
  the route mapping — the correct call for an endpoint that was never
  protected by antiforgery to begin with, not a workaround.
- **A `dotnet build` that was green minutes earlier can fail solution-wide
  with zero code changes, because NuGet's vulnerability advisory feed
  updates live and is checked on every restore.** Hit while adding a new
  package to `Brasa.Api` for an unrelated task: the very next `dotnet
  build Brasa.slnx` failed with `NU1903` on `SSH.NET` 2025.1.0
  (GHSA-q939-rpr3-3284) in `Brasa.Api.IntegrationTests`, a package this
  project never references directly — it's an optional remote-Docker-
  over-SSH dependency `Testcontainers` 4.13.0 pulls in, present all
  session. Confirmed genuinely pre-existing and unrelated by reverting
  every local change and rebuilding a byte-identical tree: still failed,
  meaning the NuGet advisory database itself had just been updated
  between builds, not anything in this repo. Fixed the correct way per
  hard rule 6 (fix the warning, don't suppress it) by adding a **direct**
  `<PackageReference Include="SSH.NET" />` to
  `Brasa.Api.IntegrationTests.csproj` pinned to `2026.0.0` in
  `Directory.Packages.props` — NuGet treats a dependency's unbracketed
  version number as a floor, not an exact pin, so a direct reference to a
  higher version wins over Testcontainers' own transitive one. Also
  surfaced a latent gap in this repo's existing pin discipline: the
  `SQLitePCLRaw.*` "Pinned: transitive …" comments in
  `Directory.Packages.props` add a `<PackageVersion>` entry but no
  project anywhere holds a matching `<PackageReference>` — under Central
  Package Management, an unreferenced `PackageVersion` is inert metadata,
  it does **not** constrain a transitive dependency's resolved version.
  Whether those particular pins are still doing anything is unverified;
  not fixed here, since it wasn't what broke the build, but worth
  checking before trusting any "pinned: transitive" comment in this file
  at face value — check for a matching direct `PackageReference` before
  assuming a comment like this one is load-bearing.
- **`ExcelDataReader` throws on every `.xlsx`, not just legacy `.xls`,
  until a codepages provider is registered — the encoding gap isn't
  BIFF-specific the way it looks at first.** Added for CAT-17's Excel
  import; a real `.xlsx` built with `exceljs` failed every single time
  with `NotSupportedException: No data is available for encoding 1252.
  For information on defining a custom encoding, see the documentation
  for the Encoding.RegisterProvider method` — easy to misread as an
  `.xls`-only (legacy BIFF) problem, since that's the usual reason this
  package needs `System.Text.Encoding.CodePages`, but it also probes
  Windows-1252 somewhere in its `.xlsx`/OpenXML path, and .NET Core ships
  without that codepage by default. Fixed with
  `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` in
  `ExcelImportParser`'s static constructor — once per process, not
  per-request. Referencing the package at all then tripped a second,
  unrelated warning: NU1510 (package pruning) claims it's redundant with
  the shared framework's reference assembly, which is true for compiling
  against the *type* but not for the runtime *provider* the static
  constructor actually needs registered — suppressed narrowly via
  `NoWarn="NU1510"` on that one `PackageReference`, with the reason
  written next to it, not a blanket suppression.
- **A documented manual step you've followed correctly several times in a
  row is still not a safety net — it only takes one skipped instance to
  ship broken, and "several times in a row" is exactly what makes the
  skip easy to miss.** `docs/openapi/README.md` has said "regenerate
  `docs/openapi/v1.json` by hand after changing any endpoint, in the same
  commit" since API-13. CAT-02, ORD-07/08/09 and CAT-17 all shipped their
  own real endpoint changes without that regeneration step — three
  commits in a row, each one locally build-and-test-and-E2E verified,
  none of that verification touching the OpenAPI document at all, since
  nothing local was checking it. CI's own `openapi-drift` job (API-14)
  had existed the whole time and caught every one of them — but only
  *after* the push, the same "runbook exists, gets followed until it
  doesn't" pattern DOC-10 named for feature pages. Fixed two ways, not
  one: regenerated the doc and closed the immediate gap, and added
  `infra/scripts/verify.ps1` so the same check that catches this in CI
  runs locally before a commit exists at all — a process fix, since a
  documentation fix alone had already failed to hold three times running.
- **A `WriteAsync` call with a zero-length buffer is not a no-op against a
  `204` response — Kestrel throws on any write attempt at all, empty or
  not, and that throw crashes the whole connection, not just the one
  request.** `IdempotencyMiddleware` buffers the downstream response into
  a `MemoryStream`, then forwards it to the real response body afterward
  — on both the normal path and the cached-replay path. It used to call
  that forwarding `WriteAsync` unconditionally, including when the
  buffered body was empty because the real status was `204`. `bytes.Length
  == 0` looks harmless to write, and a plain `Stream` would treat it as
  exactly that, but Kestrel's own response stream enforces "no body at
  all" for a `204` at the write call itself, not at the byte count —
  `InvalidOperationException: Writing to the response body is invalid for
  responses with status code 204`, thrown from inside the middleware
  after the response has already started, which takes down the whole
  Kestrel connection (the API process kept running, but stopped accepting
  new connections on that socket). This was found by `floor-drag-drop.spec.ts`'s
  own cleanup calls (`DELETE /tables/{id}` then `DELETE /rooms/{id}`, both
  `204`) reproducibly killing the API mid-test, and is the most likely
  real explanation for this project's earlier run of unexplained,
  CI-only `e2e` job failures — any DELETE endpoint, hit at any point
  in a 179-test run, could trigger it. Fixed by skipping the forwarding
  `WriteAsync` entirely whenever the buffered/cached body is empty, on
  both code paths; regression-tested by `idempotency.spec.ts`'s "a
  replayed DELETE (204) does not crash the API" case, which exercises
  both paths and asserts the API answers a plain request afterward.
- **A middleware that pushes onto `Serilog.Context.LogContext` inside its
  own `using` block cannot make those properties reach a completion-log
  line logged by an *earlier* middleware in the pipeline — even though the
  earlier middleware's own log statement runs textually after the later
  middleware's code, in execution order it runs after `next()` *returns*,
  by which point the later middleware's `using` block has already disposed
  and popped its properties back off.** This is exactly `TenantLoggingMiddleware`
  (OPS-07) vs. `UseSerilogRequestLogging()`'s completion line: the ambient
  `LogContext` stack is scoped to the `using` block that pushed onto it, not
  to the request as a whole. The fix is not to reorder the pipeline (moving
  tenant resolution earlier) — it's to have the completion log's own
  `EnrichDiagnosticContext` callback (a first-class `Serilog.AspNetCore`
  extension point, `RequestLoggingOptions.EnrichDiagnosticContext`) read
  `ITenantContext` straight from `httpContext.RequestServices` at
  completion time instead. That works because `ITenantContext` is a scoped
  per-request DI service holding its resolved value for the request's whole
  lifetime — not an ambient stack tied to any one middleware's call frame —
  so it's still there long after any inner `using` block has been disposed.
  Reach for this pattern any time a later middleware's per-request state
  needs to reach an earlier middleware's own log/response line: pull the
  value from DI at the point you need it, don't rely on `LogContext` push
  timing to carry it there for you.
- **`$PSScriptRoot` referenced inside a `.ps1` script's own `param()` block
  can be empty specifically when that script is the direct `-File` target
  of a redirected, non-interactive process launch — even though it
  resolves correctly everywhere else: in the script's own body, and in any
  script it goes on to invoke via `&`.** Confirmed live with zero .NET or
  Hangfire involved: `powershell.exe -NoProfile -ExecutionPolicy Bypass
  -File backup-database.ps1` run from a plain non-interactive shell throws
  `Join-Path : Cannot bind argument to parameter 'Path' because it is an
  empty string` at its own `param()` line, every time, reproducibly — the
  exact same script run via `& './backup-database.ps1'` from an
  interactive prompt, or invoked as a nested call from another script
  (`restore-drill.ps1`'s own `& (Join-Path $PSScriptRoot
  'backup-database.ps1') ...`), works fine. This is exactly the shape
  `Process.Start` with `RedirectStandardOutput`/`RedirectStandardError`
  produces (`DatabaseBackupJob`, OPS-10/12's own `RunScriptAsync`) — so any
  C# code that shells out to a `.ps1` file via `-File` hits this the moment
  that script's own defaults lean on `$PSScriptRoot`. Don't fix the
  symptom by touching the script's default (a temptation, since the
  scripts here are otherwise treated as "already verified, don't
  re-litigate" — see the OPS-12 entry above); pass the value the default
  would have computed as an explicit argument from the caller instead, the
  same way `DatabaseBackupJob.RunBackupAsync` passes `-OutputDir` now.
- **A backup/restore drill's row-count comparison assumes the source
  database is quiescent between "back it up" and "count it again for
  comparison" — true for tenant data at 3am with no traffic, never true
  for a background job runner's own operational tables, which mutate
  continuously regardless of tenant activity.** Once Hangfire (OPS-10)
  existed, `restore-drill.ps1`'s own row-count check started racing
  against `hangfire.lock`/`hangfire.server`'s constant heartbeat/lock
  churn — confirmed live, a real drill run genuinely caught `hangfire.lock`
  at 0 vs. 1 row while all 34 other tables matched exactly, proving the
  backup/restore mechanism itself was never broken, only the comparison's
  unstated assumption. Fixed by excluding `hangfire.*` from the table list
  the drill compares, with the reasoning (transient, self-healing
  operational state, not business data needing a disaster-recovery
  guarantee) written into `backup-and-restore.md` rather than left as a
  silent `WHERE` clause. Any future background-job-runner-style dependency
  added to this database should get the same treatment by default, not
  rediscovered as a flake.
- **Windows PowerShell 5.1's `ConvertTo-Json` has no standard-indent
  option — for a deeply nested document it silently produces something
  that is valid JSON but nothing else agrees is "the same file," even
  when the underlying data hasn't changed at all.** Its indentation
  compounds with the accumulated length of every enclosing key rather
  than using a fixed number of spaces per nesting level, so a
  semantically unchanged ~120KB `docs/openapi/v1.json` came out ~540KB
  every time this project's own documented regeneration command
  (`$doc | ConvertTo-Json -Depth 100`) ran. CI's own `openapi-drift` job
  regenerates the same document with `jq 'del(.servers)'`, which uses
  jq's ordinary 2-space pretty-print — nothing close to PowerShell's
  output, byte-for-byte, regardless of content. The practical effect: the
  `openapi-drift` CI job had been failing on **every single push**,
  including a docs-only commit that could not possibly have touched the
  API's actual contract — proven by deep-sorting both documents' keys
  down to primitives and diffing that instead of the raw text, which
  showed zero semantic difference on a run CI still flagged red. Worse,
  `infra/scripts/verify.ps1`'s own local drift check used the identical
  `ConvertTo-Json` call to build its comparison copy, so it always agreed
  with whatever was already committed (both sides using the same broken
  formatter) and could never have caught this — a local "OK" was not
  evidence the CI job would also pass, for this one specific check, this
  whole time. Fixed by moving the regeneration itself to Node
  (`infra/scripts/regenerate-openapi.mjs`, `JSON.stringify(doc, null, 2)`,
  which matches jq's default formatting because both converge on the same
  ordinary convention .NET's own OpenApi middleware already emits) and
  pointing both `docs/openapi/README.md` and `verify.ps1` at that one
  script instead of two independent hand-rolled PowerShell snippets. The
  lesson generalizes: **a local verification script that regenerates a
  file the same (buggy) way the file was originally produced can never
  catch a bug in that generation method — it can only confirm the file
  agrees with itself.** Any future "does this match CI" check needs to
  either literally shell out to the same tool CI uses, or be proven
  against CI's real output at least once, not just assumed equivalent
  because the code looks like it does the same thing.
- **`vite@8` made `rolldown` (its Rust-based bundler) a hard dependency,
  not an opt-in variant — every `npm install` of `pos`/`admin` now needs
  one of fourteen platform-specific native binary packages
  (`@rolldown/binding-<platform>`), and the npm version GitHub's
  `actions/setup-node@v5` bundled for `node-version: '24.x'` has
  repeatedly failed to install the right one for the Linux runner, even
  though `package-lock.json` correctly lists it under
  `optionalDependencies`.** This is a real, documented, still-recurring
  npm bug (`npm/cli#4828`, still-open sibling `npm/cli#8320`), not a
  mistake in this repo's own lockfiles —
  confirmed by inspecting `pos/package-lock.json` directly: every
  platform's binding, including `@rolldown/binding-linux-x64-gnu`, is
  correctly declared. The failure mode is blunt: `pos`'s (and `admin`'s)
  own Playwright `webServer` never starts (`Cannot find native binding`),
  which aborts the entire `e2e` CI job before a single test runs — not a
  flaky test, a dead job. An explicit `npm install -g npm@latest` step
  alone did **not** hold up on a real run — this class of npm bug is
  reported as recurring even on current npm, root-caused to
  `package-lock.json` having been generated on a different OS (every
  lockfile in this repo is generated on the maintainer's Windows machine)
  confusing npm's optional-dependency install step for Linux, *silently*
  rather than with a failed exit code — which is why `Install pos/admin
  dependencies` reports success right before the dev server it just
  "installed" crashes. Fixed by no longer trusting npm's
  optional-dependency resolution for the platform binary at all: an
  explicit CI-only step installs the exact `@rolldown/binding-linux-x64-gnu`
  version each project's own lockfile already pins rolldown to (`npm
  install --no-save --prefix src/web/pos
  @rolldown/binding-linux-x64-gnu@<version>`, version read from
  `node_modules/rolldown/package.json` so it can't drift out of sync with
  the lockfile), right after that project's normal `npm install`. Watch
  for this again if `admin`'s or a future client's own `package.json`
  starts depending on another bundler that ships per-platform native
  binaries the same way (esbuild, swc, lightningcss, sharp all use the
  identical `optionalDependencies`-per-platform pattern and are all
  capable of hitting the same npm bug) — the fix is the same regardless
  of which package triggers it: stop trusting npm's optional-dependency
  resolution in CI and install the platform binary explicitly.
- **`RowLevelSecurity`'s `EnableFor`/`EnableSystemReadFor`/`DisableFor`/
  `DisableSystemReadFor` take `(table, schema)`, not `(schema, table)`.**
  Easy to get backwards — the existing `payments.payments` table's own
  `InitialCreate` migration calls `EnableFor("payments", "payments")`,
  which reads identically either way since table and schema happen to
  share a name there. Writing PAY-08's `AddCashSession` migration by
  analogy produced `EnableFor("payments", "cash_sessions")` — backwards —
  and it built clean, since nothing about the call is type-checked beyond
  "two strings." Only surfaced at runtime, immediately, the first time
  `dotnet run` actually tried to apply the migration against real
  Postgres: `Npgsql.PostgresException: 3F000: schema "cash_sessions" does
  not exist` — Postgres was asked to look up a schema literally named
  after the table. A real `dotnet ef migrations add` (not a hand-written
  migration) still needs this checked by eye every time, since the
  scaffolder never emits either RLS call at all (ADR 0010's own
  already-documented gap) — it can't catch an argument-order mistake in a
  call it doesn't generate.
- **`Results.Ok<T>(null)` writes a zero-byte body for a `200`, not the
  JSON literal `"null"`.** Tempting for an endpoint like PAY-08's
  `GET /terminals/{id}/cash-sessions/current`, whose honest answer is
  sometimes "nothing" — a terminal between shifts has no open session,
  and that is a normal state, not a `404`. `Results.Ok<CashSessionDto?>(null)`
  compiles and looks reasonable, but ASP.NET Core's Minimal API JSON
  writer has a fast path that skips serialization entirely when the value
  is exactly `null`, producing an empty body rather than the 4 bytes
  `null` a client's `response.json()` could actually parse — caught live
  by the E2E suite's own first real call to the endpoint, throwing
  `SyntaxError: Unexpected end of JSON input`, not assumed from reading
  ASP.NET Core's source. Fixed by returning `204 No Content` instead — the
  correct HTTP-level way to say "no body" — with every caller checking
  `response.status === 204` before ever calling `.json()`, the same check
  already needed for API-05's idempotency-replay path and any other
  204-capable endpoint in this codebase.

## 8. Environment

- Windows 10 Home. Shell is **PowerShell 5.1** — no `&&`, no ternary, no
  null-coalescing. Chain with `;` and `if ($?) { }`.
- .NET SDK 10.0.302 · Node 24.18.1 · Docker 29.6.2 · PostgreSQL 18.4 (container)
- The user commits locally and pushes to the remote themselves. **Make small,
  focused local commits as you work.**

```powershell
dotnet build Brasa.slnx                 # must be zero-warning
dotnet test  Brasa.slnx
dotnet test  tests/Brasa.Shared.Tests   # fast path
dotnet run   --project src/backend/Brasa.Api
docker compose -f infra/docker-compose.yml up -d
cd src/web ; npm install                     # workspace root (WEB-02) -- hoists shared deps once for pos/admin/e2e/ui
cd src/web/pos ; npm run dev                 # http://localhost:5173
cd src/web/admin ; npm run dev -- --port 5174   # http://localhost:5174
cd src/web/e2e ; npx playwright test         # starts API + pos + admin itself
```

## 9. Open blockers

| # | Blocker | Owner |
|---|---|---|
| 1 | **Portuguese legal entity not formed.** Cannot submit Modelo 24 without it. On the critical path to revenue, not to code — must start now | Founder |
| 2 | **VAT rules unconfirmed by an accountant.** The `TaxRule` model absorbs any answer, but rates must be verified before launch | Founder |
| 3 | **`Brasa` trademark and domains not cleared.** INPI (PT), EUIPO (classes 9/42), `.pt`/`.com` | Founder |

## 10. Keeping this file true

This brief is only worth reading if it is accurate. Update it in the **same
commit** as the change that dates it, specifically when:

- the current phase or next task changes (§3)
- a new invariant or trap is discovered (§2, §7)
- a new ADR lands (§6)
- a blocker opens or closes (§9)
- files or directories move (§4, and [repo-map.md](repo-map.md))

Bump **Last verified** at the top when you do.
