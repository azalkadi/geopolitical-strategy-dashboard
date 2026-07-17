---
tags: [vision, game-design, economy]
---

# Economic Sectors and Companies

Part of [[Vision Overview|the vision]]. Today `EconomyState` is one aggregate GDP number plus
three spending levers (education/healthcare/infrastructure) — no industries, no companies, no
per-sector visibility. The ask is to make the economy legible as real industries with real
manpower and real ownership, not one number that goes up or down.

## Sectors
The economy should break down into real industries — oil, agriculture (including sub-sectors
like fruit specifically named), manufacturing, tech, services, and whatever else a serious
economic model needs. Each sector should be inspectable on its own: the player should be able
to see every company operating in a given sector (e.g. "show me every company in the oil
sector").

## Companies, and public/private/mixed ownership
Individual companies exist within a sector, and the player can set each one's ownership model —
fully public (state-owned), fully private, or mixed. This is a real, consequential lever (state
ownership vs. privatization is a real policy axis for [[Government, Legislature and Real
Taxes|bills]] to touch), not flavor text.

## Manpower allocation
The player should be able to directly edit how much manpower/labor goes into healthcare,
education, research, and (implicitly) other sectors — this is distinct from the current
`SpendHealthcare`/`SpendEducation`/`SpendInfrastructure` budget-percentage levers, which are
money, not people. Manpower is a separate real resource: how many workers a sector actually has,
which should itself interact with unemployment and sector output.

## Where this plugs into existing code
- `Sim/Economy.cs` currently has zero sector breakdown — this is a genuinely new data model,
  not an extension of an existing one. A `Sector` class (name, output, employment, ownership mix)
  per country, likely seeded from real approximate sector-share data where available (same
  research-effort pattern as [[Curated Datasets]]).
- A `Company` class nested under sectors, with an `Ownership` enum (Public/Private/Mixed).
- UI: a new sub-view (likely under Trade or a new "Industry" ministry sub-tab) to browse
  sectors → companies, and to reassign manpower.
- This is the most build-heavy of the six pillars — no existing system to extend, has to be
  designed from scratch. Good candidate for its own dedicated session rather than a slice of a
  bigger one.
