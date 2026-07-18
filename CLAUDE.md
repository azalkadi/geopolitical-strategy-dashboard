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

## Session handoff — update this file every session

The user works on this repo from **two computers** with two separate Claude Code sessions, and
switches between them without warning. **After every programming session (any session that
changes code, not just docs), update the "Current status" section at the bottom of this file**
with what actually shipped and what's next — that's the mechanism that lets a fresh session on
the other machine pick up with full context instead of re-discovering state from `git log`.
Keep it a short factual delta, not a changelog essay. Also always `git fetch origin` and check
for remote commits before starting local work (see the merge note in Current status below for
why this matters — the other machine's session can and does land commits between your runs).
The [[Development Roadmap.canvas]] in `docs/obsidian-vault/` tracks the big chronological
stages; this file's Current status section tracks the finer-grained "since last session" delta.

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
- Camera is now hard-clamped on both axes (`MapCameraController.ClampPosition`/`ClampAxis`) so
  the map can never scroll/zoom out past the world edge on any aspect ratio or zoom level; the
  old single-axis `latitudeClamp` field is gone.
- Economy depth: `Population`/`PopulationGrowth` (driven by `PublicMood` + healthcare spend in
  `NationalState.Tick`), `GdpPerCapita`, credit rating tiers AAA→C off `DebtToGdp` with a
  `CreditRiskPremium` that compounds into `AnnualDebtService`, negative-treasury interest
  drain. `MapRenderer.ApplySave` reseeds `Population` for saves written before this existed
  (would otherwise deserialize to 0 and simulate a permanently empty world).
- UI overhaul across `GameUIRoot.cs`: side-panel content grouped into `StartCard()`/`EndCard()`
  card sections instead of a flat stat list, 0–100 indices render as `StatBar` progress bars,
  sparklines are shaded area charts with a current-value dot, real calendar dates
  (`DateString`, epoch 2026-01-01) instead of "Day N", severity-colored toasts, and a start-
  screen country **preview card** (click a nation → stats/flavor text → "TAKE OFFICE" — this
  changed prior behavior where clicking a country began the game immediately).
- Relation-based map coloring (landed on the work-side session, `MapRenderer.RefreshCountryColors`):
  once a game has started, Political-mode country fills and Satellite-mode country borders are
  colored by `DiplomacySystem.GetRelation(playerIndex, i)` (hostile→neutral→friendly gradient),
  with the player's own country always getting a fixed highlight color; falls back to the
  original hash-based "no two neighbors share a tint" palette before a game starts / after
  "PLAY AGAIN". **Country flags are still not implemented** — next up.
- `docs/obsidian-vault/` — a full Obsidian vault documenting architecture, game design, and
  data sources (also landed on the work-side session), plus a new
  `Development Roadmap.canvas` (this session) laying out the 7 build stages from foundation
  through a tagged v1.0.
- **Merge note:** this session's local work (camera clamp, UI overhaul, economy depth) was
  started before fetching 16 commits that had landed on `origin/master` from the work-side
  session (the vault + relation coloring above). Reconciled via `git stash` → fast-forward to
  `origin/master` → `git stash pop`, which produced exactly one real conflict — both sides
  inserted a line immediately after `Wars = save.Wars;` in `MapRenderer.ApplySave`. Resolved by
  keeping both (population migration guard, then `RefreshCountryColors()`). This is the
  scenario the "Session handoff" section above exists to prevent — always fetch first.
- **Stage 4 (roadmap) — 3 of 4 done, same session as the review pass below:** country flags,
  casing-style road/rail visuals, and player-buildable infrastructure all shipped — see the new
  session entry below. Still open: deeper war/attacking-other-countries expansion.
- **Post-merge review pass (same session):** ran an 8-angle automated review over the merged
  diff (most verify sub-agents hit a session usage cap, so findings were hand-verified against
  the actual code instead). Confirmed and fixed 5 real bugs: (1) `MapCameraController` — with
  `maxOrthoSize` tightened to exactly `MapExtent` (180), the very first `ClampPosition()` call
  forced the camera to `(0,0)` on every launch regardless of the `(10,20)` start position set in
  `Awake()`/`Bootstrap.cs` — both now start at `(0,0)` to match what the clamp enforces anyway,
  instead of setting a value that was always immediately discarded. (2) `Economy.Tick()` — a
  same-day credit-rating change could get silently overwritten by the recession/unemployment/
  inflation `LastWhy` messages below it, losing the toast permanently since `LastCreditRating`
  had already advanced; folded the rating message into the same if/else-if cascade so exactly
  one message wins per tick, rating given priority. (3) `MapRenderer.ApplySave`'s population
  migration guard didn't reset `LastCreditRating` for pre-existing saves, so loading an old save
  with real debt could fire a spurious "credit rating downgraded" toast on its first tick — now
  baselines it alongside Population. (4) `GameUIRoot.StatBar` (Approval, Readiness, Standing,
  Mood, Innovation, bilateral Relationship) had silently dropped the green/red good-vs-bad fill
  coloring the old `StatColored` rows had — added a `good` predicate back, live-updated every
  refresh tick same as `LiveStat`. (5) The start-screen `previewCard` wasn't hidden/cleared on
  PLAY AGAIN, so a stale country preview + TAKE OFFICE button could linger after a completed
  game. Also deduped `Economy.Tick()`'s inline debt-service formula to call the
  `AnnualDebtService` property instead of re-deriving it. Rebuilt, relaunched, Player.log clean
  (no errors/exceptions), user confirmed the live build looks right. Two minor non-behavioral
  reuse findings (duplicated population-floor logic across 3 call sites; `Diplomacy.
  AgreementPartnersOf` hand-unpacking `PairKey`'s bit layout instead of a shared helper) were
  left as-is — cosmetic, no observed failure mode, low priority next to the roadmap's stage 4.
- **Stage 4 features (same session, after the fixes above):**
  - **Country flags**: `Assets/StreamingAssets/flags/{iso_a2}.png` — 237 small PNGs downloaded
    from flagcdn.com (see `docs/obsidian-vault/Data Sources/Country Flags.md` for the sourcing/
    override details), loaded and cached by the new `FlagLoader.cs`. Shown in the side panel
    header, the start-screen country preview card, and every row of the start-screen country list.
  - **Casing-style road/rail rendering**: `MapRenderer.cs`'s road/rail builders now do a two-pass
    "casing" (a wider, dark base layer) + "core" (the existing bright color, on top) draw, the
    standard cartographic-depth trick — refactored the old single-mesh `BuildLineFeaturesRoot`/
    `BuildRailwaysRoot` into a shared `BuildLineMesh` helper reused by both passes (and by the
    new player-infrastructure layer below). Verified this doesn't break road/rail click-to-select
    first — `MapInteraction.TryPickRoad` hit-tests the raw `World.Roads` data, not the rendered
    mesh, so restructuring the mesh hierarchy was safe.
  - **Player-buildable infrastructure**: new `Sim/Infrastructure.cs` (`InfrastructureSystem`/
    `BuiltRoute`) — pick two of your own cities in the Budget tab (`GameUIRoot.
    DrawInfrastructureBuilder`), see a live distance/cost/duration estimate (real haversine
    distance off the cities' Mercator positions converted back via `GeoMath.MercatorToLonLat`),
    build a road or railway. Costs treasury immediately, takes real sim-days
    (`MapInteraction.TickEconomy`'s daily loop ticks it, toasts completions via WorldFeed, and
    calls the new `MapRenderer.RebuildPlayerInfrastructure()` — which reuses the casing/core
    mesh helper above). Fully wired into save/load (`SaveGame.Infrastructure`, null-safe migration
    for older saves). Verified end-to-end with a new `MERIDIAN_DIAG_INFRA=1` diagnostic (mirrors
    the diplomacy/war diag pattern): a real run booked a 511km Berlin→Stuttgart road ($15.3B,
    61-day build), and it completed exactly on schedule (day 91) with real mesh geometry
    produced (`playerInfraMeshChildren=2`). A second run combined with `MERIDIAN_DIAG_SAVE=1`
    confirmed an in-progress (not yet completed) route survives a save/load roundtrip.
  - Obsidian vault updated to match: new `Architecture/Buildable Infrastructure.md` and
    `Data Sources/Country Flags.md` pages, `Development Roadmap.canvas` stage 4 marked 3-of-4
    done, `Simulation Overview.md`/`Meridian.md` cross-links added.
  - Full headless compile + player build clean throughout; not yet visually screenshot-checked
    (this dev machine's automation clicks are unreliable — see the hardware-flakiness note — so
    verification leaned on Player.log diagnostics instead, same as everything else this session).
- **Vision capture + first realism slice (same session, immediately after):** the player gave a
  long, comprehensive vision for how deep Meridian should go politically/economically/militarily
  — real governments and legislatures with named parties, real tax data, sector/company-level
  economy, the real 2026 conflict map + terrorism + military realism, supranational unions
  (EU/GCC/UN), and a long list of map/UI realism asks — and pushed back that the roadmap's
  "Done" labels on stages 1-3 overclaimed depth (fair; relabeled to "Core built"). All of it is
  captured in `docs/obsidian-vault/Vision/` (a Vision Overview + 6 pillar pages), tracked as
  Tasks #20-25, and the roadmap has a new stage 4.5 for it. Started on the highest-leverage
  piece — regime type gates almost everything else:
  - New `Sim/CountryProfiles.cs` — real `GovernmentType` (absolute/constitutional monarchy,
    presidential/parliamentary republic, one-party state) + real headline tax rates (VAT/
    corporate with high confidence; income tax as an honest single-lever approximation of real
    progressive systems, documented as such) for ~35 major/well-known countries, keyed by ISO
    A3. Not exhaustive — same "hand-researched, not a formula" honesty as the existing Curated
    Datasets. `EconomyState.Seed`/`NationalSystem.Seed` (now takes the country list, not just a
    count) apply it automatically; unlisted countries keep the old generic defaults.
  - `GameUIRoot.AddSlider` — every slider (tax, spending, interest rate, all of them) now uses
    a `FloatField` instead of a plain label, so the player can click the value and type an exact
    number instead of only drag-approximating one. Added focus-tracking (`SliderBinding.
    Editing`) so the 100ms live-refresh tick doesn't clobber a value mid-keystroke.
  - Politics tab shows the country's `GovernmentType` (honestly labeled "Unclassified (not yet
    researched)" for the ~220 countries without curated data yet, same pattern as Economy's
    "(placeholder baseline)" tag); tax section shows a note when rates came from real data.
  - Verified live: starting as Saudi Arabia produces `taxes=[income=0.0 corp=20.0 vat=15.0
    tariff=5.0]` in the existing `[econdiag]` log line — exactly the real curated profile.
  - **This is a first slice, not the full pillar** — the actual bill-proposal/vote/decree
    pipeline and real named political parties are still open (Task #20). Committed docs and
    this slice separately (`39eeb43` docs-only, code commit follows) so the vision capture
    survives even if the code needs iteration.
- **Legislature & bills shipped (next session block, after a vault audit):** the pillar's core
  mechanic is now live.
  - Vault audit first (player asked): fixed 13 dead line-wrapped wikilinks across the vault
    (Obsidian can't resolve a link containing a newline — they were silently broken in-app),
    zero broken links/orphans remain; propagated the player's correction that named examples
    (EU/GCC/UN, F-35s, oil/fruit, the four conflicts) are patterns to generalize, not
    exhaustive lists — Vision Overview now states this as a reading rule.
  - New `Sim/Legislature.cs` — bill pipeline: player proposes a tax change (4 core levers; the
    interest rate deliberately stays a direct lever — central banks aren't legislatures).
    Countries WITH curated real parties (`CountryProfiles.Parties`, ~20 multi-party countries
    with real named parties, econ lean, approximate mid-2020s seat shares) → 14-day
    parliamentary fight: stances taken at proposal from ideology (left backs raises, right
    backs cuts, deterministic per-bill wrinkle so centrists swing), weighted by seats, >50%
    passes. Countries WITHOUT parties (monarchies, one-party, uncurated) → decree: auto-enacts
    after 5 days, flavored by government type. Headlines for proposal/fight/outcome via
    WorldFeed ("Democrats back it; Republicans vow to fight it").
  - UI: own-country tax rows are now propose-a-bill fields (pending bills show status + decision
    date instead of an input; one open bill per lever); Politics tab gained PARLIAMENT (party
    composition with lean labels + seat shares, any curated country) and BILLS (player's docket,
    For/Against lists on pending votes) cards; a billsStamp forces the structural rebuild the
    day a bill resolves. Foreign countries keep the old direct sandbox sliders.
  - Save/load: `SaveGame.Legislature` + null-fallback for older saves, same pattern as
    Infrastructure.
  - **Verified both paths live** via new `MERIDIAN_DIAG_BILLS=1`: USA corporate-tax raise —
    Democrats (49%) FOR, Republicans (51%) AGAINST, defeated 49–51, rate correctly unchanged;
    Saudi Arabia — royal decree drafted, enacted automatically on day 5, 20%→24% correctly
    applied. New vault page `Architecture/Legislature and Bills.md` documents it all.
  - Still open in the pillar (Task #20): AI countries legislating, elections reshuffling seat
    shares, lobbying, bill scope beyond tax law (spending/freedoms/regime change), per-party
    approval.
- **Freedoms extended into the bill pipeline (next block — "dev based on Feature Relationships",
  i.e. build the next connected node on the canvas):** `NationalState` gained `FreedomSpeech`/
  `FreedomReligion`/`FreedomInternet` (0-100, seeded from a `GovernmentType` bucket heuristic —
  monarchies/one-party lower, established republics higher — not per-country researched data).
  `BillKind` extended with the three freedom kinds; `Legislature.Apply` now branches on
  `Bill.IsFreedom` to write to `NationalState` instead of `EconomyState`, and **tightening a
  freedom costs `InternationalStanding` on enactment, loosening earns a little back**
  (asymmetric on purpose) — the real international-reaction requirement from the Vision page,
  not just a number. Party voting on freedom bills reuses the same `EconLean` axis as tax bills
  (left backs expanding, right backs tightening) as a documented coarse proxy for a social axis
  not yet curated separately. New Politics › FREEDOMS card (`GameUIRoot.DrawFreedoms`) reuses
  the generic `DrawTaxLever` propose-a-bill UX for any `BillKind`, not just taxes; foreign
  countries see freedoms read-only.
  - `MERIDIAN_DIAG_BILLS=1` now runs a second phase after the tax bill resolves: proposes a
    freedom-of-speech tightening bill and logs the standing delta. **Verified live as USA**:
    Republicans (51% seats) backed tightening, Democrats opposed — passed 51–49, standing
    dropped 56.9→53.2 on enactment, exactly matching the ideology model and the standing-
    consequence design.
  - Canvas updated: `v_bills`/`v_freedoms` flipped to ✅ Built in Feature Relationships.canvas,
    the standing-consequence edge annotated with the verified numbers.
  - Still open: spending/regime-change bill scope, AI legislating, elections reshuffling seats,
    a real social-ideology axis, per-country freedom research.
- **Regime change shipped (same "continue" — next connected node, Bills → Regime Change):**
  new `BillKind.RegimeChange`. Deliberately special-cased vs. tax/freedom bills — always
  bypasses the party vote (`LegislatureSystem.ProposeRegimeChange`, not `Propose`; a
  legislature doesn't get to vote itself out of existence, this is the player unilaterally
  driving a constitutional transition) and runs its own 45-day timer (`RegimeChangeDays`) vs.
  the ordinary 5-day decree. Standing consequence keyed on a pluralism axis (`IsPluralistic`:
  constitutional monarchy + both republic types count, absolute monarchy + one-party don't) —
  losing real pluralism costs -25, gaining it earns +12, a same-category swap costs -3 — the
  concrete implementation of "the sim shouldn't assume monarchy=bad, democracy=good": it reacts
  to the structural fact of the change, not a scripted verdict, and post-transition stability
  still runs on the ordinary approval/mood/economy numbers like every other country. New
  Politics › CHANGE GOVERNMENT card (`GameUIRoot.DrawRegimeChange`, own country only):
  government-type dropdown + BEGIN TRANSITION button, or the pending transition's target/ETA.
  - `MERIDIAN_DIAG_BILLS=1` extended with a third phase. **Verified live as USA**: proposed
    USA→OneServiceState, zero party stances logged (confirmed always-decree), ran the full
    45-day timer (day 48→93), correctly set `Government = OneServiceState`, standing dropped
    53.2→36.0 across the transition window.
  - Canvas: `v_regime` flipped to ✅ Built; the `v_regime→w1 (World AI)` edge explicitly kept
    ⚪ open — regime change only moves the standing number today, it doesn't yet shock
    [[Diplomacy System|bilateral relations]] or trigger an actual [[World AI]] reaction.
  - Still open in the pillar: spending-bill scope, AI countries legislating/regime-changing,
    elections reshuffling seats, a real social-ideology axis, per-country freedom research,
    regime change reaching Diplomacy/World AI instead of just InternationalStanding.
