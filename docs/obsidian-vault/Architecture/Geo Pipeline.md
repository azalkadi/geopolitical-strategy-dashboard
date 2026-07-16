---
tags: [architecture, geo]
---

# Geo Pipeline

`Assets/Scripts/Geo/GeoModel.cs` + `GeoJsonLoader.cs`. Turns raw `.geojson` files into the
in-memory `GeoWorld` that [[Map Rendering]] builds meshes from and [[Camera and Input]] hit-tests
against. See [[Natural Earth Datasets]] and [[Curated Datasets]] for what data actually gets
loaded.

## Projection: Web Mercator, "degrees-normalized"

Every single `[lon, lat]` pair is reprojected the moment it's parsed, through
`GeoMath.LonLatToMercator`. `GeoWorld` never stores raw lon/lat — only this projected space. This
was a deliberate, project-wide change so real map tiles (always Web Mercator) line up with the
vector data (see [[Satellite Basemap]]).

"Degrees-normalized" means: X is still plain longitude (−180..180); Y follows the real Mercator
curve but is rescaled so it also lands in that same numeric range (±180 at the standard
85.0511° polar cutoff, `GeoMath.MaxMercatorLatitude`) instead of real-world meters. This was
chosen specifically so existing world-unit-based constants (camera zoom range, [[Map Rendering]]
zoom-gate thresholds, marker pixel radii) didn't all need re-tuning for a different scale.

**Rule for any new geo data path**: route every point through `GeoMath.LonLatToMercator`, or it
won't align with everything else already on the map.

## Data model (`GeoModel.cs`)

- `Country` — name/ISO codes/continent/subregion, population, GDP, centroid, bbox, the
  triangulated fill mesh (`MeshVerts`/`MeshIndices`), `OutlineRings` (all rings, for the border
  stroke) and `OuterRings` (outer rings only, for point-in-polygon hit tests).
- `Province` — same shape, no `OuterRings` (provinces aren't hit-tested for border crossings).
- `City` — name, country, position, population, capital flag, `CityTier` (Town/City/MajorCity/
  Megacity, thresholded at 100k/1M/10M population).
- `PointFeature` — generic named point: ports, airports, air bases, oil ports, nuclear plants.
- `LineFeature` — named polyline set (roads/railways); supports MultiLineString natively as a
  `List<List<Vector2>>`.
- `BorderCrossing` — computed, not sourced (see below). `WaterCrossing` — hand-curated (see
  [[Curated Datasets]]).
- `GeoWorld` — the aggregate root holding all of the above.
- `GeoMath` — the projection functions plus `PointInRing` (ray-casting hit test) and
  `BboxContains`/`BboxOverlaps` (cheap pre-filters before the expensive ring test).

## Loading (`GeoJsonLoader.cs`)

`Load()` reads each dataset from `StreamingAssets/worlddata/`, reprojecting and (for
polygons) triangulating via a ported `Earcut.cs` **before** triangulating — a nonlinear
Mercator warp applied *after* ear-clipping could in principle disagree with decisions made in
raw lon/lat space, so projection always happens first.

### Border crossings — computed, not a dataset

Natural Earth doesn't label border crossings, so `ComputeBorderCrossings` walks every **named**
road's polyline (unnamed/local segments skipped) and resolves which country each sampled vertex
falls in. Wherever two consecutive samples resolve to different countries, that segment's
midpoint becomes a `BorderCrossing`. Samples every 4th vertex (`BorderCrossingSampleStride`), not
every one — at 10m-resolution road geometry this is still accurate well within visual tolerance
and is the difference between seconds and ~53s of computation.

This computation is genuinely expensive (tens of thousands of point-in-country queries against
258 countries, several of which — France, USA, Russia, the UK — have far-flung territories that
poison a naive whole-country bounding-box index). The final `CountryGridIndex` buckets by
**per-ring** bbox (not per-country) on a 2°-cell uniform grid, which brought this from an
effective hang down to ~12 seconds. Once computed, the result is cached (`[map] loaded N border
crossings from cache` in Player.log on subsequent runs) rather than recomputed every launch.

### Resilience: `SafeLoad`

Every dataset loads through `SafeLoad` (3 attempts, then continue without that layer instead of
black-screening) — see the flaky-hardware note in [[Code Architecture]].

## Consumers
- [[Map Rendering]] — builds every mesh from `GeoWorld`.
- [[Camera and Input]] — `MapInteraction` hit-tests clicks against `GeoWorld` via `PointInRing`.
- [[Satellite Basemap]] — `SatelliteTileLoader` uses the same Mercator math to place live tiles.
- [[Economy System]] — `EconomyState.Seed` reads `Country.GdpMd`/`PopEst` where real data exists.
