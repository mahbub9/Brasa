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

## What this does now

**Both request and success-response bodies are typed**, as of the same
session this package first shipped. Every endpoint in `Brasa.Api` returns
a bare `Results.Ok(...)`/`IResult`, which erased the response shape from
`Microsoft.AspNetCore.OpenApi`'s reflection-based generator — it could
only ever infer a *request* body that way, never a response. Every one of
the 68 route mappings across eight endpoint files now carries an explicit
`.Produces<T>(statusCode)` call telling the generator what its success
response actually looks like, so `docs/openapi/v1.json` — and therefore
`src/schema.ts` — describes both halves of the contract now, not just
request bodies. `src/schema.guard.ts` checks a response shape
(`operations['GetFloor']['responses'][200]['content']`) the same way it
already checked request shapes, so a future endpoint change that forgets
`.Produces<T>()` fails to compile here rather than silently reverting.

**Error responses are still undescribed.** `.Produces<T>()` only names a
route's *success* shape; a `400`/`404`/`409`/`403` remains as undescribed
as every response was before this session — enumerating every distinct
error code a given endpoint can return (several handlers have 3–6
different validation paths, each its own code) is a separate, materially
larger undertaking, deliberately not attempted here. Every error response
still shares one real runtime shape (`ProblemDetails` plus a stable
`code` field), just not one this document names yet.

## What this doesn't do yet

No typed fetch client (e.g. `openapi-fetch`) wraps these types yet —
`pos`/`admin` each still hand-write their own `api/client.ts` with their
own `ApiError`/`ProblemDetails` handling, matching this codebase's error
model more precisely than a generic wrapper would today. Wiring either
app onto `@brasa/sdk` is separate, deliberately unstarted work.
