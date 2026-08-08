# Documentation contract

Documentation is maintained **alongside** the code, in the same commit — not
afterwards, and not in a separate pass. The test is simple:

> Could a developer who has never seen this repository pick up the work, make a
> fix, and understand why the code is shaped the way it is?

## Where it lives, and where it is published

The markdown under `docs/` is the single source of truth. It is read in two
places, and both are first-class:

- **In the repository**, browsing on GitHub or in an editor. Directory index
  pages are `README.md` so GitHub renders them automatically when you open a
  folder.
- **On the published site** at `https://mahbub9.github.io/Brasa/`, built with
  VitePress and deployed by [`.github/workflows/pages.yml`](https://github.com/mahbub9/Brasa/blob/main/.github/workflows/pages.yml)
  on every push to `main` that touches `docs/`.

### One-time GitHub Pages setup

The workflow passes `enablement: true`, which provisions Pages on first run. If
that fails with *"Get Pages site failed"*, set it by hand once:

> **Settings → Pages → Build and deployment → Source: _GitHub Actions_**

Do **not** pick "Deploy from a branch" — that runs Jekyll over `docs/`, which
does not rewrite `.md` links and would break navigation across the whole site.

⚠️ **Pages on a private repository requires a paid GitHub plan.** On a free
plan the repository must be public for the site to publish.

The site config rewrites `README.md` to the site's index pages, so one file
serves both audiences. Never duplicate a page to suit one of them.

```powershell
npm install          # once
npm run docs:dev     # local preview with hot reload
npm run docs:build   # production build; fails on dead internal links
```

**A dead internal link fails the build.** That is deliberate: a cross-reference
that stops resolving is a broken document, and it should block publication the
same way a compile error blocks a release. Two consequences worth knowing:

- Links into source or tests use **absolute GitHub URLs**, because the code is
  not part of the site and a relative path would 404 there.
- Adding a page under `docs/` means adding it to the sidebar in
  [`docs/.vitepress/config.mts`](https://github.com/mahbub9/Brasa/blob/main/docs/.vitepress/config.mts),
  or it will be published but unreachable by navigation.

## What to update, and when

| When you… | Also update |
|---|---|
| Finish or start a component | [../product/status.md](../product/status.md) — it is the honest inventory |
| Make a non-obvious technical choice | A new ADR in [../architecture/decisions/](../architecture/decisions/) |
| Change a shared-kernel type | Its doc page, e.g. [../architecture/money.md](../architecture/money.md) |
| Change anything fiscal | [../fiscal/README.md](../fiscal/README.md) **and** the golden files |
| Add a dependency or dev prerequisite | [getting-started.md](getting-started.md) |
| Suppress an analyzer rule | `.editorconfig` inline reason **and** [../architecture/conventions.md](../architecture/conventions.md) |
| Hit a blocker you cannot resolve now | The blockers table in [../product/status.md](../product/status.md) |

## Status markers

A scaffold makes empty things look finished. Any doc describing something not yet
built must say so at the top:

```markdown
> **Status: stub.** ... Everything below is the design, scheduled for Month 3.
```

Never describe planned behaviour in the present tense. A new developer reading
"the Site Agent signs documents offline" will reasonably assume it does.

## ADRs

Write one when a choice was **genuinely contested** — where a competent developer
would plausibly have chosen otherwise, and would waste time re-litigating it
later.

Format: Context → Decision → Consequences (good *and* bad) → **Revisit when**.

That last section is what makes an ADR useful a year later. A decision without
stated trigger conditions becomes dogma.

Do not write an ADR for the obvious. Six exist today; that is roughly the right
density for a foundation.

## Code comments

Comment the **why**. The code says what.

```csharp
// Bad:  increment the counter
// Good: chaining is per-series, never global — two series advance independently
```

Load-bearing constraints — legal, fiscal, offline-safety — get a comment **and** a
link to the relevant doc. Someone will eventually be tempted to "simplify" the
signature chain, and the comment is what stops them.

## Doc style

- Lead with why, then what.
- Link generously between docs and to source files.
- Prefer a short table to a long paragraph.
- Include real code and real values, not placeholders.
- State uncertainty as uncertainty. `docs/fiscal/README.md` flags VAT rates as
  needing accountant confirmation rather than presenting them as settled.
