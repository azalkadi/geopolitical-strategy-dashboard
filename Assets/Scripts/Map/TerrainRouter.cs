using System.Collections.Generic;
using UnityEngine;
using Meridian.Geo;

namespace Meridian.Map
{
    // Terrain-following route planner for player-built roads and railways. Real highways and
    // rail lines don't run in straight lines between cities — they bend around mountains, hug
    // valleys and coasts, and bridge or tunnel water only where they must. There's no elevation
    // dataset in the project, but the satellite basemap already encodes the terrain we care
    // about: oceans/large water read dark blue, high mountains and snow read bright white/grey.
    // So this samples the basemap into a cost field and runs A* over it — the path curves to
    // minimise terrain-weighted distance, exactly the trade-off a real route surveyor makes.
    //
    // Railways are penalised harder than roads on both water and gradient, because trains can't
    // climb steep grades or ford water the way a road can — so a rail line detours around a
    // mountain a road would switchback straight over.
    public class TerrainRouter
    {
        readonly Texture2D basemap;

        public TerrainRouter(Texture2D basemap) { this.basemap = basemap; }

        public struct Route
        {
            public List<Vector2> PathMercator; // waypoints in world (Mercator) space, incl. both cities
            public double GeometricKm;          // real great-circle length along the path
            public double WeightedKm;           // terrain-weighted length (drives cost & build time)
        }

        // Per-point terrain cost multiplier (1 = easy flat land). Samples the basemap at the
        // given lon/lat. Water and mountains cost more; rail costs even more on both.
        float TerrainCost(float lon, float lat, bool rail)
        {
            if (basemap == null) return 1f;
            float u = (lon + 180f) / 360f;
            float v = (lat + 90f) / 180f; // basemap is linear-in-latitude, north at top (v=1)
            Color c = basemap.GetPixelBilinear(u, v);
            float bright = (c.r + c.g + c.b) / 3f;

            // Water: blue is the dominant channel and the pixel is dark (open sea / large lakes).
            bool water = c.b > c.r + 0.03f && c.b > c.g && bright < 0.42f;
            // Mountains / snow / bare rock: bright, non-water terrain. Deserts are also bright, a
            // deliberate imperfection — hard-to-cross arid land reading as costly is acceptable.
            float mountain = Mathf.Max(0f, bright - 0.58f);

            float cost = 1f;
            if (water) cost += rail ? 14f : 9f;      // bridges/tunnels; rail avoids water harder
            cost += mountain * (rail ? 42f : 22f);   // gradients; rail avoids climbs harder
            return cost;
        }

        // Plans a route between two cities given in world (Mercator) space. Returns a straight
        // two-point fallback when the basemap is unavailable or the pair is degenerate/too far
        // spread (antimeridian span), so callers always get a usable Route.
        public Route Plan(Vector2 fromMercator, Vector2 toMercator, bool rail)
        {
            Vector2 aLL = GeoMath.MercatorToLonLat(fromMercator.x, fromMercator.y);
            Vector2 bLL = GeoMath.MercatorToLonLat(toMercator.x, toMercator.y);

            float lonSpan = Mathf.Abs(bLL.x - aLL.x);
            float latSpan = Mathf.Abs(bLL.y - aLL.y);
            float maxSpan = Mathf.Max(lonSpan, latSpan);
            // Basemap missing, points coincident, or a wrap-around pair we won't grid cleanly:
            // fall back to the old straight line.
            if (basemap == null || maxSpan < 1e-4f || lonSpan > 175f)
                return Straight(fromMercator, toMercator, aLL, bLL);

            // Grid the padded bounding box. Padding lets the path bow outward around an obstacle
            // instead of being trapped inside the tight A–B box.
            float pad = Mathf.Max(2f, maxSpan * 0.4f);
            float minLon = Mathf.Clamp(Mathf.Min(aLL.x, bLL.x) - pad, -180f, 180f);
            float maxLon = Mathf.Clamp(Mathf.Max(aLL.x, bLL.x) + pad, -180f, 180f);
            float minLat = Mathf.Clamp(Mathf.Min(aLL.y, bLL.y) - pad, -85f, 85f);
            float maxLat = Mathf.Clamp(Mathf.Max(aLL.y, bLL.y) + pad, -85f, 85f);

            float step = Mathf.Clamp(maxSpan / 90f, 0.08f, 0.5f);
            int nx = Mathf.Clamp(Mathf.CeilToInt((maxLon - minLon) / step) + 1, 2, 150);
            int ny = Mathf.Clamp(Mathf.CeilToInt((maxLat - minLat) / step) + 1, 2, 150);
            float dx = (maxLon - minLon) / (nx - 1);
            float dy = (maxLat - minLat) / (ny - 1);

            float LonAt(int x) => minLon + x * dx;
            float LatAt(int y) => minLat + y * dy;

            var cost = new float[nx * ny];
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                    cost[y * nx + x] = TerrainCost(LonAt(x), LatAt(y), rail);

            int Cell(int x, int y) => y * nx + x;
            (int x, int y) Nearest(Vector2 ll) => (
                Mathf.Clamp(Mathf.RoundToInt((ll.x - minLon) / dx), 0, nx - 1),
                Mathf.Clamp(Mathf.RoundToInt((ll.y - minLat) / dy), 0, ny - 1));

            var start = Nearest(aLL);
            var goal = Nearest(bLL);
            int startIdx = Cell(start.x, start.y), goalIdx = Cell(goal.x, goal.y);

            // Straight-line km between two lon/lat grid points, for edge weights and heuristic.
            double Km(int ax, int ay, int bx, int by) =>
                Meridian.Sim.InfrastructureSystem.DistanceKm(
                    new Vector2(LonAt(ax), LatAt(ay)), new Vector2(LonAt(bx), LatAt(by)));

            var g = new double[nx * ny];
            var came = new int[nx * ny];
            for (int i = 0; i < g.Length; i++) { g[i] = double.PositiveInfinity; came[i] = -1; }
            g[startIdx] = 0;

            // Lazy-deletion A* with a SortedSet as the priority queue (f, tie-break by index).
            var open = new SortedSet<(double f, int idx)>();
            double H(int idx) { int x = idx % nx, y = idx / nx; return Km(x, y, goal.x, goal.y); }
            open.Add((H(startIdx), startIdx));

            int[] ddx = { 1, -1, 0, 0, 1, 1, -1, -1 };
            int[] ddy = { 0, 0, 1, -1, 1, -1, 1, -1 };
            bool found = false;
            while (open.Count > 0)
            {
                var cur = open.Min; open.Remove(cur);
                int idx = cur.idx;
                if (idx == goalIdx) { found = true; break; }
                int cx = idx % nx, cy = idx / nx;
                for (int d = 0; d < 8; d++)
                {
                    int ex = cx + ddx[d], ey = cy + ddy[d];
                    if (ex < 0 || ex >= nx || ey < 0 || ey >= ny) continue;
                    int nIdx = Cell(ex, ey);
                    double segKm = Km(cx, cy, ex, ey);
                    double stepCost = segKm * 0.5 * (cost[idx] + cost[nIdx]);
                    double ng = g[idx] + stepCost;
                    if (ng < g[nIdx])
                    {
                        g[nIdx] = ng;
                        came[nIdx] = idx;
                        open.Add((ng + H(nIdx), nIdx));
                    }
                }
            }

            if (!found) return Straight(fromMercator, toMercator, aLL, bLL);

            // Reconstruct grid path (goal -> start), then reverse.
            var cells = new List<int>();
            for (int at = goalIdx; at != -1; at = came[at]) cells.Add(at);
            cells.Reverse();

            // Build lon/lat waypoints: exact A city, the grid interior (simplified), exact B city.
            var pts = new List<Vector2> { aLL };
            for (int i = 1; i < cells.Count - 1; i++)
            {
                int ci = cells[i];
                pts.Add(new Vector2(LonAt(ci % nx), LatAt(ci / nx)));
            }
            pts.Add(bLL);
            pts = Simplify(pts, dx * 0.5f);

            // Convert to Mercator and measure geometric + terrain-weighted length.
            var path = new List<Vector2>(pts.Count);
            double geoKm = 0, wKm = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                path.Add(GeoMath.LonLatToMercator(pts[i].x, pts[i].y));
                if (i > 0)
                {
                    double seg = Meridian.Sim.InfrastructureSystem.DistanceKm(pts[i - 1], pts[i]);
                    geoKm += seg;
                    float midLon = (pts[i - 1].x + pts[i].x) * 0.5f, midLat = (pts[i - 1].y + pts[i].y) * 0.5f;
                    wKm += seg * TerrainCost(midLon, midLat, rail);
                }
            }
            return new Route { PathMercator = path, GeometricKm = geoKm, WeightedKm = wKm };
        }

        static Route Straight(Vector2 fromM, Vector2 toM, Vector2 aLL, Vector2 bLL)
        {
            double km = Meridian.Sim.InfrastructureSystem.DistanceKm(aLL, bLL);
            return new Route
            {
                PathMercator = new List<Vector2> { fromM, toM },
                GeometricKm = km,
                WeightedKm = km,
            };
        }

        // Drop interior points that lie ~on the straight line between their neighbours, so a
        // long straight stretch isn't stored as dozens of grid cells. tol is in degrees.
        static List<Vector2> Simplify(List<Vector2> pts, float tol)
        {
            if (pts.Count < 3) return pts;
            var outp = new List<Vector2> { pts[0] };
            for (int i = 1; i < pts.Count - 1; i++)
            {
                Vector2 a = outp[outp.Count - 1], b = pts[i], c = pts[i + 1];
                Vector2 ac = c - a;
                float len = ac.magnitude;
                float dist = len < 1e-5f ? (b - a).magnitude
                    : Mathf.Abs((b.x - a.x) * ac.y - (b.y - a.y) * ac.x) / len; // perpendicular distance
                if (dist > tol) outp.Add(b);
            }
            outp.Add(pts[pts.Count - 1]);
            return outp;
        }
    }
}
