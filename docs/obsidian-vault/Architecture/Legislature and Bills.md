---
tags: [architecture, sim, politics]
---

# Legislature and Bills

`Assets/Scripts/Sim/Legislature.cs` — the bill pipeline, the first shipped *mechanic* (not just
data) of the [[Government, Legislature and Real Taxes]] vision pillar. The player proposes a
tax-law change for their own country; what happens next depends on that country's real
political structure.

## The two paths
The structural decision is **"does this country have real parties data"**
(`CountryProfiles.Parties`):

- **Parliamentary vote** (curated multi-party countries — USA, UK, Germany, France, Japan,
  ~20 total): the bill goes to the legislature for `VoteDays` (14 sim-days). Each party takes a
  public stance at proposal time from its economic ideology — left backs tax raises, right
  backs cuts, with a deterministic per-party-per-bill wrinkle so centrists genuinely swing —
  weighted by approximate real seat share. Over 50% of seats in favor on decision day passes
  the bill and applies the new rate; otherwise it dies and the old rate stands.
- **Decree** (monarchies, one-party states, and every country without curated party data): the
  change enacts automatically after `DecreeDays` (5 sim-days), no vote, with flavor by
  [[Government, Legislature and Real Taxes|government type]] — royal decree / party directive /
  executive order.

Both paths emit [[History and World Feed|WorldFeed]] headlines at proposal ("X back it; Y vow
to fight it") and resolution ("bill passes 61–39") — this is where "parties fighting over
things" is visible.

## What's a bill today
The four core tax levers (income/corporate/VAT/tariff). The interest rate deliberately stays a
direct slider — central banks aren't legislatures. Foreign countries keep direct sandbox
sliders when inspected; only the player's own country goes through the political process.

## UI
- **Economy › tax section** (`GameUIRoot.DrawTaxLever`): for the player's country, each tax
  shows its current rate plus a type-in field — committing a target rate proposes a bill. While
  a bill is pending the field is replaced by its status (target, path, decision date); one open
  bill per lever at a time.
- **Politics tab** (`GameUIRoot.DrawParliament`): real party composition with lean labels and
  seat shares for any curated country, plus the player's bill docket (last 6 bills, live
  status, For/Against party lists on pending votes).
- A `billsStamp` in `Refresh()` (same pattern as the war stamp) forces a structural panel
  rebuild the day a bill resolves.

## Persistence & verification
`LegislatureSystem`/`Bill`/`BillStance` are plain public fields → [[Save Load]] serializes them
for free; `MapRenderer.ApplySave` falls back to a fresh empty system for older saves.
`MERIDIAN_DIAG_BILLS=1` proposes a corporate-tax raise at day 20 and logs the path, every
party's stance, and the resolution — run with `MERIDIAN_AUTOSTART="United States of America"`
for the vote path and `"Saudi Arabia"` for the decree path.

## Deliberate simplifications (open follow-ups)
- Only the player proposes bills — [[World AI]] countries don't legislate yet.
- Party stances are fixed at proposal time (no lobbying/whipping mechanic yet).
- Seat shares are static seed data — elections don't yet reshuffle parliaments.
- Bill scope is tax law only — spending, freedoms, and regime change are the pillar's next
  steps (see [[Government, Legislature and Real Taxes]]).
