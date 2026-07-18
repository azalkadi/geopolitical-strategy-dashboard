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

## Regime change — a third bill kind, deliberately special-cased
`BillKind.RegimeChange` converts the country's own `NationalState.Government`. Unlike tax and
freedom bills it **always bypasses the party vote** (`ProposeRegimeChange`, not `Propose`) — a
multi-party legislature doesn't get to vote itself out of existence; this is the player, as
head of government, driving a constitutional transition unilaterally, so it's always
decree-style regardless of `CountryProfiles.Parties`. It also runs on its own, much longer
timer (`RegimeChangeDays` = 45, vs. 5 for an ordinary decree) to reflect the magnitude.

The standing consequence is keyed on a **pluralism axis** (`IsPluralistic`: constitutional
monarchy, presidential republic, parliamentary republic all count; absolute monarchy and
one-party state don't), not a judgment about which government type is "better" — losing real
pluralism costs standing hard (-25), gaining it earns real credit (+12), and even a same-
category swap carries a small transitional-uncertainty cost (-3). This is the concrete
implementation of the Vision page's explicit requirement that the sim not assume monarchy=bad,
democracy=good: the consequence reacts to the *structural fact* of the change, and whether the
country is actually stable afterward is still driven by the ordinary ApprovalRating/PublicMood/
economy numbers, exactly like every other country — not a scripted "this regime type fails."

UI: Politics › **CHANGE GOVERNMENT** card (`GameUIRoot.DrawRegimeChange`, own country only) — a
government-type dropdown plus a "BEGIN TRANSITION" button; while one is pending it shows the
target and completion date instead.

**Still open**: this only moves `InternationalStanding` — it doesn't yet shock bilateral
[[Diplomacy System|relations]] or trigger a [[World AI]] reaction the way a real regime change
would (see the "world reacts realistically" edge on [[Feature Relationships.canvas]], still
marked open).

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
`MERIDIAN_DIAG_BILLS=1` runs three phases against `MERIDIAN_AUTOSTART="United States of
America"`: a corporate-tax raise at day 20 (logs path, every party's stance, and the
resolution), a freedom-of-speech tightening bill once that resolves (logs the standing delta),
then a regime change to one-party state once that resolves (logs the timer and the standing
delta). All three verified live in one run: the tax bill died 49–51 on party lines; the
freedom bill passed 51–49 and dropped standing 56.9→53.2; the regime change skipped voting
entirely, ran the full 45-day timer, correctly set `Government = OneServiceState`, and dropped
standing further (53.2→36.0). Run with `MERIDIAN_AUTOSTART="Saudi Arabia"` separately to
confirm the decree path for ordinary bills.

## Deliberate simplifications (open follow-ups)
- Only the player proposes bills — [[World AI]] countries don't legislate yet.
- Party stances are fixed at proposal time (no lobbying/whipping mechanic yet).
- Seat shares are static seed data — elections don't yet reshuffle parliaments.
- Freedom votes reuse the economic-lean axis as a proxy for social ideology — a real second
  axis (curated per party) would be more honest.
- Freedom baselines are a government-type-bucket heuristic (`NationalState.Seed`), not
  per-country researched data (that's its own Freedom-House-scale project).
- Bill scope is tax + civil liberties + regime change — spending is the pillar's next step
  (see [[Government, Legislature and Real Taxes]]).
- Regime change doesn't shock [[Diplomacy System|bilateral relations]] or trigger [[World AI]]
  yet — only the standing number moves.
