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

## Minimap
A small overlay in the corner of the screen showing the whole world; clicking a spot on it pans
the main camera there. Standard strategy-game furniture Meridian doesn't have yet.

## Bigger, clearer flags
[[Country Flags|Flags]] exist now but are small — make them bigger/more prominent so it's
unmistakable which country you're playing, especially given how much of the identity of "which
nation am I" this game hangs on.

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
