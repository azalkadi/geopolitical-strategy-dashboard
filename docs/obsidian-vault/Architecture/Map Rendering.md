---
tags: [architecture, map]
---

# Map Rendering

`Assets/Scripts/Map/MapRenderer.cs` (~870 lines) — builds every visual layer of the map **once**,
at `Start()`, as GPU meshes rather than re-projecting geometry every frame. Reads from
[[Geo Pipeline]]'s `GeoWorld`; gated for visibility by `MapLayers.cs`; hit-tested by
[[Camera and Input]].

## Z-ordering (back to front — more negative Z = closer to camera = on top)
Satellite background (+0.05) → live satellite tiles (+0.02, see [[Satellite Basemap]]) →
country fills (0) → provinces (−0.05) → roads (−0.11) → railways (−0.12) → country borders
(−0.1) → cities (−0.2). Check the actual `const float ...Z` fields in `MapRenderer.cs` before
assuming — this list is illustrative, not authoritative.

## Layers built in `Start()`
1. **Country fills** (`BuildCountryMeshes`) — one triangulated mesh per country. Colored either
   by a deterministic name-hash palette (pre-game) or by relation-to-player once a game has
   started — see [[Map Modes and Coloring]].
2. **Country borders** (`BuildCountryBorders`) — line-list meshes, one per country, always
   visible in both map modes (this is what carries relation coloring in Satellite mode, since
   there's no fill to look at there).
3. **Province borders** (`BuildProvinceBorders`) — 4,596 of them under one gated root; Unity
   frustum-culls the individual meshes once the root is active.
4. **City markers** (`BuildCityMarkers`) — one combined mesh per `CityTier`, so [[Camera and
   Input|MapLayers]] can reveal finer tiers as you zoom in.
5. **Satellite basemap quad** (`BuildSatelliteQuad`) — see [[Satellite Basemap]].
6. **Infrastructure markers** (`BuildInfrastructureMarkers`) — airports, ports, air bases, oil
   ports, nuclear plants. See [[Curated Datasets]].
7. **Roads and railways** (`BuildRoadsAndRailways`) — see "Line rendering" below.
8. **Border crossing markers** and **water crossings** (`BuildBorderCrossingMarkers`,
   `BuildWaterCrossings`) — see [[Curated Datasets]].

## Constant-screen-size markers: `ScreenDot.shader`
City/airport/port/etc. markers all use the same trick: every quad's 4 vertices sit at the
**same** world position (the marker's lon/lat) with a UV corner tag; the vertex shader
reconstructs the actual pixel-sized offset at draw time from `_PixelRadius` and the camera's
current `orthographicSize` (pushed in globally every frame by `MapLayers` as `_OrthoSize`). This
makes a dot the same size on screen at any zoom, instead of the world-space square it would be
with a fixed lon/lat half-size.

## Constant-screen-width lines: `ScreenLine.shader`
Roads/railways/water-crossings use the same idea for lines. **This replaced an earlier
world-space-width approach that looked fine at one zoom level but ballooned into multi-km-wide
blobs once zoomed in** (1 Mercator-degree-unit ≈ 111km at the equator, so even a "thin" 0.035-unit
half-width is ~4km wide in reality). Each line vertex stores its raw position plus an offset
**direction** in UV0; `ScreenLine.shader` scales that direction by the material's
`_PixelHalfWidth` and the current world-units-per-pixel every frame — so a road stays a constant
~3px wide regardless of zoom.

Geometry is built by `AppendQuad` (one rectangle per segment) plus `AppendJoint` (a small square
at every interior vertex, closing the gap a bend would otherwise leave — without it a line reads
as disconnected chunks rather than a continuous line).

### Railways specifically: solid line + cross-ties, not a dash pattern
An earlier version tried to visually distinguish railways from roads by punching gaps in the
line (a world-space dash/gap pattern). That fought badly with the pixel-constant width: a
fixed-world-length dash is a wildly different number of screen pixels at different zooms — either
a sub-pixel sliver (reads as noise) or a many-pixels-long, only ~2px-wide needle (reads as
broken). The fix: keep the rail line **fully solid** (same joined-quad technique as roads, so
bends stay continuous) and overlay regularly-spaced **cross-tie tick marks** (`AppendTies`/
`AppendTie`) — the standard cartographic railroad symbol — using larger multiples of the same
per-vertex UV-offset scheme, so ties scale together with the line body from one material.

## Consumers / dependents
- [[Camera and Input]] — `MapLayers` toggles these layer roots by zoom; `MapInteraction`
  hit-tests against the same geometry.
- [[Map Modes and Coloring]] — `SetMode`, `RefreshCountryColors`.
- [[Satellite Basemap]] — the static quad and live tile layer both live alongside these meshes.
