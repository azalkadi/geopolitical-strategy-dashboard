---
tags: [architecture, sim]
---

# Simulation Overview

`Assets/Scripts/Sim/` — nine files, each a mostly-independent system, all ticked together once
per simulated day from `MapInteraction.TickEconomy()` (see [[Camera and Input]]). Every state
class uses **plain public fields**, deliberately — it means [[Save Load]] can serialize the
entire simulation as a dumb, complete JSON dump and get a bit-identical reload.

## The tick order (one simulated day)
1. [[Economy System]] `TickAll()` — every country's GDP/unemployment/inflation/treasury
2. [[National State]] `TickAll(econ)` — Politics/Military/Society/Technology indices, computed
   fresh from a GDP-rank percentile across all countries
3. [[Diplomacy System]] `TickAll()` — relations drift toward baseline
4. [[War System]] `TickAll(...)` — active wars advance score/exhaustion/economic drag; AI-side
   wars can auto-resolve
5. [[World AI]] `Tick(...)` — occasional AI-vs-AI wars/trade agreements, independent of the player
6. [[Elections]] `CheckElection()` — only fires once a term has elapsed
7. [[History and World Feed|PlayerHistory]].`Record()` — one sample of the player's own numbers
8. [[Decision Events]] `EventSystem.MaybeFire()` — for the player's country only; if one fires,
   the clock freezes here for the rest of the frame/until decided
9. [[Buildable Infrastructure]] `InfrastructureSystem.TickAll(day)` — flips any route whose
   construction day has arrived, toasts it, and triggers one map mesh rebuild

Map coloring also refreshes at the end of this: see [[Map Modes and Coloring]].

## Systems at a glance

| File | Owns | Depends on |
|---|---|---|
| [[Economy System]] | GDP, taxes, spending levers, treasury | [[Geo Pipeline\|Country]] (seed data) |
| [[National State]] | Politics/Military/Society/Technology indices | [[Economy System]] |
| [[Diplomacy System]] | Bilateral relations matrix, aid/agreement/denounce | [[Economy System]], [[National State]] |
| [[War System]] | War score/exhaustion, declare/concede/white-peace | [[Economy System]], [[National State]], [[Diplomacy System]] |
| [[World AI]] | Autonomous AI wars & trade agreements | [[War System]], [[Diplomacy System]] |
| [[Decision Events]] | Random 2-3-choice player events | [[Economy System]], [[National State]] |
| [[Player State and Elections]] | Which country the human plays, term/election state | [[National State]] (approval rating) |
| [[Buildable Infrastructure]] | Player-built road/rail links between own cities | [[Economy System]] (treasury), [[Geo Pipeline\|City]] positions |
| [[Save Load]] | JSON snapshot of the whole simulation | all of the above |
| [[History and World Feed]] | Player sparkline history + cross-system headline queue | [[Economy System]], [[National State]] |

## Why plain public fields everywhere
Every `*State`/`*System` class exposes its fields directly rather than through properties or a
repository layer. This is what makes [[Save Load]] trivial: `JsonConvert.SerializeObject` on the
whole graph round-trips exactly, with no custom (de)serialization code per system. The tradeoff
(no encapsulation, anyone can mutate anything) is accepted deliberately for a single-player game
with one save slot.
