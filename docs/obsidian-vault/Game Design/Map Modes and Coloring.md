---
tags: [game-design, map]
---

# Map Modes and Coloring

Two map modes, toggled from the top bar — see [[Map Rendering]] in the architecture map for how
each is actually built.

## Political
Flat country fills, colored (once a game has started) by that country's diplomatic relation to
**you**:
- **Your own country** — a fixed gold highlight (a country has no meaningful "relation to
  itself")
- **Relation gradient, 0-100**: red (hostile) → gray (neutral, ~50) → green (friendly)

Before you've picked a country — the start screen, or right after being voted out — there's no
"relation to nobody" to show, so the map falls back to a plain rainbow palette (a
deterministic color per country name, just enough so no two neighbors look identical).

## Satellite
Live/offline satellite imagery as the base layer (see [[Satellite Basemap]]) with **no** fill —
country borders are the only per-country color cue here, so they carry the exact same
relation-to-player coloring the Political mode's fills use. This is why border lines are
colored by relations too, not just left plain white: it's the only signal available once the
fill layer is gone.

## When colors update
Once per simulated day, as part of the normal tick — relations drift constantly (baseline decay,
[[World AI|AI activity]], your own [[Diplomacy Mechanics|diplomacy actions]], [[War
Mechanics|war]] fallout), so the map stays current without needing a manual refresh. It also
repaints instantly the moment you start a new game, load a save, or hit PLAY AGAIN.
