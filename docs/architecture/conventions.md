# Code conventions

## Non-negotiables

| Rule | Why |
|---|---|
| **Money is `Money`**, never `double`/`float`/bare `decimal` | Totals must reconcile to the cent — [money.md](money.md) |
| **Never call `DateTime.UtcNow`**; inject `IClock` | Fiscal `SystemEntryDate` must be monotonic per series, and the signature chain is only testable if time is injectable |
| **Never mutate an issued fiscal document** | Corrections are credit notes — [../fiscal/README.md](../fiscal/README.md) |
| **Expected failures are `Result`, not exceptions** | A waiter tapping the wrong button should cost a value, not a stack unwind |
| **Every mutating endpoint takes an idempotency key** | Offline sync retries; also the precondition for a distributed future |
| **Store UTC, render regional** | The Azores are an hour behind the mainland |

## Build policy

`TreatWarningsAsErrors` is on solution-wide, set in `Directory.Build.props`.

This is deliberate. It caught a transitive `SQLitePCLRaw` CVE on the first
restore. If a warning blocks you:

1. Fix it, or
2. Suppress it **in `.editorconfig`, with a written reason**

Do not disable the policy, and do not scatter `#pragma warning disable`.

Current suppressions, all documented inline in `.editorconfig`:

| Rule | Reason |
|---|---|
| `CA1716` | `RestaurantPos.Shared` collides with a VB keyword; we ship no VB-consumable library |
| `CA1848` | `LoggerMessage` delegates are right for hot paths, too heavy a tax repo-wide. Kept as a suggestion |
| `CA1062` | Nullable reference types already express this at compile time |
| `CA1711` | `IIntegrationEventHandler<T>` is an interface, not a delegate |
| `CA1000` | `Result<T>.Success(...)` is the standard factory shape for the result pattern |

Raised **above** default, because they matter here:

| Rule | Reason |
|---|---|
| `CA1305` | Culture must be explicit — fiscal output is machine-read, display output is pt-PT |
| `CA1307` | Explicit `StringComparison` |

## Packages

Central Package Management. Versions live in `Directory.Packages.props`;
`.csproj` files reference by name only.

Adding a package: add `<PackageVersion>` centrally, then `<PackageReference>` in
the project.

## Style

Enforced by `.editorconfig`:

- File-scoped namespaces
- Braces always, even on single-line `if`
- `_camelCase` private fields
- `using` directives outside the namespace, `System.*` first
- 4-space indent in C#, 2 elsewhere

## Naming

- Async methods end in `Async`.
- Integration events are past tense: `OrderClosed`, `FiscalDocumentIssued`.
- Error codes are stable, lower-case, dotted: `order.already_closed`. **Once
  released they must not change** — clients branch on them.

## Comments

Comment the **why**, not the what. The code already says what it does.

XML doc comments are expected on public types and members in `Shared` and on
anything in the fiscal projects. Elsewhere, use judgement — a well-named method
needs no ceremony, but a non-obvious constraint always needs a note.

Where a decision was genuinely contested, write an
[ADR](decisions/) and link to it from the code.
