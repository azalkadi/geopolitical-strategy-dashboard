using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Meridian.Geo
{
    // Faithful C# port of crates/ui_dashboard/src/geo/{mod,countries,provinces,cities,
    // infrastructure,mesh}.rs. Reads the Natural Earth GeoJSON files from StreamingAssets
    // (copied there from the original project's data/worlddata/) and builds GeoWorld.
    //
    // The Rust build embedded the GeoJSON with include_str! and parsed with serde_json; here
    // the files are read from StreamingAssets at runtime and parsed with Newtonsoft.
    //
    // Every raw [lon, lat] pair read from these files is reprojected through
    // GeoMath.LonLatToMercator the moment it's parsed (see TryPointCoords, ParseLineCoords,
    // BuildMeshFromGeometry) — GeoWorld never stores raw lon/lat, only Mercator-projected
    // positions, so this is the ONLY place that projection choice lives.
    public static class GeoJsonLoader
    {
        static string DataDir => Path.Combine(Application.streamingAssetsPath, "worlddata");

        public static GeoWorld Load()
        {
            // Every dataset loads independently: a parse failure in any one of them costs that
            // single layer (logged loudly, empty list returned), never the whole world. Before
            // this guard existed, a single intermittent InvalidCastException while parsing the
            // railways file aborted MapRenderer.Start entirely — no map, no UI, just a black
            // screen with the only clue buried in Player.log.
            var world = new GeoWorld();
            world.Countries = SafeLoad("countries", () => LoadCountries(Path.Combine(DataDir, "ne_10m_admin_0_countries.geojson")));
            world.Provinces = SafeLoad("provinces", () => LoadProvinces(Path.Combine(DataDir, "ne_10m_admin_1_states_provinces.geojson")));
            world.Cities = SafeLoad("cities", () => LoadCities(Path.Combine(DataDir, "ne_10m_populated_places.geojson")));
            world.Ports = SafeLoad("ports", () => LoadPoints(Path.Combine(DataDir, "ne_10m_ports.geojson"), "name", ""));
            world.Airports = SafeLoad("airports", () => LoadPoints(Path.Combine(DataDir, "ne_10m_airports.geojson"), "name", "iata_code"));
            // Roads/railways pre-filtered (offline, by this project's own tooling — not shipped
            // as Natural Earth's own "_major" cut) to scalerank<=7: ~34k/9k of the full ~57k/25k
            // features, keeping named highways/roads/railroads and dropping only the bottom,
            // mostly-unclassified tier that would be pure clutter at this map's zoom range. Hand-
            // curated air bases (filtered from the airports set by its own "military" type tag),
            // oil ports, and nuclear plants (no public bundled dataset for either, so a
            // real-but-not-exhaustive list of major, well-known facilities).
            world.Roads = SafeLoad("roads", () => LoadLines(Path.Combine(DataDir, "ne_10m_roads_extended.geojson"), "name"));
            world.Railways = SafeLoad("railways", () => LoadLines(Path.Combine(DataDir, "ne_10m_railroads_extended.geojson"), "name"));
            world.AirBases = SafeLoad("air bases", () => LoadPoints(Path.Combine(DataDir, "ne_10m_airbases_military.geojson"), "name", ""));
            world.OilPorts = SafeLoad("oil ports", () => LoadPoints(Path.Combine(DataDir, "ne_10m_oilports.geojson"), "name", ""));
            world.NuclearPlants = SafeLoad("nuclear plants", () => LoadPoints(Path.Combine(DataDir, "ne_10m_nuclearplants.geojson"), "name", ""));

            // Derived from the layers above, so they inherit those layers' guards — but each
            // still gets its own SafeLoad since the computation itself could throw.
            world.BorderCrossings = SafeLoad("border crossings", () => LoadOrComputeBorderCrossings(world));
            world.WaterCrossings = SafeLoad("water crossings", WaterCrossingsSeed);
            return world;
        }

        // The border-crossing walk over all named roads costs ~12s of a ~24s total load, and its
        // output is fully determined by the roads + countries data — so compute once, cache to
        // persistentDataPath, and reload instantly forever after. The cache key is the input
        // datasets' feature counts: any data update changes those, invalidating the cache. (Keyed
        // counts, not hashes — a hash would need to read all 50MB anyway, spending what it saves.)
        [System.Serializable]
        class BorderCrossingCache
        {
            public string Key;
            public List<BorderCrossingDto> Crossings = new();
        }

        [System.Serializable]
        class BorderCrossingDto
        {
            public float X, Y;
            public string A, B, Road;
        }

        static List<BorderCrossing> LoadOrComputeBorderCrossings(GeoWorld world)
        {
            string cachePath = Path.Combine(Application.persistentDataPath, "border_crossings_cache.json");
            string key = $"v1:roads={world.Roads.Count}:countries={world.Countries.Count}";

            try
            {
                if (File.Exists(cachePath))
                {
                    var cached = Newtonsoft.Json.JsonConvert.DeserializeObject<BorderCrossingCache>(File.ReadAllText(cachePath, Encoding.UTF8));
                    if (cached != null && cached.Key == key)
                    {
                        var list = new List<BorderCrossing>(cached.Crossings.Count);
                        foreach (var d in cached.Crossings)
                            list.Add(new BorderCrossing { Pos = new Vector2(d.X, d.Y), CountryA = d.A, CountryB = d.B, RoadName = d.Road });
                        Debug.Log($"[map] loaded {list.Count} border crossings from cache");
                        return list;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[geo] border-crossing cache unreadable ({e.GetType().Name}) — recomputing");
            }

            var computed = ComputeBorderCrossings(world);

            try
            {
                var cache = new BorderCrossingCache { Key = key };
                foreach (var c in computed)
                    cache.Crossings.Add(new BorderCrossingDto { X = c.Pos.x, Y = c.Pos.y, A = c.CountryA, B = c.CountryB, Road = c.RoadName });
                File.WriteAllText(cachePath, Newtonsoft.Json.JsonConvert.SerializeObject(cache), Encoding.UTF8);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[geo] couldn't write border-crossing cache ({e.GetType().Name}) — will recompute next boot");
            }
            return computed;
        }

        // Retries before giving up: this dev machine intermittently produces one-off corrupt
        // reads/parses (three DIFFERENT datasets have each randomly failed with three DIFFERENT
        // exception types across runs — an InvalidCastException in railways, an
        // IndexOutOfRangeException in provinces, and a JsonReaderException in countries with a
        // literal corrupted character inside a number — while the same files parse fine on the
        // next run; the files on disk never changed). That failure pattern points at flaky
        // hardware, not code, and a transient corruption virtually never repeats on an
        // immediate retry — so retrying converts "layer randomly missing this session" into
        // "layer loads on attempt 2".
        const int LoadAttempts = 3;

        static List<T> SafeLoad<T>(string what, System.Func<List<T>> loader)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    var result = loader();
                    if (attempt > 1) Debug.LogWarning($"[geo] {what} loaded on attempt {attempt} (transient failure earlier — see hardware note in GeoJsonLoader.SafeLoad)");
                    return result;
                }
                catch (System.Exception e)
                {
                    if (attempt < LoadAttempts)
                    {
                        Debug.LogWarning($"[geo] {what} attempt {attempt} failed ({e.GetType().Name}: {e.Message}) — retrying");
                        continue;
                    }
                    Debug.LogError($"[geo] {what} failed {LoadAttempts} attempts ({e.GetType().Name}: {e.Message}) — continuing without this layer");
                    return new List<T>();
                }
            }
        }

        // --- Border crossings -----------------------------------------------------------
        //
        // Not sourced from any dataset — Natural Earth doesn't label border crossings. Instead,
        // for every NAMED road (unnamed/local segments are skipped — this keeps the count to a
        // meaningful set of real highways rather than every minor track that happens to clip a
        // border) we walk its polyline and resolve which country each vertex falls in via the
        // same bbox-prefiltered point-in-ring test used for click hit-testing. Wherever two
        // consecutive vertices resolve to different countries, that segment's midpoint is a
        // real border crossing on a real road.
        // Samples every Nth vertex instead of every single one. At 10m-resolution road geometry,
        // consecutive vertices are typically well under a km apart — a border crossing detected
        // a few vertices later than the true crossing is still accurate to well within visual
        // tolerance at this map's scale, and this is the difference between the whole computation
        // taking seconds vs. the ~53s a full-resolution per-ring-indexed pass still measured at
        // (see CountryGridIndex's own history-of-attempts comment for the earlier, slower ones).
        const int BorderCrossingSampleStride = 4;

        static List<BorderCrossing> ComputeBorderCrossings(GeoWorld world)
        {
            var result = new List<BorderCrossing>();
            var grid = new CountryGridIndex(world.Countries);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            foreach (var road in world.Roads)
            {
                if (string.IsNullOrEmpty(road.Name)) continue;
                foreach (var line in road.Lines)
                {
                    if (line.Count < 2) continue;
                    string prevCountry = grid.CountryAt(line[0]);
                    Vector2 prevPt = line[0];
                    for (int i = BorderCrossingSampleStride; i < line.Count; i += BorderCrossingSampleStride)
                    {
                        string here = grid.CountryAt(line[i]);
                        if (here != null && prevCountry != null && here != prevCountry)
                        {
                            var mid = (prevPt + line[i]) * 0.5f;
                            result.Add(new BorderCrossing { Pos = mid, CountryA = prevCountry, CountryB = here, RoadName = road.Name });
                        }
                        if (here != null) prevCountry = here;
                        prevPt = line[i];
                    }
                }
            }
            Debug.Log($"[map] computed {result.Count} border crossings from named roads in {sw.ElapsedMilliseconds} ms");
            return result;
        }

        // Uniform grid over *per-ring* bboxes (5°-ish cells across the ±180 Mercator-normalized
        // world — see GeoMath.LonLatToMercator's doc comment for why that range is exactly
        // ±180). ComputeBorderCrossings calls CountryAt for every vertex of every named road
        // (tens of thousands of calls); a naive linear scan against all 258 countries — several
        // of which (archipelago nations) have thousands-of-points polygons — measured as an
        // effective hang (multiple minutes with no progress).
        //
        // The first version of this index bucketed by each *country's* overall bbox — which
        // measured only marginally faster (158 seconds, not a hang) because several countries'
        // bboxes span nearly the entire globe due to far-flung territory in the same Feature
        // (France mainland + French Guiana, USA mainland + Alaska + Hawaii, Russia's longitude
        // span, UK's overseas territories...). Those few countries were getting registered into
        // almost every cell, which defeats the whole point of a grid. Indexing per RING instead
        // (France's mainland ring and its French Guiana ring get separate, tight bboxes) fixes
        // this at the root rather than special-casing those countries.
        class CountryGridIndex
        {
            const int Cols = 180, Rows = 90; // 2° per cell — tighter than the first (5°) attempt,
                                              // which still left crowded cells over Europe/the
                                              // Mediterranean (many small countries + many
                                              // islands + dense named-road coverage, all at once).
            readonly List<(int country, int ring, Vector2 rMin, Vector2 rMax)>[] cells =
                new List<(int, int, Vector2, Vector2)>[Cols * Rows];
            readonly List<Country> countries;

            public CountryGridIndex(List<Country> countries)
            {
                this.countries = countries;
                for (int i = 0; i < cells.Length; i++) cells[i] = new List<(int, int, Vector2, Vector2)>();

                for (int ci = 0; ci < countries.Count; ci++)
                {
                    var rings = countries[ci].OuterRings;
                    for (int ri = 0; ri < rings.Count; ri++)
                    {
                        var ring = rings[ri];
                        if (ring.Count == 0) continue;
                        Vector2 rMin = ring[0], rMax = ring[0];
                        foreach (var p in ring)
                        {
                            if (p.x < rMin.x) rMin.x = p.x;
                            if (p.y < rMin.y) rMin.y = p.y;
                            if (p.x > rMax.x) rMax.x = p.x;
                            if (p.y > rMax.y) rMax.y = p.y;
                        }
                        var (x0, y0) = CellOf(rMin);
                        var (x1, y1) = CellOf(rMax);
                        for (int x = x0; x <= x1; x++)
                            for (int y = y0; y <= y1; y++)
                                cells[y * Cols + x].Add((ci, ri, rMin, rMax));
                    }
                }
            }

            static (int, int) CellOf(Vector2 p)
            {
                int x = Mathf.Clamp(Mathf.FloorToInt((p.x + 180f) / 360f * Cols), 0, Cols - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt((p.y + 180f) / 360f * Rows), 0, Rows - 1);
                return (x, y);
            }

            public string CountryAt(Vector2 pt)
            {
                var (cx, cy) = CellOf(pt);
                foreach (var (ci, ri, rMin, rMax) in cells[cy * Cols + cx])
                {
                    // Cheap per-ring bbox reject before the O(ring length) point-in-ring test —
                    // a cell can hold a small island's ring alongside a country's whole mainland
                    // ring; without this, every query pays the full ring test for both even when
                    // the point is nowhere near one of them.
                    if (pt.x < rMin.x || pt.x > rMax.x || pt.y < rMin.y || pt.y > rMax.y) continue;
                    if (GeoMath.PointInRing(pt, countries[ci].OuterRings[ri])) return countries[ci].Name;
                }
                return null;
            }
        }

        // --- Water crossings (hand-curated) ----------------------------------------------
        //
        // Real intercountry causeways/bridges over water. Coordinates are approximate landfall
        // points (accurate to a few km — fine at this map's scale), sourced from public
        // references on each structure. Deliberately excludes anything not actually built: no
        // Qatar-Bahrain Friendship Bridge (announced ~2005, never built) and no Kuwait entry —
        // Kuwait's only major causeway (Sheikh Jaber Al-Ahmad Al-Sabah) is entirely domestic,
        // Kuwait City to Subiya/Bubiyan Island, not a link to another country.
        static List<WaterCrossing> WaterCrossingsSeed()
        {
            List<Vector2> L(float lonA, float latA, float lonB, float latB) =>
                new() { GeoMath.LonLatToMercator(lonA, latA), GeoMath.LonLatToMercator(lonB, latB) };

            return new List<WaterCrossing>
            {
                new() { Name = "King Fahd Causeway", CountryA = "Saudi Arabia", CountryB = "Bahrain",
                        Line = L(50.2077f, 26.2038f, 50.4886f, 26.1875f) }, // Khobar, SA <-> Al Jasra, BH
                new() { Name = "Øresund Bridge", CountryA = "Denmark", CountryB = "Sweden",
                        Line = L(12.6197f, 55.5697f, 12.8300f, 55.5550f) }, // Kastrup, DK <-> Lernacken, SE
                new() { Name = "Johor–Singapore Causeway", CountryA = "Malaysia", CountryB = "Singapore",
                        Line = L(103.7631f, 1.4655f, 103.7864f, 1.4484f) }, // Johor Bahru, MY <-> Woodlands, SG
                new() { Name = "Tuas Second Link", CountryA = "Malaysia", CountryB = "Singapore",
                        Line = L(103.6167f, 1.3667f, 103.6367f, 1.3465f) }, // Tanjung Kupang, MY <-> Tuas, SG
                new() { Name = "Hong Kong–Zhuhai–Macau Bridge", CountryA = "Hong Kong S.A.R.", CountryB = "China",
                        Line = L(113.9067f, 22.2897f, 113.5539f, 22.2264f) }, // HK landfall <-> Zhuhai/Macau junction
                new() { Name = "Thai–Lao Friendship Bridge (1st)", CountryA = "Thailand", CountryB = "Laos",
                        Line = L(102.7233f, 17.8833f, 102.7264f, 17.8961f) }, // Nong Khai, TH <-> Vientiane area, LA
            };
        }

        // --- Countries -----------------------------------------------------------------

        public static List<Country> LoadCountries(string path)
        {
            var outList = new List<Country>();
            if (!File.Exists(path)) { Debug.LogError($"[geo] missing {path}"); return outList; }

            var root = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
            foreach (var f in root["features"])
            {
                var props = f["properties"];
                var mesh = BuildMeshFromGeometry(f["geometry"]);
                outList.Add(new Country
                {
                    Name = PropStr(props, "NAME"),
                    NameLong = PropStr(props, "NAME_LONG"),
                    IsoA2 = PropStr(props, "ISO_A2"),
                    // Natural Earth quirk: a few countries carry ISO_A3="-99" (notably FRANCE and
                    // NORWAY, because their sovereignty covers overseas collectivities NE splits
                    // out). ADM0_A3 always has the real code — without this fallback every
                    // CountryProfiles/curated-data lookup silently failed for those countries.
                    IsoA3 = NormalizeIso(PropStr(props, "ISO_A3"), PropStr(props, "ADM0_A3")),
                    Continent = PropStr(props, "CONTINENT"),
                    Subregion = PropStr(props, "SUBREGION"),
                    PopEst = PropLong(props, "POP_EST"),
                    GdpMd = PropLong(props, "GDP_MD"),
                    Centroid = mesh.Centroid,
                    BboxMin = mesh.BboxMin,
                    BboxMax = mesh.BboxMax,
                    MeshVerts = mesh.Verts,
                    MeshIndices = mesh.Indices,
                    OutlineRings = mesh.OutlineRings,
                    OuterRings = mesh.OuterRings,
                });
            }
            return outList;
        }

        // --- Provinces -----------------------------------------------------------------

        public static List<Province> LoadProvinces(string path)
        {
            var outList = new List<Province>();
            if (!File.Exists(path)) { Debug.LogWarning($"[geo] missing {path}"); return outList; }

            var root = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
            foreach (var f in root["features"])
            {
                var props = f["properties"];
                var mesh = BuildMeshFromGeometry(f["geometry"]);
                outList.Add(new Province
                {
                    Name = PropStr(props, "name"),
                    AdminCountry = PropStr(props, "admin"),
                    Adm0A3 = PropStr(props, "adm0_a3"),
                    TypeEn = PropStr(props, "type_en"),
                    Centroid = mesh.Centroid,
                    BboxMin = mesh.BboxMin,
                    BboxMax = mesh.BboxMax,
                    MeshVerts = mesh.Verts,
                    MeshIndices = mesh.Indices,
                    OutlineRings = mesh.OutlineRings,
                });
            }
            return outList;
        }

        // --- Cities --------------------------------------------------------------------

        public static List<City> LoadCities(string path)
        {
            var outList = new List<City>();
            if (!File.Exists(path)) { Debug.LogWarning($"[geo] missing {path}"); return outList; }

            var root = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
            foreach (var f in root["features"])
            {
                var props = f["properties"];
                if (!TryPointCoords(f["geometry"], out var pos)) continue;
                long popMax = PropLong(props, "POP_MAX");
                outList.Add(new City
                {
                    Name = PropStr(props, "NAME"),
                    Country = PropStr(props, "ADM0NAME"),
                    Pos = pos,
                    PopMax = popMax,
                    IsCapital = PropLong(props, "ADM0CAP") == 1,
                    Tier = CityTierExt.FromPop(popMax),
                });
            }
            return outList;
        }

        // --- Point features (ports, airports) ------------------------------------------

        public static List<PointFeature> LoadPoints(string path, string nameKey, string codeKey)
        {
            var outList = new List<PointFeature>();
            if (!File.Exists(path)) { Debug.LogWarning($"[geo] missing {path}"); return outList; }

            var root = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
            foreach (var f in root["features"])
            {
                var props = f["properties"];
                if (!TryPointCoords(f["geometry"], out var pos)) continue;
                outList.Add(new PointFeature
                {
                    Name = PropStr(props, nameKey),
                    Code = string.IsNullOrEmpty(codeKey) ? "" : PropStr(props, codeKey),
                    Pos = pos,
                });
            }
            return outList;
        }

        // --- Line features (roads, railways) --------------------------------------------

        public static List<LineFeature> LoadLines(string path, string nameKey)
        {
            var outList = new List<LineFeature>();
            if (!File.Exists(path)) { Debug.LogWarning($"[geo] missing {path}"); return outList; }

            var root = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
            int skipped = 0;
            foreach (var f in root["features"])
            {
                // Per-feature guard: one malformed feature costs itself, not the whole file.
                try
                {
                    var props = f["properties"];
                    var geom = f["geometry"];
                    string gtype = geom?["type"]?.ToString() ?? "";
                    var coords = geom?["coordinates"];
                    if (coords == null) continue;

                    var lines = new List<List<Vector2>>();
                    if (gtype == "LineString")
                        lines.Add(ParseLineCoords(coords));
                    else if (gtype == "MultiLineString")
                        foreach (var line in coords)
                            lines.Add(ParseLineCoords(line));
                    else
                        continue;

                    outList.Add(new LineFeature { Name = PropStr(props, nameKey), Lines = lines });
                }
                catch (System.Exception) { skipped++; }
            }
            if (skipped > 0) Debug.LogWarning($"[geo] {Path.GetFileName(path)}: skipped {skipped} unparseable line feature(s)");
            return outList;
        }

        static List<Vector2> ParseLineCoords(JToken coords)
        {
            var pts = new List<Vector2>();
            foreach (var pt in coords)
            {
                if (!TryReadLonLat(pt, out float lon, out float lat)) continue;
                pts.Add(GeoMath.LonLatToMercator(lon, lat));
            }
            return pts;
        }

        // Type-checked coordinate extraction. The naive `pt[0].Value<double>()` path throws
        // InvalidCastException from deep inside Newtonsoft if the token isn't a plain numeric
        // JValue — observed intermittently in a built player on real (verified-clean) data, so
        // never trust the fast path: verify shape, convert defensively, skip on any failure
        // instead of letting one bad point abort a whole dataset.
        static bool TryReadLonLat(JToken pt, out float lon, out float lat)
        {
            lon = 0f; lat = 0f;
            if (pt is not JArray arr || arr.Count < 2) return false;
            return TryTokenToFloat(arr[0], out lon) && TryTokenToFloat(arr[1], out lat);
        }

        static bool TryTokenToFloat(JToken t, out float result)
        {
            result = 0f;
            if (t is not JValue v || v.Value == null) return false;
            try
            {
                result = (float)System.Convert.ToDouble(v.Value, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            catch { return false; }
        }

        // --- Geometry -> mesh (port of build_mesh_from_geometry) ------------------------

        struct MeshParts
        {
            public List<Vector2> Verts;
            public List<int> Indices;
            public List<List<Vector2>> OutlineRings;
            public List<List<Vector2>> OuterRings;
            public Vector2 BboxMin, BboxMax, Centroid;
        }

        static MeshParts BuildMeshFromGeometry(JToken geom)
        {
            var verts = new List<Vector2>();
            var indices = new List<int>();
            var outlineRings = new List<List<Vector2>>();
            var outerRings = new List<List<Vector2>>();
            var bboxMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var bboxMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            double cx = 0, cy = 0, cn = 0;

            string gtype = geom?["type"]?.ToString() ?? "";
            var coords = geom?["coordinates"];
            if (coords == null)
                return new MeshParts { Verts = verts, Indices = indices, OutlineRings = outlineRings, OuterRings = outerRings, BboxMin = bboxMin, BboxMax = bboxMax, Centroid = Vector2.zero };

            // Normalize Polygon vs MultiPolygon into a list of polygons (each a list of rings).
            var polygons = new List<JToken>();
            if (gtype == "MultiPolygon")
                foreach (var poly in coords) polygons.Add(poly);
            else
                polygons.Add(coords);

            foreach (var poly in polygons)
            {
                var flat = new List<double>();
                var holeIndices = new List<int>();
                int ri = 0;
                foreach (var ringVal in poly)
                {
                    var ringPts = new List<Vector2>();
                    if (ri > 0) holeIndices.Add(flat.Count / 2);
                    foreach (var pt in ringVal)
                    {
                        if (!TryReadLonLat(pt, out float lon, out float lat)) continue;
                        // Reproject to Mercator BEFORE triangulating, not after — Earcut needs
                        // to operate on the actual final planar shape, and a nonlinear warp
                        // applied post-triangulation could in principle disagree with the
                        // ear-clipping decisions made in raw lon/lat space.
                        Vector2 m = GeoMath.LonLatToMercator(lon, lat);
                        flat.Add(m.x);
                        flat.Add(m.y);
                        ringPts.Add(m);
                        bboxMin.x = Mathf.Min(bboxMin.x, m.x);
                        bboxMin.y = Mathf.Min(bboxMin.y, m.y);
                        bboxMax.x = Mathf.Max(bboxMax.x, m.x);
                        bboxMax.y = Mathf.Max(bboxMax.y, m.y);
                        cx += m.x; cy += m.y; cn += 1;
                    }
                    if (ri == 0) outerRings.Add(new List<Vector2>(ringPts));
                    outlineRings.Add(ringPts);
                    ri++;
                }

                var tri = Earcut.Tessellate(flat.ToArray(), holeIndices.ToArray(), 2);
                int baseIdx = verts.Count;
                for (int i = 0; i + 1 < flat.Count; i += 2)
                    verts.Add(new Vector2((float)flat[i], (float)flat[i + 1]));
                foreach (int t in tri) indices.Add(baseIdx + t);
            }

            var centroid = cn > 0 ? new Vector2((float)(cx / cn), (float)(cy / cn)) : Vector2.zero;
            return new MeshParts { Verts = verts, Indices = indices, OutlineRings = outlineRings, OuterRings = outerRings, BboxMin = bboxMin, BboxMax = bboxMax, Centroid = centroid };
        }

        // --- Property helpers (port of mesh.rs prop_* / point_coords) -------------------

        static string PropStr(JToken props, string key) => props?[key]?.Type == JTokenType.String ? props[key].ToString() : (props?[key]?.ToString() ?? "");

        static string NormalizeIso(string isoA3, string adm0A3) =>
            string.IsNullOrEmpty(isoA3) || isoA3 == "-99" ? adm0A3 : isoA3;

        static long PropLong(JToken props, string key)
        {
            var v = props?[key];
            if (v == null) return 0;
            if (v.Type == JTokenType.Integer) return v.Value<long>();
            if (v.Type == JTokenType.Float) return (long)v.Value<double>();
            return 0;
        }

        static bool TryPointCoords(JToken geom, out Vector2 pos)
        {
            pos = Vector2.zero;
            var coords = geom?["coordinates"];
            if (!TryReadLonLat(coords, out float lon, out float lat)) return false;
            pos = GeoMath.LonLatToMercator(lon, lat);
            return true;
        }
    }
}
