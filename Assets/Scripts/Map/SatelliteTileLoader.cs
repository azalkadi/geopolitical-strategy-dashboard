using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Meridian.Map
{
    // Live, zoom-dependent satellite tile streaming — the actual fix for "zoomed in looks
    // pixelated": a single bundled texture has a hard resolution ceiling no matter how big you
    // make it (see MapRenderer.BuildSatelliteQuad), but real map tile services (Google/Bing/
    // ESRI/every commercial map app) serve progressively higher-resolution tiles as you zoom
    // in — a quadtree pyramid, not one flat image. This overlays exactly that on top of the
    // always-available offline Blue Marble background, only once zoomed in past the point
    // where live tiles would actually add detail. Requires an internet connection to do
    // anything; if a fetch fails (offline, tile doesn't exist, etc.) it silently leaves the
    // static background showing through underneath — no error state, no broken visuals.
    //
    // Tiles come from ESRI's public World Imagery service (standard Web Mercator z/x/y scheme,
    // no API key required, verified working down to z=18 city-block detail). World space here
    // is already Web Mercator (GeoMath.LonLatToMercator, degrees-normalized so x=lon and y
    // ranges ±180 at the standard polar cutoff) specifically so tile bounds compute directly
    // from world position with no per-tile lat/lon conversion needed.
    [RequireComponent(typeof(MapRenderer))]
    public class SatelliteTileLoader : MonoBehaviour
    {
        [Tooltip("Orthographic size below which live tiles start loading. Above this the static offline background already looks fine and no network requests are made.")]
        public float activateBelowOrthoSize = 10f;

        [Tooltip("Highest zoom level ever requested. ESRI has real detail well past this in cities, but this keeps tile count/request volume reasonable.")]
        public int maxZoomLevel = 17;

        public int maxConcurrentFetches = 8;
        public float refreshInterval = 0.25f;

        // z/y/x — ESRI's own path order, not the more common z/x/y.
        const string TileUrlTemplate = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{0}/{2}/{1}";
        // In front of the static satellite background (SatelliteZ=0.05 in MapRenderer) so live
        // tiles paint over it; behind every vector overlay (fills/borders/roads/cities all use
        // Z<=0 there) so borders/labels/markers still read on top of the imagery.
        const float TileZ = 0.02f;
        const int MaxCachedTiles = 900;

        Camera cam;
        MapRenderer map;
        Transform tilesRoot;
        Material tileMaterialTemplate;
        int activeFetches;
        float nextRefreshTime;
        bool warnedOnce;

        struct TileKey : IEquatable<TileKey>
        {
            public readonly int Z, X, Y;
            public TileKey(int z, int x, int y) { Z = z; X = x; Y = y; }
            public bool Equals(TileKey o) => Z == o.Z && X == o.X && Y == o.Y;
            public override bool Equals(object o) => o is TileKey k && Equals(k);
            public override int GetHashCode() => (Z * 397 ^ X) * 397 ^ Y;
        }

        class TileEntry
        {
            public GameObject Go;
            public int LastSeenFrame;
        }

        readonly Dictionary<TileKey, TileEntry> tiles = new();
        readonly HashSet<TileKey> pending = new();

        void Start()
        {
            cam = Camera.main;
            map = GetComponent<MapRenderer>();

            var shader = Shader.Find("Meridian/UnlitTexture");
            if (shader == null)
            {
                Debug.LogWarning("[map] UnlitTexture shader not found; live satellite tiles unavailable");
                enabled = false;
                return;
            }
            tileMaterialTemplate = new Material(shader);

            tilesRoot = new GameObject("SatelliteTiles").transform;
            tilesRoot.SetParent(transform, false);
            tilesRoot.gameObject.SetActive(false);
        }

        void Update()
        {
            if (cam == null || map == null) return;

            bool wantTiles = map.CurrentMode == MapMode.Satellite && cam.orthographicSize < activateBelowOrthoSize;
            if (tilesRoot.gameObject.activeSelf != wantTiles) tilesRoot.gameObject.SetActive(wantTiles);
            if (!wantTiles) return;

            if (Time.unscaledTime < nextRefreshTime) return;
            nextRefreshTime = Time.unscaledTime + refreshInterval;
            RefreshVisibleTiles();
        }

        void RefreshVisibleTiles()
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 c = cam.transform.position;
            float xMin = Mathf.Clamp(c.x - halfW, -180f, 180f);
            float xMax = Mathf.Clamp(c.x + halfW, -180f, 180f);
            float yMin = Mathf.Clamp(c.y - halfH, -180f, 180f);
            float yMax = Mathf.Clamp(c.y + halfH, -180f, 180f);

            // Pick a zoom level whose tile pixel density roughly matches screen pixel density —
            // each tile is 256px covering (360 / 2^z) world-units, so solve for z where that
            // matches world-units-per-screen-pixel.
            float worldUnitsPerScreenPixel = (halfH * 2f) / Mathf.Max(1, Screen.height);
            int z = Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(360f / (256f * worldUnitsPerScreenPixel), 2f)), 0, maxZoomLevel);
            int tilesAcross = 1 << z;

            int xLo = Mathf.Clamp(Mathf.FloorToInt((xMin + 180f) / 360f * tilesAcross) - 1, 0, tilesAcross - 1);
            int xHi = Mathf.Clamp(Mathf.CeilToInt((xMax + 180f) / 360f * tilesAcross) + 1, 0, tilesAcross - 1);
            int yLo = Mathf.Clamp(Mathf.FloorToInt((1f - yMax / 180f) / 2f * tilesAcross) - 1, 0, tilesAcross - 1);
            int yHi = Mathf.Clamp(Mathf.CeilToInt((1f - yMin / 180f) / 2f * tilesAcross) + 1, 0, tilesAcross - 1);

            for (int y = yLo; y <= yHi; y++)
                for (int x = xLo; x <= xHi; x++)
                    RequestTile(new TileKey(z, x, y), tilesAcross);
        }

        void RequestTile(TileKey key, int tilesAcross)
        {
            if (tiles.TryGetValue(key, out var entry))
            {
                entry.LastSeenFrame = Time.frameCount;
                return;
            }
            if (pending.Contains(key) || activeFetches >= maxConcurrentFetches) return;
            pending.Add(key);
            activeFetches++;
            StartCoroutine(FetchTile(key, tilesAcross));
        }

        IEnumerator FetchTile(TileKey key, int tilesAcross)
        {
            string url = string.Format(TileUrlTemplate, key.Z, key.X, key.Y);
            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();
                activeFetches--;
                pending.Remove(key);

                if (req.result != UnityWebRequest.Result.Success)
                {
                    if (!warnedOnce)
                    {
                        warnedOnce = true;
                        Debug.LogWarning($"[map] satellite tile fetch failed ({req.error}) — live tiles unavailable (no internet connection?). Falling back to the offline basemap only; this warning won't repeat.");
                    }
                    yield break;
                }

                var tex = DownloadHandlerTexture.GetContent(req);
                tex.wrapMode = TextureWrapMode.Clamp;
                CreateTileGo(key, tex, tilesAcross);
            }
        }

        void CreateTileGo(TileKey key, Texture2D tex, int tilesAcross)
        {
            float xMin = (float)key.X / tilesAcross * 360f - 180f;
            float xMax = (float)(key.X + 1) / tilesAcross * 360f - 180f;
            float yTop = 180f - (float)key.Y / tilesAcross * 360f;
            float yBottom = 180f - (float)(key.Y + 1) / tilesAcross * 360f;

            var mesh = new Mesh { name = $"tile_{key.Z}_{key.X}_{key.Y}" };
            mesh.vertices = new[]
            {
                new Vector3(xMin, yBottom, TileZ),
                new Vector3(xMax, yBottom, TileZ),
                new Vector3(xMax, yTop, TileZ),
                new Vector3(xMin, yTop, TileZ),
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();

            var go = new GameObject($"Tile_{key.Z}_{key.X}_{key.Y}");
            go.transform.SetParent(tilesRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(tileMaterialTemplate) { mainTexture = tex };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            tiles[key] = new TileEntry { Go = go, LastSeenFrame = Time.frameCount };
            EvictIfNeeded();
        }

        // Simple LRU-ish cap so an extended play session panning all over the map doesn't grow
        // tile memory forever — Unity's own frustum culling already keeps off-screen tiles from
        // costing anything to render, so this is purely a memory bound, not a visibility one.
        void EvictIfNeeded()
        {
            if (tiles.Count <= MaxCachedTiles) return;
            var ordered = new List<KeyValuePair<TileKey, TileEntry>>(tiles);
            ordered.Sort((a, b) => a.Value.LastSeenFrame.CompareTo(b.Value.LastSeenFrame));
            int toRemove = tiles.Count - MaxCachedTiles;
            for (int i = 0; i < toRemove; i++)
            {
                Destroy(ordered[i].Value.Go);
                tiles.Remove(ordered[i].Key);
            }
        }
    }
}
