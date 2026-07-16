---
tags: [architecture, sim]
---

# History and World Feed

Two small files that both exist to move information **out of Sim/ and into the UI** without
Sim/ ever depending on UI/.

## History.cs — `PlayerHistory`
A fixed-capacity ring buffer (`HistorySeries`, capacity 1,460 days ≈ 4 years) of the player
country's key indicators, feeding the side-panel sparkline charts in [[UI System]]. Constant
memory regardless of how long a game runs.

- `HistorySeries` — `float[] buf`, `head`, `count`; `Add(v)`; indexer `this[i]` (0 = oldest,
  `Count-1` = newest); `Clear()`; `ToArray()`/`LoadFrom(float[])` for [[Save Load]] round-trips;
  `Range()` (min/max, for chart auto-scaling).
- `PlayerHistory` (static) — six series: `Gdp`, `Growth`, `Approval`, `Treasury`,
  `Unemployment`, `Inflation`. `Record(econState, nationalState)` appends one day's sample to all
  six at once. `Reset()` clears everything — called on `PlayerState.Begin` so a new government
  doesn't inherit the previous one's chart history.

Populated once per simulated day from `MapInteraction.TickEconomy` (see [[Camera and Input]])
using the player's own `EconomyState`/`NationalState`, serialized/restored via [[Save Load]],
and rendered by `GameUIRoot`'s private `Sparkline` class (a `Painter2D`-based line chart).

## WorldFeed.cs
A one-way static queue carrying human-readable headlines (war declarations, peace deals, AI
trade agreements — see [[War System]] and [[World AI]]) from Sim code to the UI toast feed,
specifically so `Sim/` never needs a UI dependency to announce something happened.

- `Push(source, message)`, `TryDequeue(out source, out message)`, `Clear()` — backed by a
  `Queue<(string,string)>`.
- Pushed to by `MapInteraction.TickEconomy` after relaying `WarSystem.TickAll`/`WorldAI.Tick`
  results (see [[Camera and Input]]).
- Drained by `GameUIRoot.Refresh()` to call `ShowToast` (see [[UI System]]).
- Cleared by `SaveLoad.Apply` and `PlayerState.Begin` — stale headlines from a previous game
  never carry over.
