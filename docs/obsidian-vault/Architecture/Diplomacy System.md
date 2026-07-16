---
tags: [architecture, sim]
---

# Diplomacy System

`Assets/Scripts/Sim/Diplomacy.cs` — a symmetric relations score (0-100) for **every** country
pair, seeded from real geography plus deterministic per-pair noise, drifting slowly back toward
its seeded baseline so player actions matter without permanently rewriting the world with one aid
check. See [[Diplomacy Mechanics]] for the player-facing version.

## Seeding
`DiplomacySystem.Seed(countries)` starts every pair at 50, adds +8 for sharing a continent, +10
for sharing a subregion, then ±15 deterministic noise from a symmetric hash of both countries'
ISO codes (`PairHash` — order-independent, so A→B and B→A always seed identically). Clamped to
[5, 95].

## Storage
`Relations`/`Baselines` are **upper-triangle packed** symmetric matrices (`float[n*(n-1)/2]`),
not full `n×n` arrays — `PackIndex(a,b)` does the row-major triangle math. `GetRelation(a,b)`
returns 100 if `a == b` (a country's relation to itself is undefined/meaningless — see
[[Map Modes and Coloring]] for how the map handles this case).

## Player actions
- `SendAid(from, to, ...)` — costs 0.05% of the donor's GDP (min $0.2B), +12 relation,
  +1.5 `InternationalStanding`
- `SignAgreement(a, b, ...)` — needs relation ≥ `AgreementThreshold` (65), permanent (until game
  reset), adds `AgreementExportBonus` (0.015) to **both** sides' `EconomyState.
  TradeAgreementExportBonus`, +5 relation
- `Denounce(from, to, ...)` — −15 relation, +1.5 `ApprovalRating` (cheap domestic applause),
  −1 `InternationalStanding` (real international cost)

All three are gated by a 90-day per-pair cooldown (`CanAct`/`MarkActed`, keyed by a packed
`(min,max)` pair).

## Ticking
`TickAll()` — every pair's relation drifts 0.1%/day toward its seeded `Baseline`. Only the
player's own rows matter for gameplay today, but the full matrix stays coherent for
[[World AI]]'s AI-vs-AI diplomacy.

## Other
- `HasAgreement(a,b)`, `RankedFor(a, friendliest, topN)` — top-N friendliest/frostiest countries
  from `a`'s perspective, used by the diplomacy self-test and by [[World AI]]'s trade-agreement
  search.

## Consumers
- [[War System]] — `Declare` checks relation ceiling; war outcomes change relations
- [[World AI]] — finds hostile pairs to war and friendly pairs to sign agreements
- [[Map Modes and Coloring]] — colors every country by `GetRelation(player, country)`
- [[UI System]] — `GameUIRoot.DrawDiplomacy`/`DrawBilateralDiplomacy` (aid/agreement/denounce
  buttons) and `DrawDiplomacyOverview`
- [[Save Load]] — the whole `DiplomacySystem` object serializes directly (it's just two float
  arrays, a hash set, and a dictionary)
