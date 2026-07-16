---
tags: [architecture, sim]
---

# Player State and Elections

`Assets/Scripts/Sim/PlayerState.cs` — minimal game-layer state: which country the human is
playing, and the election/term mechanic that gives the simulation real stakes. See
[[Elections]] for the player-facing rules.

## PlayerState (static)
- `State` (`GameState`: NotStarted/Playing/GameOver), `CountryIndex`, `CountryName`
- `TermStartDay`, `TermsServed`, `TermLengthDays = 1460` (one 4-year term)
- `LastResultMessage`, `WonLastElection`
- `Reset()` — back to NotStarted, clears everything, calls `EventSystem.Reset()` so no stale
  pending decision carries into a new game
- `Begin(countryIndex, countryName, currentDay)` — starts a new term **relative to now**, not to
  a stale day-90 mark the clock may be long past (important since the sim clock never rewinds
  across "Play Again"); also resets [[History and World Feed|PlayerHistory]] and clears
  [[History and World Feed|WorldFeed]] so a previous government's charts/headlines don't bleed
  into the new one

## The election check (`MapInteraction.CheckElection`, see [[Camera and Input]])
Fires once a term has elapsed (`day - TermStartDay >= TermLengthDays`), reading
[[National State]]`.ApprovalRating` — **the exact same number already shown on the Politics
tab**, driven by the same growth/unemployment/inflation/event/diplomacy/war numbers everything
else touches. There's no separate hidden "win" stat.
- ≥ 50% approval → safe re-election
- < 35% → loss, `GameState.GameOver`
- Between 35-50% → a weighted coin flip (`Mathf.InverseLerp(35, 50, approval)`), so it isn't a
  hard cliff-edge right at 50%

## Consumers
- Almost everything reads `PlayerState.CountryIndex` to know "which country is the player's":
  [[Economy System]]/[[National State]] diagnostics, [[Decision Events]] (fires only for this
  country), [[World AI]] (excludes this country from AI pair selection), [[Map Modes and
  Coloring]] (relation coloring is *relative to* this country)
- [[UI System]] — `BeginGame`/`ContinueSavedGame` set it; the top bar's player badge and the
  game-over screen read `LastResultMessage`/`TermsServed`
- [[Save Load]] — every `PlayerState` field is part of the save
