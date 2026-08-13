# @brasa/sdk

TypeScript types generated from [`docs/openapi/v1.json`](../../../docs/openapi/v1.json)
(API-15) via [`openapi-typescript`](https://openapi-ts.dev/). Not yet consumed
by `pos`/`admin` — see "What this doesn't do yet" below — the same
"mechanism before the trigger" shape most of this codebase's other
cross-cutting pieces have shipped in.

## Regenerating

```sh
npm run generate
```

Regenerates `src/schema.ts` from the **committed** OpenAPI document, not a
live API — same manual-regeneration discipline
[`docs/openapi/README.md`](../../../docs/openapi/README.md) already
documents for that file itself. If an endpoint changed, regenerate
`docs/openapi/v1.json` from the running API **first**, in the same commit
as the endpoint change; a stale `v1.json` produces a schema that silently
no longer matches the real API. `src/schema.guard.ts` is a permanent,
cheap type-level check against exactly that: it fails to compile if a
request field this codebase actually depends on stops existing or changes
shape.

## What this doesn't do yet

**Response bodies are not typed.** Every endpoint in `Brasa.Api` returns
a bare `Results.Ok(...)`/`IResult` rather than a `TypedResults`-based
concrete return type or explicit `.Produces<T>()` metadata, so
`Microsoft.AspNetCore.OpenApi`'s reflection-based generator has no way to
know what shape a `200` actually carries — every response in
`docs/openapi/v1.json` today is `{"description": "OK"}` with no `content`
key at all. This was already true the moment API-13 first committed that
file; it went unnoticed until API-15 needed response shapes to be useful
for anything beyond request bodies. Request bodies, path parameters and
query parameters — the half of an endpoint contract a generator *can*
infer from a strongly-typed C# parameter — are correctly generated and
verified against real endpoints (`src/schema.guard.ts`).

Fixing this for real means adding `.Produces<T>()` (or switching to
`TypedResults`) across roughly 68 route mappings spanning eight endpoint
files — a bounded, mechanical, but broad change deserving its own
dedicated pass, not a side effect of shipping the generator. Until then,
this package types what a client *sends*, not what it gets back — a
real, honest limitation, not a bug in the generator itself.

## What this doesn't do yet, either

No typed fetch client (e.g. `openapi-fetch`) wraps these types yet —
`pos`/`admin` each still hand-write their own `api/client.ts` with their
own `ApiError`/`ProblemDetails` handling, matching this codebase's error
model more precisely than a generic wrapper would today. Wiring either
app onto `@brasa/sdk` — for request bodies now, response bodies once the
gap above closes — is separate, deliberately unstarted work.
