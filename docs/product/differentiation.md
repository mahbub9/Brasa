# Differentiation

> **Status: hypotheses, not conclusions.** Nothing here has been validated with
> a real Portuguese restaurateur or accountant. Treat every item as something to
> test cheaply before building. Tracked as epic **DIF** in
> [backlog.md](backlog.md).

## The market reality

The Portuguese restaurant POS market is not empty, and it is not won on feature
count.

| Incumbent | Shape | Weakness |
|---|---|---|
| **WinRest** | Windows, on-premise, long-established | Dated UX; mobile is an afterthought; per-terminal licensing |
| **Zone Soft (ZSRest)** | Windows, feature-rich, strong in larger venues | Complex; needs a reseller to configure; heavy |
| **Cegid Vendus** | Cloud, certified, retail-first | Restaurant workflows are shallower than retail; degrades badly offline |
| **XD Software / Sage / PHC** | ERP-adjacent | Restaurant is one module among many; enterprise pricing |
| **Bitte** | Modern cloud entrant | Newer, smaller footprint — the closest competitor in spirit |
| **Square / Lightspeed** | International cloud | Weak Portuguese fiscal depth; ignore local workflows entirely |

**A restaurant with a working POS does not switch for a nicer interface.** They
switch when something breaks, when their accountant tells them to, when their
hardware dies, or when opening a second site. Positioning has to speak to those
moments, not to a feature comparison table.

## Where a new entrant can actually win

Three moats, in descending order of how hard they are to copy.

---

### 1. Technical moat — offline invoicing that is legally valid

Every competitor claims "works offline". Almost all of them mean *order-taking*
works offline, and payment or invoicing does not.

Brasa can issue **fully compliant fiscal documents with no internet at all**,
because the Site Agent owns its site's document series, registered independently
with AT, and signs locally. See
[ADR 0003](../architecture/decisions/0003-site-agent.md).

This is hard to copy — not because the code is difficult, but because it demands
a specific architectural commitment made early. A cloud-only competitor cannot
retrofit it without redesigning their fiscal core.

**Why it matters commercially:** internet in Portuguese restaurants is
unreliable, especially outside Lisbon and Porto — rural Alentejo, Algarve in
August with the network saturated, basement dining rooms. Every restaurateur has
a story about the POS dying on a Saturday night. That story is the sales pitch.

> **Make it demonstrable.** DIF-19: an offline mode a prospect can trigger
> themselves during a demo — pull the cable, keep serving, take payment, print a
> valid invoice. A claim anyone can make; a demo is proof.

---

### 2. Distribution moat — the *contabilista* is the hidden buyer

This may be the most underrated lever in the Portuguese market.

Most small restaurants do not choose accounting-adjacent software alone. Their
**accountant recommends it, and sometimes mandates it** — because the accountant
is the one who suffers when SAF-T files are late, malformed, or chased by email
every month.

An accountant with 40 restaurant clients is a **distribution channel**, not a
user. Serve them and they will bring clients.

| ID | What that means concretely |
|---|---|
| DIF-01 | A read-only portal so the accountant sees their clients' data without asking |
| DIF-02 | SAF-T delivered to them automatically — no more monthly chasing |
| DIF-03 | A compliance dashboard: what is filed, what is missing, what is due |
| DIF-21 | Clean export into Primavera, Sage, PHC, Moloni |

Nobody in this market treats the accountant as a first-class user. Doing so is
cheap to build and hard for an incumbent to prioritise, because their product
org is oriented around the restaurant.

**Validate first:** talk to five Portuguese accountants with restaurant clients
before building any of it. Ask what their month-end actually looks like.

---

### 3. Product moat — margin intelligence

Restaurants run on **3–5% net margin**. Every POS reports revenue. Very few
connect sales to *cost*, and almost none do it live.

| ID | Feature | Why it lands |
|---|---|---|
| DIF-08/09 | Recipe costing → live margin per dish | "Your *bacalhau à Brás* lost €0.40 a plate this week" is actionable in a way a sales report never is |
| DIF-10 | Supplier price tracking with alerts | Ingredient prices move constantly; menu prices do not |
| DIF-11 | **True margin by channel** | Uber Eats / Glovo / Bolt take 25–30%. Many owners genuinely do not know which dishes lose money on delivery |
| DIF-12 | Menu engineering classification | Star / plowhorse / puzzle / dog, computed automatically rather than in a consultant's spreadsheet |
| DIF-13 | Void & discount anomaly detection | Shrinkage runs 3–5% of revenue and is mostly staff-driven. Unusual void patterns are detectable and nobody surfaces them |

DIF-11 and DIF-13 are the two I would test first. Both tell an owner something
they do not know and cannot easily find out, and both are computed from data the
POS already has.

---

## Portuguese-specific fit

International platforms lose here because they design for the US and localise
afterwards. These are small individually and compound into "this was built for
us":

- **Couvert** — bread and olives charged only if consumed (CAT-12). A genuine
  daily workflow problem that US-designed POS handle badly.
- **Prato do dia / menu do dia** — central to Portuguese lunch trade, usually
  bolted on elsewhere (CAT-10, CAT-11).
- **Automatic IVA split** — alcohol at 23%, food at 13%, on the same table, on
  the same invoice (CAT-09).
- **Meal vouchers** — Ticket Restaurante, Edenred, Sodexo are widespread and
  painful to reconcile (PAY-12).
- **Regional VAT** — Madeira and the Azores, with the Azores an hour behind,
  which moves the daily close.
- **Conta separada** — splitting is a cultural default, not an edge case.

---

## Reducing switching cost

The single biggest barrier is not price. It is **ten years of menu, customer and
history data**, and the fear of a broken Saturday.

| ID | Approach |
|---|---|
| DIF-05/06 | Importers for Zone Soft and WinRest — unglamorous, high value, nobody does it well |
| DIF-07 | **Parallel-run mode** — real orders flow through Brasa while fiscal documents are still issued by the incumbent. Zero-risk trial, and legal before our certificate arrives |

Parallel-run doubles as the answer to "how do we pilot before certification"
(see [ADR 0002](../architecture/decisions/0002-own-fiscal-engine.md)). One
mechanism, two jobs.

---

## What not to do

- **Do not compete on feature count.** Zone Soft has a fifteen-year head start.
  Losing that comparison is guaranteed; avoiding it is free.
- **Do not ship AI theatre.** A chatbot on a POS is a demo, not a product.
  Forecasting (DIF-14), anomaly detection (DIF-13) and natural-language
  reporting (DIF-17) earn their place only if they change a decision.
- **Do not build the accountant portal before talking to accountants.** The
  entire thesis rests on an assumption about their behaviour.
- **Do not promise integrated card payments early.** SIBS certification is its
  own project. Manual card capture is fine at MVP.

## The recommended wedge

A startup needs one sharp claim, not a list.

> **"The POS that keeps taking payments when the internet dies — and the one
> your accountant will thank you for."**

Two claims, aimed at the two people who actually decide: the owner who has been
burned by an outage, and the accountant who influences the purchase.

Everything in DIF is downstream of one of those two. Margin intelligence is the
expansion story once they are already customers — it is what raises retention
and price, but it does not win the first conversation.

## Validation plan

Before building anything in DIF:

| # | Test | Cost |
|---|---|---|
| 1 | Interview 10 restaurant owners: what made you last consider switching? | Days |
| 2 | Interview 5 accountants with restaurant clients: describe your month-end | Days |
| 3 | Ask both: how often does your POS lose connectivity, and what happens? | Same calls |
| 4 | Show a paper prototype of channel-margin (DIF-11); measure the reaction | Hours |
| 5 | Confirm the accountant-as-influencer thesis, or kill it | From #2 |

If #5 comes back negative, the distribution moat disappears and the positioning
above should be rewritten around the offline claim alone. **Better to find that
out in a week of conversations than in six months of building.**
