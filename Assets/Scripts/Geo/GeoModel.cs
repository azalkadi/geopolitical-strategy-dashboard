using System.Collections.Generic;
using UnityEngine;

namespace Meridian.Geo
{
    // Faithful C# port of crates/ui_dashboard/src/geo/ from the original Rust/Bevy build.
    // Longitude/latitude are stored as Vector2 (x = lon, y = lat) so Unity's vector math and
    // Mesh APIs apply directly. Everything here is plain data loaded once at startup by
    // GeoJsonLoader; MapRenderer turns it into Unity meshes.

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

    public class PointFeature // ports, airports
    {
        public string Name = "";
        public string Code = "";
        public Vector2 Pos;
    }

    public class GeoWorld
    {
        public List<Country> Countries = new();
        public List<Province> Provinces = new();
        public List<City> Cities = new();
        public List<PointFeature> Ports = new();
        public List<PointFeature> Airports = new();
    }

    public static class GeoMath
    {
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
