---
tags: [vision, game-design, economy]
---

# Economic Sectors and Companies

Part of [[Vision Overview|the vision]]. Today `EconomyState` is one aggregate GDP number plus
three spending levers (education/healthcare/infrastructure) — no industries, no companies, no
per-sector visibility. The ask is to make the economy legible as real industries with real
manpower and real ownership, not one number that goes up or down.

> **Status (2026-07-17): first slice shipped.** `Sim/Companies.cs` — real named companies
> (~13 curated countries, `CountryProfiles.Companies`) in real sectors, with player-changeable
> ownership routed through the [[Legislature and Bills|bill pipeline]] (`BillKind.
> CompanyOwnership`) exactly like tax/freedom bills — voted on by real party ideology or
> decreed, same as everything else in [[Government, Legislature and Real Taxes]]. Enactment
> charges/credits the treasury a one-time buyout-or-sale transaction sized by the company's
> approximate real scale. Trade tab gained a COMPANIES card. See
> [[Sectors and Companies]] for the full architecture. **Still open:** sector output doesn't
> compose GDP yet, no ongoing dividend/tax revenue tied to ownership, and manpower allocation
> (below) hasn't started.

## Sectors
The player named oil, agriculture (with fruit specifically called out as a sub-sector), as
examples of wanting the economy broken into real industries — the fuller real taxonomy that
points at:
- **Energy**: oil & gas, renewables, nuclear power generation (distinct from nuclear *weapons*
  programs in [[Conflicts, Terrorism and Military Realism]] — same underlying technology, very
  different game system).
- **Agriculture**: crops (with real sub-sectors like fruit, grain, etc.), livestock, fisheries,
  forestry.
- **Manufacturing**: heavy industry, automotive, electronics/semiconductors, textiles.
- **Technology**: software, telecom, biotech.
- **Finance**: banking, insurance.
- **Services**: tourism, retail, logistics.
- **Mining**: metals, rare earths (increasingly geopolitically significant in reality).
- **Construction / real estate.**
- **Defense industry**: arms manufacturing — the natural supplier side of
  [[Conflicts, Terrorism and Military Realism|the military equipment pillar]].
- **Healthcare / pharma.**

Each sector should be inspectable on its own: the player should be able to see every company
operating in a given sector (e.g. "show me every company in the oil sector").

> [!success] Status: ✅ Sectors compose GDP (`Sim/SectorModel.cs`). Every country's GDP is
> decomposed into the 10 sectors as shares summing to 100%, seeded by development tier (poor =
> agriculture/mining-heavy, rich = services/finance/tech-heavy) and then bumped wherever the
> country has big real curated companies — so Saudi Arabia reads **Energy 39%** from Aramco's
> real output, not the flat 2% tier baseline (verified live). Shares **drift over time** toward
> the faster-growing sectors (real structural transformation), and the composition feeds a
> small bounded nudge back into aggregate growth (tech/finance-weighted economies grow a hair
> faster). GDP still *grows* via the tuned macro model — sectors decompose it rather than
> replacing it, a deliberate choice to avoid destabilizing the balanced core. Per-sector profit
> margins (energy/finance fat, agriculture thin) now drive SOE dividends too, replacing the old
> flat 10%. Shown on the Economy tab as a GDP-BY-SECTOR card. **Still open:** manpower
> allocation (below) and per-sector detail beyond one margin number each.

## Companies, and public/private/mixed ownership
Individual companies exist within a sector, and the player can set each one's ownership model —
fully public (state-owned), fully private, or mixed. This is a real, consequential lever (state
ownership vs. privatization is a real policy axis for [[Government, Legislature and Real Taxes|bills]] to touch), not flavor text.

## Manpower allocation
The player should be able to directly edit how much manpower/labor goes into healthcare,
education, research, and (implicitly) other sectors — this is distinct from the current
`SpendHealthcare`/`SpendEducation`/`SpendInfrastructure` budget-percentage levers, which are
money, not people. Manpower is a separate real resource: how many workers a sector actually has,
which should itself interact with unemployment and sector output.

## Where this plugs into existing code
- ✅ `Sim/Companies.cs` — `Sector`/`Ownership` enums, `CompanySeed` (static real roster) and
  `Company` (mutable per-game copy on `EconomyState.Companies`).
- ✅ `Sim/CountryProfiles.cs` — real curated companies for ~13 major countries (Saudi Aramco,
  Apple, Sinopec, Volkswagen, Toyota, BP, TotalEnergies, Gazprom, Reliance Industries,
  Petrobras — kept genuinely Mixed, a real example — ADNOC, Royal Bank of Canada, Samsung).
- ✅ `Sim/Legislature.cs` — `BillKind.CompanyOwnership` reuses the existing vote/decree pipeline
  (encodes the enum as a float scale rather than needing a special-cased path); enactment
  charges/credits treasury a one-time buyout-or-sale transaction.
- ✅ `GameUIRoot.DrawCompanies` — Trade tab COMPANIES card, own-country ownership dropdowns,
  read-only for foreign countries.
- Verified live as the USA: proposed nationalizing Apple, Democrats (49% seats) backed it,
  Republicans (51%) opposed — correctly rejected, ownership stayed Private, no treasury
  transaction fired (confirms `Apply` only runs on `Passed`, not `Rejected`). The passed-
  transaction arithmetic itself was verified by direct code inspection rather than a live
  passing vote (would have needed a left-leaning-majority country or another multi-day cycle)
  — noted here rather than overclaiming a test that wasn't actually run.
- ⬜ Still fully open: sector output doesn't compose GDP (a genuinely new economic model, kept
  out of this slice on purpose — see [[Sectors and Companies]]), no ongoing dividend/tax
  revenue tied to ownership, and manpower allocation is a separate, unstarted axis. This is
  still the most build-heavy of the six pillars overall — the company/ownership slice is a
  real start, not the whole pillar.
