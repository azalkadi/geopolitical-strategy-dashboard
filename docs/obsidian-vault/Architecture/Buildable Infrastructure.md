---
tags: [architecture, sim, map]
---

# Buildable Infrastructure

`Assets/Scripts/Sim/Infrastructure.cs` — the only system where the player adds permanent new
*geometry* to the map, not just numbers. Lets the player connect two of their own cities with a
new road or railway, live, mid-game: the UI lives in `GameUIRoot.DrawInfrastructureBuilder`
(Budget tab, own country only), the mesh lives in
[[Map Rendering|MapRenderer.RebuildPlayerInfrastructure]].

## Terrain-following routes
Routes are **not** straight lines — they follow the terrain, the way real highways and railways
do. `Map/TerrainRouter` runs A* pathfinding between the two cities over a cost field sampled from
the satellite basemap (`MapRenderer.SatelliteTexture`): open water reads dark-blue and costs a
lot (bridges/tunnels), bright snow/rock/desert reads costly (mountain grades), normal land is
cheap. The path bends to minimise terrain-weighted distance, so a route skirts a gulf or detours
around a mountain block instead of cutting across it — verified live: Istanbul→Ankara routes
+37km (350→387) around the Gulf of Izmit, a realistic ~10% detour. **Railways are penalised
harder than roads** on both water and gradient (a train can't climb or ford what a road can), so
rail takes a different, often longer path. No elevation dataset exists in the project; brightness
is the honest mountain proxy — strong for snow-capped/desert ranges (Himalaya, Andes, Alps,
Rockies), weaker for green low mountains. Two returned lengths: `GeometricKm` (real length shown
to the player) and `WeightedKm` (terrain-weighted, drives cost + build time).

## The loop
1. **Pick two cities.** `DrawInfrastructureBuilder` lists every city in the player's own country
   (`City.Country == countryName`) in two `DropdownField`s, biggest population first.
2. **See the cost live.** `MapRenderer.RouteBetween` plans the terrain-following route (separately
   for road and rail, since their paths differ). `WeightedKm` drives `EstimateCost` (road:
   `$0.03B/km`, railway: `×2.5`, `$0.5B` floor) and `EstimateDays` (`0.12 days/km`, `20`-day
   floor), so a mountain/water crossing genuinely costs more and takes longer; `GeometricKm` is
   the length shown. The estimate flags "routes around rough terrain" when the path bends
   significantly.
3. **Build it.** `InfrastructureSystem.Begin` charges the treasury immediately (same unconditional
   spend pattern as `DiplomacySystem.SendAid` — going into debt over it is the player's call, same
   as every other lever) and books a `BuiltRoute` with a `CompletionDay`.
4. **Wait.** `MapInteraction.TickEconomy`'s daily loop calls `InfrastructureSystem.TickAll(day)`
   every simulated day. A route whose day has arrived flips `Completed`, and only newly-completed
   routes get returned — the caller toasts each one via [[History and World Feed|WorldFeed]] and
   calls `MapRenderer.RebuildPlayerInfrastructure()` exactly once per completion, not every tick.
5. **See it on the map.** `RebuildPlayerInfrastructure` throws away the whole `PlayerInfrastructure`
   GameObject and rebuilds it from every `Completed` route in `Infrastructure.Routes` — cheap at
   the scale one game produces (dozens of routes, not thousands), so there's no incremental-append
   complexity. Reuses the same casing+core two-pass mesh helper (`BuildLineMesh`) the natural
   roads/railways layer uses (see [[Map Rendering]]), just slightly bolder and drawn a hair closer
   to the camera so a player's own construction visibly reads as *theirs*.

## Persistence
`BuiltRoute`/`InfrastructureSystem` are plain public fields (same rule as every other `Sim/`
class — see [[Simulation Overview]]) so [[Save Load]] serializes them for free. The one exception
is `BuiltRoute.PathMercator` (the terrain path geometry), marked `[JsonIgnore]` — Unity's
`Vector2` has recursive computed properties (`normalized`/`magnitude`) that break Newtonsoft, and
the A* path is deterministic anyway, so `MapRenderer.ApplySave` **re-plans every route's path from
its city endpoints on load** rather than serializing it. Saves written before this system existed
deserialize `Infrastructure` as `null` — `ApplySave` falls back to a fresh empty
`InfrastructureSystem()`, so a loaded pre-existing save just shows no player-built routes rather
than crashing.

## Verifying it (no pixel-clicking required)
`MERIDIAN_DIAG_INFRA=1` books a road between the player's two biggest own cities at day 30, then
watches every tick for it to actually complete and produce mesh geometry — logs
`[infradiag] Road construction begun: ... treasury X->Y` at booking and
`[infradiag] route completed day N (scheduled N) playerInfraMeshChildren=M` once it finishes,
so the whole loop (cost/duration math, the daily completion tick, the mesh rebuild) is
verifiable from `Player.log` alone. Combine with `MERIDIAN_DIAG_SAVE=1` to also confirm an
in-progress (not-yet-completed) route survives a save/load roundtrip.

## What this deliberately doesn't do (yet)
- Doesn't snap to the *real* road/rail network (`ne_10m_roads_extended` — already loaded): the
  most authentic "follow terrain" would route a new highway along the actual road corridors that
  real engineers surveyed. That's a graph-pathfinding project of its own; the basemap-cost A*
  here is a self-contained first version that already bends around water and bright terrain.
- No ongoing gameplay bonus — a completed route is currently a purely visual/flavor achievement
  (see [[Development Roadmap.canvas]] stage 5 for the "finishing pass" this could feed into,
  e.g. a small trade/growth bonus for connected city pairs).
- Own-country only — no cross-border construction.
