---
tags: [architecture]
---

# Code Architecture

Four folders under `Assets/Scripts/`, each with a distinct job. Data flows roughly
Geo → Map → Sim, with UI reading from all three.

```
Assets/Scripts/
├─ Geo/     data model + GeoJSON loading      → [[Geo Pipeline]]
├─ Map/     rendering, camera, interaction     → [[Map Rendering]], [[Camera and Input]]
├─ Sim/     economy/diplomacy/war/AI/save      → [[Simulation Overview]]
└─ UI/      the entire HUD (code-built)        → [[UI System]]
```

## Geo/
- `GeoModel.cs` — plain data classes (`Country`, `Province`, `City`, `PointFeature`,
  `LineFeature`, `BorderCrossing`, `WaterCrossing`, `GeoWorld`) plus `GeoMath` (the Mercator
  projection and point-in-polygon tests). See [[Geo Pipeline]].
- `GeoJsonLoader.cs` — reads every `.geojson` file in `StreamingAssets/worlddata/` at runtime,
  reprojects every coordinate through `GeoMath.LonLatToMercator`, triangulates country/province
  polygons via a ported `Earcut.cs`, and computes border crossings. See [[Geo Pipeline]] and
  [[Natural Earth Datasets]].

## Map/
- `MapRenderer.cs` — the biggest file in this folder. Builds one Unity mesh per country/province,
  city/port/airport marker sets, roads/railways, water/border crossings, and the satellite
  basemap quad — all at `Start()`, once. See [[Map Rendering]].
- `MapLayers.cs` — zoom-gated visibility (which layer shows at which `orthographicSize`) plus
  `OnGUI` name labels for whatever's on screen.
- `MapCameraController.cs` — pan/zoom for the orthographic camera. See [[Camera and Input]].
- `MapInteraction.cs` — the sim clock lives here, plus click-to-select hit-testing (cities, roads,
  border crossings, water crossings, countries) and the election/term mechanic. See
  [[Camera and Input]] and [[Player State and Elections]].
- `SatelliteTileLoader.cs` — live ESRI tile streaming once zoomed in close. See
  [[Satellite Basemap]].
- `Bootstrap.cs` — spawns camera + renderer + interaction + layers + UI at Play with zero manual
  scene setup.

## Sim/
Nine small, mostly-independent systems ticked together once per simulated day — see
[[Simulation Overview]] for how they connect. All state is public fields specifically so
[[Save Load]] can serialize the whole simulation with a plain JSON dump.

## UI/
- `GameUIRoot.cs` — the entire HUD: top bar, ministry bar, side panel, toasts, start screen,
  decision-event modal, game-over screen. 1,700+ lines, hand-built with UI Toolkit
  `VisualElement`s (no UXML/USS, no built-in Button/Slider controls). See [[UI System]].
- `FeaturePanel.cs` — a separate small panel for clicked map features (city/road/border
  crossing/water crossing), independent of the country ministry panel.
- `GameTheme.cs`, `NationCategory.cs`, `UIState.cs` — shared color palette, the 8 ministry
  category definitions, and small shared UI state. See [[Ministries]].

## Build pipeline
Headless, no Editor UI required:
```
powershell -ExecutionPolicy Bypass -File "Tools\build.ps1" -Mode compile   # fast compile check
powershell -ExecutionPolicy Bypass -File "Tools\build.ps1" -Mode build     # full player build
```
Drives `Assets/Editor/HeadlessBuild.cs` in batchmode; writes `Tools\last_build.log`. Runtime logs
land in `%AppData%\..\LocalLow\DefaultCompany\MeridianUnity\Player.log` — always check this after
a run, since a clean build can still misbehave silently at runtime.

## Known project-specific gotchas
- **Picking-eating UI**: a full-screen `VisualElement` with default `PickingMode.Position`
  sitting above other interactive elements silently swallows their clicks. If a button "does
  nothing" and its code looks fine, suspect z-order/picking first.
- **Shader stripping**: custom shaders only referenced via `Shader.Find` at runtime get stripped
  from standalone builds unless added to Always Included Shaders in `GraphicsSettings.asset`
  (`HeadlessBuild.cs` does this for the built-in ones; a new custom shader needs the same
  treatment — see the three `Meridian/*` shaders already registered there).
- **Texture size ceiling**: `SystemInfo.maxTextureSize` is a real per-device cap; exceeding it
  silently swaps in an 8×8 placeholder with only a native-side log warning, not a C# exception.
- **Flaky hardware on the primary dev machine**: GeoJSON parsing has intermittently failed on
  different files with different exception types across otherwise-identical runs — suspected RAM
  issue, not a code bug. `GeoJsonLoader.SafeLoad` retries 3× per dataset before giving up on that
  layer rather than black-screening.
