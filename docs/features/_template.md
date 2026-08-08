# <Feature name>

<!--
Copy this file to docs/features/<kebab-case-name>.md and fill it in.
Delete sections that genuinely do not apply — but do not delete "Offline
behaviour" or "Failure modes" without a reason, because those are the sections
people most often skip and most often need.

Add the page to the index in docs/features/README.md.
-->

> **Status:** ⬜ planned | 🚧 in progress | ✅ built
> **Module:** <Identity | Catalog | Ordering | Fiscal | Payments | Reporting | Site Agent | Web>
> **Roadmap:** Month <n>

## What it is

<!-- Two or three sentences. What a restaurant can do that it could not before. -->

## Why it works this way

<!--
The reasoning a reader cannot recover from the code. Constraints, rejected
alternatives, legal requirements. If the decision was genuinely contested, write
an ADR and link it instead of repeating it here.
-->

## Behaviour

<!-- The normal path, as a numbered sequence. Concrete, with real values. -->

## Offline behaviour

<!--
Required for anything on the service path.

- What happens with no internet?
- What happens if the Site Agent is unreachable?
- What is queued, and what is refused outright?
- What does the user see?

"Not applicable — back-office only, requires connectivity" is a valid answer.
State it explicitly rather than leaving the section empty.
-->

## Failure modes

| What goes wrong | What the system does | What the user sees |
|---|---|---|
| | | |

## Data

<!-- Entities owned, and which module owns them. Note anything copied across a
     module boundary deliberately — e.g. order lines copy item name, price and
     VAT rate at the time of sale, which is correctness, not denormalisation. -->

## API

<!-- Endpoints, with method and route. Note the idempotency key on mutations. -->

| Method | Route | Purpose |
|---|---|---|
| | | |

## Integration events

<!-- Published and consumed. Remember handlers must be idempotent. -->

| Event | Direction | Purpose |
|---|---|---|
| | | |

## Fiscal impact

<!--
Delete if the feature touches nothing fiscal. Otherwise: does it issue,
reference, or report on a fiscal document? Is it certification relevant?
See ../fiscal/README.md
-->

## Permissions

<!-- Which roles can do this. Anything requiring a manager PIN? -->

## Testing

<!-- What must be tested, and to which tier — see ../development/testing.md.
     Name the specific invariant each test protects. -->

## Open questions

<!-- Record them here rather than losing them. Better a known gap than a
     forgotten one. -->
