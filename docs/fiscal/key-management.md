# Fiscal signing key management

> **Status: design only.** No key has been generated. Month 3–4.

## What is at stake

The fiscal RSA private key signs legally binding documents under a certificate
issued to us by AT. It is the **single most sensitive artefact in this system's
blast radius**.

Compromise means someone can forge documents that appear to originate from our
certified software, on behalf of our customers. That is a company-ending event,
not an incident.

## Rules

1. **Never in git.** `.gitignore` blocks `*.pem`, `*.key`, `*.pfx`, `*.p12`. The
   only exceptions are explicitly-scoped test fixtures under `Fixtures/` or
   `testdata/`, which must use **throwaway keys that were never registered with
   AT**.
2. **Never in a container image**, never in `appsettings.json`, never in an
   environment variable that gets logged.
3. **Never leaves the Site Agent** in usable form. The agent signs; it does not
   export.
4. **Never logged.** Not the key, not a fingerprint that could aid brute force.
5. Test fixtures use a **separate key pair** from production. A golden-file test
   must never be signable with the real key.

## Custody

The key lives with the **Site Agent**, because the agent is what signs and it must
sign while offline. See
[../architecture/decisions/0003-site-agent.md](../architecture/decisions/0003-site-agent.md).

Open questions, to resolve before Month 4:

- **At-rest protection on the agent.** DPAPI on Windows, or a passphrase supplied
  at pairing and held only in memory? A passphrase means the agent cannot restart
  unattended, which is a real operational cost in a restaurant.
- **One key for all sites, or one per site?** One key per producer is the simpler
  reading of the AT model, and the public half is registered once. Per-site keys
  would limit blast radius but multiply registration overhead. **Confirm the
  correct model with AT before building.**
- **Rotation.** What happens to already-signed chains when a key rotates? The
  chain must remain verifiable, which likely means a series boundary at rotation.
- **Recovery.** If the agent hardware dies, how is the key restored without ever
  materialising it somewhere less protected?

## Provisioning

Intended flow, not yet implemented:

1. Generate the key pair in a controlled environment.
2. Register the **public** half with AT.
3. Deliver the private half to the agent during pairing, over a channel that never
   persists it in transit.
4. Store it protected at rest, decryptable only by the agent process.

## Audit

Every signing operation is recorded in the append-only audit log: which series,
which document number, which key fingerprint, when. The log is what lets us prove
the chain is intact — and detect it if it ever is not.

## If a key is suspected compromised

1. Stop issuing against every series that key signs.
2. Notify AT. This is a regulatory obligation, not a discretionary call.
3. Preserve the existing chains — they must stay verifiable for the **ten years**
   Portuguese law requires fiscal records be retained.
4. Provision a new key and new series; do not reuse numbering.

Write the real runbook before the first customer goes live.
