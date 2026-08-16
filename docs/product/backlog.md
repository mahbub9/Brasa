# Backlog

The plan of record. Every feature and task, with a stable ID and a status.

**How this relates to the other product docs**

| Doc | Answers |
|---|---|
| **This page** | What are we building, and how far along is each task? |
| [roadmap.md](roadmap.md) | **When** — which increment each task belongs to, and its demo |
| [status.md](status.md) | Which code actually exists and works today? |
| [../features/](../features/) | How does a built feature behave — offline, on failure? |
| [plan.md](plan.md) | Why is the roadmap shaped this way? (historical record) |

> **Delivery is incremental** — vertical slices, each ending in a runnable demo,
> not layer-by-layer. The epic tables below are grouped by area for reference;
> **[roadmap.md](roadmap.md) is what says what to build next.** Tasks from many
> epics land in the same week.

**Rules**

- IDs are **stable and never reused**. Reference them in commits and issues:
  `feat(identity): terminal pairing (IDN-06)`.
- Update the status in the **same commit** as the work. A backlog that lags is
  worse than none, because it is trusted.
- New work gets the next free ID in its epic. Never renumber.

**Legend** ✅ done · 🚧 in progress · ⬜ todo · 🔒 blocked · ⏭ deferred past MVP

**Last updated:** 2026-08-13

---

## Progress

| Epic | Area | Done | Total | Phase |
|---|---|---:|---:|---|
| **FND** | Foundation & shared kernel | 10 | 12 | I0 |
| **OPS** | Infrastructure, CI, observability | 9 | 16 | I0 → ongoing |
| **DOC** | Documentation system | 9 | 10 | I0 → ongoing |
| **API** | API platform & mobile readiness | 15 | 18 | I0 (rest: I3) |
| **DAT** | Persistence, tenancy, RLS | 10 | 11 | I0 |
| **IDN** | Identity & access | 3 | 16 | I3 |
| **CAT** | Catalog & menu | 18 | 19 | I0 (rest: I1) |
| **FLR** | Floor plan & tables | 6 | 7 | I1 |
| **ORD** | Ordering | 20 | 22 | I0 (rest: I2) |
| **SYN** | Offline sync engine | 0 | 13 | I5 |
| **AGT** | Site Agent | 0 | 15 | I4–I5 |
| **KIT** | Kitchen printing & KDS | 0 | 14 | I4 |
| **FIS** | Fiscal engine | 3 | 24 | I0 (rest: I7) |
| **WEB** | Web clients | 7 | 13 | I0 (rest: I1–I8) |
| **PAY** | Payments & cash sessions | 0 | 14 | I6 |
| **RPT** | Reporting | 0 | 12 | I8 |
| **QR** | QR self-ordering | 0 | 9 | Post-I8 |
| **QA** | Automated testing | 9 | 14 | I0–I1 → ongoing |
| **MOB** | Mobile apps | 0 | 12 | Post-launch |
| **DIF** | Differentiators | 0 | 21 | Post-MVP — see [differentiation.md](differentiation.md) |
| | **Total** | **112** | **292** | |

> Phase labels now follow the increments in [roadmap.md](roadmap.md) (I0…I8),
> not the original Month-based sequencing — see
> [ADR 0009](../architecture/decisions/0009-incremental-delivery.md).
>
> 95 of 292 — I0 (backend, `pos` shell with pt/en i18n, a first Playwright
> harness) is done except deployment, I1's opening slice — real rooms and
> tables (FLR) and menu modifiers (CAT-03/04, which turned out to already
> cover ORD-05 too) — is done and proven against a live API, there is now a
> real automated regression test for tenant isolation (QA-09/10, DAT-11)
> instead of only the manual verification that first caught ADR 0010, an
> accessibility scan (QA-14) that found and fixed 5 real contrast failures
> on its first run, the error-code contract (hard rule 11) is now
> mechanically enforced (API-04) rather than just stated, `/health/ready`
> (OPS-09) actually checks PostgreSQL instead of only proving the process
> itself is alive, the pre-bill a table sees before paying (ORD-18/19)
> is provably a *documento não fiscal* — no document number, ATCUD or QR
> anywhere on the wire, and never issued through `IFiscalProvider` —
> `GET /orders` (ORD-22) gives history/search by status, table and
> opened-date range, a line can now carry a free-text kitchen note
> (ORD-06) added after it's rung up, a party can transfer to a
> different table mid-service (ORD-12) with both the old and new table's
> state committing atomically, a single line can move onto a different
> open order instead (ORD-13), two orders can combine onto one table
> (ORD-14, the secondary ending up `Merged` — never `Closed`, since no
> fiscal document was issued for it), and a bill can be split by item
> instead of evenly (ORD-16, exact per-allocation portions, no `Allocate`
> remainder needed) or by cover (ORD-17, reusing `Money.Allocate`'s own
> weighted overload directly), a counter sale can be rung up with no
> table at all (ORD-20, `Order.IsTakeaway`), and a menu item can now declare
> a description and its allergens (CAT-02, still 🚧 — image upload needs
> file storage infra not built yet), and `GET /menu` now answers a repeat
> pull with a bodyless `304` when the client's `If-None-Match` shows it
> already has the current menu (API-10 — deliberately not extended to
> `GET /floor`, whose state changes too often for caching to pay off), and
> the idempotency guarantee every mutation relies on (hard rule 7) now has
> automated proof rather than just a doc comment: replaying the same
> `Idempotency-Key` 3× never creates a second order or issues a second
> fiscal document on a retried close (QA-11), and the API can now tell
> which client is calling and answer "what version do you need to be"
> (API-06/07, `X-Brasa-Client` parsing + `GET /client-requirements`) —
> ahead of any client that sends the header yet, same as CAT-02/CAT-18
> shipped ahead of their UI, and `GET /orders` — the one collection here
> that's genuinely unbounded over a restaurant's lifetime — now paginates
> via an opaque `X-Next-Cursor` header rather than the flat capped `take`
> it shipped with (API-09, additive: the response body shape didn't change),
> and every response is now Brotli/gzip-compressed, including error bodies
> (API-11), and the API's shape is now reviewable in a diff instead of only
> inspectable by running the app ([docs/openapi/v1.json](../openapi/v1.json),
> API-13 — regenerated by hand for now, CI drift-checking is API-14, since
> pulled forward too, not yet exercised by an actual CI run), and a menu
> can now be bulk-loaded from a CSV file
> instead of one item at a time (CAT-17, still 🚧 — Excel not built; rows
> import independently, so one bad row is reported, not fatal to the file).
> Staff can now flag a table as having asked for the bill, too — `BillRequested`
> (FLR-04) already had its domain transition, CSS and i18n strings sitting
> unused; only the endpoint connecting a "Pedir conta" button to them was
> missing. Same story for 86-ing (CAT-13): `MarkAvailable`/`MarkUnavailable`
> and the `AddLine` guard that respects them both existed since I0, but
> nothing could ever actually set `IsAvailable` to `false` until now — and
> a third instance of the identical shape, `MenuItem.Reprice` (CAT-19,
> newly minted): the domain method and its negative-price guard existed
> since I0 with no caller either, and past order lines were already
> immune to a future reprice by construction (they snapshot the price at
> add-time), so the only missing piece really was the endpoint — and a
> fourth, one level up: `MenuCategory.IsVisible` had no setter *at all*,
> even though CAT-01's own title names "visibility" as in scope and the
> row was already marked done. Hiding a category now removes it and every
> item under it from the menu in one call, and the OpenAPI document was
> regenerated to catch up with all five of those endpoints, which had each
> skipped the documented hand-regeneration step. I1's back-office shell
> (WEB-09) now exists too — `src/web/admin`, a second web client — with a
> pt/en toggle of its own from day one (real staff here aren't all
> Portuguese speakers), and its first real editor (WEB-10's menu slice):
> a new `GET /menu/all` — `GET /menu` is guest-facing and filters to what a
> guest may actually order, so it can never show a hidden category or an
> 86'd item for staff to turn back on — backs a screen that toggles
> category visibility, 86's/reprices/deletes an item, and bulk-imports more
> via CAT-17's existing CSV pipeline. Floor plan/Staff stay labelled
> "Brevemente" rather than silently absent, since FLR-03 and WEB-11 aren't
> built yet. The API can now also announce its own eventual retirement
> before it happens (API-08) — `Deprecation`/`Sunset` response headers,
> RFC 8594, config-driven and a no-op until a real `/api/v2` gives them
> something to say — and can now protect itself from a runaway client
> (API-12): a fixed-window limit per `(tenant, X-Brasa-Client client id)`,
> `429`s shaped like every other error, a new sixth `ErrorType.RateLimited`
> alongside the five the registry already pinned. Building it caught its
> own bug immediately — the first production-shaped default throttled the
> E2E suite itself, since every client today shares one bucket with none
> sending `X-Brasa-Client` yet — fixed with a generous dev-only override
> rather than a production default weakened to match. Every log line during
> a request now carries `TenantId` too (OPS-07, `TenantLoggingMiddleware`) —
> verified live against a real request's EF Core command log. The one-line
> HTTP completion summary now carries it too (closed in a later session) —
> not via reordering the pipeline as first assumed necessary, but an
> `EnrichDiagnosticContext` callback reading `ITenantContext` straight from
> DI at completion time, sidestepping `LogContext`'s own push/pop timing
> entirely. The two deep-link
> verification documents (API-18) now exist too — honestly empty rather
> than fabricated, since no bundle id or package name exists to put in
> either until a native app does — and the `/ping` endpoint's own
> `DateTimeOffset.UtcNow` call (a hard-rule-2 violation found while adding
> them) now goes through `IClock` like everything else
> (details:
> [status.md](status.md#i0-demo-verified-live-not-just-unit-tested)). Every
> epic marked "I0 (rest: …)" is intentionally partial: I0 builds only the
> single vertical slice the walking-skeleton demo needs, not a whole epic.
> Discounts (ORD-11) now exist too — a manager comping half off a late dish,
> or a regular's 10% off the whole table — both line- and order-level,
> percentage or fixed, composing (line discounts apply first, then the
> order-level one on top of the already-discounted subtotal). No
> manager-authorisation gate yet, the same "ship the seam ahead of the
> trigger" shape as CAT-13/19: IDN-11 is the real gate once staff accounts
> and roles exist. The two most fiscally sensitive edges got real design
> attention rather than an approximation: a fixed discount that would exceed
> the total it's applied to is rejected outright, not silently clamped, so a
> mistyped value surfaces immediately instead of quietly comping an item;
> and `CloseOrderAsync`'s own invariant (`order.Total` must equal
> `document.GrossTotal` to the cent) still holds with a discount applied,
> because the same discount amount that reduces `OrderLine.LineTotal`/
> `Order.Total` is rendered as its own negative `FiscalDocumentLine` on the
> issued document — an order-level discount prorated across lines by
> `Money.Allocate`, the same proportional-distribution tool `SplitByCover`
> already uses, so it reconciles by construction rather than by convention.
> Pre-bill and close now share one `BuildFiscalLines` helper for this, which
> also tightens ORD-19's own guarantee (a pre-bill must match the eventual
> invoice) to cover the discounted case, not just the undiscounted one it
> already covered. Known, documented gap: `SplitByItem`'s by-item preview
> doesn't yet fold in a discount, unlike `SplitEvenly`/`SplitByCover`, which
> inherit it automatically through `Total`. A menu item can now also declare
> which course it's served at (CAT-14) — `PUT /menu/items/{id}/course`,
> `Starter`/`Main`/`Dessert`/`Drink`, independent of `MenuCategory` (how the
> menu is organised for browsing, not when a dish is fired to the kitchen).
> A pure greenfield addition, unlike most of this session's Catalog work —
> there was no dormant domain method waiting for a caller this time — and it
> ships the same way CAT-02's allergen set did: ahead of both its consumers,
> since neither an admin editor nor course *firing* (ORD-07) exists yet.
> Finding it also surfaced a real, if narrow, test-infrastructure bug:
> `TenantIsolationIntegrationTests` constructed `CatalogDbContext` without
> the `MigrationsHistoryTable` override `Program.cs` and the design-time
> factory both use, so its model silently disagreed with every migration's
> snapshot — invisible until a new migration actually needed the pending-
> changes check EF Core 8+ runs before applying one. Fixed by matching the
> test's setup to how the app is actually configured, not by suppressing
> the check. A menu item can now also declare which kitchen station
> prepares it (CAT-15) — `PUT /menu/items/{id}/station`,
> `Grill`/`Bar`/`ColdKitchen`/`Fryer`/`Pastry`, independent of both
> `MenuCategory` and `Course`: a starter and a main can both come off the
> grill. Same greenfield, ships-ahead-of-its-consumer shape as CAT-14 —
> station *routing* (KIT-06) needs printers and a KDS that don't exist yet.
> The `TenantIsolationIntegrationTests` fix above turned out to be
> necessary but not sufficient: adding *this* migration hit the identical
> `PendingModelChangesWarning` again despite the matching
> `MigrationsHistoryTable` override, meaning the test's hand-rolled
> `DbContextOptionsBuilder` still disagreed with the design-time factory in
> some way neither a source diff nor `dotnet ef migrations
> has-pending-model-changes` (clean) surfaced. Fixed properly this time by
> having the test call `CatalogDbContextFactory` directly instead of
> duplicating its configuration by hand — the same code path `dotnet ef`
> itself uses, so there is no second configuration left to drift out of
> sync, whatever the exact trigger was. Seq itself turned out to be
> silently broken along the way — a newer `datalust/seq:latest` requires
> an explicit first-run admin password or an opt-out, and
> `infra/docker-compose.yml` set neither, so the container crash-looped on
> every restart (fixed with `SEQ_FIRSTRUN_NOAUTHENTICATION`, fine for a
> localhost-only dev instance). With Seq actually healthy, real
> distributed tracing and metrics now exist too (OPS-08): ASP.NET Core,
> outbound `HttpClient` and Npgsql spans, plus ASP.NET Core/HTTP/.NET
> runtime metrics, OTLP-exported to Seq (which ingests OTLP natively — no
> separate collector needed). Config-bound and empty by default, the same
> seam-ahead-of-the-trigger shape as `ApiDeprecationOptions`: no endpoint
> configured means the instrumentation still runs but nothing is
> exported, since there's no real OTLP collector for a production
> deployment yet (OPS-11), only the local dev Seq instance. Building this
> caught a real, non-obvious gotcha: `AddOtlpExporter` posts to the
> configured `Endpoint` exactly as given — it does **not** append a
> per-signal path itself (that auto-append only happens for the separate,
> unified `UseOtlpExporter()` helper reading `OTEL_EXPORTER_OTLP_ENDPOINT`,
> not the per-signal `TracerProviderBuilder`/`MeterProviderBuilder`
> extension used here) — so the first attempt 404'd against Seq silently:
> no exception anywhere in the app, since export failures are an
> internal SDK concern by design, not a request-path error. Found by
> temporarily subscribing a raw `ActivityListener` to inspect the
> exporter's own outbound HTTP spans directly, confirming a 404 no
> ordinary log line would have surfaced. Fixed by appending `/v1/traces`
> and `/v1/metrics` explicitly. **Verified live**, not just "no exceptions
> thrown": a real request's HTTP span and its child Npgsql query span both
> land in Seq with correct parent/child linkage (`ParentId`) and
> `service.name=brasa-api`, and a periodic metrics export lands too.
> Documentation debt got some real attention too (DOC-10): the per-feature
> page index had said "no feature pages yet" since before this session
> started, despite dozens of backlog items having shipped — the "written
> as the feature is built" policy was never actually being followed.
> Two pages now exist (discounts, menu item course/station), covering
> what was just built with full context rather than attempting to
> backfill everything at once; both indexed in the sidebar, docs site
> build verified clean. A line can now be voided after it's already been
> rung up too (ORD-10) — `POST /orders/{id}/lines/{lineId}/void`, a
> required reason, no manager-authorisation gate yet, the exact same
> "ships ahead of the trigger" shape as ORD-11's discounts, and it reuses
> `BuildFiscalLines` from that same feature almost unchanged: a voided
> line is simply omitted from the issued document rather than rendered as
> a 100%-discount line, since it was never actually delivered. Building
> it surfaced a genuine edge case rather than one invented for symmetry:
> voiding every line on an order still passes `Order.Close()`'s own
> "at least one line" guard (voided or not), but then correctly fails at
> the fiscal layer instead — `BuildFiscalLines` omits every voided line,
> so `IFiscalProvider.IssueSimplifiedInvoiceAsync`'s own pre-existing
> `fiscal.no_lines` guard rejects the empty result, and because
> `CloseOrderAsync` never persists `Close()`'s in-memory transition until
> the fiscal document actually issues, the order is left genuinely
> `Open` in the database, not silently closed-with-nothing-to-show-for-it.
> An early draft of this doc comment guessed at different, wrong
> behaviour (a zero-value document being issued) before the E2E suite
> caught the real outcome — fixed to describe what the code actually
> does, not what seemed plausible. The `SplitByItem` (ORD-16) gap both
> ORD-11 and ORD-10 had explicitly documented and left open — portions
> computed from a line's raw unit price rather than `LineTotal`, so a
> line-level discount or a voided line wasn't reflected in a by-item
> split preview — is now fixed, not just re-documented: each line's own
> `LineTotal` (already net of any line discount, already zero if voided)
> is allocated across its quantity via `Money.Allocate` before being
> handed to whichever groups claim those units, so a discounted or
> voided line now splits correctly regardless of how its quantity is
> divided between guests. The fix also removed a small duplication that
> predated it: `Order.SplitByItem` now returns one portion per line
> allocation (grouped the same shape as the request) instead of only
> group totals, so `OrderEndpoints`'s per-line breakdown and per-group
> total are computed from the exact same numbers rather than two
> independent formulas that had to be kept in sync by hand — they simply
> can't disagree now. What's still deliberately open: an *order-level*
> discount still isn't prorated into a by-item split, because doing so
> needs a real answer to a genuine product question ("how do you fairly
> divide 10% off the table between two guests who ordered different
> things?") that guessing at would be worse than leaving unaddressed.
> Staff can now sign in with a PIN too (IDN-08/09) — PBKDF2-hashed
> (`Rfc2898DeriveBytes`, 210k iterations, no new dependency), locking out
> after 5 consecutive wrong guesses for 15 minutes, rotated by an admin
> reset. Verifies a *known* staff id's PIN rather than a blind PIN pad
> searching everyone at a site — the latter would incorrectly penalise
> every non-matching staff member's own lockout counter on someone else's
> wrong guess, so it was deliberately not built that way. `admin` gets a
> real "Equipa" screen (add staff, see who's locked, reset a PIN); no
> `pos`/terminal sign-in flow yet, and nothing gated on it — until now.
> Manager authorisation (IDN-11) is that real gate, wired into both void
> (ORD-10) and discount (ORD-11) the same session it became possible:
> every void/discount request now carries a manager's own staff id and
> PIN, checked against a real `StaffRole.Manager` *before* the PIN is even
> verified (so a Staff-role credential is rejected without spending any of
> that person's own lockout budget), then verified through the exact same
> `Staff.VerifyPin` mechanism the sign-in endpoint itself uses — same
> lockout rules, no session, a fresh proof on every privileged call. The
> registry's `ErrorType.Forbidden` (403) — reserved since API-03, never
> constructed until now — finally gets a real call site:
> `identity.staff_not_manager`. Still no pos/admin UI prompting for the
> credential (no staff-picker exists in either client yet), so this ships
> the gate itself, proven at the API level exactly the way ORD-10/11
> themselves shipped ahead of *their* triggers.
> Section assignment to waiters exists too now (FLR-06), unblocked the same
> session by IDN-08/09's `Staff` — `PUT /rooms/{id}/section` assigns or
> clears which waiter is working a room. A room was already the "which
> area" granularity a real *secção* means, so this is one new nullable
> field (`Room.AssignedStaffId`) rather than a new entity — the same "no
> entity where a plain field says the same thing" call FLR-05's
> `Table.GroupId` and FLR-07's `Room.FloorLevel` already made. Turned out
> to key off `Staff` directly rather than `Site` as IDN-01's own row once
> expected — a room has no site relationship of its own to match against,
> so confirming the id names a real `Staff` row is the only check there is
> to make. `RoomDto.AssignedStaffName` resolves fresh from Identity on
> every `GET /floor`, one batched query across every room rather than
> N+1, so a later-renamed staff member never shows stale here. Any staff
> role works — unlike IDN-11's manager-only gate shipped the same session,
> this isn't a privileged action. `admin`'s room editor gets a section
> dropdown; `pos` shows nothing yet. **Verified live**:
> `floor-section-assignment.spec.ts` — a plain Staff-role member (not a
> manager) is assigned and resolves correctly both on the assignment
> response and a fresh `GET /floor`, clearing removes both fields
> together; an unknown staff id and an unknown room both 404 with their
> own codes; the admin UI assigns and clears a section, both confirmed to
> actually take effect via a follow-up API call.
> A first `web/sdk` slice landed too (API-15/WEB-03) — `openapi-typescript`
> generating request/path/query types from the committed
> `docs/openapi/v1.json`, which surfaced a real gap in API-13 itself:
> every response body had been undescribed since that row first shipped,
> because `Brasa.Api`'s endpoints all return a bare `Results.Ok(...)`/
> `IResult`, and OpenAPI reflection can only infer a *request* body's
> shape that way on its own. Closed the same session: all 68 route
> mappings across eight endpoint files now carry an explicit
> `.Produces<T>(statusCode)` call, so `GET /floor`'s `200` describes
> `RoomDto[]` and every other success response its own real shape, not
> just `{"description": "OK"}` — verified by regenerating both the
> OpenAPI doc and the SDK schema and confirming `content?: never` only
> remains on genuinely bodyless statuses (`204`/`304`) plus the
> out-of-scope `/ping`. Error responses stay undescribed — a separate,
> materially larger task, not attempted here. `schema.guard.ts` now
> checks a response shape the same way it already checked request
> shapes. Along the way: found and fixed a real Windows PowerShell 5.1
> trap (`Set-Content -Encoding utf8` silently writes a UTF-8 BOM, unlike
> PowerShell Core) and a stale `CLAUDE.md` claim that Docker isn't
> installed — it is, and `Brasa.Api.IntegrationTests` has been passing
> against it all session. Full suite green (64 backend, 160 E2E) — no
> new runtime behaviour, so no new E2E coverage; verification was
> regenerate-and-typecheck, the same shape API-13 itself was verified by.
> `pos` can sign a staff member in now too (WEB-07), unblocked the same
> session by IDN-08/09's `Staff` — a "Sign in" button in the header opens
> a staff-picker-then-PIN modal, the same `POST /staff/{id}/verify-pin`
> mechanism IDN-11's own gate reuses, so this is that mechanism's second
> real caller. Deliberately non-blocking: signing in gates nothing else
> in `pos` — building a real login gate would touch every existing E2E
> spec that drives `pos` directly from a fresh `page.goto('/')`, a
> materially larger and riskier change than "a real, working sign-in
> exists," the same "mechanism before the trigger" shape IDN-08/09
> itself shipped in. A locked-out staff member shows in the picker but
> can't even be tapped into a PIN attempt; a wrong PIN shows an inline
> error and leaves the picker on the same screen to retry; success shows
> "Hi, {name}" and a sign-out control. No persistence across a reload —
> plain React state, since no session/token concept exists yet
> (IDN-03…05) to persist into more durably. **Verified live**:
> `staff-login.spec.ts` — wrong-then-correct PIN, sign-out, cancelling
> the modal, and a freshly-locked staff member (never the shared seeded
> "Ana Ferreira"/"Tiago Costa," so other specs relying on them stay
> unaffected) showing disabled. Full suite green (64 backend, 163 E2E,
> two confirmed pre-existing QA-02 table-pool flakes — a different
> unrelated spec each run, clean in isolation both times) — the existing
> 160 all still pass unchanged, confirming the non-blocking design
> really doesn't disturb anything that never signs in.
> CAT-02's own image-upload gap ("needs file storage infra") is closed
> now too — `POST`/`DELETE /menu/items/{id}/image`, local disk under a
> new `MenuItemImageStorage`, the same honestly-scoped "no real infra
> credentials, skip it like OPS-11" reasoning applied to storage instead
> of deployment. GUID-named files (never the caller's own filename),
> served back through `UseStaticFiles` at `/uploads/menu-items/…` —
> outside `/api/v1` entirely, so both `pos` and `admin` gained a small
> `apiOrigin` export (the API base URL's own origin) to build a fetchable
> `<img src>` from a path that isn't API data. Saves the new file and
> persists the new `ImageUrl` before ever deleting whichever file it
> replaces, so a failed upload degrades to "still has the old photo,"
> never "has no photo." Building it surfaced a real gotcha, caught live
> by the E2E suite rather than at compile time: ASP.NET Core silently
> attaches antiforgery request-validation metadata to any Minimal API
> endpoint that binds an `IFormFile`, even in an app that registers no
> antiforgery services at all (hard rule 7 — no cookies here) — every
> upload 500'd with `ThrowMissingAntiforgeryMiddlewareException` until
> `.DisableAntiforgery()` was added to the route mapping. `admin`'s menu
> editor gets a thumbnail + remove button when a photo is set, or a
> styled upload control (a hidden native `<input type="file">` behind a
> clickable label) when none is; `pos`'s menu grid renders the same
> thumbnail read-only. **Verified live**: `menu-item-image.spec.ts` — a
> real uploaded PNG's returned URL is genuinely fetchable through
> `UseStaticFiles`, not just present in the DTO; replacing a photo
> deletes the old file from disk (confirmed 404 on its old URL
> afterward); an empty file, an oversized (>5MB) file, a disallowed
> content type and an unknown item are each rejected with their own code
> (`catalog.image_required`/`catalog.image_too_large`/`catalog.invalid_image_type`/`catalog.item_not_found`);
> removing a never-set image is a no-op; the `admin` UI uploads and
> removes a real file through the real browser, confirmed via a
> follow-up API call. Full suite green (64 backend, 168 E2E, one
> confirmed pre-existing QA-02 table-pool flake, clean in isolation) —
> the pre-existing 163 all still pass unchanged. Course firing
> (ORD-07/08/09) shipped the same session — see those rows' own detail
> above; full suite green (64 backend, 173 E2E) once it landed.
> CAT-17's own remaining gap ("Excel not built") is closed too —
> `POST /menu/items/import/excel` accepts a real `.xlsx` file
> (`ExcelDataReader`, chosen over ClosedXML/NPOI specifically because it
> carries no transitive vulnerabilities, see below) and shares the exact
> same `ImportRowsAsync` row-processing pipeline the CSV endpoint already
> used, once `ExcelImportParser` turns the first worksheet into the
> identical row shape `CsvParser` produces — every validation, every
> error code, the per-row-independence guarantee, all of it now applies
> to both formats from one place, not two parallel implementations.
> `admin`'s single import control now routes by file extension rather
> than gaining a second control. Two real, non-obvious problems surfaced
> along the way, neither about the CSV/Excel logic itself: first,
> `ClosedXML` (this task's first attempt) pulls in `SSH.NET` 2025.1.0
> transitively, which the zero-warning build gate correctly refused —
> switched to `ExcelDataReader`, a read-only, dependency-light
> alternative, since this codebase never needs to *write* Excel, only
> read it. Second, and unrelated to either library: NuGet's live
> vulnerability advisory feed flagged that same `SSH.NET` version in
> `Testcontainers` (already present all session, pulled in for optional
> remote-Docker-over-SSH support) between one `dotnet build` and the
> next, with zero local code changes — confirmed genuinely external by
> reverting every change and rebuilding a byte-identical tree, still
> broken. Fixed by pinning a direct `SSH.NET` reference to a patched
> version in `Brasa.Api.IntegrationTests`, since NuGet treats a
> dependency's unbracketed version as a floor, not an exact pin — a
> transitive dependency neither this task nor any earlier one actually
> introduced, but which was blocking every build regardless (see the new
> §7 trap entries for both). A third, smaller gotcha specific to Excel
> itself: ExcelDataReader throws `NotSupportedException: No data is
> available for encoding 1252` on every `.xlsx` (not just legacy `.xls`)
> until `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`
> is registered — .NET Core dropped that codepage by default — fixed
> with a first-party `System.Text.Encoding.CodePages` package and a
> static constructor on `ExcelImportParser`. **Verified live**:
> `menu-import-excel.spec.ts` — 2 valid + 2 invalid rows plus a wholly-
> blank row in one real `.xlsx` (built at test time via `exceljs`, a
> devDependency of `e2e` only, never shipped) → `created: 2`, the same
> two row-level errors the CSV spec already proves, the blank row
> silently skipped rather than reported; an empty file, a non-`.xlsx`
> file, a genuinely corrupt `.xlsx`, and a header missing a required
> column each 400 with their own code; the `admin` UI imports a real
> `.xlsx` end to end. The pre-existing CSV spec (`menu-import.spec.ts`)
> re-verified unchanged, confirming the shared-pipeline refactor didn't
> disturb it. Full suite green (64 backend, 176 E2E).

---

## FND — Foundation & shared kernel

| ID | Task | Status |
|---|---|---|
| FND-01 | .NET 10 solution, 14 projects, modular monolith structure | ✅ |
| FND-02 | Central package management, zero-warning build policy | ✅ |
| FND-03 | `Money` — integer minor units, allocation-based splitting | ✅ |
| FND-04 | `Result` / `Error` — expected failures as values | ✅ |
| FND-05 | `ITenantContext` / `TenantContext` — resolve once per scope | ✅ |
| FND-06 | `IClock`, `PortugueseRegion`, business-day calculation | ✅ |
| FND-07 | `Entity` base — UUIDv7, tenant-owned, auditable, soft-delete | ✅ |
| FND-08 | Integration event and outbox **contracts** | ✅ |
| FND-09 | API host bootstrap — Serilog, ProblemDetails, health | ✅ |
| FND-10 | Site Agent worker host | ✅ |
| FND-11 | In-process integration event **dispatcher** implementation | ⬜ |
| FND-12 | Outbox processor — polling, retry, backoff, poison handling | ⬜ |

## DAT — Persistence, tenancy, RLS

| ID | Task | Status |
|---|---|---|
| DAT-01 | EF Core + Npgsql wiring, connection resilience | ✅ |
| DAT-02 | Schema-per-module conventions | ✅ `catalog` / `ordering` schemas |
| DAT-03 | `Money` value converter / owned type mapping | ✅ `MapMoney` |
| DAT-04 | Global query filter for `ITenantOwned` | ✅ |
| DAT-05 | PostgreSQL RLS policies on every tenant-owned table | ✅ **verified live** — see [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md) |
| DAT-06 | Session variable set from `ITenantContext` per request | ✅ `TenantSessionInterceptor` |
| DAT-07 | Privileged role + `ResolveAsSystem()` path for background jobs | ✅ `brasa_system` (`infra/initdb/02-system-role.sql`) — an ordinary role, no `SUPERUSER`/`BYPASSRLS`, the exact "third, narrowly-scoped role" this row's own note once deferred to, not the tempting-but-wrong shortcut [ADR 0010](../architecture/decisions/0010-rls-runtime-role-split.md) already named. `RowLevelSecurity.EnableSystemReadFor` grants it read-only (`FOR SELECT` policy + a `GRANT` that never includes write verbs, enforced twice redundantly on purpose) across every tenant on a table, scoped `TO "brasa_system"` so it can never widen what an ordinary `brasa_app` request sees — Postgres only evaluates a policy for the role it names. Deliberately a second, separate call from `EnableFor`, not folded into it — folding it in would have silently changed what already-committed historical migrations do the next time they run against a fresh database, found exactly this way when a first attempt did fold it in and a fresh Testcontainers run failed with "policy already exists." `ModulePersistenceExtensions` picks a physically separate connection string/pool (`ConnectionStrings:PostgresSystem`) whenever a scope's `ITenantContext.IsSystemContext` is set — a structural guarantee that an ordinary request can never end up on a connection still carrying `brasa_system`'s privileges, not one that depends on connection-pool reset-on-close behaving correctly. See [multi-tenancy.md](../architecture/multi-tenancy.md#the-system-context) for the full design, including the non-obvious `IgnoreQueryFilters()` requirement (the RLS policy admitting every row is only half the picture — EF's own query filter still applies and still matches nothing for a system context unless lifted explicitly) and the accepted InMemory/beta-pilot gap. No real consumer yet (the SAF-T sweep, FIS-23, is unbuilt) — ships the mechanism only, the same "mechanism before the trigger" shape this codebase already uses everywhere else. **Verified live**: `SystemContextIntegrationTests.cs` — `brasa_system` sees rows across multiple tenants with no session variable set at all, `brasa_app` stays exactly as isolated as before (adding the new role can't widen the old one's own policy), a write and a DDL statement both rejected with `insufficient_privilege`, and the real `ResolveAsSystem()` → `ModulePersistenceExtensions` → EF path proven end to end: the query filter alone still shows nothing, `IgnoreQueryFilters()` shows every tenant |
| DAT-08 | Audit interceptor — `CreatedAt/By`, `ModifiedAt/By` | ✅ `TenantAwareDbContext.StampEntities` |
| DAT-09 | `AssignTenant` interceptor on insert | ✅ |
| DAT-10 | Initial migration + migration-on-startup policy | ✅ migrate-on-startup, elevated role only |
| DAT-11 | Reflection test: every entity is `ITenantOwned` or allow-listed | ✅ `TenantIsolationReflectionTests` — one per module's built EF model, no DB connection needed |

## API — API platform & mobile readiness

> Rules: [../architecture/api-contract.md](../architecture/api-contract.md).
> These are the seams that let Android and iOS ship with no backend change.

| ID | Task | Status |
|---|---|---|
| API-01 | `/api/v1` versioning via `Asp.Versioning` | ✅ literal prefix + `ApiVersionSet`, not a templated segment — see commit message for why |
| API-02 | `/api/public/v1` consumer surface, separated from tenant API | ⬜ |
| API-03 | ProblemDetails mapping from `ErrorType` → HTTP status | ✅ |
| API-04 | Stable error-code registry + test that codes never change meaning | ✅ [error-codes.md](../architecture/error-codes.md), enforced by `ErrorCodeRegistryTests` — scans every `Error.*(...)` call site, fails on a removed/renamed code, an undocumented new one, or a code whose `Type` (and therefore HTTP status) changed. Verified it actually catches drift, not just that it compiles |
| API-05 | `Idempotency-Key` middleware + store | ✅ **verified live**: replayed request returns identical order id; DB confirms one row. In-memory store — durable store needed before scaling out |
| API-06 | `X-Brasa-Client` header parsing (id / version / platform) | ✅ `ClientVersionMiddleware` — best-effort: no client sends this header yet, so a missing/malformed value never fails the request, it just skips enrichment. Parsed `ClientInfo` is stashed on `HttpContext.Items` for `GET /client-requirements` (API-07) and pushed into Serilog's `LogContext` (`ClientId`/`ClientVersion`/`ClientPlatform`) so every log line for the request carries it — **verified live** via the console/Seq output |
| API-07 | `GET /client-requirements` — min & recommended version, sunset | ✅ Looks up the calling client's id (from `X-Brasa-Client`, API-06) in a config-bound `ClientRequirements` section — no admin UI to edit this yet, so it's configuration, not a database table. **Verified live**: known client id → `200` with its policy; missing/malformed header → `400 client.header_required`; well-formed header naming an unconfigured client id → `404 client.unknown_client_id` |
| API-08 | RFC 8594 `Deprecation` / `Sunset` response headers | ✅ `ApiDeprecationMiddleware` — a no-op today (config-bound under `Api:Deprecation`, empty by default), the same "ship the seam ahead of the trigger" shape as API-06/07: nothing to deprecate exists until a real `/api/v2` does. When configured, adds RFC 7231 IMF-fixdate `Deprecation`/`Sunset` headers (verified against a fixed instant, not just "a header exists") and an optional `Link: <url>; rel="sunset"` (RFC 8594 §5.2) to every response. **Verified**: `ApiDeprecationMiddlewareTests` (6 tests, incl. a non-UTC offset converting correctly before formatting) plus a live check against the running API with an env-var config override |
| API-09 | Cursor pagination helper, applied to every collection | ✅ `CursorPagination` (opaque base64 bookmark token) applied to `GET /orders` (ORD-22) — the only genuinely unbounded collection today; `/menu` and `/floor` are both bounded by the restaurant's own size and don't need it yet. Additive, not a body-shape change: the response is still a bare array exactly as it shipped, an `X-Next-Cursor` response header carries the next page's bookmark (present only when the page came back full). **Verified live**: page 1 returns the header, page 2 fetched with it returns older, non-overlapping rows; a malformed `cursor` 400s (`order.invalid_cursor`) |
| API-10 | `ETag` / `If-None-Match` on config and menu reads | ✅ `GET /menu` only — deliberately not `GET /floor`, whose state changes continuously through service. **Verified live**: 200 with a computed `ETag` on first pull, 304 with no body when it's echoed back as `If-None-Match`. Caught a real bug in review: the helper's own JSON serialization used `System.Text.Json`'s default (PascalCase) instead of ASP.NET Core's configured camelCase, silently breaking the `pos` client — fixed by resolving the app's configured `JsonSerializerOptions` from DI instead of using the type default |
| API-11 | Response compression | ✅ Brotli + gzip, `EnableForHttps = true` — safe here since the API has no cookie-reflected secrets for BREACH to exploit (bearer-token auth, ADR 0008). `application/problem+json` added to the default MIME type list so error responses compress too, not just success bodies. **Verified live**: `br` when offered, falls back to `gzip`, uncompressed when the client sends no `Accept-Encoding`, and confirmed it doesn't interfere with `ETag`'s `304` path (API-10) |
| API-12 | Rate limiting, keyed by client and tenant | ✅ `ApiRateLimiting`/`RateLimitingOptions` — a fixed-window limiter per `(tenantId, X-Brasa-Client client id)` partition on `/api/**`, config-bound under `RateLimiting`. Health checks and the OpenAPI document are never metered (`GetNoLimiter`). Rejections return `429` shaped like every other failure (`request.rate_limited`, new `ErrorType.RateLimited`) with a `Retry-After` header, not the framework's bare default body. Coarser than the eventual goal — nothing upstream of auth (IDN-03…08) identifies a *terminal* yet, so every `pos-web` client in a tenant shares one bucket, called out directly in the code rather than left implicit. Caught a real self-inflicted bug before it shipped: the production-shaped default (300/60s) throttled the E2E suite itself, since every dev client shares that one bucket with none sending `X-Brasa-Client` yet — fixed with a generous `appsettings.Development.json` override, proving the actual 429/`Retry-After`/body behaviour against a tight override instead. **Verified**: `ApiRateLimitingTests` (4 tests, partitioning logic) plus a live check hitting the running API with `RateLimiting__PermitLimit=3` — 4th request in a window gets `429`, `Retry-After: 30`, a fresh `X-Brasa-Client` value gets its own untouched bucket, `/health` stays unmetered throughout |
| API-13 | OpenAPI document generation, committed to the repo | ✅ [docs/openapi/v1.json](../openapi/v1.json), generated by `Microsoft.AspNetCore.OpenApi` (already wired for the dev-only Swagger-style UI) and committed so the API's shape is reviewable in a diff. Regenerated by hand for now — CI enforcement that it hasn't drifted is API-14, deliberately not built yet. **Gap found while building API-15, closed the same session:** every response body was undescribed — every endpoint returns a bare `Results.Ok(...)`/`IResult`, and ASP.NET Core's OpenAPI reflection can only infer a *request* body's shape that way on its own, never a response's. All 68 route mappings across eight endpoint files now carry an explicit `.Produces<T>(statusCode)` call; `GET /floor`'s `200` describes `RoomDto[]`, not just `{"description": "OK"}`. Error responses (`400`/`404`/etc.) are still undescribed — enumerating every distinct error code an endpoint can return is a separate, larger task, deliberately not attempted here. See [docs/openapi/README.md](../openapi/README.md) |
| API-14 | CI breaking-change detection against previous OpenAPI | ✅ `openapi-drift` job — starts the API for real, regenerates `docs/openapi/v1.json` (`GET /openapi/v1.json`, `servers` stripped) via `jq 'del(.servers)'`, and fails the build on any diff. Real breaking-change detection now sits alongside it, not folded in — `infra/scripts/check-breaking-changes.mjs` is a conservative, high-confidence *subset* of what "additive vs. breaking" really means (its own header names exactly what it does and doesn't cover — no recursive schema diffing past one `$ref` level, no type/enum/format changes), flagging: a removed operation, a newly-required parameter, a parameter that became required, a request body that became required (or a newly-required property inside one), a removed 2xx/3xx response status, or a property removed from a 2xx/3xx response's own top-level schema. Calibrated against this repo's own real commit history (every commit that ever changed `docs/openapi/v1.json`, diffed against its own parent) rather than only synthetic fixtures: it correctly stayed silent on every purely-additive commit and correctly flagged IDN-11's manager-authorisation gate (three already-shipped endpoints gained a newly-required `managerStaffId`/`managerPin`) as a genuine breaking change — a real one, confirming the tool's true-positive case actually happened in this project's own history, not just a hypothetical. It also surfaced one honest false-positive class, named rather than hidden: API-15/API-13's own response-description commit (adding `.Produces<T>()` everywhere) made several DELETE/POST operations' *documented* status code disappear (200 → the real 204/201, now accurately described for the first time) — the tool can only diff what's written in the document, not verify what the runtime actually returned before, so a doc *correction* reads identically to a real removal. Non-blocking everywhere it runs (`verify.ps1` and CI) — unlike drift, which is always a bug to fix, a breaking change is sometimes the intended one (IDN-11 again), so this reports for a human decision rather than forcing a bypass flag that would just train the habit of ignoring it. CI's own step (`.github/workflows/ci.yml`, `fetch-depth: 2` so `HEAD^`'s committed document is reachable) is written and locally-equivalent-tested via `verify.ps1`, but — same honesty this project already applies to untested CI changes — not yet exercised by a real push. **Exercised by real CI runs now** (the drift half), **and it caught real drift three times running** — CAT-02, ORD-07/08/09 and CAT-17 each shipped its own endpoint code without the regeneration step, exactly the FLR-04/CAT-01/13/17/19-then-API-18 pattern this job exists to catch, now happening *after* the job existed instead of before. Fixed by regenerating and committing the doc, and by adding `infra/scripts/verify.ps1` — a local script mirroring CI's build/test/openapi-drift/vulnerable-package jobs, meant to be run before a commit so this stops being a check that only fires after a push. **Then failed on every subsequent push regardless of content** — the documented regeneration command used PowerShell's `ConvertTo-Json`, whose indentation never matches this job's own `jq`-based regeneration byte-for-byte even when the underlying data is identical (confirmed by deep-sorting both documents to primitives and diffing that, not raw text — zero semantic difference on a run CI still failed). `verify.ps1`'s own drift check used the same broken formatter on both sides of its comparison, so it always agreed with itself and could never have caught this. Fixed by moving regeneration to Node (`infra/scripts/regenerate-openapi.mjs`), which both the docs and `verify.ps1` now call instead of hand-rolling the transform twice. See the trap entry in `docs/ai/README.md` |
| API-15 | TypeScript SDK generation into `web/sdk` | 🚧 A new `@brasa/sdk` workspace package (`npm run generate`) runs `openapi-typescript` against the committed `docs/openapi/v1.json`, producing `src/schema.ts`. Shipped in two slices the same session: request bodies/paths/query params first, which surfaced a real, previously-unnoticed gap in API-13 itself (every response body had always been undescribed — see that row's own update); closing that gap second, so both halves of every one of the 68 route mappings are now typed and verified against real endpoints (`src/schema.guard.ts`, a permanent type-level check covering both a request and a response shape, not a throwaway). Error responses (`400`/`404`/etc.) stay undescribed — a separate, materially larger task. Not yet consumed anywhere — no `pos`/`admin` call site switched over, and there's no typed fetch client (e.g. `openapi-fetch`) wrapping these types yet, since this codebase's own `ApiError`/`ProblemDetails` handling doesn't map onto a generic wrapper without its own dedicated design pass. See [src/web/sdk/README.md](https://github.com/mahbub9/Brasa/blob/main/src/web/sdk/README.md) for the exact scope |
| API-16 | SignalR hub, JSON protocol | ✅ A narrow first slice — `FloorHub` at `/hubs/floor` (outside `/api/v1`, since a connection isn't a versioned resource), broadcast-only, JSON protocol (`AddSignalR()`'s own default, no MessagePack). Broadcasts a single, payload-less `FloorChanged` signal whenever any table's state changes — `ClearTableAsync`/`RequestBillAsync` (`FloorEndpoints.cs`) and `OpenOrderAsync`/`TransferOrderAsync`/`MergeOrdersAsync`/`CloseOrderAsync` (`OrderEndpoints.cs`, composing `IHubContext<FloorHub>` at the API layer the same way these handlers already compose Floor + Ordering) all call the same `NotifyFloorChangedAsync` helper right after their own successful save. No per-tenant/per-terminal targeting yet — every connection is in one `Clients.All` group, a deferred gap named in `FloorHub`'s own remarks, waiting on IDN-06/07 to key a group by |
| API-17 | REST equivalent for every realtime message (enforced by test) | ✅ Satisfied by construction rather than a separate mechanical check, for this one hub: the message itself carries no data — a client that receives it always re-fetches `GET /floor`, so there is nothing for a realtime payload to ever disagree with the REST response about. `pos` is the first (and so far only) subscriber (`connectFloorHub`, `@microsoft/signalr`), reconnecting and re-fetching on `onreconnected` too so a dropped connection never leaves it silently stale. **Verified live**: `floor-realtime.spec.ts` — two real browser tabs, no `reload()` anywhere in either test: opening a table from a second tab flips it to `Occupied` in the first purely from the pushed signal, and clearing a dirty table flips it back to `Free` the same way |
| API-18 | `/.well-known/` app-link documents for iOS and Android | ✅ `GET /.well-known/apple-app-site-association` (`{"applinks":{"apps":[],"details":[]}}`) and `GET /.well-known/assetlinks.json` (`[]`) — structurally valid, honestly empty rather than fabricated: no bundle id, Team id or Android package name exists to put in either file until a native app does (MOB-01+, not started), and inventing placeholder ones would make a real verification document lie. `Content-Type: application/json`, unversioned (not under `/api/v1`), no auth. **Verified live** |

## IDN — Identity & access

| ID | Task | Status |
|---|---|---|
| IDN-01 | Organization / Site / Terminal hierarchy | ✅ A narrow first slice — `Brasa.Modules.Identity`, previously an empty stub, now owns the `identity` schema: `Organization` (tenant-scoped, a business), `Site` (belongs to an organization, carries a real `PortugueseRegion` — Continental/Madeira/Azores — from day one, not a placeholder), `Terminal` (belongs to a site, a bare registry row). Create + list only via `POST`/`GET /organizations`, `POST`/`GET /organizations/{id}/sites`, `POST`/`GET /sites/{id}/terminals` — no update/delete yet, and no pairing/auth (IDN-06/07 are separate, not-yet-built rows), a deliberately minimal slice. `DevIdentitySeeder` seeds one full chain ("Brasa Demo, Lda" → "Restaurante Central" → "Caixa 1") the same way `DevFloorSeeder` seeds the floor plan. Exists to give `Site` a stable, referenceable id — the intended near-term consumer was CAT-05 (price lists per site, now built the same session). FLR-06 (waiter section assignment, now also built) turned out to key off `Staff` (IDN-08/09) directly rather than `Site` — a room has no site relationship of its own, so there was nothing for FLR-06 to match a section's staff id against beyond confirming the id names a real `Staff` row at all. **Verified live**: `identity-organization-site-terminal.spec.ts` — create/list at all three levels, the region round-trips, validation and 404 paths (`identity.invalid_organization_name`/`invalid_site_name`/`invalid_region`/`invalid_terminal_label`/`organization_not_found`/`site_not_found`), and the seeded demo chain resolves end to end |
| IDN-02 | User accounts, email verification, password reset | ⬜ |
| IDN-03 | OAuth 2.1 / OIDC authorization-code flow with PKCE | ⬜ |
| IDN-04 | Access token (JWT) issuance and validation | ⬜ |
| IDN-05 | Refresh token — opaque, rotating, device-bound, replay detection | ⬜ |
| IDN-06 | Device registry — register, list, revoke individually | ⬜ |
| IDN-07 | Terminal pairing via short-lived device code | ⬜ |
| IDN-08 | Staff PIN sign-in on a paired terminal | 🚧 The PIN-verification half only — "on a paired terminal" is not: no terminal pairing exists (IDN-07), so `POST /staff/{id}/verify-pin` checks a PIN against a *known* staff id, not "identify me by PIN alone with no picker." That broader UX was deliberately not chosen — without knowing who's attempting, a failed PIN can't be attributed to the right person's lockout counter. See IDN-09 and the feature page for the full mechanism |
| IDN-09 | PIN hashing, lockout, and rotation policy | ✅ `Staff` (Identity module, site-scoped like `Terminal`) — PBKDF2-HMAC-SHA256 (`Rfc2898DeriveBytes`, no new dependency, 210k iterations per OWASP's 2023 minimum), locks out after 5 consecutive incorrect PINs for 15 minutes (even a *correct* PIN is refused while locked), and `PUT /staff/{id}/pin` rotates a PIN and clears any lockout — an admin reset, not self-service, same "ships ahead of manager authorisation" shape every other admin mutation in this codebase has today. Not wired into any endpoint's own authorization decision — ORD-10 (void)/ORD-11 (discount) both still have "no manager-authorisation gate yet" as their own named, deferred gap (IDN-11). **Verified live**: `staff.spec.ts` — a correct PIN verifies; an incorrect one is rejected (`identity.pin_incorrect`); 5 consecutive failures lock the account so even the *correct* PIN is then refused (`identity.staff_locked`); resetting the PIN clears the lockout and the new PIN works immediately; an empty name, a malformed PIN (too short/long/non-digit), an unrecognised role and unknown site/staff ids are all rejected with their own codes; `admin`'s new staff screen adds a staff member and resets their PIN through the real UI, both proven to actually take effect via a follow-up API call, not just "the UI showed no error" |
| IDN-10 | Roles & permissions model | ⬜ |
| IDN-11 | Manager-authorisation flow for privileged actions (voids, discounts) | ✅ The real trigger ORD-10/ORD-11 both shipped ahead of. `POST /orders/{id}/lines/{lineId}/void`, `PUT /orders/{id}/lines/{lineId}/discount` and `PUT /orders/{id}/discount` now all require `managerStaffId`/`managerPin` in the body — `OrderEndpoints.AuthorizeManagerAsync` composes Identity into Ordering at the API layer (module-boundaries.md rule 5, same shape `PriceListEndpoints` already uses for Catalog+Identity), checks the credential names a real `StaffRole.Manager` *before* ever touching `Staff.VerifyPin` (a Staff-role id is rejected outright, without spending any of that person's own lockout budget), then reuses `Staff.VerifyPin` exactly as `POST /staff/{id}/verify-pin` does — same lockout mechanics, same "known id, not blind PIN entry" shape IDN-08/09 already established. A per-call credential, not a session or a login: nothing is cached, every privileged call re-proves it. `identity.staff_not_manager` is the registry's first real `ErrorType.Forbidden` (403) call site — the type existed since API-03 but had never been constructed until now. No pos/admin UI prompts for a manager PIN yet (no staff-picker exists in either client — WEB-07 is still unbuilt), so this ships the gate itself, verified live only. **Verified live**: `manager-authorization.spec.ts` — a non-manager staff credential is rejected (`identity.staff_not_manager`) without touching the line; an unknown `managerStaffId` (`identity.staff_not_found`) and a manager's own wrong PIN (`identity.pin_incorrect`) are both rejected, then the correct PIN succeeds; the same gate covers line and order discounts; 5 consecutive wrong PINs against an isolated manager lock them out (`identity.staff_locked`) even for their own correct PIN afterward, mirroring IDN-09's own lockout test exactly. `void-line.spec.ts`/`discounts.spec.ts` continue passing unchanged, now exercising the real gate via a seeded-manager default in the test helpers rather than no gate at all. |
| IDN-12 | Consumer identity realm for the public surface | ⬜ |
| IDN-13 | Tenant provisioning / onboarding | ⬜ |
| IDN-14 | Push token registration endpoints | ⬜ |
| IDN-15 | `IPushChannel` abstraction (no provider adapter yet) | ⬜ |
| IDN-16 | Per-tenant, per-platform feature flags | ✅ `FeatureFlag` (Identity) — `Key`/`Platform`/`IsEnabled`, `Platform` never null (`"all"` is the sentinel for every platform — a nullable column would have silently let two "all platforms" rows for the same key coexist, since Postgres never treats two `NULL`s as equal in a unique index). `PUT /feature-flags/{key}` upserts (optionally scoped to one platform), `GET /feature-flags` lists every flag for the tenant, `GET /feature-flags/{key}/resolve` is the shape a real consumer will actually call — a platform-specific row wins over the tenant's "all platforms" row, an unconfigured flag defaults to disabled, never enabled. No consumer anywhere in this codebase reads a flag yet — the same "mechanism before the trigger" shape CAT-05/CAT-10/CAT-16/FLR-05 already established; this was named explicitly as a day-one scale decision in the project's own build plan. `admin` gets a "Feature flags" screen — add a flag, see every key/platform combination grouped by key, toggle one on or off. **Verified live**: `feature-flags.spec.ts` |

## CAT — Catalog & menu

| ID | Task | Status |
|---|---|---|
| CAT-01 | Menu categories, ordering, visibility | ✅ `MenuCategory.IsVisible` had no setter at all until now — nothing could ever set it to anything but its default `true`, despite this row's own title naming "visibility" as in scope and being marked done. `PUT /menu/categories/{id}/visibility` (found by the same sweep as FLR-04/CAT-13/CAT-19 — a domain gap one level up, a category rather than an item) closes it: hiding a category removes it *and every item under it* from `GET /menu` in one call. Ships ahead of any UI. **Verified live**: hide → category and its items vanish from the menu; show → both restored; unknown category `404`s (`catalog.category_not_found`) |
| CAT-02 | Menu items — name, description, image, allergens | ✅ `PUT /menu/items/{id}/details` sets description + declared allergens (14 fixed EU-regulated allergens, Regulation (EU) No 1169/2011 — stable taxonomy, not a Portugal-specific figure needing an accountant's confirmation like `VatRate`); rendered on the `pos` menu screen. `POST`/`DELETE /menu/items/{id}/image` upload/remove a photo — local disk storage (`MenuItemImageStorage`), an honest dev-only placeholder: no S3/Blob credentials in this environment (same reasoning as skipping OPS-11), no per-tenant isolation yet |
| CAT-03 | Modifier groups (required / optional, min / max) | ✅ `ModifierGroup` belongs to one `MenuItem` (not yet shared across items — see its doc comment); server enforces min/max on `POST /orders/{id}/lines`, not just the UI |
| CAT-04 | Modifiers with price deltas | ✅ `Modifier.PriceDelta` (can be negative — e.g. "Meia dose"); snapshotted onto `OrderLineModifier` at the time of sale, folded into `LineTotal` and the fiscal document's gross total |
| CAT-05 | Price lists per site | ✅ A narrow first slice, unblocked by IDN-01's `Site` — `PriceList` (`SiteId`, `Name`) owns `PriceListEntry` rows (`MenuItemId`, `Price`), the same ownership shape `MenuItem`/`ModifierGroup` already use. `POST`/`GET /price-lists`, `GET /sites/{id}/price-lists`, `POST /price-lists/{id}/entries` (rejects a second entry for the same item — one price per item per list, backed by both a domain guard and a DB unique index), `GET /price-lists/{id}/effective-price/{menuItemId}` (the list's own override, or the item's ordinary price when none is set — the actual resolution logic, not just storage). No rename/delete/remove-entry yet. `SiteId`/`MenuItemId` are plain opaque references, never a live join — the same pattern `Order.TableId`/`OrderLine.MenuItemId` already use; the create/list-for-site endpoints compose `CatalogDbContext` and `IdentityDbContext` in the same handler to confirm a site is real, sanctioned at the API layer per module-boundaries.md rule 5. Nothing in `AddLine` or either web client resolves an effective price through this yet — there is no site-selection concept in `pos`/`admin` today, so this ships the pricing model itself, the same "mechanism before the trigger" shape CAT-14/15 already established. **Verified live**: `price-lists.spec.ts` — a fresh item resolves to its ordinary price before any override, then to the list's own price once one is added; the entry persists across a refetch and appears in the site's list; a duplicate entry, a negative price, an unknown item and an unknown price list are all rejected with their own codes |
| CAT-06 | Channel pricing — dine-in / takeaway / delivery | 🚧 Dine-in/takeaway ✅ — `MenuItem.TakeawayPrice` (nullable, "same as dine-in" when unset), `PUT /menu/items/{id}/takeaway-price`, `AddLineAsync` picks it over `Price` when `Order.IsTakeaway`. VAT rate is unaffected — that's `TaxRule` (CAT-07/08), a separate concern. Delivery not built: there is no delivery order path in this codebase at all yet, so there's nothing for a delivery price to attach to. `pos`'s menu button shows the price for the order actually being rung up; `admin` gets an inline add/edit/clear editor next to the dine-in price. **Verified live**: `menu-item-takeaway-price.spec.ts` |
| CAT-07 | `TaxRule` — item × channel × region, effective-dated | ✅ "Item" is the alcohol band (`IsAlcoholic`), not a per-`MenuItemId` key — Portuguese VAT law taxes categories of goods, never an individual named item, the same reason CAT-09's `MenuItem.IsAlcoholic` flag exists at all. `TaxRule(isAlcoholic, isTakeaway, region, rate, effectiveFromUtc, effectiveToUtc)` via `POST /tax-rules`; create + list only, no update/delete — a correction is a new, later-effective row, never an edit to one already on file, the same "never mutate, only add" instinct fiscal documents themselves follow. Delivery out of scope, the same gap CAT-06 already named. Rates stay data, never a hardcoded constant — see `VatRate`'s own remarks; current rates are still unconfirmed by an accountant. **Not yet wired into `AddLine`/the fiscal document builder** — see CAT-08 |
| CAT-08 | VAT resolution service with date-aware lookup | ✅ `GET /tax-rules/resolve?isAlcoholic=&isTakeaway=&region=&atUtc=` (atUtc optional, defaults to `IClock.UtcNow`) — `TaxRule.Resolve` picks the rule in force for a combination at an instant, and if two rules' ranges wrongly overlap, the most recently-*started* one wins (the same resolution a correction would rely on). `404 catalog.tax_rule_not_found` when nothing covers the combination. Deliberately not wired into `AddLine`/`AddComboLineAsync`/the fiscal document builder yet — swapping every one of those call sites from the flat `MenuItem.VatRate` to a live `TaxRule` lookup touches the most fiscal-sensitive code in the system and deserves its own dedicated, carefully-verified pass, not a side effect of shipping the data model — the same "mechanism before the trigger" shape CAT-05/CAT-10/CAT-16/FLR-05 already established. Dev-seeded: mainland dine-in/takeaway rows for both bands (13%/23%), effective from a fixed 2024-01-01 anchor so "today" always resolves regardless of which day the seeder runs; Madeira/Azores deliberately not seeded, since no seeded site claims either region yet. **Verified live**: `tax-rules.spec.ts` — the seeded mainland rates resolve correctly for both bands and both channels; a later rule supersedes an earlier one within its own window, and nothing resolves before the earliest rule's start; an unrecognised region, an out-of-range percentage, an unparsable date and a backwards effective range are all rejected with their own codes; an uncovered combination 404s |
| CAT-09 | Alcohol flag driving the 23% band separation | ✅ `MenuItem.IsAlcoholic` |
| CAT-10 | Combos / menus (*menu do dia*) | ✅ A narrow first slice — `Combo` (`Name`, `Price`) owns `ComboComponent` rows (`MenuItemId`, always exactly one unit — no guest choice among several and no quantity>1 yet, both real product features deliberately deferred rather than guessed at), the same ownership shape `MenuItem`/`ModifierGroup` and `PriceList`/`PriceListEntry` already use. `POST`/`GET /combos`, `GET /combos/{id}`, `POST /combos/{id}/components`. The genuinely new piece is `POST /orders/{id}/combo-lines` (in `OrderEndpoints.cs`, composing Catalog + Ordering the same way `AddLineAsync` already does): resolves the combo's components, allocates `Combo.Price` across them via `Money.Allocate` weighted by each component's own current standalone price — the exact same proration ORD-11 already uses to prorate an order-level discount across lines — then adds each component as an ordinary `OrderLine` at its own real VAT rate via the existing `Order.AddLine`. A combo is therefore never a new fiscal concept: mixed-rate items (e.g. 13% food + 23% wine) itemize correctly and reconcile to the cent by construction, reusing machinery already proven correct, not inventing new fiscal logic. Nothing in `pos`/`admin` offers a "ring up this combo" UI yet — verified at the API level only, the same "mechanism before the trigger" shape CAT-14/15 already established. **Verified live**: `combos.spec.ts` — a combo priced below the sum of two differently-priced, differently-taxed components allocates proportionally (weights 4:3 → 3.43/2.57, summing back to the combo's own price exactly), the pre-bill's VAT breakdown shows both bands separately and reconciles to the total; a duplicate component, an unknown item/combo/order, an empty combo, an unavailable component, an empty name and a negative price are all rejected with their own codes |
| CAT-11 | *Prato do dia* — daily specials with schedules | ✅ `MenuItem.Schedule` (`MenuItemSchedule`: a `[Flags] ScheduleDays` mask + start/end `TimeOnly`, start inclusive/end exclusive, no overnight wraparound — see its own doc comment) via `PUT /menu/items/{id}/schedule`, all-or-nothing (days+start+end together, or all three cleared). `GET /menu` filters a scheduled item out entirely outside its window; `GET /menu/all` never filters on it, same shape as CAT-01/13's visible-vs-management split. No per-tenant region/site record exists yet (IDN-01/CAT-05), so this uses mainland `Europe/Lisbon` time via the existing `PortugueseTimeZone` helper — an honest gap, not silently wrong, and the same default the rest of the app already assumes. `admin` gets a day-checkboxes + two time-inputs editor; `pos` needed no changes at all, since an out-of-window item simply isn't in `GET /menu`'s response. **Verified live**: `menu-item-schedule.spec.ts` — a window covering today (computed from the real `Europe/Lisbon` date, not hardcoded) keeps the item on `GET /menu`; a window excluding today removes it from `GET /menu` but not `GET /menu/all`; clearing restores it; an invalid day name, an unparsable time, a backwards window, a partial (some-but-not-all-fields) update and an unknown item are all rejected |
| CAT-12 | *Couvert* handling — charged only when consumed | ✅ `MenuItem.IsCouvert` via `PUT /menu/items/{id}/couvert` — a plain tag, not a filter: unlike CAT-11's schedule, it never removes the item from `GET /menu`, and `AddLine` needs no changes at all since adding couvert is the same call as any other line. "Charged only when consumed" was already true of every menu item (nothing is ever added except by an explicit `AddLine`); what this closes is the *workflow* gap — a dedicated one-tap `pos` affordance (`CouvertBar`) that rings a couvert item up at the order's own cover count instead of the usual quantity of 1, hidden for takeaway orders (no cover count to ring up against). `admin` gets a mark/unmark toggle next to the availability one. **Verified live**: `menu-item-couvert.spec.ts` — the flag sets/persists/clears without ever hiding the item from `GET /menu`; the couvert bar tapped once on a 3-cover table adds a line at quantity 3; the bar is absent for a takeaway order (the item still orderable the normal way); the admin toggle round-trips; an unknown item 404s |
| CAT-13 | Item availability / 86-ing (out of stock) | ✅ `MarkAvailable`/`MarkUnavailable` existed since I0 and `AddLine` already enforced `IsAvailable`, but no endpoint ever called either — `IsAvailable` could never actually become `false`. `PUT /menu/items/{id}/availability` closes that: ships ahead of any UI that will call it (no admin app, no in-order 86 control), same as CAT-02/CAT-17/CAT-18. **Verified live**: 86'ing an item hides it from `GET /menu` and the previously-dead `catalog.item_unavailable` guard on `AddLine` finally fires for real; un-86'ing restores both; unknown item `404`s |
| CAT-14 | Course assignment per item | ✅ `PUT /menu/items/{id}/course` — `Course?` (`Starter`/`Main`/`Dessert`/`Drink`), null when not yet assigned (a data-entry gap, same convention as an empty `Allergens` list). Independent of `MenuCategory`: a menu can be organised for browsing by ingredient/style while every item still belongs to exactly one course. No admin UI yet, and course *firing* (ORD-07) isn't built either — ships ahead of both, the tag it will read from once it is. **Verified live**: set/persist/clear, an unrecognised course name and an unknown item both rejected (`catalog.invalid_course`/`catalog.item_not_found`) |
| CAT-15 | Kitchen station routing per item | ✅ `PUT /menu/items/{id}/station` — `KitchenStation?` (`Grill`/`Bar`/`ColdKitchen`/`Fryer`/`Pastry`), null when not yet assigned. Independent of both `MenuCategory` and `Course` (CAT-14): a starter and a main can both come off the grill. No admin UI yet, and station *routing* (KIT-06) isn't built either — ships ahead of both, the tag it will read from once it is. **Verified live**: set/persist/clear, an unrecognised station name and an unknown item both rejected (`catalog.invalid_station`/`catalog.item_not_found`) |
| CAT-16 | Menu versioning with effective dates | ✅ Scoped to scheduled future price changes — the other half this title could mean, a full historical-menu audit trail, is a separate, deferred concern (order lines already snapshot their own price/VAT at time of sale, so per-sale history was already solved before this row). `MenuItem.ScheduledPrice`/`ScheduledPriceEffectiveFromUtc` (persisted as two flat fields, not one nested value object — EF Core cannot constructor-bind a complex type nested inside another) via `PUT /menu/items/{id}/scheduled-price`, all-or-nothing like CAT-11's schedule. Deliberately not driven by a background job — nothing runs one yet (Hangfire is OPS-10) — `MenuItem.EffectivePrice(nowUtc)` resolves it lazily on every read instead, the same "computed, never promoted" shape CAT-11 already proved: a change due five minutes ago applies correctly with zero manual step. `GetMenuAsync`/`GetMenuAllAsync`'s `Price` field and `AddLine`/`AddComboLineAsync`'s snapshot all resolve through `EffectivePrice`, not the raw stored value — the guest is charged exactly what the menu just displayed. Only one pending change at a time, only the dine-in price (not `TakeawayPrice`), both documented gaps. **Verified live**: `menu-item-scheduled-price.spec.ts` — a change scheduled ~1.5s out is confirmed *not* active, a real wait elapses it, then `GET /menu` and a real `AddLine` call both reflect the new price with no intervening action; a partial request, a non-future date, an unparsable date, a negative price and an unknown item are all rejected with their own codes |
| CAT-17 | Bulk import (CSV / Excel) | ✅ `POST /menu/items/import` (CSV, hand-written RFC 4180 parser, `CsvParser`, 8 unit tests) and `POST /menu/items/import/excel` (`.xlsx`, `ExcelDataReader`) share one `ImportRowsAsync` pipeline once each turns its file into the same row shape — every validation/behaviour below applies to both. Rows import independently — an unknown category or an unparsable price is reported per-row (1-indexed against the data rows) rather than failing the whole file. Create-only, not upsert: importing the same file twice creates duplicates. `admin`'s single import control routes by file extension. **Verified live**: 2 valid + 2 invalid rows in one file → `created: 2`, two row-level errors with the exact bad value named, both formats; empty file, a header missing a required column, a non-`.xlsx` file and a corrupt `.xlsx` all `400` with their own codes; a wholly-blank Excel row is skipped, not reported as invalid |
| CAT-18 | Soft delete preserving historical order references | ✅ `MenuItem` only (what `OrderLine.MenuItemId` can reference) — `DELETE /menu/items/{id}`, no admin UI yet. Verified live: deleted item vanishes from `/menu` and can't be re-ordered, but a past order's line keeps its name/price. See `ISoftDeletable` in `docs/architecture/multi-tenancy.md` |
| CAT-19 | Menu item price editing | ✅ `MenuItem.Reprice` existed with its own negative-price guard since I0, with no endpoint calling it — found the same way as CAT-13's availability gap. `PUT /menu/items/{id}/price` closes it; ships ahead of any UI, same as CAT-02/13/17/18. Safe by construction, not convention: `OrderLine.UnitPrice` snapshots at add-time, so repricing never rewrites a past order. **Verified live**: reprice a seeded item, confirm the *already-open* order's existing line total is unchanged while `GET /menu` shows the new price; negative price and unknown item both rejected (`catalog.invalid_price`/`catalog.item_not_found`) |

## FLR — Floor plan & tables

| ID | Task | Status |
|---|---|---|
| FLR-01 | Rooms / areas (indoor, esplanada, bar) | ✅ `Room` — seeded (Salão, Esplanada), no editor UI yet |
| FLR-02 | Tables — number, seats, position, shape | ✅ `Table` — position/shape stored for FLR-03 to use later; `pos` renders a static grid, not the coordinates |
| FLR-03 | Drag-and-drop floor plan editor | ✅ Table and room CRUD, plus the drag-and-drop canvas itself — `POST /rooms/{id}/tables`, `PUT /tables/{id}`, `DELETE /tables/{id}` (guarded to `Free` only), `POST /rooms`, `PUT /rooms/{id}`, `DELETE /rooms/{id}` (guarded to zero tables); `admin`'s "Plano de sala" has plain add/edit/delete forms for both, plus `FloorCanvas` — a pointer-events (not HTML5 drag-and-drop, which is flaky under Playwright) canvas rendering each room's tables on a 70px grid, snapping a drag to the nearest cell and calling the same `PUT /tables/{id}` the form already used. `aria-hidden` on the canvas: it's a mouse-only convenience layer, every capability it exposes stays fully reachable via the pre-existing accessible form, so hiding it from assistive tech loses no functionality. **Verified live**: `floor-table-management.spec.ts`, `floor-drag-drop.spec.ts` |
| FLR-04 | Table states (free, occupied, bill requested, dirty) | ✅ all four wired end-to-end through `pos`, including `BillRequested` now: `POST /tables/{id}/request-bill` + a "Pedir conta" button, distinct from the pre-bill preview (ORD-18/19, "Ver conta") — that stays a read-only `GET`, this is the explicit floor-plan signal for staff. **Verified live** in a real browser: clicking it flags the table `BillRequested` on `GET /floor`; a free table 409s (`floor.table_not_occupied`), an unknown table 404s |
| FLR-05 | Table merge / split for large parties | ✅ Scoped to a floor-plan seating group — pushing 2+ *free* tables together into one unit for a large party, not a full order-merge (that already exists, separately, as `ORD` table-transfer/merge-orders). `Table.GroupId` (a plain `Guid?`, no FK — the same opaque-reference convention `OrderLine.MenuItemId` already established) via `POST /table-groups` / `DELETE /table-groups/{id}`, both requiring every table to be `Free` first. Deliberately given real teeth rather than staying cosmetic: `Table.Occupy()` itself now refuses a grouped table (`floor.table_grouped`) — a grouped table shown as `Free` with nothing stopping it being seated individually would have actively contradicted the feature's own purpose. Cascading `Occupy`/`Clear`/`Release` across a group's siblings was deliberately *not* built (a materially larger change touching every already-shipped table-state endpoint) — grouping only blocks individual seating for now, a named gap. No client UI yet — no floor-plan multi-select exists in `admin`/`pos` today, same "mechanism before the trigger" call CAT-05/CAT-10/CAT-16 each made. **Verified live**: `table-groups.spec.ts` — grouping blocks `POST /orders` on every member table with `floor.table_grouped`, ungrouping restores ordinary seating; rejects fewer than 2 tables (`floor.table_group_too_small`), an unknown table (`floor.table_not_found`), a non-free table (`floor.table_not_free`), a table already in another group (`floor.table_already_grouped`), and 404s deleting an unknown group (`floor.table_group_not_found`) |
| FLR-06 | Section assignment to waiters | ✅ Unblocked by IDN-01 (`Site`)/IDN-08-09 (`Staff`) — `PUT /rooms/{id}/section` assigns or clears (`staffId: null`) which waiter is working a room as their section. A room is already the "which area" granularity (Salão, Esplanada), so this is a new field, not a new entity — `Room.AssignedStaffId`, a plain opaque `Guid?` reference to Identity's `Staff`, the same pattern `Order.TableId` uses for a Floor `Table`. `FloorEndpoints` composes `IdentityDbContext` to confirm a non-null `staffId` is real before assigning (`404 identity.staff_not_found` otherwise); `RoomDto.AssignedStaffName` is resolved fresh from Identity on every `GET /floor` (batched into one query across every room, not N+1), never snapshotted, so a renamed staff member is never shown stale. Any staff role works, Manager or plain Staff — unlike IDN-11's manager-only gate, this isn't a privileged action. `admin`'s room editor gets a section `<select>` next to each room; `pos` shows nothing yet, the same "mechanism before the trigger" shape most of this session's other Identity-adjacent work has used. **Verified live**: `floor-section-assignment.spec.ts` — a plain Staff-role member (not a manager) is assigned and its name resolves correctly on both the assignment response and a fresh `GET /floor`, clearing removes both fields together; an unknown staff id and an unknown room both 404 with their own codes; the admin UI assigns and clears a section, both confirmed to actually take effect via a follow-up API call |
| FLR-07 | Multi-floor support | ✅ `Room.FloorLevel: int` (default `0`, so every existing/seeded room needed no data migration) — `0` ground floor, positive above it, negative below (a basement or cave). Deliberately not a separate `Floor` entity: every field a floor needs (which rooms, which tables) is computed from the rooms that carry its level, the same "no entity where a plain field says the same thing" call FLR-05's `Table.GroupId` already made. `POST`/`PUT /rooms` both accept it (`PUT` requires it explicitly, `POST` defaults to `0`). Given real teeth as a **display** concern, not an access one — a floor badge/heading only ever earns its place once a tenant's own rooms actually span more than one level; seeded restaurants (single-storey) render exactly as before, byte-for-byte. `admin`'s room editor shows a "Floor N" badge on every room once ambiguity exists (labelling *every* room, not just the odd one out, once there is more than one — otherwise "no badge" would be a second, silent way to mean "ground floor"), plus a floor-level number input on add/edit; `pos`'s table picker groups rooms under a floor heading the same way. **Verified live**: `floor-multi-level.spec.ts` — floorLevel round-trips through create/`GET /floor`/update; omitting it on create defaults to `0`; `admin`'s floor badge appears on every room (incl. the seeded ground-floor ones) once a second floor is created; editing a room's floor level through the UI round-trips to the API; `pos`'s table picker shows both floor headings and the new room under its own |

## ORD — Ordering

| ID | Task | Status |
|---|---|---|
| ORD-01 | Order aggregate — lifecycle and state machine | ✅ `Open`/`Closed`; richer states (courses, kitchen status) are I2 |
| ORD-02 | Open a table, set cover count | ✅ opens against a real `Table` (FLR), not free text — see `Order.TableId` |
| ORD-03 | Add / remove / edit order lines | 🚧 add ✅, edit (quantity) ✅ — `PUT /orders/{id}/lines/{lineId}/quantity`, recomputes `LineTotal` and any discount automatically since both are already derived from `Quantity`. Outright "remove" deliberately not built as a separate endpoint: void (ORD-10) already covers undoing a line, with the reason and audit trail a bare delete would lose, so `SetLineQuantity` rejects a voided line rather than editing around it. `pos` gets a +/− stepper per line. **Verified live**: `order-line-quantity.spec.ts` |
| ORD-04 | Line snapshots — name, price, VAT rate at time of sale | ✅ |
| ORD-05 | Apply modifiers to a line | ✅ shipped alongside CAT-03/04 — `AddLine`'s `selectedModifierIds` resolved and validated at the API layer (`ResolveModifiers`), folded into `OrderLine.ModifiersTotal`/`LineTotal` |
| ORD-06 | Free-text kitchen notes | ✅ `PUT /orders/{id}/lines/{lineId}/notes` — per-line, set after the line is rung up; staff/kitchen visibility only, never a Fiscal concern |
| ORD-07 | Courses and course firing | ✅ `OrderLine.Course` snapshots CAT-14's `MenuItem.Course` at add-time (the same `ItemName`/price/VAT convention, not a live join) — `Course.cs`'s own doc comment named this task as its future consumer before it existed. `POST /orders/{id}/fire` fires a named course or (course `null`) everything still pending. No manager authorisation — firing touches no money, unlike void/discounts |
| ORD-08 | Send to kitchen (partial and full) | ✅ `Order.FireLines(course, firedAtUtc)` — a specific course is the "partial" send, `course: null` is the "full" send, both the same endpoint. Idempotent: an already-fired line is silently skipped, not re-fired or rejected. No real kitchen exists yet (KIT-01…09/AGT unbuilt) — this only flips `OrderLine.IsFired`/`FiredAtUtc`, the seam a future ticket-printing consumer reads from |
| ORD-09 | Order line status tracking | ✅ Scoped to the one status meaningful before a real kitchen exists — `OrderLine.IsFired`/`FiredAtUtc`. Richer states (`Preparing`/`Ready`/`Served`) need a KDS to transition them and are deferred to I4 alongside KIT-10…13 |
| ORD-10 | Void a line, with reason and manager authorisation | ✅ `POST /orders/{id}/lines/{lineId}/void` — both halves of this row's own title now: manager authorisation (IDN-11) gates every void, requiring a real Manager-role staff id and their correct PIN in the same request. `reason` is required, rejected outright if missing/blank. The line is never deleted — `ItemName`/`UnitPrice`/`Quantity` stay exactly as rung up, an audit trail of what was ordered and then cancelled; only `LineTotal` drops to zero. `BuildFiscalLines` (shared with ORD-11) omits a voided line entirely, so it never reaches the issued fiscal document, while the pre-bill still lists it (with `isVoided: true`) for staff visibility. Found and correctly handled a real edge case along the way: voiding every line on an order leaves `Order.Close()`'s own guard satisfied (it only counts lines, not non-voided ones) but `IFiscalProvider.IssueSimplifiedInvoiceAsync`'s pre-existing `fiscal.no_lines` guard then rejects it — the order correctly stays `Open` in the database, since `CloseOrderAsync` doesn't persist the `Close()` transition until the fiscal document is actually issued. `SplitByItem` (ORD-16)'s by-item preview now correctly gives a voided line's units a zero share too (fixed same day, alongside the equivalent line-discount gap). **Verified live**: void zeroes the line and drops the order total by exactly that amount, a missing/blank reason and a double-void are both rejected, the pre-bill/fiscal-document reconciliation invariant holds with a voided line in the mix, and closing a fully-voided order correctly 400s without corrupting order state |
| ORD-11 | Discounts — line, order, percentage and fixed | ✅ `PUT /orders/{id}/lines/{lineId}/discount` and `PUT /orders/{id}/discount` — both fields null clears an existing discount; a percentage must be in (0, 100], a fixed amount must be positive and not exceed the total it's applied to (rejected, never silently clamped). Composes: an order-level discount applies on top of the already line-discounted subtotal. Gated by real manager authorisation now (IDN-11) — every call requires a Manager-role staff id and their correct PIN. `SplitByItem`'s preview reflects a line-level discount correctly (fixed same day); an order-level discount is still not prorated into it — `SplitEvenly`/`SplitByCover` do, automatically, via `Total`. **Verified live**: line + order discount composing correctly, clearing, every rejection path (bad type, out-of-range percentage, oversized fixed, one-of-the-pair, unknown line, closed order), and — the fiscally load-bearing check — `document.GrossTotal` still equals `order.Total` to the cent through `CloseOrderAsync` with a discount applied, and the pre-bill's VAT breakdown still sums to the same total (`discounts.spec.ts`) |
| ORD-12 | Transfer table | ✅ `POST /orders/{id}/transfer` — moves an open order to a different `Free` table. Order status checked before either table is touched; the old table's `Release()` and the new table's `Occupy()` then commit atomically together in one `FloorDbContext.SaveChangesAsync` |
| ORD-13 | Transfer individual lines between tables | ✅ `POST /orders/{id}/lines/{lineId}/transfer` — moves one line onto a different open order. Pure Ordering, no Floor involvement (unlike ORD-12). No `pos` UI yet — deliberately: picking *another* currently-open order is a real product-design question, same scoping call already made for ORD-22 |
| ORD-14 | Merge orders | ✅ `POST /orders/{id}/merge` — moves every line from a secondary open order into the primary, marks the secondary `Merged` (new terminal status, distinct from `Closed`: no fiscal document was ever issued for it), frees its table directly via `Release()`. No `pos` UI yet, same scoping call as ORD-13 |
| ORD-15 | Split bill evenly (`Money.Allocate`) | ✅ **verified live**: 22.60 EUR → 7.54/7.53/7.53, sums to the cent |
| ORD-16 | Split bill by item | ✅ `POST /orders/{id}/split/by-item` — a preview, like ORD-15, but needs a structured body (which line/quantity goes to which guest) so it's a `POST`. Every line's quantity must be allocated exactly once across the groups; each portion is an exact multiple of the line's own price, so unlike `SplitEvenly` this never needs `Allocate`'s remainder distribution |
| ORD-17 | Split bill by cover | ✅ `GET /orders/{id}/split/by-cover?covers=2&covers=3` — reuses `Money.Allocate(ReadOnlySpan<int>)` directly, the exact "split unevenly by covers" case that overload's own remarks call out. Weights must sum to the order's `CoverCount` |
| ORD-18 | Pre-bill — *documento não fiscal*, correctly labelled | ✅ `GET /orders/{id}/pre-bill` — reuses `FiscalDocumentLine`'s gross→net/VAT math purely as a calculator, never calls `IFiscalProvider`; `PreBillDto` has no document number/ATCUD/QR field at all, plus a `documentKind` discriminator, so it can't be mistaken for an invoice on the wire |
| ORD-19 | Reprint pre-bill (must match the original exactly) | ✅ pre-bill is never persisted or numbered, so requesting it any number of times against an unchanged order reproduces identical figures — verified live (`pre-bill.spec.ts`), not just by construction |
| ORD-20 | Takeaway and counter-sale flow | ✅ `POST /orders/takeaway` — pure Ordering, no Floor at all. `Order.IsTakeaway` is the real signal (`TableId` stays `Guid.Empty`, never treated as magic elsewhere); transferring a takeaway order onto a real table (ORD-12) converts it to dine-in. `pos` gets a "Nova venda ao balcão" entry point on the table picker |
| ORD-21 | Order ownership + concurrent-terminal conflict protocol | ✅ `Order` now carries the same `xmin` optimistic-concurrency token `Table` already had (`OrderConfiguration.cs`, the same deliberately-empty-migration trap `AddTableXminConcurrencyToken` first worked through). Every order-mutating endpoint (add line/combo line, line notes/quantity/discount, void, fire, order discount, transfer-line, transfer-order, merge) now catches a lost `xmin` race via a shared `TrySaveOrderAsync` helper and returns `409 order.concurrently_modified` instead of silently letting the second writer overwrite the first with no trace. `CloseOrderAsync` deliberately does not share that helper — its own save can only lose this race *after* `IFiscalProvider.IssueSimplifiedInvoiceAsync` already issued a real document (hard rule 3: never un-issuable), so a generic "reload and retry" would risk a client naively double-issuing; it gets its own distinct `order.close_conflict_after_fiscal_issuance` code instead, whose message says explicitly not to retry. See [docs/features/order-concurrency.md](../features/order-concurrency.md) for the full design reasoning, including the honest limits (no cross-`DbContext` atomicity fix for `TransferOrderAsync`'s own Floor+Ordering split, deferred to the I5+ outbox work). **Verified live**: `OrderConcurrencyIntegrationTests.cs` proves the underlying `DbUpdateConcurrencyException` deterministically, with two directly-controlled `OrderingDbContext`s against a real Testcontainers Postgres — no timing dependence at all; `order-concurrency.spec.ts` proves the client-visible contract over real HTTP (no lost or duplicated line under real concurrent load, and a well-formed `order.concurrently_modified` body whenever the race does land — see that spec's own remarks on why forcing the race itself isn't reliably possible from outside a real HTTP client, even at 40-way concurrency). Along the way, found and fixed a real bug in the test *infrastructure* itself: two integration-test classes that each migrate through the same process-wide `BRASA_MIGRATIONS_CONNECTION` env var raced when xUnit ran them in parallel, silently pointing one test's migration at the other's Postgres container — fixed with a shared `[Collection]` (`MigrationsEnvVarCollection.cs`) forcing them to run sequentially instead |
| ORD-22 | Order history and search | ✅ `GET /orders` — filter by `status`/`tableId`/`openedFrom`/`openedTo`, capped `take` (1–200, default 50). Returns the lighter `OrderSummaryDto`, not full line detail |

## SYN — Offline sync engine

| ID | Task | Status |
|---|---|---|
| SYN-01 | Client outbox schema (IndexedDB / SQLite) | ⬜ |
| SYN-02 | `POST /sync/push` — idempotent mutation batch | ⬜ |
| SYN-03 | `GET /sync/pull` — cursor-based delta | ⬜ |
| SYN-04 | Opaque server-issued cursor (never a timestamp) | ⬜ |
| SYN-05 | Conflict resolution policy per entity type | ⬜ |
| SYN-06 | Client-side queue with retry and backoff | ⬜ |
| SYN-07 | Connectivity detection and mode switching | ⬜ |
| SYN-08 | LAN-first, cloud-fallback endpoint resolution | ⬜ |
| SYN-09 | Sync status UI — pending count, last sync, errors | ⬜ |
| SYN-10 | Initial full-sync / bootstrap for a new terminal | ⬜ |
| SYN-11 | Compaction of superseded local mutations | ⬜ |
| SYN-12 | Clock-skew tolerance | ⬜ |
| SYN-13 | Chaos tests — kill network mid-order, mid-payment, mid-print | ⬜ |

## AGT — Site Agent

| ID | Task | Status |
|---|---|---|
| AGT-01 | SQLite local store + EF Core model sharing | ⬜ |
| AGT-02 | Pairing flow with the cloud | ⬜ |
| AGT-03 | LAN REST API for terminals | ⬜ |
| AGT-04 | LAN SignalR hub | ⬜ |
| AGT-05 | mDNS / discovery so terminals find the agent | ⬜ |
| AGT-06 | Outbox sync to cloud | ⬜ |
| AGT-07 | Config pull from cloud | ⬜ |
| AGT-08 | Fiscal key custody + at-rest protection | ⬜ |
| AGT-09 | Offline document signing | ⬜ |
| AGT-10 | Series counter with crash-safe persistence | ⬜ |
| AGT-11 | Health endpoint and diagnostics | ⬜ |
| AGT-12 | Installer / deployment (Windows Service or container) | ⬜ |
| AGT-13 | Auto-update with version pinning | ⬜ |
| AGT-14 | Agent-down degraded mode (reserve series) | ⬜ |
| AGT-15 | Remote log shipping | ⬜ |

## KIT — Kitchen printing & KDS

| ID | Task | Status |
|---|---|---|
| KIT-01 | ESC/POS command builder | ⬜ |
| KIT-02 | TCP printer transport | ⬜ |
| KIT-03 | USB / serial printer transport | ⬜ |
| KIT-04 | Printer configuration and station mapping | ⬜ |
| KIT-05 | Ticket layout templates | ⬜ |
| KIT-06 | Station routing rules | ⬜ |
| KIT-07 | Print retry, queue, and failure surfacing on the POS | ⬜ |
| KIT-08 | Printer-down fallback rerouting | ⬜ |
| KIT-09 | Cash drawer kick | ⬜ |
| KIT-10 | KDS — station views | ⬜ |
| KIT-11 | KDS — bump and recall | ⬜ |
| KIT-12 | KDS — prep timers and colour-coded ageing | ⬜ |
| KIT-13 | KDS — course firing controls | ⬜ |
| KIT-14 | Verified hardware shortlist + test matrix | ⬜ |

## FIS — Fiscal engine

> ⚠️ Certification-relevant. Read [../fiscal/README.md](../fiscal/README.md)
> before starting any of these.

| ID | Task | Status |
|---|---|---|
| FIS-01 | `IFiscalProvider` abstraction | ✅ |
| FIS-02 | `Fiscal.Mock` deterministic provider | ✅ every value `MOCK-`-prefixed; VAT correctly derived from gross price |
| FIS-03 | Production guard — mock must never load in Production | ✅ enforced at DI registration, not just documented |
| FIS-04 | `FiscalSeries` entity and lifecycle | ⬜ |
| FIS-05 | AT webservice client — series registration | ⬜ |
| FIS-06 | Série validation code storage | ⬜ |
| FIS-07 | ATCUD generation | ⬜ |
| FIS-08 | RSA key loading and signing | ⬜ |
| FIS-09 | Signature chain — hash of previous document in series | ⬜ |
| FIS-10 | Gapless sequential numbering, crash-safe | ⬜ |
| FIS-11 | QR code payload to AT field spec | ⬜ |
| FIS-12 | QR rendering at ≥30×30mm | ⬜ |
| FIS-13 | Document type `FS` — fatura simplificada | ⬜ |
| FIS-14 | Document type `FT` — fatura with NIF | ⬜ |
| FIS-15 | Document type `FR` — fatura-recibo | ⬜ |
| FIS-16 | Document type `NC` — nota de crédito | ⬜ |
| FIS-17 | Non-fiscal document generation | ⬜ |
| FIS-18 | Immutability enforcement — no update path to issued documents | ⬜ |
| FIS-19 | Append-only fiscal audit log | ⬜ |
| FIS-20 | Chain verification job with alerting | ⬜ |
| FIS-21 | SAF-T (PT) XML export | ⬜ |
| FIS-22 | SAF-T XSD validation in CI | ⬜ |
| FIS-23 | Monthly SAF-T submission job (by the 5th) with retry and paging | ⬜ |
| FIS-24 | Golden-file test suite | ⬜ |

## WEB — Web clients

> React + TypeScript, PWA. `pos`, `kds`, `admin`, `order` each get their own
> app; `web/ui` and `web/sdk` are shared across all four. This epic covers the
> client shells themselves — the domain work they call into (menu, orders,
> floor plan) is tracked in its own epic (CAT, ORD, FLR, ...).

| ID | Task | Status |
|---|---|---|
| WEB-01 | `pos` minimal shell — open table, ring up, split preview, close | ✅ I0: one screen, no auth, no offline. See [status.md](status.md#web-clients) |
| WEB-02 | Shared `web/ui` component library | ✅ `src/web/ui` — no build step, consumed by `pos`/`admin` via a Vite `resolve.alias` + matching TS `paths` (source treated as each app's own, never a `node_modules` package); an npm workspace root (`src/web/package.json`) hoists `react`/`react-i18next` so the shared source's own imports resolve. Started scoped to three genuinely duplicated, low-risk files — `formatMoney` (`lib/money.ts`), the pt/en cookie store (`i18n/languageStorage.ts`), and `LanguageToggle.tsx` — deliberately not `errorReporting.ts`/`i18n.ts` themselves, which stay per-app (Sentry init and translation resources differ enough that sharing them would trade real duplication for a worse abstraction). Grew into a real beta UI refresh in a later session: a design-token layer (`tokens.css` — spacing/radius/elevation/type-scale/motion on top of the existing terracotta identity, 44px touch targets) plus seven components — `Button`, `Card`, `Badge`, `TextField`, `SelectField`, `Modal`/`ModalActions`, `Icon` (inline SVGs, no icon-font dependency). `pos` was wired through fully first (`StaffLogin`, `TablePicker`, `OrderSummary`, `ModifierPicker`, `PreBill`, `TransferTablePicker`, `Receipt`, `ErrorBanner`, `MenuGrid`); `admin` initially got only the CSS tokens loaded with no component wired in, closed in a follow-up session — all four editors (`MenuManager`, `FloorManager`, `StaffManager`, `FeatureFlagManager`) now render through the same `Button`/`Badge`/`TextField`/`SelectField`, with `App.css`'s hand-rolled badge-pill and input/select border styling deleted now that the shared classes supply it. `Card` is the one component neither app has adopted yet — no consumer exists for it. Both apps' duplicate copies deleted; both typecheck, build and lint clean post-refactor. **Verified live**: full E2E suite, 108/109 at the original library's own landing (one `merge-orders.spec.ts` flake, confirmed pre-existing table-pool contention under parallel load, not a regression — passes in isolation); 186/186 after the `admin` wiring follow-up (one `order-course-firing.spec.ts` flake, the same pre-existing QA-02 table-pool shape, clean in isolation) |
| WEB-03 | Shared `web/sdk` — OpenAPI-generated typed client | 🚧 The same underlying deliverable as API-15 (this epic's own row predates that one and named the same package first) — see that row for what's actually built: both request *and* response types now generated and verified against real endpoints, not yet a typed *client* this row's own title promises, and not yet consumed by `pos`'s hand-written `src/api/` (still the I0 placeholder) or `admin`'s |
| WEB-04 | `pos` — Dexie local store and offline-first data layer | ⬜ I2, depends on SYN |
| WEB-05 | `pos` — floor plan / table selection screen | ✅ `TablePicker` — static grid per room, colour-coded by state, tap Free to open / tap Dirty to clear. `pos` itself has no drag-and-drop layout (that's an `admin` editing concern, FLR-03) — this screen only ever renders the positions/shapes `admin` set |
| WEB-06 | `pos` — menu browsing with modifiers and courses | ✅ Modifiers — `ModifierPicker.tsx` (CAT-03/04), required single-select and optional multi-select groups both proven live in `modifiers.spec.ts`. Courses — this row's own note once deferred it to kitchen firing (ORD-07/08/09), now built: `OrderSummary`'s fire-controls bar groups an order's still-pending lines by course (one "Fire {course}" button per course present, plus "Fire all"), and a fired line carries a "Sent" badge — see [course firing](../features/course-firing.md). Browsing the *menu grid itself* by course (as opposed to the seeded `MenuCategory` grouping) was never built and isn't planned — `MenuCategory` is how this codebase organises browsing, `Course` is when a dish is served, a distinction [menu item classification](../features/menu-item-classification.md) draws deliberately |
| WEB-07 | `pos` — staff PIN login screen | ✅ Unblocked by IDN-08/09 (`Staff`) and, indirectly, IDN-11 — proof the same `POST /staff/{id}/verify-pin` mechanism has a second real caller now. Header gets a "Sign in" button opening a staff-picker-then-PIN modal (`StaffLogin.tsx`) — a locked-out staff member appears but is disabled, so there's no way to even attempt a PIN against one through the UI; a wrong PIN shows an inline error and leaves the picker on the PIN screen for retry; success shows "Hi, {name}" plus a sign-out control. Deliberately non-blocking — signing in gates nothing else in `pos`, since building a real login *gate* would touch every existing E2E spec that drives `pos` directly from a fresh `page.goto('/')`, a materially larger and riskier change than "a real, working sign-in exists" — the same "mechanism before the trigger" shape IDN-08/09 itself shipped in. Resolves the site the same "first organization's first site" way `admin` already does. No persistence across a reload (plain React state) — no session/token concept exists yet (IDN-03…05) to persist it into. **Verified live**: `staff-login.spec.ts` — a wrong PIN then a correct one, sign-out returning to the sign-in button, cancelling the modal leaving the unsigned-in state untouched, and a staff member locked out via 5 API-level attempts showing disabled in the picker. Full existing E2E suite re-run clean, confirming the non-blocking design doesn't disturb any spec that drives `pos` without ever signing in |
| WEB-08 | `kds` shell — station view, bump, prep timers | ⬜ I4 |
| WEB-09 | `admin` shell — back-office SPA scaffold | ✅ `src/web/admin` — Vite + React + TS, same tooling as `pos`. "Visão geral"/"Overview" shows real counts from `GET /menu/all`/`GET /floor`, proving the shell is actually wired to the API rather than a static mock. Floor plan and Menu nav entries are both live now (FLR-03, WEB-10); Staff stays a real screen too (WEB-11's staff half). Full pt/en i18n toggle (WEB-13's own ADR 0011 pattern, same `brasa.lang` cookie as `pos` so the preference carries across both apps) — genuine English words throughout, not just a token toggle, since not every staff member reading the English UI is a Portuguese speaker. No auth yet (depends on IDN). **Verified live**: `admin-shell.spec.ts`, `admin-language-toggle.spec.ts` |
| WEB-10 | `admin` — menu and floor-plan editors | ✅ Menu editor and floor-plan editor (FLR-03, including its drag-and-drop canvas) both done. `GET /menu/all` (new — `GET /menu` is guest-facing and filters to visible categories/available items, so it can never be the data source for a screen that needs to *show* a hidden category to turn it back on) backs a screen that toggles category visibility, 86's/reprices/deletes an item, and bulk-imports more via the existing CSV pipeline (CAT-17). No "create category" or "create item" form — neither endpoint exists yet, so CSV import is the only way to add one, same real gap at the API layer, not a UI shortcut. Every mutation refetches rather than reconciling state by hand. **Verified live**: `admin-menu-management.spec.ts`, `floor-table-management.spec.ts`, `floor-drag-drop.spec.ts` |
| WEB-11 | `admin` — staff, roles and reporting screens | 🚧 The staff half only — list staff, add a staff member with an initial PIN, reset a PIN; no roles-and-permissions editor (IDN-10 is the real, larger model beyond the bare Staff/Manager tag this uses) and no reporting screens (RPT epic, unbuilt). No site-selector exists anywhere in either client yet, so this assumes the first organization's first site, the same single-site shortcut every other admin screen already takes |
| WEB-12 | `order` shell — QR self-ordering PWA | ⬜ Post-I8 |
| WEB-13 | i18n — pt default / en toggle, cookie-persisted, mobile storage seam | ✅ i18next, `src/i18n/`. See [ADR 0011](../architecture/decisions/0011-i18n.md). Extended after real-world feedback (Brasa's actual floor/kitchen staff are not all Portuguese speakers): seeded table labels ("Mesa 1") now render as "Table 1" in English via `src/lib/tableLabel.ts`, and a blank takeaway ticket defaults to "Takeaway" instead of leaking the API's own Portuguese default ("Levantamento") — both are generic operational words, not identity-bearing content like a dish name, so they don't fall under the menu-item exception. Closed the last known gap named in the ADR: server `ProblemDetails.title` was always raw English regardless of the toggle — `describeError()` (`App.tsx`) now looks up `error.code.<code>` in `resources/{pt,en}.ts` first (the ~20 codes `pos`'s own API calls can trigger), falling back to the raw message + code only for anything not yet covered. `admin`'s equivalent dictionary doesn't exist yet. **Verified live**: `language-toggle.spec.ts`, `error-localization.spec.ts` |

## PAY — Payments & cash sessions

| ID | Task | Status |
|---|---|---|
| PAY-01 | Payment / tender model | ⬜ |
| PAY-02 | Cash tender with change calculation | ⬜ |
| PAY-03 | Card tender, manually captured from a standalone TPA | ⬜ |
| PAY-04 | Split tender across multiple methods | ⬜ |
| PAY-05 | Partial payment against an open order | ⬜ |
| PAY-06 | Tips — recording and attribution | ⬜ |
| PAY-07 | Refunds via credit note | ⬜ |
| PAY-08 | Cash session — *abertura de caixa* with float | ⬜ |
| PAY-09 | Cash movements — pay-in, pay-out, with reason | ⬜ |
| PAY-10 | Blind cash count | ⬜ |
| PAY-11 | *Fecho de caixa* with variance reporting | ⬜ |
| PAY-12 | Meal-voucher tenders (Ticket Restaurante, Edenred, Sodexo) | ⬜ |
| PAY-13 | MB WAY / Multibanco references via Ifthenpay or Easypay | ⏭ |
| PAY-14 | Integrated TPA (SIBS, Unicre, SumUp, myPOS) | ⏭ |

## RPT — Reporting

| ID | Task | Status |
|---|---|---|
| RPT-01 | Reporting read-model schema, isolated from transactional tables | ⬜ |
| RPT-02 | X report — mid-shift snapshot | ⬜ |
| RPT-03 | Z report — daily close, reconciling to the cent | ⬜ |
| RPT-04 | Sales by item | ⬜ |
| RPT-05 | Sales by category | ⬜ |
| RPT-06 | Sales by hour / daypart | ⬜ |
| RPT-07 | Sales by staff member | ⬜ |
| RPT-08 | VAT summary by rate | ⬜ |
| RPT-09 | Payment method breakdown | ⬜ |
| RPT-10 | Void and discount report | ⬜ |
| RPT-11 | SAF-T download from the back-office | ⬜ |
| RPT-12 | Business-day boundary handling (regional, incl. Azores) | ⬜ |

## QR — QR self-ordering

| ID | Task | Status |
|---|---|---|
| QR-01 | Per-table QR code generation | ⬜ |
| QR-02 | Public menu browsing (no account) | ⬜ |
| QR-03 | Guest cart and order submission | ⬜ |
| QR-04 | Route guest order → cloud → Site Agent | ⬜ |
| QR-05 | Staff approval before firing to kitchen | ⬜ |
| QR-06 | Order status for the guest | ⬜ |
| QR-07 | Allergen and dietary filtering | ⬜ |
| QR-08 | Multi-language guest UI (pt / en / es / fr) | ⬜ |
| QR-09 | Guest payment | ⏭ |

## QA — Automated testing

> See [../development/e2e-testing.md](../development/e2e-testing.md) for the
> harness itself and an honest account of what QA-04/06/07/08 are still
> blocked on.

| ID | Task | Status |
|---|---|---|
| QA-01 | Choose an E2E framework (Playwright vs alternatives) | ✅ Playwright + TypeScript, `src/web/e2e` |
| QA-02 | E2E harness — app + Postgres + seeded tenant | 🚧 `webServer` starts API + `pos` fresh each run; database is the persistent dev instance, not disposable per run — the interim mitigation (more seeded tables in `DevFloorSeeder`) has been applied twice now, 8→16 then 16→32, each time the tests-per-table ratio made back-to-back full runs start occasionally exhausting the pool again; confirmed live at 32: three consecutive full runs, no pause, 185/185 clean each time. Disposable-per-run is still the real fix, not a substitute for it — this mitigation doesn't scale indefinitely |
| QA-03 | Deterministic test data builders | ✅ `tests/support/api.ts` — looks menu items up by name, never by id |
| QA-04 | Fixed-clock control for time-dependent tests | ✅ Revisited once CAT-16/CAT-07-08 actually needed it — both had shipped by waiting out a real wall-clock window instead. `TestableClock : IClock` (`Brasa.Shared`) holds its override in a static `AsyncLocal<DateTimeOffset?>`, not instance or DI-scoped state: `MockFiscalProvider` is deliberately **singleton** (its in-memory sequential numbering must survive across requests), and a singleton can't consume a scoped `IClock` — ASP.NET Core's own DI validation refused to start the app on the first attempt, a real captive-dependency bug caught before it shipped (see the trap in `docs/ai/README.md`). `TestClockMiddleware` reads an optional `X-Brasa-Test-Clock` header (ISO 8601) and fixes the override for that request's own async call chain only — two Playwright workers overriding to different instants against the one shared running API never interfere with each other. Mirrors `DevTenantMiddleware` exactly: registered unconditionally, throws on the very first request if `IsProduction()`, so a misconfigured deployment fails loudly rather than ever letting a client forge the instant a fiscal document is issued at. A missing/unparseable header is silently ignored — this is a testing lever, not public API surface. **Verified live**: `test-clock.spec.ts` proves the mechanism directly against `GET /ping` (which echoes `IClock.UtcNow`) — an override lands exactly on the request that sent it, a concurrent unheadered request still sees the real clock, and an unparseable header degrades to the real clock too. `menu-item-scheduled-price.spec.ts` (CAT-16) is the real first consumer, retrofitted to fast-forward past a scheduled price's effective date instead of `test.slow()`-waiting a real ~2s window — same assertions, now instant and deterministic every run |
| QA-05 | E2E: full service loop, seat → order → fire → pay → close | ✅ `walking-skeleton.spec.ts` — open → order → split → close → receipt, driving the real UI. "Fire" and "pay" aren't built yet (KIT/PAY), so the loop ends at close |
| QA-06 | E2E: offline mode — network killed mid-service | ⬜ blocked on WEB-04/SYN — no offline capability exists to test |
| QA-07 | E2E: split-bill flows | ✅ Even split (`split-preview.spec.ts`, sweeps 1/2/3/5/7 ways), by-item (`split-by-item.spec.ts`, ORD-16) and by-cover (`split-by-cover.spec.ts`, ORD-17) all covered — this row's own status had gone stale, still citing ORD-16/17 as unbuilt blockers after both shipped |
| QA-08 | E2E: multi-terminal concurrency | ✅ Unblocked by ORD-21 the same session — `order-concurrency.spec.ts` fires several genuinely concurrent `POST /orders/{id}/lines` requests at one order (standing in for several terminals) and confirms no lost or duplicated line regardless of whether the race actually lands on a given run; the deterministic proof that a lost race is caught at all lives at the integration-test level instead (`OrderConcurrencyIntegrationTests.cs`), since real HTTP timing turned out not to reliably reproduce the race in this environment even at 40-way concurrency |
| QA-09 | Testcontainers integration-test base fixture | ✅ `TenantIsolationIntegrationTests` — real disposable Postgres per run, migrates for real, creates `brasa_app` the same way `initdb` does. One fixture so far; extract a shared base once a second test needs it |
| QA-10 | Tenant isolation test suite (RLS) | ✅ Automated version of the manual verification that caught ADR 0010: zero rows with no/wrong tenant set, own rows only with the right one, DDL refused — queried as `brasa_app` via raw SQL, deliberately bypassing the EF convenience filter so a silently-disabled RLS policy can't hide behind it |
| QA-11 | Idempotency replay test harness | ✅ `idempotency.spec.ts` — a mutating request replayed 3× with the same `Idempotency-Key` returns byte-identical responses (`Idempotent-Replay: true` on replays 2/3), and the underlying side effect runs exactly once: `POST /orders` replayed never creates a second order for the table, `POST /orders/{id}/close` replayed never issues a second fiscal document (the exact scenario `IdempotencyMiddleware`'s own doc comment calls out — CLAUDE.md hard rule 3). Also proves the negative cases: a *different* key against the same now-occupied table is a genuine 409, not a cache hit, and a missing key 400s (`request.idempotency_key_required`) |
| QA-12 | Fiscal golden-file infrastructure | ⬜ |
| QA-13 | Load test — 50 sites × 5 terminals at service rates | 🚧 Scoped honestly, not as this row's own title literally says: `src/web/e2e/load/{read-load,write-load,run-all}.mjs` (`npm run load`) test read (`GET /menu`/`GET /floor`, autocannon) and write (open→add line→close→clear, a hand-rolled correlated-flow harness bounded by the seeded table pool, 32 as of this writing) paths against the *current* single-tenant, direct-to-cloud architecture. "50 sites" isn't testable — no multi-tenant seed data exists; "reporting queries never touch the transactional path" isn't testable — `Reporting` is empty (I8). See [load-testing.md](../development/load-testing.md). **Verified live**: read path passes the 200ms p95 target at 20 concurrent connections (menu p95 199ms, floor p95 112ms); write path passes at 10 concurrent terminals with zero failures across 2,204 mutating requests (open/addLine/close/clear all p95 ≤132ms). Found a real ~40x latency trap along the way: `Debug`-level Serilog logging (the dev default) inflated `GET /menu` from p50 67ms to p50 2583ms under load — not an app bug, a measurement-methodology trap now written down |
| QA-14 | Accessibility checks on POS and guest UIs | ✅ `pos` only (no guest UI yet — `order`/QR is post-I8) — `accessibility.spec.ts`, axe-core against WCAG 2.0/2.1 A+AA. Found and fixed 5 real color-contrast failures on first run, not suppressed |

## OPS — Infrastructure, CI, observability

| ID | Task | Status |
|---|---|---|
| OPS-01 | Docker Compose — PostgreSQL 18 (ICU pt-PT) + Seq | ✅ Seq was actually crash-looping (`datalust/seq:latest` started requiring an explicit first-run admin password or an opt-out, neither was set) — found live, not in source, since the container reported `Up` briefly before each restart rather than failing outright. Fixed with `SEQ_FIRSTRUN_NOAUTHENTICATION`, appropriate here since Seq binds to localhost only, the same trust level as Postgres's own `devonly`/`brasa` dev credentials. **Verified live**: container stays up, `GET /api` 200s, and a real API request's Serilog output actually lands in Seq (confirmed via its own `/api/events`) |
| OPS-02 | CI — build gate, tests, vulnerability scan | ✅ |
| OPS-03 | CI — documentation link checking | ✅ |
| OPS-04 | Docs site published to GitHub Pages | ✅ |
| OPS-05 | `.gitattributes` line-ending normalisation | ✅ |
| OPS-06 | Issue and PR templates | ✅ |
| OPS-07 | Structured logging with tenant / site / terminal enrichment | ✅ `TenantLoggingMiddleware` pushes `TenantId`/`SiteId`/`TerminalId`/`UserId` onto Serilog's `LogContext` for the rest of the request — every EF Core command, every application log line, carries it, verified live against the console/Seq sink (a real `GET /menu` request's `CommandExecuted` line shows `"TenantId":"…"`; a hidden field is simply absent, not a literal "null"). The one-line HTTP completion summary (`UseSerilogRequestLogging`) now carries the same ids too, closing what was flagged as a known gap — not via the pipeline reordering that gap's own note once assumed was required, but an `EnrichDiagnosticContext` callback that reads `ITenantContext` straight from DI at request-completion time, sidestepping `LogContext`'s push/pop timing (see the trap entry in `docs/ai/README.md`). Verified live: a real `GET /floor` request's completion line shows `"TenantId":"…"` directly on the `Serilog.AspNetCore.RequestLoggingMiddleware` line itself, not just on lines logged during the request. `Site`/`Terminal`/`User` ids are always absent today — nothing populates them before auth (IDN-03…08) exists |
| OPS-08 | OpenTelemetry traces and metrics | ✅ ASP.NET Core, outbound `HttpClient` and Npgsql tracing (the latter via `AddSource("Npgsql")` — Npgsql emits its own spans natively since v7) plus ASP.NET Core/HTTP/.NET runtime metrics, OTLP-exported to Seq (native OTLP ingestion, no separate collector). Config-bound (`Otel:OtlpEndpoint`), empty in the base `appsettings.json` — no real OTLP collector exists for a production deployment yet (OPS-11) — set only in `appsettings.Development.json`, pointed at the local Seq instance. **Verified live**: a real request's HTTP span and its child Npgsql query span both land in Seq with correct `ParentId` linkage and `service.name=brasa-api`, and a periodic metrics export lands too |
| OPS-09 | Health and readiness probes including the database | ✅ `GET /health` (liveness, no dependencies) / `GET /health/ready` (PostgreSQL reachability, `DatabaseHealthCheck`). Verified live: healthy with DB up, `503` with the container stopped, recovers once it's back |
| OPS-10 | Hangfire setup and dashboard | ✅ `Hangfire.AspNetCore`/`Hangfire.PostgreSql`, registered on the migrations (superuser) connection — Hangfire's own storage schema needs DDL rights the unprivileged `brasa_app` runtime role deliberately does not have, the same reasoning `MigrateAsync` already uses. Dashboard at `/hangfire`, mapped only outside Production (no auth story exists at all yet — IDN-03…08 — so there's nothing to gate a dashboard behind today; structurally impossible in Production, the same shape `DevTenantMiddleware`/`TestClockMiddleware` already use). Exists to schedule `DatabaseBackupJob` (OPS-12's own "nothing schedules it yet" gap). **Verified live**: dashboard reachable, both recurring jobs registered and manually triggered end to end — see OPS-12 |

| OPS-11 | Production deployment (Hetzner + Caddy) | ⬜ |
| OPS-12 | Automated database backup and a tested restore drill | ✅ The mechanism, the drill, and now the "automated" half too — `infra/scripts/backup-database.ps1`/`restore-database.ps1`/`restore-drill.ps1`, `pg_dump`/`pg_restore` via `docker exec`+`docker cp` (never a PowerShell text redirect — binary corruption risk), restoring into a scratch database and comparing every table's row count across every schema (Hangfire's own `hangfire.*` schema deliberately excluded from that comparison now — its lock/heartbeat tables mutate continuously and independently of the drill's own timing, a race no other schema had before OPS-10 existed, and losing that transient state in a real restore is not a meaningful disaster-recovery failure the way losing tenant data would be). `DatabaseBackupJob` (`Brasa.Api/Jobs/`, OPS-10) wraps both scripts as two Hangfire recurring jobs — a nightly backup, a weekly full drill — never in Production (both scripts only work against the local dev Docker container). **Verified live**: both jobs manually triggered end to end through the running Hangfire server (not just the underlying scripts run by hand) — a real ~1MB backup file produced, a real drill against the dev database passing clean (23 tables, all row counts matched). Found and fixed a real bug doing it: `backup-database.ps1`'s own `-OutputDir` default resolves via `$PSScriptRoot` inside a `param()` block, which Windows PowerShell 5.1 leaves empty specifically when that script is the direct `-File` target of a redirected, non-interactive process launch (exactly what `Process.Start` does, and reproducible with no Hangfire involved at all) — fixed by having `DatabaseBackupJob` pass `-OutputDir` explicitly rather than relying on the script's own default, without touching the already-verified script. See [backup-and-restore.md](../development/backup-and-restore.md) |
| OPS-13 | Secret management | ⬜ |
| OPS-14 | Error tracking (Sentry) for web clients | ✅ `@sentry/react` in both `pos` and `admin` — `Sentry.init()` runs unconditionally with `dsn` from `VITE_SENTRY_DSN` (empty in every committed `.env.example`, no real Sentry project exists yet — same "ship the seam, no real collector" shape as OPS-08), so it never sends anywhere yet, but automatic `window.onerror`/`unhandledrejection` capture and a `Sentry.ErrorBoundary` around `<App />` both work locally. Neither app had any error boundary before — a render-phase throw took the whole screen to blank white. Fallback UI is a translated "something broke, reload" screen, not a blank one. **Verified live**: `window.__errorReportingInitialized` confirms init completes on a normal load; a dev-only, build-stripped crash trigger (`?__crashTest=1`) proves the boundary catches a real thrown error and shows the fallback, in a real browser, in both apps; the fallback passes the same WCAG A/AA scan (QA-14) as every other screen |
| OPS-15 | Uptime and SAF-T submission alerting | ⬜ |
| OPS-16 | Staging environment | ⬜ |

## DOC — Documentation system

| ID | Task | Status |
|---|---|---|
| DOC-01 | Architecture overview and three-tier design | ✅ |
| DOC-02 | Fiscal reference — ATCUD, signature, QR, SAF-T | ✅ |
| DOC-03 | ADRs 0001–0008 with an index | ✅ |
| DOC-04 | AI session brief + repo map | ✅ |
| DOC-05 | Glossary of Portuguese fiscal and restaurant terms | ✅ |
| DOC-06 | Documentation contract and PR checklist | ✅ |
| DOC-07 | Feature page template | ✅ |
| DOC-08 | API contract for multi-platform clients | ✅ |
| DOC-09 | Backlog and progress tracking (this page) | ✅ |
| DOC-10 | Per-feature pages, written as features land | 🚧 16 pages exist (`docs/features/discounts.md`, `void-a-line.md`, `edit-line-quantity.md`, `course-firing.md`, `menu-bulk-import.md`, `menu-item-photos-and-details.md`, `menu-item-classification.md`, `channel-pricing.md`, `combos.md`, `pre-bill.md`, `floor-plan-editor.md`, `tax-rules.md`, `realtime-floor-updates.md`, `staff-pin-accounts.md`, `manager-authorization.md`, `feature-flags.md`) against dozens of shipped backlog items — the "written as the feature is built" policy hasn't actually been followed until now; these sixteen are a start on backfilling it, not the policy catching up on its own. `floor-plan-editor.md` was extended in place (FLR-05/FLR-07 are the same screen/concept as FLR-03, not a fresh page each); `manager-authorization.md` got its own page rather than being folded into `staff-pin-accounts.md`, since it's a distinct consumer (Ordering's void/discount endpoints) built on top of that mechanism, not the same screen/concept; `feature-flags.md` (IDN-16) is a genuinely new page, written the same commit as the code for the first time this policy has actually held from the start rather than backfilling after the fact; `combos.md` (CAT-10) and `pre-bill.md` (ORD-18/19) are pure backfill — both features shipped sessions earlier with no page at all until now. All sixteen indexed in `docs/features/README.md` and the VitePress sidebar; docs site build verified clean |

## MOB — Mobile apps

> Post-launch. The backend seams (API-01…18, IDN-03…07) are what make these
> require **no backend change**.

| ID | Task | Status |
|---|---|---|
| MOB-01 | Choose the mobile stack | ⬜ |
| MOB-02 | Generate the platform API client from OpenAPI | ⬜ |
| MOB-03 | Staff handheld — ordering | ⬜ |
| MOB-04 | Staff handheld — offline engine | ⬜ |
| MOB-05 | Staff handheld — LAN discovery of the Site Agent | ⬜ |
| MOB-06 | Owner dashboard app | ⬜ |
| MOB-07 | Customer app — menu, ordering, loyalty | ⬜ |
| MOB-08 | Native KDS app | ⬜ |
| MOB-09 | APNs push adapter | ⬜ |
| MOB-10 | FCM push adapter | ⬜ |
| MOB-11 | Deep linking / universal links | ⬜ |
| MOB-12 | App Store and Play Store release pipelines | ⬜ |

## DIF — Differentiators

> Rationale, market analysis and validation status:
> **[differentiation.md](differentiation.md)**. Nothing here should be built
> before it is validated with real restaurants.

| ID | Task | Status |
|---|---|---|
| DIF-01 | Accountant portal — read-only tenant access for the *contabilista* | ⬜ |
| DIF-02 | Automated SAF-T delivery direct to the accountant | ⬜ |
| DIF-03 | Compliance dashboard — what is filed, what is missing, what is due | ⬜ |
| DIF-04 | VAT rate change auto-application via effective dates | ⬜ |
| DIF-05 | Migration importer — Zone Soft | ⬜ |
| DIF-06 | Migration importer — WinRest | ⬜ |
| DIF-07 | Parallel-run mode alongside an incumbent system | ⬜ |
| DIF-08 | Recipe and ingredient costing | ⬜ |
| DIF-09 | Live margin per dish | ⬜ |
| DIF-10 | Supplier price tracking with margin alerts | ⬜ |
| DIF-11 | True margin by channel, including aggregator commission | ⬜ |
| DIF-12 | Menu engineering classification (star / plowhorse / puzzle / dog) | ⬜ |
| DIF-13 | Void and discount anomaly detection (shrinkage) | ⬜ |
| DIF-14 | Demand forecasting for prep quantities | ⬜ |
| DIF-15 | Staff scheduling driven by forecast demand | ⬜ |
| DIF-16 | Waste tracking and reporting | ⬜ |
| DIF-17 | Natural-language reporting for owners | ⬜ |
| DIF-18 | Direct reservations and waitlist (TheFork commission alternative) | ⬜ |
| DIF-19 | Offline-capability proof — a visible, demonstrable guarantee | ⬜ |
| DIF-20 | Order-entry speed as a tracked product metric | ⬜ |
| DIF-21 | Accounting integrations — Primavera, Sage, PHC, Moloni | ⬜ |
