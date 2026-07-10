using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Meridian.Geo;
using Meridian.Sim;

namespace Meridian.Map
{
    public enum MapMode { Political, Satellite }


    // First-milestone flat map renderer: loads GeoWorld and builds one Unity mesh per country,
    // flat-colored by a hash of the country name (the same "no two neighbors share a tint"
    // trick the original Rust build used). lon/lat maps straight to world XY (x = lon in
    // [-180,180], y = lat in [-90,90]) so an orthographic camera shows an equirectangular map.
    //
    // This is the equivalent of the old draw_map country-fill pass, but built once into GPU
    // meshes instead of re-projected every frame — which is exactly the performance fix the
    // Bevy/egui version needed. Borders, provinces, cities, hit-testing, and map modes are
    // follow-on milestones.
    public class MapRenderer : MonoBehaviour
    {
        [Tooltip("Material used for all country fills. If left empty, one is created from the bundled FlatVertexColor shader.")]
        public Material fillMaterial;

        [Tooltip("Ocean/background color (also set the Camera's background to match).")]
        public Color oceanColor = new Color(0.086f, 0.149f, 0.227f);

        [Tooltip("Country border line color.")]
        public Color borderColor = new Color(1f, 1f, 1f, 0.85f);

        [Tooltip("Province border line color (subtler than country borders).")]
        public Color provinceColor = new Color(1f, 1f, 1f, 0.28f);

        public GeoWorld World { get; private set; }
        public EconomySystem Economy { get; private set; }
        public NationalSystem National { get; private set; }

        // Zoom-gated layer roots (toggled by MapLayers based on camera zoom).
        public GameObject ProvincesRoot { get; private set; }
        public GameObject[] CityTierRoots { get; private set; } // indexed by (int)CityTier

        public MapMode CurrentMode { get; private set; } = MapMode.Political;

        Material borderMaterial;
        readonly List<GameObject> countryFillObjects = new();
        GameObject satelliteGo;
        // z offsets so each layer draws above the one under it.
        const float FillZ = 0f;
        const float BorderZ = -0.1f;
        const float ProvinceZ = -0.05f;
        const float CityZ = -0.2f;
        const float SatelliteZ = 0.05f; // behind fills, so hiding fills reveals it

        static readonly Color[] Palette =
        {
            new Color32(196, 74, 68, 255),   // red
            new Color32(74, 122, 196, 255),  // blue
            new Color32(86, 156, 90, 255),   // green
            new Color32(214, 172, 66, 255),  // gold
            new Color32(138, 96, 176, 255),  // purple
            new Color32(214, 128, 62, 255),  // orange
            new Color32(70, 158, 158, 255),  // teal
            new Color32(196, 100, 140, 255), // rose
        };

        void Start()
        {
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = oceanColor;

            if (fillMaterial == null)
            {
                // Try the custom vertex-color shader, then progressively more built-in
                // fallbacks. In a stripped build Shader.Find can return null for any of
                // these, so never pass null to `new Material` (that throws) — warn instead.
                var shader = Shader.Find("Meridian/FlatVertexColor")
                             ?? Shader.Find("Sprites/Default")
                             ?? Shader.Find("Unlit/Color")
                             ?? Shader.Find("Hidden/Internal-Colored");
                if (shader == null)
                {
                    Debug.LogError("[map] no usable shader found (all stripped from build?) — add Meridian/FlatVertexColor to Always Included Shaders");
                    return;
                }
                fillMaterial = new Material(shader);
            }

            borderMaterial = new Material(fillMaterial.shader);

            Debug.Log("[map] loading geo data (this parses + triangulates ~258 countries; one-time cost)...");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            World = GeoJsonLoader.Load();
            sw.Stop();
            Debug.Log($"[map] loaded {World.Countries.Count} countries, {World.Provinces.Count} provinces, {World.Cities.Count} cities in {sw.ElapsedMilliseconds} ms");

            // Seed a live economy for every country (ticked by MapInteraction).
            Economy = EconomySystem.Seed(World.Countries);
            Debug.Log($"[map] seeded {Economy.States.Count} country economies");

            // Politics/Military/Diplomacy/Society/Technology — derived indices, ticked alongside Economy.
            National = NationalSystem.Seed(World.Countries.Count);

            BuildCountryMeshes();
            BuildCountryBorders();
            BuildProvinceBorders();
            BuildCityMarkers();
            BuildSatelliteQuad();
        }

        // Loads the satellite basemap (StreamingAssets/basemap/satellite.jpg) onto a single quad
        // spanning the same lon/lat space as the country meshes. Starts hidden; SetMode reveals
        // it and hides the flat country fills (they're opaque, so both can't show at once).
        void BuildSatelliteQuad()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "basemap", "satellite.jpg");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[map] satellite basemap not found at {path} — Satellite mode will be unavailable");
                return;
            }

            var shader = Shader.Find("Meridian/UnlitTexture");
            if (shader == null)
            {
                Debug.LogWarning("[map] UnlitTexture shader not found; skipping satellite quad");
                return;
            }

            byte[] bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.LoadImage(bytes); // auto-sizes the texture to the source image
            tex.wrapMode = TextureWrapMode.Clamp;
            var mat = new Material(shader) { mainTexture = tex };

            var mesh = new Mesh { name = "SatelliteQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-180f, -90f, SatelliteZ),
                new Vector3(180f, -90f, SatelliteZ),
                new Vector3(180f, 90f, SatelliteZ),
                new Vector3(-180f, 90f, SatelliteZ),
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();

            satelliteGo = new GameObject("SatelliteBasemap");
            satelliteGo.transform.SetParent(transform, false);
            satelliteGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = satelliteGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            satelliteGo.SetActive(false);
            Debug.Log($"[map] loaded satellite basemap ({tex.width}x{tex.height})");
        }

        public void SetMode(MapMode mode)
        {
            CurrentMode = mode;
            foreach (var go in countryFillObjects) go.SetActive(mode == MapMode.Political);
            if (satelliteGo != null) satelliteGo.SetActive(mode == MapMode.Satellite);
        }

        void BuildCountryBorders()
        {
            foreach (var c in World.Countries)
                MakeLineMeshObject(c.Name + " (border)", c.OutlineRings, borderColor, BorderZ, transform);
            Debug.Log("[map] built country border meshes");
        }

        // Province borders live under a single root the MapLayers component enables only when
        // zoomed in — 4,596 of them are far too dense to show (or afford) at world view.
        // Unity frustum-culls the individual meshes once the root is active, so only the ones
        // actually on screen cost anything.
        void BuildProvinceBorders()
        {
            ProvincesRoot = new GameObject("Provinces");
            ProvincesRoot.transform.SetParent(transform, false);
            ProvincesRoot.SetActive(false);
            foreach (var p in World.Provinces)
                MakeLineMeshObject(p.Name + " (prov)", p.OutlineRings, provinceColor, ProvinceZ, ProvincesRoot.transform);
            Debug.Log($"[map] built {World.Provinces.Count} province border meshes");
        }

        // One combined quad mesh per city tier, each under its own root so MapLayers can reveal
        // finer tiers as you zoom in (megacities always; smaller towns only up close). Markers
        // render as constant-screen-size soft dots via the ScreenDot shader: every quad's 4
        // vertices sit at the SAME world position (the city's lon/lat) with a UV corner tag; the
        // shader reconstructs the actual pixel-sized offset at draw time, so a dot looks the same
        // size on screen at any zoom instead of ballooning into a giant world-space square (which
        // is what happened when this used a fixed lon/lat-degree half-size).
        void BuildCityMarkers()
        {
            var dotShader = Shader.Find("Meridian/ScreenDot");
            var tiers = System.Enum.GetValues(typeof(CityTier));
            CityTierRoots = new GameObject[tiers.Length];
            if (dotShader == null)
            {
                Debug.LogWarning("[map] ScreenDot shader not found; skipping city markers");
                return;
            }

            // Marker radius in SCREEN PIXELS per tier — bigger tiers read as bigger dots.
            float[] pixelRadius = { 2f, 3f, 4.5f, 6.5f }; // Town, City, MajorCity, Megacity
            Color cityColor = new Color(1f, 1f, 1f, 0.95f);
            Color capitalColor = new Color(1f, 0.85f, 0.3f, 1f);

            var byTier = new List<City>[tiers.Length];
            for (int t = 0; t < byTier.Length; t++) byTier[t] = new List<City>();
            foreach (var city in World.Cities) byTier[(int)city.Tier].Add(city);

            for (int t = 0; t < byTier.Length; t++)
            {
                var root = new GameObject($"Cities_{((CityTier)t)}");
                root.transform.SetParent(transform, false);
                CityTierRoots[t] = root;

                var list = byTier[t];
                if (list.Count == 0) continue;

                var verts = new List<Vector3>(list.Count * 4);
                var cols = new List<Color>(list.Count * 4);
                var uvs = new List<Vector2>(list.Count * 4);
                var tris = new List<int>(list.Count * 6);
                foreach (var city in list)
                {
                    int b = verts.Count;
                    var center = new Vector3(city.Pos.x, city.Pos.y, CityZ);
                    verts.Add(center); verts.Add(center); verts.Add(center); verts.Add(center);
                    uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                    uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                    var col = city.IsCapital ? capitalColor : cityColor;
                    cols.Add(col); cols.Add(col); cols.Add(col); cols.Add(col);
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                    tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
                }

                var mesh = new Mesh { name = $"Cities_{((CityTier)t)}" };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(verts);
                mesh.SetColors(cols);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                // Every vertex sits at the same point pre-shader (the shader expands it in clip
                // space), so auto-computed bounds are a zero-size point per marker — pad them or
                // Unity's frustum culling will drop markers near the screen edge.
                var b2 = mesh.bounds;
                b2.Expand(5f);
                mesh.bounds = b2;

                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = root.AddComponent<MeshRenderer>();
                var dotMat = new Material(dotShader) { name = $"CityDot_{(CityTier)t}" };
                dotMat.SetFloat("_PixelRadius", pixelRadius[t]);
                mr.sharedMaterial = dotMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                // MapLayers turns finer tiers on by zoom; start all off except megacities.
                root.SetActive((CityTier)t == CityTier.Megacity);
            }
            Debug.Log("[map] built city markers");
        }

        // Builds a line-list mesh GameObject from a set of lon/lat rings, parented under `parent`.
        void MakeLineMeshObject(string name, List<List<Vector2>> rings, Color color, float z, Transform parent)
        {
            if (rings == null || rings.Count == 0) return;

            var verts = new List<Vector3>();
            var idx = new List<int>();
            foreach (var ring in rings)
            {
                if (ring.Count < 2) continue;
                int baseIdx = verts.Count;
                for (int i = 0; i < ring.Count; i++)
                    verts.Add(new Vector3(ring[i].x, ring[i].y, z));
                for (int i = 0; i < ring.Count; i++)
                {
                    idx.Add(baseIdx + i);
                    idx.Add(baseIdx + (i + 1) % ring.Count);
                }
            }
            if (verts.Count == 0) return;

            var mesh = new Mesh { name = name };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var colors = new Color[verts.Count];
            for (int i = 0; i < colors.Length; i++) colors[i] = color;
            mesh.SetVertices(verts);
            mesh.colors = colors;
            mesh.SetIndices(idx.ToArray(), MeshTopology.Lines, 0);
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = borderMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        void BuildCountryMeshes()
        {
            for (int ci = 0; ci < World.Countries.Count; ci++)
            {
                var c = World.Countries[ci];
                if (c.MeshVerts.Count == 0 || c.MeshIndices.Count == 0) continue;

                var color = Palette[(int)(HashName(c.Name) % (ulong)Palette.Length)];

                var mesh = new Mesh { name = c.Name };
                // Countries like Russia/Canada/Antarctica exceed the 16-bit vertex limit.
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                var verts = new Vector3[c.MeshVerts.Count];
                var colors = new Color[c.MeshVerts.Count];
                for (int i = 0; i < c.MeshVerts.Count; i++)
                {
                    verts[i] = new Vector3(c.MeshVerts[i].x, c.MeshVerts[i].y, 0f);
                    colors[i] = color;
                }
                mesh.vertices = verts;
                mesh.colors = colors;
                mesh.triangles = c.MeshIndices.ToArray();
                mesh.RecalculateBounds();

                var go = new GameObject(c.Name);
                go.transform.SetParent(transform, false);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = fillMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                countryFillObjects.Add(go);
            }
            Debug.Log("[map] built country fill meshes");
        }

        // FNV-1a over the name — deterministic-but-varied palette index per country.
        static ulong HashName(string s)
        {
            ulong h = 0xcbf29ce484222325UL;
            foreach (char ch in s)
            {
                h ^= (byte)ch;
                h *= 0x100000001b3UL;
            }
            return h;
        }
    }
}
