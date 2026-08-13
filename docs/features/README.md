# Feature documentation

One page per feature, written **as the feature is built** — not afterwards.

A feature page answers what the code cannot: what the feature is *for*, what it
does when things go wrong, and which rules constrain it. Signatures and types
live in the source; **behaviour, edge cases and intent live here.**

> **Looking for what to build next, or whether something is done?**
> That is [../product/backlog.md](../product/backlog.md) — 292 tasks with stable
> IDs and statuses. This page is only an index of *written documentation*, not a
> tracker. Keeping them separate stops two lists disagreeing about reality.

## Writing a page

1. Copy [`_template.md`](https://github.com/mahbub9/Brasa/blob/main/docs/features/_template.md).
2. Save as `docs/features/<kebab-case-name>.md`.
3. Add it to the index below **and** to the sidebar in
   [`docs/.vitepress/config.mts`](https://github.com/mahbub9/Brasa/blob/main/docs/.vitepress/config.mts),
   or it will publish but be unreachable.
4. Reference the backlog IDs it covers, e.g. *Covers ORD-15, ORD-16, ORD-17.*

The template keeps **Offline behaviour** and **Failure modes** mandatory. Those
are the sections most often skipped and most often needed later.

## Index

Most shipped features don't have a page yet — this index is being backfilled
against [status.md](../product/status.md)'s much larger inventory, not
written fresh alongside every commit despite the policy above. Treat a
missing page as a documentation gap, not evidence the feature isn't built.

| Page | Covers | Module |
|---|---|---|
| [Discounts](discounts.md) | ORD-11 | Ordering |
| [Void a line](void-a-line.md) | ORD-10 | Ordering |
| [Edit a line's quantity](edit-line-quantity.md) | ORD-03 | Ordering |
| [Course firing](course-firing.md) | ORD-07, ORD-08, ORD-09 | Ordering |
| [Menu bulk import (CSV / Excel)](menu-bulk-import.md) | CAT-17 | Catalog |
| [Menu item photos, description and allergens](menu-item-photos-and-details.md) | CAT-02 | Catalog |
| [Menu item classification: course and station](menu-item-classification.md) | CAT-14, CAT-15 | Catalog |
| [Channel pricing — dine-in vs takeaway](channel-pricing.md) | CAT-06 | Catalog |
| [Floor-plan editor: table/room CRUD, seating groups, multiple floors, section assignment](floor-plan-editor.md) | FLR-03, FLR-05, FLR-06, FLR-07 | Floor |
| [Effective-dated tax rules](tax-rules.md) | CAT-07, CAT-08 | Catalog |
| [Realtime floor updates](realtime-floor-updates.md) | API-16, API-17 | Web |
| [Staff PIN accounts](staff-pin-accounts.md) | IDN-08, IDN-09, WEB-07 | Identity |
| [Manager authorisation for voids and discounts](manager-authorization.md) | IDN-11 | Identity + Ordering |
| [Feature flags](feature-flags.md) | IDN-16 | Identity |
