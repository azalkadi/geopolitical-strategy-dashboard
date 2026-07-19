---
tags: [vision, game-design, politics]
---

# Government, Legislature and Real Taxes

The core of [[Vision Overview|the vision]]. Currently every country has the same generic
politics (one `ApprovalRating` number, no parties, no regime type) and the same generic tax
sliders regardless of what that country actually does in reality. This is the fix.

> **Status (2026-07-17): first slice shipped.** `Sim/CountryProfiles.cs` — real `GovernmentType`
> + real headline tax rates (VAT/corporate confidently, income tax as an honest single-lever
> approximation of a real progressive system — see the file's own precision note) for ~35
> major/well-known countries, keyed by ISO A3. `EconomyState.Seed`/`NationalSystem.Seed` apply
> it automatically; unlisted countries keep the old generic simulated defaults. Verified live:
> starting as Saudi Arabia seeds income=0%, corporate=20%, VAT=15%, tariff=5% exactly. Every
> slider (not just tax) now also supports clicking the value and typing an exact number
> (`FloatField`, `GameUIRoot.AddSlider`) instead of only dragging. **Still open, in priority
> order:** real named political parties per country, the actual bill-proposal → vote/decree →
> enactment pipeline, freedom-of-speech/religion/internet levers, regime-change mechanics with
> realistic (not moralized) AI-driven outcomes.

## Regime types
Every country needs a real `GovernmentType` — the player named Saudi Arabia, the UK/GCC, and
the USA as examples of the pattern, not the exhaustive list. The real category, with more real
examples per bucket:
- **Absolute monarchy**: Saudi Arabia, UAE, Qatar, Oman, Brunei, Eswatini.
- **Constitutional monarchy**: UK, Jordan, Kuwait, Bahrain, Morocco, Spain, the Netherlands,
  Sweden, Norway, Japan.
- **Presidential republic** (multi-party legislature, directly-elected head of state/government
  split from it varies): USA, Brazil, Mexico, South Korea, Indonesia, Turkey, France
  (semi-presidential — a real hybrid case worth its own nuance later, not forced into one bucket).
- **Parliamentary republic**: Germany, Italy, India, Israel, most of continental Europe.
- **One-party state**: China, North Korea, Vietnam, Cuba, Eritrea.
- **Military/transitional government**: covers real unstable/interim cases (varies year to year
  by definition — this bucket should stay a live judgment call, not a fixed list).

This isn't cosmetic — it decides how bills get enacted (below) and what freedoms (below) mean
politically.

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
The player named freedom of speech, religion, and internet access — the real civil-liberties
axis (the kind Freedom House-style indices actually track) is a bit wider: press freedom,
freedom of assembly, and judicial independence are the same category and worth the same
treatment. Each independently adjustable, tightening or loosening. These should feed [[National State]]'s indices (approval, international standing) and **draw real international reaction**:
tightening freedoms as a democracy should cost you standing and possibly trigger AI-country
response, not be a free slider.

## Regime change, and the world reacting realistically
The player should be able to convert a country's regime type (e.g. democratic → monarchy) — and
the world should genuinely react, not just tick a number. Critically: **the simulation shouldn't
assume monarchy = bad, democracy = good.** The player named the GCC states as successful
monarchies and Iraq/Libya/Tunisia/Syria as failed democratic transitions — the fuller real
picture on both sides matters for the outcome modeling to be honest rather than a coin flip:
- **Stable/successful monarchies beyond the GCC**: Jordan, Morocco, Bhutan.
- **Democratic transitions that succeeded**: South Korea, Indonesia, Ghana, post-Franco Spain.
- **Transitions/collapses that struggled beyond the four named**: Yemen, Sudan, Afghanistan.

[[World AI]] and the outcome modeling for this need to reflect that real, uncomfortable nuance —
governance quality, institutional strength, and external pressure predict outcomes far better
than regime type alone — not a simple morality slider. This was stated explicitly as a
requirement, not a suggestion.

## Where this plugs into existing code
- ✅ `Sim/CountryProfiles.cs` — the real government-type + tax data source, hand-researched
  (same research-effort pattern as [[Curated Datasets]], not a formula), ~35 countries.
- ✅ `Sim/NationalState.cs` — has `GovernmentType` now, still just `ApprovalRating` for the
  actual politics number — per-party support is still open.
- ✅ `Sim/Economy.cs` — `EconomyState.Seed` applies `CountryProfiles` tax rates when present.
- ✅ `GameUIRoot.AddSlider` — click-and-type numeric entry (`FloatField`), not drag-only.
- ✅ `Sim/Legislature.cs` — the proposal → vote/decree → enactment pipeline is LIVE for the
  four core tax levers, the three freedom levers, AND regime change, with real named parties
  (~20 curated multi-party countries) voting by ideology and seat share, decree paths for
  monarchies/one-party states, headlines for the fight and the outcome, parliament + bill-
  docket + FREEDOMS + CHANGE GOVERNMENT cards in the Politics tab, and save/load. Verified all
  live: a USA corporate-tax raise died 49–51 on party lines (Democrats for, Republicans
  against); a Saudi royal decree enacted automatically after 5 days; a USA freedom-of-speech
  tightening bill passed 51–49 (Republicans for, Democrats against) and correctly cost
  international standing on enactment (56.9→53.2); a USA→one-party-state regime change
  bypassed the party vote entirely (no stances logged — regime change is always a decree, see
  [[Legislature and Bills]] for why), ran the full 45-day transition, correctly flipped
  `NationalState.Government`, and cost standing hard for losing real pluralism (53.2→36.0
  across the transition window, alongside the normal 45 days of drift already happening in
  parallel). See [[Legislature and Bills]] for the full architecture.
- ✅ **AI countries legislate on their own** (`MapInteraction.MaybeAILegislate`): each day a
  small deterministic sample of foreign countries considers a tax bill fitting its own fiscal
  situation and ruling ideology — a country running a real deficit moves to raise a tax, one
  with healthy books and a right-leaning legislature moves to cut. It runs through the exact
  same `LegislatureSystem.Propose` pipeline the player uses: curated-party countries fight a
  weighted floor vote, uncurated ones decree. Only major economies surface a headline (no spam
  from 258 countries); `TickAll` now prunes resolved bills older than 60 days so the list stays
  bounded. Verified live (autopilot, ~350 days): Switzerland & Kuwait raised taxes on deficits
  by decree, Israel/Netherlands/India/Italy cut taxes via real party votes with surplus +
  right-leaning legislatures — ideology-correct, zero exceptions.
- ⬜ Still open within the pillar: elections reshuffling
  seat shares, lobbying/whipping, bill scope beyond tax+freedoms+regime (spending), a real
  second (social) party-ideology axis instead of reusing economic lean, per-country researched
  freedom baselines instead of a government-type-bucket heuristic, per-party approval
  replacing the single ApprovalRating number, and [[World AI]] actually reacting to a regime
  change beyond the standing number (a relation shock, a broadcast headline other countries
  respond to).
