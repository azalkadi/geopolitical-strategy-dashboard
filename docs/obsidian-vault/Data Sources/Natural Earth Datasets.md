---
tags: [data]
---

# Natural Earth Datasets

All public domain, no attribution required, straight from Natural Earth's official 10m-resolution
distribution unless noted. Loaded by [[Geo Pipeline]] into the shapes described in
`GeoModel.cs`.

| File | Feeds | Notes |
|---|---|---|
| `ne_10m_admin_0_countries` | `Country` (258 of them) | fill mesh + outline rings |
| `ne_10m_admin_1_states_provinces` | `Province` (4,596) | fill mesh + outline, no hit-test rings |
| `ne_10m_populated_places` | `City` (7,342) | tiered by population — see [[Geo Pipeline]] |
| `ne_10m_ports` | `PointFeature` (1,081) | seaports |
| `ne_10m_airports` | `PointFeature` (893 civil) | air bases filtered out of this same file — see [[Curated Datasets]] |

## Roads and railways — extended, not the "_major" cut

Natural Earth only ships roads/railroads as **shapefiles** officially; this project pulled
pre-converted GeoJSON from `raw.githubusercontent.com/nvkelso/natural-earth-vector` and filtered
by its own tooling to `scalerank ≤ 7` — roughly 34k of 57k total road features and 9k of 25k
railway features. This keeps named highways/railroads while dropping only the bottom,
mostly-unclassified tier that would be pure visual clutter at country/continent zoom.

This superseded an earlier, much sparser "_major" cut (roads: `min_zoom ≤ 3`, ~10k features;
railways: `scalerank ≤ 4`, ~2.8k features) once "make every road clickable" and "many roads are
missing" feedback made clear the original cut was too thin. See [[Map Rendering]] for how these
render (constant-pixel-width lines with cross-tie ties for railways) and [[Camera and Input]]
for how a click resolves to a specific road/railway segment.

## GDP / population seeding
`Country.GdpMd`/`PopEst` come from the same `ne_10m_admin_0_countries` file's own attribute
columns and feed [[Economy System]]'s seeding directly where present.
