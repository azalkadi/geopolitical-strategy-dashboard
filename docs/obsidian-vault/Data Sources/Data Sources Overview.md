---
tags: [data]
---

# Data Sources Overview

Everything geographic lives in `Assets/StreamingAssets/worlddata/` (read raw at runtime, not
Unity-imported) plus one basemap image, all loaded by [[Geo Pipeline]]. All of it is either
public-domain Natural Earth data or hand-curated by this project — no proprietary or licensed
map data anywhere.

- [[Natural Earth Datasets]] — countries, provinces, cities, ports, airports, roads, railways:
  straight from Natural Earth's official public-domain distribution
- [[Curated Datasets]] — air bases, oil ports, nuclear plants, water crossings, and computed
  border crossings: no public dataset exists for these, so they're either filtered out of
  existing data or hand-researched
- [[Satellite Basemap]] — the offline whole-world background image plus live ESRI tile streaming

## Scale, roughly
258 countries · 4,596 provinces · 7,342 cities · 893 airports · 1,081 ports · ~34,130 road
features · ~9,062 railway features · 44 air bases · 28 oil ports · 56 nuclear plants · 6 water
crossings · 181 computed border crossings.
