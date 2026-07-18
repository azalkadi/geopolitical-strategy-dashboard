---
tags: [architecture, map]
---

# Camera and Input

Two files: `MapCameraController.cs` (pan/zoom) and `MapInteraction.cs` (everything else — sim
clock, click-to-select, elections).

## MapCameraController.cs
Orthographic camera pan/zoom over the flat Web-Mercator-projected map — no 3D-globe projection
math, just XY movement and `orthographicSize` changes.
- `minOrthoSize`/`maxOrthoSize` (0.03 – 200), `zoomSpeed` (0.15), `latitudeClamp` (170).
- `HandlePan()` — left-drag panning via `ScreenToWorldPoint` deltas.
- `HandleZoom()` — scroll-wheel zoom **toward the cursor**, preserving the point under it.
- `ClampPosition()` — clamps Y to ±`latitudeClamp` so you can't pan off into empty space above/
  below the projected world.
- `PointerOverUI()` — checks `UIDocument`/`RuntimePanelUtils.ScreenToPanel` + `panel.Pick` so
  dragging or scrolling a UI Toolkit element doesn't also pan/zoom the map underneath it.

## MapInteraction.cs
The busiest file in Map/ — three unrelated jobs share it because they all need the same
per-frame `Update()`:

### 1. The sim clock
`TickEconomy()` accumulates `daysPerSecond * Time.deltaTime` and advances whole simulated days,
each of which: ticks [[Economy System]], [[National State]], [[Diplomacy System]]
(`TickAll`), [[War System]] and [[World AI]] (pushing headlines to [[History and World Feed|WorldFeed]]), checks for an election ([[Elections]]), records [[History and World Feed|PlayerHistory]], and calls `EventSystem.MaybeFire` for a [[Decision Events|decision event]].
**The clock freezes** whenever `PlayerState.State != Playing` (start/game-over screens) or a
decision event is pending — the world waits for the head of state.

Also calls `map.RefreshCountryColors()` once per frame that actually advanced a day, so relation
drift and diplomacy actions show up on the map promptly — see [[Map Modes and Coloring]].

### 2. Click-to-select hit-testing
`HandleClickSelect()` — priority order, most-specific-target-wins:
**water crossings (14px) → border crossings (10px) → cities (9px, only currently-visible tiers)
→ roads (6px) → countries (fallback, point-in-polygon)**. Cities are filtered to only the zoom
tier `MapLayers` currently has active — without that filter, invisible off-screen-tier towns
were swallowing clicks meant for a country at world zoom (the classic bug: clicking China
selected Choibalsan, Mongolia instead).

`PointerOverUI()` (a legacy-Input-vs-UI-Toolkit bridge) is checked first — Legacy `Input`
polling doesn't know UI Toolkit panels exist, so without this every click on a HUD button/panel/
modal *also* fell through to the map underneath and click-selected whatever country was under
the cursor.

`SelectedCity`/`SelectedRoad`/`SelectedBorderCrossing`/`SelectedWaterCrossing` are a separate,
mutually-exclusive selection from the country ministry panel — picking one clears the other, so
only one info surface shows at a time (rendered by `FeaturePanel.cs`, see [[UI System]]).

### 3. Elections and dev-only self-tests
`CheckElection()` — see [[Elections]]. Several `MaybeRunXDiag()` methods gated behind env vars
(`MERIDIAN_DIAG_DIPLOMACY`, `MERIDIAN_DIAG_WAR`, `MERIDIAN_DIAG_SAVE`) exercise diplomacy/war/
save-load mechanics and log exact before/after numbers to Player.log — used instead of
pixel-precise UI automation, which has proven flaky on the primary dev machine (see the
flaky-hardware note in [[Code Architecture]]).

## Consumers
- [[UI System]] — `GameUIRoot.cs` reads `Selected`, `SimDay`, `daysPerSecond`; calls `SaveNow`,
  `SelectCountry`, `RestoreClock`.
- [[Map Modes and Coloring]] — the per-day color refresh hook lives in `TickEconomy`.
