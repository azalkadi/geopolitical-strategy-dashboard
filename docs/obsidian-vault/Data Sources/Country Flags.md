---
tags: [data, ui]
---

# Country Flags

`Assets/StreamingAssets/flags/{code}.png` — 237 small (48×36) flag PNGs, one per ISO 3166-1
alpha-2 code actually present in the country dataset (see [[Natural Earth Datasets]]), sourced
from [flagcdn.com](https://flagcdn.com) (free, public, no API key) and bundled offline the same
way the satellite basemap is (see [[Satellite Basemap]]) — no network access needed at runtime.

## Code resolution
Most countries resolve straight from `Country.IsoA2` (lowercased). A handful of real,
commonly-governed entities Natural Earth's admin_0 dataset gives `ISO_A2 = "-99"` (no officially
assigned code) but flagcdn still serves under a widely-used conventional code — `FlagLoader.cs`
carries a small override table for these (currently Taiwan → `tw`, Kosovo → `xk`). Everything
else with no valid 2-letter code (disputed buffer zones, ice fields, military bases, uninhabited
reef claims — see [[Natural Earth Datasets]] for why these are in the dataset at all) simply has
no flag; the UI skips the flag element rather than showing a broken icon.

## Where they show
`FlagLoader.Get(name, isoA2)` loads-and-caches a `Texture2D` on first request. Used in three
places in `GameUIRoot.cs`: the side panel header (next to the selected country's name), the
start-screen country preview card, and a small icon in every row of the start-screen country
list.
