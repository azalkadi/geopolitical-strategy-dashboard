# Meridian — guide for Claude

This file is for a fresh Claude Code session opening this project cold (e.g. on a different
computer). Read this before touching anything.

## What this is

"Meridian" is a single-player geopolitical strategy game: pick a country, govern it, watch an
economy simulation run, manage ministries (Economy, Budget, Trade, Politics, Military,
Diplomacy, Society, Technology) via a UI, win/lose elections based on approval rating.

It is a **Unity/C# port of an original Rust/Bevy game**. The Rust original still exists,
untouched, at `..\Geo political\` (one level up from this folder) — it still builds and is the
reference for "what should this feature actually do" if a ported behavior looks wrong. Do not
touch it; it's kept around purely as a reference.

The user (owner of this repo) is non-technical — they don't write code themselves and drive
this entirely through Claude. They also said explicitly at one point: **don't do work beyond
what's asked in a given message.** If a session surfaces something worth doing that wasn't
requested, mention it and ask, don't just do it.

## Running it

Unity Editor **6000.5.3f1** (Unity 6.5) is the target, installed via Unity Hub with an
activated free Personal license (already signed in — batchmode licensing works with no further
setup).

**Headless build-fix loop** (works without opening the Unity Editor UI at all):

```
powershell -ExecutionPolicy Bypass -File "Tools\build.ps1" -Mode compile
```

Drives Unity in batchmode, compiles all scripts, writes `Tools\last_build.log`. Read that log:
- `[headlessbuild] OK: all scripts compiled` → clean.
- `error CS####` → fix the C# and re-run.
- Licensing errors → user needs to sign into Unity Hub (should already be done, but if it
  regresses, that's on the user, not something you can fix from here).

```
powershell -ExecutionPolicy Bypass -File "Tools\build.ps1" -Mode build
```

Does a full Windows player build to `Build\Windows\Meridian.exe`. Iterate compile → fix →
compile until clean, *then* do a full build — don't full-build on every tiny change, it's
slower.

To actually run and eyeball the game, launch `Build\Windows\Meridian.exe` directly (`start ""
"Build\Windows\Meridian.exe"` from Bash, or just open it). Runtime logs (including
`Debug.Log`/`Debug.LogError` output) land in
`C:\Users\PC\AppData\LocalLow\DefaultCompany\MeridianUnity\Player.log` — **always check this
after a run**, not just whether the build succeeded. A clean compile and a clean build can
still crash or silently misbehave at runtime; grep this log for exceptions, and for `[map]`
lines specifically (every major subsystem logs a one-line summary on load — country/city
counts, marker counts, basemap resolution — which is the fastest way to confirm something
loaded correctly without eyeballing the map).

If multiple `Meridian.exe` processes end up running at once (common if you relaunch without
killing the old one first) you'll get confusing overlapping-window behavior. Kill stragglers
first: `powershell -Command "Get-Process -Name Meridian -ErrorAction SilentlyContinue | Stop-Process -Force"`.

## Layout

- `Assets/Scripts/Geo/` — data model (`GeoModel.cs`) and GeoJSON loader (`GeoJsonLoader.cs`).
  Everything geographic (countries, provinces, cities, ports, airports, roads, railways, air
  bases, oil ports, nuclear plants) is parsed here from the files in
  `Assets/StreamingAssets/worlddata/`.
- `Assets/Scripts/Map/` — rendering and interaction. `MapRenderer.cs` builds all the meshes
  (country fills, borders, provinces, cities, roads/rail, infrastructure markers, the satellite
  background). `MapCameraController.cs` is pan/zoom. `MapInteraction.cs` is click-to-select +
  the sim clock + election mechanic. `MapLayers.cs` is zoom-gated LOD (what's visible at what
  zoom, and city/infrastructure name labels via `OnGUI`). `SatelliteTileLoader.cs` is live
  satellite tile streaming (see "Satellite imagery" below — **unverified**, see Status).
  `Bootstrap.cs` spawns everything at Play with zero manual scene setup.
- `Assets/Scripts/Sim/` — the economy simulation (`Economy.cs`) and player/election state.
- `Assets/Scripts/UI/` — the whole HUD, built entirely in C# via UI Toolkit
  (`UIDocument`/`VisualElement`), no `.uxml`/`.uss` assets, no UI Builder. `GameUIRoot.cs` is
  the single largest file and owns the top bar, ministry bar, side panel, start/game-over
  screens, toasts. `GameTheme.cs` is the color palette (institutional "UN Security Council"
  look — deep navy, gold accents). `NationCategory.cs` defines the 8 ministries and their
  per-category accent colors and hover-dropdown topics.
- `Assets/StreamingAssets/worlddata/` — GeoJSON data files, mostly Natural Earth 10m. Read raw
  at runtime (not Unity-imported/compressed), see "Data sources" below for where each came
  from and how big it is.
- `Assets/StreamingAssets/basemap/satellite.jpg` — the offline whole-world background image
  (NASA Blue Marble). See "Satellite imagery" below.
- `Assets/Editor/HeadlessBuild.cs` — the batchmode build entry points `Tools/build.ps1` calls
  into. `Assets/Editor/EnsurePanelSettingsAsset.cs` creates the UI Toolkit `PanelSettings`
  asset the first time (needed for text rendering to work at all in a build — see gotchas).

## Architecture notes worth knowing before you change anything

**Projection: everything is Web Mercator, not raw lon/lat.** `GeoJsonLoader` reprojects every
single `[lon, lat]` pair through `GeoMath.LonLatToMercator` the moment it's parsed — country
polygons, province polygons, cities, ports, airports, roads, railways, all of it. `GeoWorld`
never stores raw lon/lat, only this projected space. This was a significant, project-wide
change (see Status — not yet runtime-verified) made specifically so real map tiles (which are
always Web Mercator) line up with the vector data. The projection is "degrees-normalized": x is
still plain longitude (-180..180), y follows the real Mercator curve but scaled so it also
lands in the same numeric range (±180 at the standard 85.0511° polar cutoff) instead of
real-world meters — chosen so existing world-unit-based constants (camera zoom range, zoom-gate
thresholds in `MapLayers`, marker pixel radii) didn't all need re-tuning. If you ever add a new
geo data loader or point-conversion path, route it through `GeoMath.LonLatToMercator` too, or
it won't align with everything else.

**Z-ordering convention:** more negative Z = closer to camera = drawn on top. Order from back
to front: satellite background (+0.05) → live satellite tiles (+0.02) → country fills (0) →
provinces (-0.05) → roads (-0.11) → railways (-0.12) → country borders (-0.1) → cities (-0.2).
(Yes borders and roads/rail overlap in that ordering somewhat — check the actual constants in
`MapRenderer.cs` before assuming, this list is illustrative not authoritative.)

**Satellite imagery has two layers:**
1. A static, bundled, always-available whole-world JPEG (`satellite.jpg`, currently NASA Blue
   Marble at 16384×8192 — see "Data sources" for why that exact size). Rendered as a
   vertically-subdivided strip mesh (not a flat quad) so its rows land at the correct
   nonlinear Mercator Y per latitude — longitude needs no subdivision since it's linear in both
   projections.
2. `SatelliteTileLoader.cs` — live, zoom-dependent tile streaming from ESRI's public World
   Imagery service (no API key, works down to city-block detail, verified via curl up to
   z=18), layered on top once zoomed in past `activateBelowOrthoSize` (default 10). This is
   what actually fixes "zoomed in looks pixelated" — a single static texture has a hard
   resolution ceiling no matter how big you make the file; real tile streaming doesn't.
   Requires an internet connection to show anything; if a fetch fails it just silently leaves
   the static background visible underneath, no error state. **This subsystem compiled clean
   but was not yet confirmed working at runtime as of the last session — see Status.**

**UI Toolkit gotchas already hit once, don't repeat them:**
- Any full-screen-or-large `VisualElement` with default `PickingMode.Position` sitting on top
  of other interactive elements in the visual tree order will silently eat their clicks with
  zero error. This exact bug took a long debugging session to find once (the ministry-bar
  hover-dropdown container). If a button "doesn't work" and its code looks fine, suspect a
  z-order/picking issue before suspecting the button itself — add a `TrickleDown.TrickleDown`
  `PointerDownEvent` logger on `root` that prints `evt.target`, that's the fastest way to see
  what's actually eating the click.
- Don't animate `style.scale` on a container that has interactive descendants — runtime UI
  Toolkit picking isn't guaranteed to correctly re-project pointer coordinates through an
  ancestor's transform. Use opacity for pop-in/feedback animations instead.
- A `PanelSettings` asset (not a runtime-created instance) is required for text to render at
  all in a build — `EnsurePanelSettingsAsset.cs` handles this, don't delete
  `Assets/Resources/GamePanelSettings.asset`.

**Shader stripping:** custom shaders only referenced via `Shader.Find` at runtime get stripped
from standalone builds unless explicitly protected. `HeadlessBuild.cs` force-adds them to
GraphicsSettings' Always Included Shaders before building. If you add a new custom shader, it
needs the same treatment or it'll compile fine and then throw `ArgumentNullException` at
runtime in a build only (works fine in-editor, which makes this an easy trap to miss).

**Texture size limits:** `SystemInfo.maxTextureSize` is a real per-device ceiling (this dev
machine caps at 16384). Uploading a texture bigger than that doesn't throw a C# exception — 
Unity silently swaps in an 8×8 placeholder and only logs a native-side warning to Player.log
that's easy to miss (`Texture has out of range width...`). `MapRenderer.BuildSatelliteQuad`
now has an explicit `Debug.LogError` guard for this after getting bitten by it once (a fully
black map with zero managed-side error). Keep that guard if you touch this code.

**Unity built-in modules aren't all enabled by default here.** This project's
`Packages/manifest.json` only lists what's actually used — `UnityWebRequest`/
`UnityWebRequestTexture` had to be explicitly added (`com.unity.modules.unitywebrequest`,
`com.unity.modules.unitywebrequesttexture`) when the satellite tile loader needed them; they
don't come for free just because they're built into the engine binary. If a new feature needs
another built-in Unity module and you get `CS0103: The name '...' does not exist`, check
`Packages/manifest.json` before assuming it's a real bug.

## Data sources (StreamingAssets/worlddata + basemap)

All from Natural Earth (public domain) unless noted, either bundled directly or filtered down:

- Countries, provinces, cities, ports, airports: `ne_10m_admin_0_countries`,
  `ne_10m_admin_1_states_provinces`, `ne_10m_populated_places`, `ne_10m_ports`,
  `ne_10m_airports` — straight from Natural Earth's official GeoJSON distribution.
- Roads/railways (`ne_10m_roads_major.geojson`, `ne_10m_railroads_major.geojson`): Natural
  Earth only ships these as shapefiles officially; pulled pre-converted GeoJSON from
  `raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/` and filtered to the
  world-scale subset (roads: `min_zoom<=3`, ~10k of 56k total features; railways:
  `scalerank<=4`, ~2.8k of 25k) — full detail is far too dense to matter at country/continent
  zoom and would bloat load time for nothing.
- Air bases (`ne_10m_airbases_military.geojson`): not a separate dataset — filtered out of the
  existing airports file by its own `type` property containing "military" (44 features).
- Oil ports and nuclear plants (`ne_10m_oilports.geojson`, `ne_10m_nuclearplants.geojson`): no
  public bundled dataset exists for either. **Hand-curated, not exhaustive.** 28 major oil
  export/import terminals and 56 major nuclear power plants — real, well-known facilities with
  approximate (not survey-precise) coordinates, picked for global spread and confidence in
  both existence and rough location. Nuclear alone has ~440 real reactors worldwide; this is
  the notable/major subset, not a complete registry. If this ever needs to be more complete,
  that's a real data-sourcing task, not a quick edit.
- Satellite basemap (`basemap/satellite.jpg`): NASA's "Blue Marble: Next Generation" cloud-free
  composite, public domain, from `eoimages.gsfc.nasa.gov/images/imagerecords/57000/57752/`
  (`land_shallow_topo` series). Currently 16384×8192 — the largest size that fits under this
  dev machine's `SystemInfo.maxTextureSize` (21600px, the next size up, silently fails — see
  gotchas above). If targeting different hardware, re-derive from the same NASA source at
  whatever the real ceiling is, don't just guess a bigger number.

## Save/load

`Sim/SaveLoad.cs` snapshots the ENTIRE mutable simulation (economies, national indices,
diplomacy, wars, player/election state, event schedule, history charts) as one JSON file at
`persistentDataPath/meridian_save.json`. Geography is never saved — it reloads from
StreamingAssets and the save refuses to load against different geo data (country-count key).
Sim classes keep all state in PUBLIC members specifically so this can be a dumb complete
Newtonsoft dump — if you add sim state, make it public or it will silently not persist.
SAVE button in top bar; autosave on quit (note: killing the process skips OnApplicationQuit);
CONTINUE on the start screen. Dev verification: `MERIDIAN_DIAG_SAVE=1` (save at day 60 +
in-process roundtrip check) then relaunch with `MERIDIAN_LOADSAVE=1` (auto-continue at boot).

## Gameplay systems (Sim/)

- `Economy.cs` — per-country ticking economy (taxes, custom taxes, interest, trade, treasury)
  plus adjustable budget spending levers (education/healthcare/infrastructure, % of GDP) that
  feed growth/innovation/mood honestly. `TradeAgreementExportBonus` is set by diplomacy.
- `Events.cs` — decision events: every 150–360 sim days one fires for the player's country
  (11-event pool, some condition-gated e.g. banking crisis only during weak growth). The sim
  clock FREEZES while one is pending (`MapInteraction` checks `EventSystem.Pending`) and game
  speed drops to 1x when one fires. UI modal lives in `GameUIRoot.BuildEventModal`.
- `Diplomacy.cs` — symmetric relations matrix for all country pairs, geography-seeded
  (same continent/subregion bonus + deterministic per-pair noise), slow drift to baseline.
  Player actions: aid / trade agreement (needs 65+ relations, permanent export boost both
  sides) / denounce, with a 90-day per-country cooldown. UI in `GameUIRoot.DrawDiplomacy` —
  bilateral view when a foreign country is selected, overview otherwise.
- All three were verified in a real build: events fire/pause/resume correctly, and a scripted
  self-test (`MERIDIAN_DIAG_DIPLOMACY=1` env var + `MERIDIAN_AUTOSTART=<country>`) exercises
  aid/agreement/denounce/cooldown and logs exact before/after numbers to Player.log.

## ⚠ This dev machine has flaky hardware (probably RAM)

Across many runs, GeoJSON parsing randomly fails on DIFFERENT files with DIFFERENT exception
types (`InvalidCastException` in railways, `IndexOutOfRangeException` in provinces,
`JsonReaderException` in countries with a literal corrupted character inside a number), while
the exact same files parse fine on the next run and never change on disk. That pattern is
data corruption in memory, not a code bug. Mitigations already in place: `GeoJsonLoader`
loads every dataset through `SafeLoad` (3 attempts, then continue without that layer instead
of black-screening). If you see a `[geo] ... attempt N failed` warning in Player.log, that's
this. The user should ideally run a memtest / disable XMP to confirm — mention it if flakiness
shows up again, but don't burn a session chasing it as a software bug.

Related: automation (computer-use) clicks on this machine intermittently land on the wrong
UI element — see the memory note on batched clicks, and prefer Player.log-based verification
(env-var diagnostics like the diplomacy self-test) over pixel-clicking for anything precise.

## Current status (as of the last worked session)

Everything below is built, launched, and verified via Player.log + visual checks:
- Web Mercator projection across all layers; vector/imagery alignment confirmed from world
  view down to city zoom.
- Live satellite tile streaming (ESRI World Imagery) — verified fetching and aligning down to
  Paris-metro detail.
- Roads/railways extended datasets (~34k/9k features) with constant-pixel-width ScreenLine
  rendering, 181 computed border crossings, 6 hand-curated water crossings, click-to-select
  feature info panel (`FeaturePanel.cs`) — this arrived from the work-side session and was
  merged with the gameplay systems.
- Decision events, diplomacy, budget levers (see Gameplay systems above).
- City click-picking is gated by tier visibility (only clickable when its dot is visible at
  the current zoom) — without that, invisible towns swallowed every country click at world
  zoom.
