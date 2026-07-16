---
tags: [architecture, sim]
---

# Economy System

`Assets/Scripts/Sim/Economy.cs` — deterministic, per-day economic simulation running for **every**
country simultaneously (not just the player's), ported from the original Rust prototype. See
[[Economy Mechanics]] for the player-facing version of this.

## EconomyState (per country)
- Core numbers: `Gdp`, `GrowthRate`, `BaseGrowthTarget`, `Unemployment`, `Inflation`, `Treasury`
- Four adjustable tax levers: `TaxIncome`, `TaxCorporate`, `TaxVat`, `TaxTariff`, plus a
  player-definable `CustomTaxes` list (`CustomTax { Name, Rate }`)
- `InterestRate`
- Budget spending levers: `SpendEducation`, `SpendHealthcare`, `SpendInfrastructure` (as % of GDP,
  relative to a `SpendBase` constant) — these feed growth/innovation/mood honestly rather than
  being cosmetic
- `TradeAgreementExportBonus` — bumped by [[Diplomacy System]] when a trade agreement is signed
- `ExportPropensity`/`ImportPropensity` and derived properties: `Exports`, `Imports`,
  `TradeBalance`, `AnnualRevenue`, `AnnualExpenditure`, `AnnualDeficit`, `PublicDebt`,
  `DebtToGdp`
- `LastWhy` — a narration string set whenever a threshold is crossed (e.g. entering recession);
  this is what the toast feed in [[UI System]] surfaces as "why did this change"

## Seeding and ticking
- `EconomyState.Seed(Country, salt)` — seeds GDP tier, growth target, and trade openness from
  real Natural Earth `GdpMd`/`PopEst` data where available (see [[Natural Earth Datasets]]),
  falling back to a placeholder distribution otherwise. Uses a per-country deterministic xorshift32
  PRNG (seeded via FNV-1a hash) — not `Random`/`UnityEngine.Random` — so a given country always
  seeds identically.
- `Tick()` — one simulated day: growth from tax/interest-rate drag + spending boost + noise, then
  GDP/unemployment/inflation/treasury update from that.
- `EconomySystem` — the top-level holder: `List<EconomyState> States`, `Seed()`, `TickAll()`.

## Read by
- [[National State]] — growth/unemployment/inflation/spend feed the derived indices
- [[War System]] — military strength is derived partly from GDP × defense spending
- [[Decision Events]] — event effects mutate `EconomyState` directly
- [[UI System]] — every tax/spending slider in `GameUIRoot.DrawEconomy`/`DrawTaxSection`/
  `DrawBudget` writes here live
- [[Save Load]] — serialized wholesale as `List<EconomyState>`
