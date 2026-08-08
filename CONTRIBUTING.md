# Contributing

## First time here?

- **Human?** Start at [docs/development/getting-started.md](docs/development/getting-started.md).
- **AI session?** Start at [docs/ai/README.md](docs/ai/README.md) — it is written
  to bring you fully up to speed without reading the whole repository.

## The loop

```powershell
dotnet build RestaurantPos.slnx      # must be clean — zero warnings
dotnet test  RestaurantPos.slnx      # must be green
git commit                            # small, focused, with the why in the body
```

Commits are **small and local**. Each one should build and pass tests on its own.

## Commit messages

Conventional-commit prefix, then a body explaining **why**.

```
feat(shared): add Money with allocation-based bill splitting

Splitting uses Allocate, never division. Dividing 10.00 EUR three ways gives
3.33 each and loses a cent, which breaks Z-report reconciliation against the
fiscal documents.
```

Prefixes: `feat`, `fix`, `docs`, `chore`, `refactor`, `test`, `perf`.
Scopes: `shared`, `api`, `agent`, `fiscal`, `identity`, `catalog`, `ordering`,
`payments`, `reporting`, `pos`, `kds`, `admin`, `infra`.

The subject line says what. **The body says why** — that is the part that is
still useful in a year.

## Documentation is part of the change

Not a follow-up task. The full contract is in
[docs/development/documentation.md](docs/development/documentation.md); the
short version:

| You changed | Also update |
|---|---|
| Started or finished a component | [docs/product/status.md](docs/product/status.md) |
| Added a feature | A page under [docs/features/](docs/features/) |
| Made a contested technical choice | An [ADR](docs/architecture/decisions/) |
| Added or moved files | [docs/ai/repo-map.md](docs/ai/repo-map.md) |
| Anything fiscal | [docs/fiscal/README.md](docs/fiscal/README.md) **and** the golden files |
| Added a dependency or prerequisite | [docs/development/getting-started.md](docs/development/getting-started.md) |

CI checks that every relative markdown link resolves, and warns when source
changes without a status update.

## Rules that are not negotiable

1. **Money is `Money`** — never `double`, `float`, or bare `decimal`. Split with
   `Allocate`, never division.
2. **Never call `DateTime.UtcNow`** — inject `IClock`.
3. **Never mutate an issued fiscal document** — corrections are credit notes. A
   code path that can invisibly alter fiscal data is a *certification failure*,
   not a code smell.
4. **Modules never reference or query each other** — use integration events.
5. **Expected failures return `Result`**, not exceptions.
6. **Do not weaken the build policy** — suppress in `.editorconfig` with a
   written reason, or fix the warning.

Full detail: [docs/architecture/conventions.md](docs/architecture/conventions.md).

## Fiscal changes

Read [docs/fiscal/README.md](docs/fiscal/README.md) before touching anything
under `RestaurantPos.Fiscal.*` or `RestaurantPos.Modules.Fiscal`.

Fiscal behaviour changes must cite the actual legal instrument — a Portaria,
Decreto-Lei, Ofício Circulado — not a blog summary. Use the
[fiscal change issue template](.github/ISSUE_TEMPLATE/fiscal-change.md).

## Secrets

Never commit a key, certificate, or credential. The fiscal RSA private key is the
most sensitive artefact in this system's blast radius — see
[docs/fiscal/key-management.md](docs/fiscal/key-management.md).

`.gitignore` blocks `*.pem`, `*.key`, `*.pfx`, `*.p12` and `.env`. Test fixtures
must use throwaway keys that were never registered with AT.
