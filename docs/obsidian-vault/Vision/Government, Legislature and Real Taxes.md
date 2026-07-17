---
tags: [vision, game-design, politics]
---

# Government, Legislature and Real Taxes

The core of [[Vision Overview|the vision]]. Currently every country has the same generic
politics (one `ApprovalRating` number, no parties, no regime type) and the same generic tax
sliders regardless of what that country actually does in reality. This is the fix.

## Regime types
Every country needs a real `GovernmentType` — at minimum: **absolute monarchy** (Saudi Arabia),
**constitutional monarchy** (UK, Jordan, most of the GCC's smaller states), **presidential
republic with multi-party legislature** (USA), **parliamentary multi-party republic** (most of
Europe), **one-party state** (China, North Korea), **military/transitional government** (covers
real unstable cases). This isn't cosmetic — it decides how bills get enacted (below) and what
freedoms (below) mean politically.

## Real political parties, not a generic slider
Named, real parties per country, each with an actual ideological position (economic left-right,
social liberal-conservative at minimum) that determines how they vote on a bill:
- USA → Republicans, Democrats (+ real seat/Congress framing)
- Saudi Arabia → no parties; an advisory Shura Council instead, decisions are royal decree
- Every multi-party country needs its own real party set, not a placeholder "Party A/B"

## Bills, not just sliders
The player should be able to **propose or change policy directly** — click the specific tax/law
you want to change, type the new number/value, rather than dragging a generic slider that
doesn't map to anything real. What happens next depends on regime type:
- **Monarchy** → it's decreed. Done, maybe with a delay and a cost, no vote.
- **Multi-party legislature** → it goes to the parties. They vote based on their ideology
  (a bill cutting corporate tax gets support from the right-leaning party, opposition from the
  left-leaning one, etc.) — this is where "parties fighting over things" becomes visible, and
  where it should show up in the news feed the same way [[War System|war headlines]] do.

## Real seeded tax data — don't slider what's already real
If a country's real tax system is known (e.g. Saudi Arabia's VAT is 15% in reality), seed it as
a fact, not a generic default the player has to first "discover" via a slider. The player edits
it by clicking the specific tax and typing the new percentage, not dragging toward a value that
was already correct. This needs real research per major country, not one universal formula —
and there are more tax types in reality than the current four core levers (income/corporate/
VAT/tariff) — unemployment insurance tax is one named example, there will be others per country.

## Freedoms as real levers with real consequences
Freedom of speech, freedom of religion, freedom of the internet — each independently
adjustable, tightening or loosening. These should feed [[National State]]'s indices (approval,
international standing) and **draw real international reaction**: tightening freedoms as a
democracy should cost you standing and possibly trigger AI-country response, not be a free
slider.

## Regime change, and the world reacting realistically
The player should be able to convert a country's regime type (e.g. democratic → monarchy) — and
the world should genuinely react, not just tick a number. Critically: **the simulation shouldn't
assume monarchy = bad, democracy = good.** Some real monarchies are stable and successful (the
GCC states); some real democratic transitions failed (Iraq, Libya, Tunisia, Syria). [[World AI]]
and the outcome modeling for this need to reflect that real, uncomfortable nuance, not a simple
morality slider — this was stated explicitly as a requirement, not a suggestion.

## Where this plugs into existing code
- `Sim/NationalState.cs` — needs `GovernmentType`, replaces/extends the single `ApprovalRating`
  with per-party support.
- `Sim/Economy.cs` — real per-country tax seeding needs a data source (similar research effort
  to how [[Curated Datasets]] were hand-researched), not a formula.
- `Sim/Diplomacy.cs`/[[World AI]] — regime-change reactions are a form of relation shock, same
  channel as denounce/war, but internationally broadcast rather than bilateral.
- New: a Legislature/Bills system (`Sim/Legislature.cs`?) — the actual proposal → vote →
  enactment pipeline, plus the UI in the Politics tab to browse/propose/customize bills.
