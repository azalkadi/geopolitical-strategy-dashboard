---
tags: [architecture, sim]
---

# World AI

`Assets/Scripts/Sim/WorldAI.cs` — makes the **rest of the world** act without any player
involvement: occasional AI-vs-AI wars between hostile regional neighbors, and trade agreements
between friendly states. Deliberately rare (roughly a handful of headline events per simulated
decade) so each surfaces meaningfully via the toast feed rather than becoming background noise.

## How it decides
- `Tick(day, econ, national, diplomacy, wars, names)` — considers a new AI-vs-AI war roughly
  every 240-360 days (capped at `MaxConcurrentAiWars = 3` so the world doesn't spiral into
  constant conflict), and a new AI-vs-AI trade agreement every 120-300 days.
- `TryFindHostilePair` — samples random country pairs (excluding the player, via
  `PlayerState.CountryIndex`), requires low relations, same continent, neither already at war,
  and passes [[War System]]'s own `CanDeclare` eligibility.
- `TryFindFriendlyPair` — samples pairs with relation ≥ 75 and no existing
  [[Diplomacy System|agreement]] yet.
- Internal deterministic xorshift32 PRNG (`Next()`), same pattern as [[Economy System]]'s
  per-country noise generator.

## Why the player is excluded from AI selection
`PlayerState.CountryIndex` is checked explicitly in both pair-finding methods — the player's own
wars/agreements only ever happen through their own deliberate action (see [[War Mechanics]] and
[[Diplomacy Mechanics]]), never assigned to them by this autonomous system.

## Output
Returns headline strings, which the caller (`MapInteraction.TickEconomy`, see [[Camera and Input]]) pushes onto [[History and World Feed|WorldFeed]] for the toast UI to surface.

## Consumers
- [[Camera and Input]] — calls `Tick()` once per simulated day alongside `WarSystem.TickAll`
- [[War System]], [[Diplomacy System]] — the actual state changes AI decisions cause
