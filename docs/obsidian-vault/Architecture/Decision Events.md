---
tags: [architecture, sim]
---

# Decision Events

`Assets/Scripts/Sim/Events.cs` — every 150-360 simulated days, the player's country faces a
random 2-3-choice situation (corruption scandal, catastrophic flooding, banking crisis, etc.)
with concrete, immediate effects on [[Economy System|EconomyState]]/[[National
State|NationalState]]. **The sim clock pauses** while a decision is pending — checked by
`MapInteraction` (see [[Camera and Input]]) before ticking any further days.

## Data shape
- `GameEvent` — `Title`, `Description`, an array of `EventChoice`, and an optional `Condition`
  eligibility predicate (e.g. a banking crisis only fires during weak growth).
- `EventChoice` — `Label`, `Outcome` text, and an `Apply` action that mutates the economy/national
  state directly.

## EventSystem (static)
- `Pending` — the current modal event, or `null`. `NextEventDay` — when the next one is eligible
  to fire.
- `MaybeFire(day, econState, nationalState)` — called once per simulated day for the player's
  country only; if `day >= NextEventDay`, picks a random eligible event (deterministic PRNG,
  filtered by `Condition`) and sets `Pending`.
- `Choose(index, econState, nationalState)` — applies the chosen `EventChoice.Apply`, returns the
  outcome toast text, clears `Pending`, reschedules `NextEventDay`.
- `Reset()` — called on new game start so no stale pending decision carries over.

## The pool (`BuildPool`)
~11 hardcoded events: Corruption Scandal, Catastrophic Flooding, General Strike, Banking Wobble,
Tech Relocation, Flu Outbreak, Commodity Spike, University Funding, Inflation Protest, Military
Modernization, Boom-Time Windfall. Several are condition-gated (e.g. the banking crisis only
during weak growth). Effect magnitudes scale as a fraction of GDP via a local `Pct()` helper, so
a $2T economy and a $20B economy both feel a proportional hit rather than the same flat number.

## Consumers
- [[Camera and Input]] — `MapInteraction.TickEconomy` calls `MaybeFire` once/day and freezes the
  clock (and drops game speed to 1×) whenever `Pending != null`
- [[UI System]] — `GameUIRoot`'s decision-event modal polls `Pending` and calls `Choose` when the
  player picks an option; deliberately has **no dismiss button**
- [[Player State and Elections]] — `PlayerState.Begin`/`Reset` call `EventSystem.Reset`
- [[Save Load]] — `NextEventDay` is part of the save; `Pending` is cleared on load (never
  serialized mid-decision)

## Dev-only autopilot
`MERIDIAN_AUTOPILOT=1` auto-picks the first option of every event so unattended long-run tests
(war diag, soak tests) aren't frozen forever waiting for a human to click a modal nobody's
watching.
