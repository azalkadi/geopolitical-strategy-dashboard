using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public static class GeoJsonLoader
    {
        static string DataDir => Path.Combine(Application.streamingAssetsPath, "worlddata");

        public static GeoWorld Load()
        {
            var world = new GeoWorld();
            world.Countries = LoadCountries(Path.Combine(DataDir, "ne_10m_admin_0_countries.geojson"));
            world.Provinces = LoadProvinces(Path.Combine(DataDir, "ne_10m_admin_1_states_provinces.geojson"));
            world.Cities = LoadCities(Path.Combine(DataDir, "ne_10m_populated_places.geojson"));
            world.Ports = LoadPoints(Path.Combine(DataDir, "ne_10m_ports.geojson"), "name", "");
            world.Airports = LoadPoints(Path.Combine(DataDir, "ne_10m_airports.geojson"), "name", "iata_code");
            return world;
        }

        // --- Countries -----------------------------------------------------------------

        public static List<Country> LoadCountries(string path)
        {
            var outList = new List<Country>();
            if (!File.Exists(path)) { Debug.LogError($"[geo] missing {path}"); return outList; }

            var root = JObject.Parse(File.ReadAllText(path));
            foreach (var f in root["features"])
            {
                var props = f["properties"];
                var mesh = BuildMeshFromGeometry(f["geometry"]);
                outList.Add(new Country
                {
                    Name = PropStr(props, "NAME"),
                    NameLong = PropStr(props, "NAME_LONG"),
                    IsoA2 = PropStr(props, "ISO_A2"),
                    IsoA3 = PropStr(props, "ISO_A3"),
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

            var root = JObject.Parse(File.ReadAllText(path));
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

            var root = JObject.Parse(File.ReadAllText(path));
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

            var root = JObject.Parse(File.ReadAllText(path));
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
                        float lon = (float)(pt[0]?.Value<double>() ?? 0.0);
                        float lat = (float)(pt[1]?.Value<double>() ?? 0.0);
                        flat.Add(lon);
                        flat.Add(lat);
                        ringPts.Add(new Vector2(lon, lat));
                        bboxMin.x = Mathf.Min(bboxMin.x, lon);
                        bboxMin.y = Mathf.Min(bboxMin.y, lat);
                        bboxMax.x = Mathf.Max(bboxMax.x, lon);
                        bboxMax.y = Mathf.Max(bboxMax.y, lat);
                        cx += lon; cy += lat; cn += 1;
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
            if (coords == null || coords.Type != JTokenType.Array || coords.Count() < 2) return false;
            pos = new Vector2((float)coords[0].Value<double>(), (float)coords[1].Value<double>());
            return true;
        }
    }
}
