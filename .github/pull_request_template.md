<!--
Documentation is part of the change, not a follow-up. See
docs/development/documentation.md
-->

## What and why

<!-- What changed, and what problem it solves. Link the issue if there is one. -->

## Documentation

- [ ] `docs/product/status.md` updated if this starts or finishes a component
- [ ] Feature doc added or updated under `docs/features/`
- [ ] ADR added if a non-obvious technical choice was made
- [ ] Anything not yet built is marked `> **Status: stub.**` and written in future tense
- [ ] `docs/ai/repo-map.md` updated if files or directories were added or moved

## Checks

- [ ] `dotnet build RestaurantPos.slnx` — clean, zero warnings
- [ ] `dotnet test RestaurantPos.slnx` — green
- [ ] No analyzer rule suppressed without a written reason in `.editorconfig`

## Fiscal impact

<!--
Delete this section if the change touches nothing under RestaurantPos.Fiscal.*,
RestaurantPos.Modules.Fiscal, or the Site Agent's signing path.
-->

- [ ] Golden-file tests re-run and reviewed — a diff here is either a bug or a change requiring AT notification
- [ ] Document series numbering remains gapless
- [ ] No code path can alter an issued document without leaving evidence
- [ ] SAF-T output still validates against the official XSD

> ⚠️ Changes to signing, numbering or document generation are **certification
> relevant**. See [docs/fiscal/certification.md](../docs/fiscal/certification.md).

## Money

<!-- Delete if the change touches no monetary amount. -->

- [ ] All amounts use `Money`, never `double`/`float`/bare `decimal`
- [ ] Any splitting uses `Allocate`, never division
