---
tags: [vision, game-design, politics]
---

# Supranational Unions

Part of [[Vision Overview|the vision]]. Real geopolitics happens at more than one level —
countries belong to blocs that have their own governance layered above individual national
politics.

> [!success] Status: 🟡 First slice built (`Sim/UnionSystem`). The 14 curated blocs in
> `WorldAlignments` are now **typed by their real function** and confer real per-function passive
> effects on members, baked in at world seed:
> - **Economic** (EU, GCC, ASEAN, Mercosur, Nordic, Benelux, CARICOM) → a single-market export &
>   growth dividend scaled by how big the bloc is in-game (a member of the 27-strong EU gains far
>   more than one of a 3-member bloc). Folds into `EconomyState.TradeAgreementExportBonus`.
> - **Military** (NATO, CSTO, AES) → a collective-security standing + readiness bonus
>   (`NationalState.AllianceStandingBonus`/`AllianceReadinessBonus`), **plus mutual defence**:
>   declaring war on a member turns that member's *whole* alliance against the aggressor
>   (`WarSystem.Declare` drops every ally's relations with the aggressor and adds an extra global
>   standing hit) — so invading a NATO/CSTO member is a categorically different proposition than
>   invading a non-aligned country.
> - **Intelligence** (Five Eyes) → a shared-intel standing bonus.
> - **Political** (Visegrad, Turkic States, Baltic Assembly) → alignment only (the relation
>   floor), no economic/military bonus.
>
> A UNIONS & BLOCS card on the Diplomacy tab lists any country's memberships and each one's
> function. Verified live: Germany reads 2 memberships (EU + NATO), export bonus 0.060 from the
> EU, standing +4 / readiness +5 from NATO. **Still open:** union-level legislation (a bloc
> passing law binding on members), evolving membership (join/leave), and commodity cartels (OPEC
> production quotas) as their own distinct type. This is the *effects* slice; the *governance*
> layer below is the larger remaining piece.

> **Naming note:** the player named EU/GCC/UN as examples while describing this, not the
> complete list — they explicitly asked for the fuller real category and its real distinct
> *functions* to be captured, not just the three named blocs. Below is that fuller pass. A real
> multilateral organization is not one thing — a military alliance, an economic union, and a
> commodity cartel all "connect countries," but they do fundamentally different jobs and should
> be modeled differently, not collapsed into one generic "Union" type.

## By function, not just by name

**Political + economic unions (deep integration, real sovereignty pooling)**
- **EU** — the deepest real example: member states keep [[Government, Legislature and Real Taxes|their own legislature]], but some law is reviewed/decided at the union level (trade
  regulation, competition law, a shared currency for most members).
- **GCC** — explicitly on a similar trajectory per the player's own framing: currently closer to
  a cooperation council, actively trying to deepen toward the EU model. The simulation should
  let this evolve rather than treating it as static.

**Military alliances (collective defense, not economic governance)**
- **NATO** — the obvious real example: a mutual-defense trigger (an attack on one member is
  treated as an attack on all), not a trade or lawmaking body. A country's [[Conflicts, Terrorism and Military Realism|war]] calculus should look completely different if an ally with a mutual-
  defense obligation could be pulled in.
- Smaller/regional mutual-defense pacts exist too (e.g. the GCC's own Peninsula Shield Force is a
  security arm distinct from its economic-cooperation side) — a country can belong to a
  political/economic union AND a separate military alliance with an overlapping or different
  membership list.

**Regional cooperation blocs (real, but looser than the EU — consensus-based, limited
sovereignty pooling)**
- **ASEAN** (Southeast Asia), **African Union** (continent-wide, includes a peacekeeping
  mandate), **Arab League** (regional political cooperation among Arab states), **Mercosur**
  (South American trade bloc). Different regions, same rough tier of integration: real,
  consequential, but not EU-deep.

**Resource/commodity cartels (coordinate production or pricing — NOT a governance body at all)**
- **OPEC / OPEC+** — member states coordinate oil output and pricing; this has real, direct
  [[Economic Sectors and Companies|economic]] consequences (a production-cut decision should
  move [[Economy System|GDP/Treasury]] for every member and every oil-importing country) but has
  nothing to do with law, elections, or collective defense. Structurally the odd one out, and
  worth keeping that way rather than forcing it into the same mechanic as the EU.

**Global legitimacy / security body (near-universal membership, limited direct enforcement)**
- **UN** — the broadest layer; largely a backdrop/legitimacy mechanic (resolutions, Security
  Council-style sanctions authority, peacekeeping mandates) rather than something the player
  directly governs day to day.

**Informal economic coordination blocs (real influence, no formal treaty union)**
- **BRICS** and similar groupings — economic/diplomatic coordination among major powers without
  a shared legislature or currency. Should move [[Diplomacy System|bilateral relations]] and
  trade posture between members without pretending it's a governance layer.

**Legacy/cultural associations (minimal binding function, real but soft)**
- **Commonwealth of Nations** and similar historical associations — mostly a soft-power/cultural
  relations layer, shouldn't cost the player real mechanical weight to belong to, but should be
  modeled as *something* rather than nothing, since it's real.

## Federal internal structure — the reverse direction
*Within* one country, individual states/provinces can have their own distinct laws, not just one
national policy — the USA is the named example, but the mechanic (a federation where sub-units
have real legislative autonomy) should be general enough to also cover other real federations
(Germany's Länder, India's states, Nigeria's states, the UAE as a federation of emirates with
notably different internal rules emirate-to-emirate).

## The point
Playing a country inside a union should visibly change the UI/options available — an EU member
gets a union tab a non-member doesn't; a NATO member's war calculus accounts for allies; an OPEC+
member's economy panel shows a production-quota lever a non-member doesn't have; a federal
country gets a states sub-view a unitary one doesn't. This is the same "per-country-type UI"
principle as [[Government, Legislature and Real Taxes|government type]] — not one generic panel
set for all 258 countries, and not one generic "Union" type pretending every bloc does the same
job.

## Where this plugs into existing code
- No existing analog — this is new. Likely one `Union` base concept with a `UnionFunction` axis
  (PoliticalEconomic / MilitaryAlliance / RegionalBloc / ResourceCartel / GlobalBody /
  InformalCoordination / CulturalAssociation) rather than a single flat type, so the mechanics
  can actually differ — a military alliance needs to hook into [[War System]], a cartel needs to
  hook into [[Economy System]], neither needs the other's machinery.
- Sits above [[Diplomacy System]]'s bilateral relations, since any of these is really a standing
  multilateral structure — countries can belong to several (an EU member is very likely also
  NATO, and could separately be in the UN and the Commonwealth).
- UI: conditionally-shown tabs in `GameUIRoot.cs` based on the selected/played country's actual
  memberships — ties into [[Government, Legislature and Real Taxes]]'s regime-type work, since
  "which unions can this country even join" is itself gated by real facts (e.g. the EU's own
  membership criteria touch on government type and rule-of-law standards).
- Reasonable to sequence AFTER [[Government, Legislature and Real Taxes]] ships for a single
  country, since unions are really "the same legislature mechanic, one level up (or down, for
  federal states)" for the political/economic type, but the military-alliance and cartel types
  can be built independently against [[War System]]/[[Economy System]] without waiting on that.
