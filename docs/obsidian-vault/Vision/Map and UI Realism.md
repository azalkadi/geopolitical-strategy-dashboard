---
tags: [vision, game-design, ui, map]
---

# Map and UI Realism

Part of [[Vision Overview|the vision]]. The explicit bar stated: **"I want this to look like a
real game — don't make it look like it came from ChatGPT or Claude."** The mechanics can be
right and it will still fail this ask if it doesn't feel game-like. This page collects every
concrete UI/map ask from the session.

## Right-click context menu
Right-clicking a country should open an action menu — declare war, propose trade, and whatever
else makes sense contextually — instead of every action living only inside the side panel tabs.
This is a faster, more game-like interaction model than "select country → open ministry → find
button."
> [!success] Status: ✅ Built. `MapInteraction.HandleRightClick`/`TryHitCountry` (the country
> hit-test refactored out of the existing left-click select) detects a right-click and exposes
> `ContextMenuCountry`/`ContextMenuScreenPos`; `GameUIRoot.BuildContextMenu`/`RebuildContextMenu`
> renders a small popup at the cursor with "Open Ministry Panel" plus — for a foreign country,
> while playing — Declare War / Send Aid / Sign Trade Agreement / Denounce, each calling the
> exact same `WarSystem`/`DiplomacySystem` methods the Diplomacy and Military tabs already use
> (no duplicated game logic, just a second entry point). Verified live via a new
> `MERIDIAN_DIAG_CONTEXTMENU=1` flag (opens the menu on a foreign country at day 45, closes it at
> day 47) — `Player.log` shows both diag lines and zero exceptions through the whole run.

## Minimap
A small overlay in the corner of the screen showing the whole world; clicking a spot on it pans
the main camera there. Standard strategy-game furniture Meridian didn't have.
> [!success] Status: ✅ Built. Bottom-left corner (`GameUIRoot.BuildMinimap`), showing the same
> equirectangular satellite basemap `MapRenderer` already loads (`MapRenderer.SatelliteTexture`),
> with a live viewport rectangle and click-to-pan (`MapCameraController.PanTo`). The one real
> technical wrinkle: the minimap image is plain linear-latitude but the camera/world are Mercator
> — every UV↔world conversion routes latitude through `GeoMath.MercatorToLonLat`/
> `LonLatToMercator` (longitude needs no conversion, it's linear in both spaces) so the viewport
> rectangle lines up correctly even away from the equator, where a Mercator-degree view height
> corresponds to a different real latitude span than at the equator.

## Bigger, clearer flags
[[Country Flags|Flags]] exist now — make them bigger/more prominent so it's unmistakable which
country you're playing, especially given how much of the identity of "which nation am I" this
game hangs on.
> [!success] Status: ✅ Built. Source PNGs re-fetched at 96×72 (2x the original 48×36) for all 237
> flags; display sizes doubled in the side panel header, start-screen preview card, and
> start-screen country list, each with a subtle border so the flag reads as a distinct plate.

## Camera should never show the void beyond the map
Explicitly confirmed as *fixed and working* in this same session (see
[[Camera and Input|MapCameraController's clamp]]) — noted here so it's not lost, but this
specific complaint should already be resolved; flag it if it recurs.

## City and province interaction depth
- Clicking a city should show real detail about it, not just a name label.
- Clicking a province should visibly gray out / highlight so it's clear it's selected.
- **Tiered, distinct icons**: a capital should look different from an ordinary major city,
  which should look different from a small town — not the same dot in three sizes. Airports
  should look like airports, roads like roads, rail like rail (not a generic colored line) —
  this is the same spirit as the casing-style rendering already shipped this session, taken
  further into genuinely representational iconography.

## Movement and flow visuals
- Trains should visibly move along the rail lines they're on, not just exist as a static line.
- Cargo volume on a given line/route should be visible somehow (not just an abstract trade
  number).
- Ports should be visually/logically connected to the rail lines that serve them — the
  supply-chain relationship should read on the map, not just exist in data.

## Strike/attack visuals
Covered in depth in [[Conflicts, Terrorism and Military Realism]] — missiles traveling
visibly, impact effects — but it's fundamentally a Map and UI ask as much as a military one.

## General polish bar
"The UI looks a bit silly still, but it can work — we need it to look like a real game." This
isn't one ticket, it's a standard to hold every future UI addition to: game-like presentation
(iconography, motion, feedback) over spreadsheet-like presentation (rows of plain stats), even
where the underlying data is the same.
> [!success] Status: ✅ First full pass shipped — "remake the UI, make it look like a game not
> an HTML website." Every panel/button in the game was flat-color rectangles before this; now:
> - New `UIVisuals.cs` — procedural texture generation (no external art assets, same approach as
>   the satellite/flag pipelines): cached 1×2 vertical-gradient textures and a radial vignette,
>   generated once per color pair and reused.
> - `GameUIRoot.MakeButton`/`SetButtonColor` (the single shared factory behind every clickable
>   element in the game — top bar, ministry bar, dropdowns, side-panel actions, context menu,
>   event modal, start screen) now renders a top-lit gradient plus a 1px inner highlight instead
>   of a flat fill — one change that cascades a "carved nameplate" look across the entire
>   interface for free.
> - `StartCard`/side panel/minimap/context menu/toasts/event modal all gained the same panel
>   chrome (`UIVisuals.ApplyPanelChrome`): a subtle top-lit gradient off their own base color
>   plus a bevel highlight/seam, instead of a flat rectangle.
> - Top bar: richer gradient, a thicker high-alpha gold trim line (was a nearly-invisible 1px
>   border), a small ◆ emblem beside the title.
> - Start screen: the flat 94%-opacity black overlay is now a real gradient plus a radial
>   vignette overlay (darkens the corners so the centered title/list reads as the focus), a
>   double gold rule under the title, and emblems flanking "MERIDIAN".
> - **Verified visually**: full headless build, launched windowed, real screenshots captured
>   (not just log inspection, since this is a purely visual change) of both the start screen
>   (gradient/vignette/gold rules/gradient list rows all rendering correctly) and the in-game HUD
>   (top bar trim, gradient ministry-bar buttons, minimap) — plus `Player.log` shows zero
>   exceptions across the run.
> - Still open: tiered/representational city-road-rail icons, train/cargo movement, port-rail
>   visualization, deeper city/province interaction — the remaining, larger builds in this pillar.

## Where this plugs into existing code
- Right-click menu: new interaction path in `MapInteraction.cs`, alongside the existing
  left-click select.
- Minimap: a new small always-on-top UI Toolkit element in `GameUIRoot.cs`, likely rendering a
  cheap top-down snapshot or a simplified pre-baked world texture with a viewport rectangle
  overlay.
- Tiered icons / representational road-rail-airport art: extends [[Map Rendering]]'s existing
  marker/line-building systems (`ScreenDot`/`ScreenLine` shaders) — likely needs real per-type
  icon textures rather than solid-color dots, a step up from the casing-style pass already
  shipped.
- Train movement: a new per-frame (or per-tick) animation system moving a marker along a
  railway `LineFeature`'s polyline — nothing existing to extend, genuinely new.
- This pillar is the most "many small high-visibility wins" of the six — good candidate to
  interleave with the bigger systems rather than doing last.
