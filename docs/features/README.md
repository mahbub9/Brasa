# Feature documentation

One page per feature, written **as the feature is built** — not afterwards.

A feature page answers what the code cannot: what the feature is *for*, what it
does when things go wrong, and which rules constrain it. Signatures and types
live in the source; **behaviour, edge cases and intent live here.**

> **Looking for what to build next, or whether something is done?**
> That is [../product/backlog.md](../product/backlog.md) — 278 tasks with stable
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

*No feature pages yet — no features are built. Pages appear here as epics land.*

| Page | Covers | Module |
|---|---|---|
| — | — | — |
