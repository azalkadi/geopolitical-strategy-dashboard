---
tags: [data, map]
---

# Satellite Basemap

Two layers, described in [[Map Rendering]] and [[Geo Pipeline]] for the code side.

## Static offline background
NASA's "Blue Marble: Next Generation" cloud-free composite (`land_shallow_topo` series),
public domain, from `eoimages.gsfc.nasa.gov`. Currently **16384×8192** — the largest size that
fits under this dev machine's `SystemInfo.maxTextureSize` (21600px is the next size up, and
silently fails — see the texture-size gotcha in [[Code Architecture]]). If targeting different
hardware, re-derive from the same NASA source at whatever the real ceiling is on that machine;
don't just guess a bigger number.

Rendered as a vertically-subdivided strip mesh (not a flat quad) so its rows land at the correct
nonlinear Mercator Y per latitude — longitude needs no subdivision since it's linear in both
the source image's projection and this project's Web Mercator space (see [[Geo Pipeline]]).

## Live satellite tiles
`SatelliteTileLoader.cs` streams live, zoom-dependent tiles from ESRI's public World Imagery
service (no API key required, works down to city-block detail) once the camera zooms in past a
threshold (`activateBelowOrthoSize`, default 10) where the static background alone would look
pixelated — a single static texture has a hard resolution ceiling no matter how large the file;
real tile streaming doesn't. Standard Web Mercator z/x/y addressing.

Requires an internet connection; if a fetch fails it silently leaves the static background
visible underneath — no error state, no broken-image placeholder. Tiles are cached with an
LRU-style eviction (`MaxCachedTiles`) so long play sessions don't grow memory unbounded.
