using System.Collections.Generic;
using UnityEngine;

namespace Meridian.Geo
{
    // Faithful C# port of crates/ui_dashboard/src/geo/ from the original Rust/Bevy build.
    // Positions are stored as Vector2 (x, y) so Unity's vector math and Mesh APIs apply
    // directly. GeoJsonLoader reprojects every raw lon/lat pair through GeoMath.LonLatToMercator
    // as it parses, so by the time anything here is populated, x/y is Web Mercator space (in
    // degrees-normalized units, NOT raw longitude/latitude) — this is what lets live map tiles
    // (also Web Mercator) line up with country borders, cities, roads, etc. MapRenderer turns
    // it all into Unity meshes.

    public enum CityTier { Town, City, MajorCity, Megacity }

    public static class CityTierExt
    {
        public static string Label(this CityTier t) => t switch
        {
            CityTier.Town => "Town",
            CityTier.City => "City",
            CityTier.MajorCity => "Major City",
            CityTier.Megacity => "Megacity",
            _ => "Town",
        };

        // Marker radius in world units, before the capital bump.
        public static float Radius(this CityTier t) => t switch
        {
            CityTier.Town => 0.15f,
            CityTier.City => 0.20f,
            CityTier.MajorCity => 0.275f,
            CityTier.Megacity => 0.35f,
            _ => 0.15f,
        };

        public static CityTier FromPop(long popMax)
        {
            if (popMax >= 10_000_000) return CityTier.Megacity;
            if (popMax >= 1_000_000) return CityTier.MajorCity;
            if (popMax >= 100_000) return CityTier.City;
            return CityTier.Town;
        }
    }

    public class Country
    {
        public string Name = "";
        public string NameLong = "";
        public string IsoA2 = "";
        public string IsoA3 = "";
        public string Continent = "";
        public string Subregion = "";
        public long PopEst;
        public long GdpMd;
        public Vector2 Centroid;
        public Vector2 BboxMin;
        public Vector2 BboxMax;

        // Triangulated fill mesh, in lon/lat space.
        public List<Vector2> MeshVerts = new();
        public List<int> MeshIndices = new();

        // All rings (outer + holes, every polygon part) — for border stroke rendering.
        public List<List<Vector2>> OutlineRings = new();

        // Outer ring only, one per polygon part — for point-in-polygon hit testing.
        public List<List<Vector2>> OuterRings = new();
    }

    public class Province
    {
        public string Name = "";
        public string AdminCountry = "";
        public string Adm0A3 = "";
        public string TypeEn = "";
        public Vector2 Centroid;
        public Vector2 BboxMin;
        public Vector2 BboxMax;
        public List<Vector2> MeshVerts = new();
        public List<int> MeshIndices = new();
        public List<List<Vector2>> OutlineRings = new();
    }

    public class City
    {
        public string Name = "";
        public string Country = "";
        public Vector2 Pos;
        public long PopMax;
        public bool IsCapital;
        public CityTier Tier;
    }

    public class PointFeature // ports, airports, air bases, oil ports, nuclear plants
    {
        public string Name = "";
        public string Code = "";
        public Vector2 Pos;
    }

    // Roads and railways — open polylines (not closed rings like country/province outlines).
    // A single feature can be a MultiLineString, hence a list of separate line segments.
    public class LineFeature
    {
        public string Name = "";
        public List<List<Vector2>> Lines = new();
    }

    // A point where a named road's polyline crosses from one country's polygon into
    // another's — computed once at load time (GeoJsonLoader.ComputeBorderCrossings), not
    // sourced from any dataset (Natural Earth doesn't label border crossings).
    public class BorderCrossing
    {
        public Vector2 Pos;
        public string CountryA = "";
        public string CountryB = "";
        public string RoadName = "";
    }

    // A real intercountry causeway/bridge over water — hand-curated (no free dataset covers
    // these specifically), see GeoJsonLoader.WaterCrossingsSeed for sourcing notes per entry.
    public class WaterCrossing
    {
        public string Name = "";
        public string CountryA = "";
        public string CountryB = "";
        public List<Vector2> Line = new(); // real endpoint coordinates, straight segment between
    }

    public class GeoWorld
    {
        public List<Country> Countries = new();
        public List<Province> Provinces = new();
        public List<City> Cities = new();
        public List<PointFeature> Ports = new();
        public List<PointFeature> Airports = new();
        public List<LineFeature> Roads = new();
        public List<LineFeature> Railways = new();
        public List<PointFeature> AirBases = new();
        public List<PointFeature> OilPorts = new();
        public List<PointFeature> NuclearPlants = new();
        public List<BorderCrossing> BorderCrossings = new();
        public List<WaterCrossing> WaterCrossings = new();
    }

    public static class GeoMath
    {
        // Standard Web Mercator's polar cutoff — chosen (by every real map tile scheme) so
        // that the projected Y range at this latitude exactly matches the X range at the
        // longitude extremes, making the tile grid square (2^z by 2^z tiles per zoom level).
        public const float MaxMercatorLatitude = 85.05112878f;

        // "Degrees-normalized" Web Mercator: x = lon unchanged, y follows the standard Mercator
        // curve but scaled so it lands in the same numeric range as longitude (±180 at
        // MaxMercatorLatitude) instead of real-world meters. Everything in this project — country/
        // province polygons, cities, roads, camera zoom, the satellite basemap — uses this same
        // space, which is what makes live map tiles (also Web Mercator) line up with all of it.
        public static Vector2 LonLatToMercator(float lon, float lat)
        {
            float clamped = Mathf.Clamp(lat, -MaxMercatorLatitude, MaxMercatorLatitude);
            float latRad = clamped * Mathf.Deg2Rad;
            float y = Mathf.Log(Mathf.Tan(Mathf.PI / 4f + latRad / 2f)) * Mathf.Rad2Deg;
            return new Vector2(lon, y);
        }

        // Inverse of LonLatToMercator — used to compute a map tile's lon/lat corners from its
        // z/x/y index before reprojecting those corners into this same Mercator space.
        public static Vector2 MercatorToLonLat(float x, float y)
        {
            float latRad = 2f * Mathf.Atan(Mathf.Exp(y * Mathf.Deg2Rad)) - Mathf.PI / 2f;
            return new Vector2(x, latRad * Mathf.Rad2Deg);
        }

        // Ray-casting point-in-polygon test against a single ring (port of point_in_ring).
        public static bool PointInRing(Vector2 pt, List<Vector2> ring)
        {
            int n = ring.Count;
            if (n < 3) return false;
            bool inside = false;
            int j = n - 1;
            for (int i = 0; i < n; i++)
            {
                float xi = ring[i].x, yi = ring[i].y;
                float xj = ring[j].x, yj = ring[j].y;
                if (((yi > pt.y) != (yj > pt.y)) && (pt.x < (xj - xi) * (pt.y - yi) / (yj - yi) + xi))
                    inside = !inside;
                j = i;
            }
            return inside;
        }

        public static bool BboxContains(Vector2 min, Vector2 max, Vector2 pt) =>
            pt.x >= min.x && pt.x <= max.x && pt.y >= min.y && pt.y <= max.y;

        public static bool BboxOverlaps(Vector2 aMin, Vector2 aMax, Vector2 bMin, Vector2 bMax) =>
            aMin.x <= bMax.x && aMax.x >= bMin.x && aMin.y <= bMax.y && aMax.y >= bMin.y;
    }
}
