---
tags: [architecture, sim, economy]
---

# Sectors and Companies

`Assets/Scripts/Sim/Companies.cs` — first slice of the [[Economic Sectors and Companies]]
vision pillar. Real named companies within real industry sectors, with a player-changeable
ownership model, connected into the same [[Legislature and Bills|bill pipeline]] everything
else in the [[Government, Legislature and Real Taxes]] pillar uses.

## Data model
- `Sector` — 10 real industries (Energy, Agriculture, Manufacturing, Technology, Finance,
  Services, Mining, Construction, Defense, Healthcare), matching the taxonomy in the Vision page.
- `Ownership` — Public / Private / Mixed.
- `CompanySeed` (static, `CountryProfiles.Companies`) — the real curated roster: ~13 major
  countries, 1-3 real, well-known companies each (Saudi Aramco, Apple, Sinopec, Volkswagen,
  Toyota, BP, TotalEnergies, Gazprom, Reliance Industries, Petrobras, ADNOC, Royal Bank of
  Canada, Samsung Electronics...). Revenue figures (`OutputBillions`) are approximate/rounded
  public-knowledge numbers for gameplay sizing, not audited financials. Petrobras is kept as a
  genuinely **Mixed**-ownership example (real majority state stake in a publicly-traded
  company) rather than rounded to Public or Private.
- `Company` (mutable, `EconomyState.Companies`) — `EconomyState.Seed` copies from
  `CountryProfiles.Companies` so ownership changes during play never mutate the shared static
  seed data other saves/games would also read.

## Ownership as a bill
`BillKind.CompanyOwnership` encodes `Ownership` as a float scale (0=Public, 1=Mixed,
2=Private) in `Bill.OldValue`/`NewValue` specifically so it can reuse
`LegislatureSystem.Propose` — the exact same vote-or-decree pipeline every tax/freedom bill
uses — instead of a fourth special-cased path (unlike regime change, which genuinely needed
one). `ProposeOwnershipChange` is a thin convenience wrapper around `Propose`.

Party voting reuses the tax-cut sign convention: economic-right parties back privatizing
(moving toward Private), economic-left back nationalizing (moving toward Public) — the same
real, uncontroversial partisan pattern as tax cuts, since privatization is a shrink-the-state
move in the same sense.

## The real effect — a one-time transaction, not yet ongoing
On enactment, `Legislature.Apply` charges/credits the treasury a **one-time buyout-or-sale
transaction**, sized by `(oldStake - newStake) × company.OutputBillions × 0.4` where stake is
1.0 for Public, 0.5 for Mixed, 0 for Private — nationalizing costs a real buyout, privatizing
raises a real one-time windfall. `Company.Ownership` flips to the new value.

**Deliberately does not yet**: add an ongoing dividend/tax stream tied to the new ownership, or
feed sector output into the GDP growth formula at all — `EconomyState.Tick()`'s core macro
model is untouched by this slice. Modeling sectors as truly composing GDP is real future work
kept out of this slice specifically to avoid touching the GDP formula until that's properly
designed, not a silently-faked shortcut.

## UI
**Trade tab › COMPANIES card** (`GameUIRoot.DrawCompanies`) — lists the selected country's
curated companies (silently absent for uncurated countries). Player's own country gets a
per-company ownership dropdown that proposes a bill on change; a pending bill shows its target
and decision date instead. Foreign countries see the roster read-only.

## Persistence & verification
`Company` is a plain public-field class → [[Save Load]] serializes `EconomyState.Companies`
for free. `MERIDIAN_DIAG_BILLS=1`'s fourth phase (after regime change resolves) proposes
nationalizing/privatizing the player's first curated company and logs the path, the treasury
delta, and the resulting ownership — verified live nationalizing Apple as the USA: routed
through a real party vote, treasury paid the buyout cost on enactment, `Ownership` flipped to
`Public`.

## Deliberate simplifications (open follow-ups)
- Only 13 countries have curated companies — most countries show no COMPANIES card at all.
- No ongoing dividend/tax revenue tied to ownership — only the one-time transaction.
- Sector output doesn't compose GDP yet — `Sector` currently only labels a company, it isn't
  aggregated into anything.
- [[Economic Sectors and Companies|Manpower allocation]] (a people, not money, resource axis)
  is a separate, unstarted piece of this pillar.
- AI countries don't propose ownership bills.
