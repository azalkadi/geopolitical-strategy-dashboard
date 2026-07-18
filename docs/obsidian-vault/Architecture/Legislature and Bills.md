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
The four core tax levers (income/corporate/VAT/tariff) **and** the three civil-liberty levers
(speech/religion/internet — `NationalState.FreedomSpeech/FreedomReligion/FreedomInternet`,
0-100). The interest rate deliberately stays a direct slider — central banks aren't
legislatures. Foreign countries keep direct sandbox sliders/read-only stats when inspected;
only the player's own country goes through the political process.

Freedom bills apply to `NationalState` instead of `EconomyState` — `LegislatureSystem.Apply`
branches on `Bill.IsFreedom` and writes to whichever system the bill kind actually belongs to.
**Tightening a freedom (new value < old) costs international standing on enactment; loosening
earns a little back**, asymmetric on purpose (losing standing is easy, earning it back is
slow) — this is the real international-reaction requirement from
[[Government, Legislature and Real Taxes]], not just a cosmetic number.

Party voting on freedom bills reuses the same single `EconLean` axis as tax bills (left backs
expanding freedoms, right backs tightening) as a coarse proxy for a social lib-conservative
axis that hasn't been curated separately yet — documented as a known simplification in
`PartySupports`' own comment, not a claim of real second-axis data.

## UI
- **Economy › tax section** (`GameUIRoot.DrawTaxLever`): for the player's country, each tax
  shows its current rate plus a type-in field — committing a target rate proposes a bill. While
  a bill is pending the field is replaced by its status (target, path, decision date); one open
  bill per lever at a time.
- **Politics › FREEDOMS card** (`GameUIRoot.DrawFreedoms`): the same propose-a-bill UX
  (`DrawTaxLever` is generic over any `BillKind`, not tax-specific) for the three civil-liberty
  levers, own country only; foreign countries see them as read-only stats.
- **Politics › PARLIAMENT** (`GameUIRoot.DrawParliament`): real party composition with lean
  labels and seat shares for any curated country, plus the player's bill docket (last 6 bills,
  live status, For/Against party lists on pending votes — freedom and tax bills interleaved).
- A `billsStamp` in `Refresh()` (same pattern as the war stamp) forces a structural panel
  rebuild on the Economy AND Politics tabs the day a bill resolves.

## Persistence & verification
`LegislatureSystem`/`Bill`/`BillStance` are plain public fields → [[Save Load]] serializes them
for free; `MapRenderer.ApplySave` falls back to a fresh empty system for older saves.
`MERIDIAN_DIAG_BILLS=1` runs two phases: a corporate-tax raise at day 20 (logs path, every
party's stance, and the resolution), then once that resolves, a freedom-of-speech tightening
bill (logs the path and the international-standing delta on enactment). Run with
`MERIDIAN_AUTOSTART="United States of America"` for the vote path and `"Saudi Arabia"` for the
decree path — both verified live: the USA tax bill died 49–51 on party lines; the freedom
bill's standing consequence and the Saudi decree path were confirmed the same way.

## Deliberate simplifications (open follow-ups)
- Only the player proposes bills — [[World AI]] countries don't legislate yet.
- Party stances are fixed at proposal time (no lobbying/whipping mechanic yet).
- Seat shares are static seed data — elections don't yet reshuffle parliaments.
- Freedom votes reuse the economic-lean axis as a proxy for social ideology — a real second
  axis (curated per party) would be more honest.
- Freedom baselines are a government-type-bucket heuristic (`NationalState.Seed`), not
  per-country researched data (that's its own Freedom-House-scale project).
- Bill scope is tax + civil liberties — spending and regime change are the pillar's next steps
  (see [[Government, Legislature and Real Taxes]]).
