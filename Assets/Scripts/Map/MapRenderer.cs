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

        [Tooltip("Major road line color.")]
        public Color roadColor = new Color(0.90f, 0.70f, 0.20f, 0.95f);

        [Tooltip("Railway line/tie color.")]
        public Color railwayColor = new Color(0.12f, 0.12f, 0.14f, 0.95f);

        [Tooltip("Road half-width, in SCREEN PIXELS (constant regardless of zoom — see ScreenLine.shader). A fixed world-space width used to balloon into multi-km-wide blobs once zoomed in, since 1 Mercator-degree-unit is ~111km at the equator.")]
        public float roadWidth = 1.5f;

        [Tooltip("Railway rail-line half-width, in screen pixels — the base line is thinner than roads; cross-ties (see railTieColor) are what read as \"railway\" rather than extra line width.")]
        public float railwayWidth = 1f;

        [Tooltip("Cross-tie tick mark color — drawn at regular intervals across the railway line, the classic cartographic railroad symbol.")]
        public Color railTieColor = new Color(0.85f, 0.84f, 0.80f, 0.95f);

        [Tooltip("Spacing between cross-ties, in world (Mercator-degree) units.")]
        public float railTieSpacing = 0.5f;

        [Tooltip("Water-crossing (causeway/bridge) line color — bright enough to read as special infrastructure, distinct from ordinary roads.")]
        public Color waterCrossingColor = new Color(0.55f, 0.95f, 1f, 1f);

        [Tooltip("Border-crossing marker color.")]
        public Color borderCrossingColor = new Color(1f, 0.55f, 0.25f, 1f);

        [Tooltip("Fill/border color for the player's own country once a game has started — a country has no meaningful 'relation to itself', so it always gets this fixed highlight instead of a spot on the relation gradient.")]
        public Color selfColor = new Color(0.80f, 0.66f, 0.22f, 1f);

        [Tooltip("Relation-gradient color at 0 (hostile) — see RefreshCountryColors.")]
        public Color hostileRelationColor = new Color(0.72f, 0.20f, 0.16f, 1f);

        [Tooltip("Relation-gradient color at 50 (neutral).")]
        public Color neutralRelationColor = new Color(0.50f, 0.48f, 0.42f, 1f);

        [Tooltip("Relation-gradient color at 100 (friendly).")]
        public Color friendlyRelationColor = new Color(0.22f, 0.60f, 0.28f, 1f);

        public GeoWorld World { get; private set; }
        public EconomySystem Economy { get; private set; }
        public NationalSystem National { get; private set; }
        public DiplomacySystem Diplomacy { get; private set; }
        public WarSystem Wars { get; private set; }
        public WorldAI WorldAI { get; private set; }
        public GeoWorldNames CountryNames { get; private set; }
        public InfrastructureSystem Infrastructure { get; private set; }
        public LegislatureSystem Legislature { get; private set; }

        // Zoom-gated layer roots (toggled by MapLayers based on camera zoom).
        public GameObject ProvincesRoot { get; private set; }
        public GameObject[] CityTierRoots { get; private set; } // indexed by (int)CityTier
        public GameObject AirportsRoot { get; private set; }
        public GameObject PortsRoot { get; private set; }
        public GameObject RoadsRoot { get; private set; }
        public GameObject RailwaysRoot { get; private set; }
        public GameObject AirBasesRoot { get; private set; }
        public GameObject OilPortsRoot { get; private set; }
        public GameObject NuclearPlantsRoot { get; private set; }
        public GameObject BorderCrossingsRoot { get; private set; }
        public GameObject WaterCrossingsRoot { get; private set; }
        public GameObject WaterCrossingLinesRoot { get; private set; }
        public GameObject PlayerInfrastructureRoot { get; private set; }

        public MapMode CurrentMode { get; private set; } = MapMode.Political;

        Material borderMaterial;
        readonly List<GameObject> countryFillObjects = new();
        GameObject satelliteGo;
        // Exposed for the minimap (GameUIRoot) — reuses the already-loaded basemap instead of
        // rendering/loading a second copy just for a small corner overlay.
        public Texture2D SatelliteTexture { get; private set; }
        // Indexed by country index (same order as World.Countries) so RefreshCountryColors can
        // recolor an individual country's fill/border without rebuilding its geometry — null
        // entries are countries whose mesh/outline was empty and never got a GameObject at all.
        Mesh[] fillMeshByCountry;
        Mesh[] borderMeshByCountry;
        // z offsets so each layer draws above the one under it.
        const float FillZ = 0f;
        const float BorderZ = -0.1f;
        const float ProvinceZ = -0.05f;
        const float RoadZ = -0.11f;
        const float RailZ = -0.12f;
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
            National = NationalSystem.Seed(World.Countries);

            // Bilateral relations for every country pair, seeded from real geography.
            Diplomacy = DiplomacySystem.Seed(World.Countries);
            Debug.Log($"[map] seeded diplomacy relations for {World.Countries.Count} countries");

            // Wars + autonomous world activity (AI wars/agreements, surfaced via WorldFeed toasts).
            Wars = new WarSystem();

            // Real early-2026 geopolitics overlaid on the geography baseline: alliance blocs,
            // curated hostilities/friendships, and the interstate wars actually in progress
            // when the game clock starts (see Sim/WorldAlignments.cs).
            int alignedPairs = WorldAlignments.Apply(World.Countries, Diplomacy);
            int seededWars = WorldAlignments.SeedWars(World.Countries, Wars);
            Debug.Log($"[map] applied {alignedPairs} curated relation pairs, seeded {seededWars} active conflicts");
            // Spot checks in the boot log: a war pair, a bloc-floor pair (France also proves
            // the ISO_A3="-99" fallback works — without it FRA resolves to nothing and gets no
            // EU floor), and a curated-pair-overrides-bloc case (GRC-TUR are both NATO).
            int FindIso(string iso) => World.Countries.FindIndex(c => c.IsoA3 == iso);
            int iRus = FindIso("RUS"), iUkr = FindIso("UKR"), iFra = FindIso("FRA"), iDeu = FindIso("DEU"), iGrc = FindIso("GRC"), iTur = FindIso("TUR");
            if (iRus >= 0 && iUkr >= 0 && iFra >= 0 && iDeu >= 0 && iGrc >= 0 && iTur >= 0)
                Debug.Log($"[map] alignment spot-check: RUS-UKR={Diplomacy.GetRelation(iRus, iUkr):0} (expect 0, atWar={Wars.WarBetween(iRus, iUkr) != null}) FRA-DEU={Diplomacy.GetRelation(iFra, iDeu):0} (expect >=72) GRC-TUR={Diplomacy.GetRelation(iGrc, iTur):0} (expect 30)");
            CountryNames = new GeoWorldNames(i => i >= 0 && i < World.Countries.Count ? World.Countries[i].Name : "?");
            WorldAI = new WorldAI(i => i >= 0 && i < World.Countries.Count ? World.Countries[i].Continent : "");

            // Player-buildable road/rail links — empty until the player actually builds one
            // (or a save is loaded); see RebuildPlayerInfrastructure.
            Infrastructure = new InfrastructureSystem();

            // The bill pipeline (propose → parliamentary vote or decree → enact).
            Legislature = new LegislatureSystem();

            BuildCountryMeshes();
            BuildCountryBorders();
            BuildProvinceBorders();
            BuildCityMarkers();
            BuildSatelliteQuad();
            BuildInfrastructureMarkers();
            BuildRoadsAndRailways();
            BuildBorderCrossingMarkers();
            BuildWaterCrossings();
            RebuildPlayerInfrastructure();

            // Satellite imagery is the default view — the flat political fills are a toggle,
            // not the base map — but only if the basemap actually loaded; otherwise stay on
            // Political so the world isn't just an empty ocean-colored void.
            SetMode(satelliteGo != null ? MapMode.Satellite : MapMode.Political);
        }

        // Swaps the freshly-seeded simulation for a saved one (geography and meshes stay as
        // built by Start — only the mutable sim state changes). Caller has already validated
        // the save via SaveLoad.TryRead.
        public void ApplySave(SaveGame save)
        {
            SaveLoad.Apply(save, Economy, National);
            Diplomacy = save.Diplomacy;
            Wars = save.Wars;
            // Older saves predate player-buildable infrastructure / the legislature and
            // deserialize these as null.
            Infrastructure = save.Infrastructure ?? new InfrastructureSystem();
            Legislature = save.Legislature ?? new LegislatureSystem();

            // Migration: saves written before population dynamics existed deserialize with
            // Population = 0 — reseed those from the geo data instead of simulating a
            // permanently empty world. LastCreditRating shipped in the same feature batch, so
            // the same saves deserialize it to its "AAA" field default regardless of the
            // country's actual carried-over debt — baseline it to the current real rating too,
            // or a save with pre-existing debt fires a spurious downgrade toast on its first tick.
            for (int i = 0; i < Economy.States.Count && i < World.Countries.Count; i++)
            {
                var e = Economy.States[i];
                if (e.Population < 1)
                {
                    e.Population = System.Math.Max(World.Countries[i].PopEst, 10_000L);
                    if (e.PopulationGrowth == 0f) e.PopulationGrowth = 1.0f;
                    e.LastCreditRating = e.CreditRatingLabel;
                }
                // Migration: saves written before sector composition existed deserialize with an
                // empty Sectors list — reseed from the country's current GDP/companies so the
                // Economy panel and the growth nudge have real data instead of nothing.
                if (e.Sectors == null || e.Sectors.Count == 0)
                    e.Sectors = SectorInfo.Seed(e.Gdp, e.GdpPerCapita, e.Companies);

                // Migration: saves written before live parliaments existed have National.Parties
                // == null — reseed the per-game party list from the curated static data so
                // elections and vote math work on a loaded game too.
                if (National != null && i < National.States.Count && National.States[i].Parties == null)
                {
                    var prof = CountryProfiles.Get(World.Countries[i].IsoA3);
                    if (prof?.Parties != null)
                    {
                        var copy = new List<PartyProfile>(prof.Parties.Count);
                        foreach (var p in prof.Parties) copy.Add(new PartyProfile(p.Name, p.EconLean, p.SeatShare));
                        National.States[i].Parties = copy;
                    }
                }
            }
            // Route paths aren't serialized (see BuiltRoute.PathMercator) — re-plan each one
            // deterministically from its city endpoints so loaded roads/rail still follow the
            // terrain instead of snapping back to straight lines.
            if (Infrastructure != null)
                foreach (var r in Infrastructure.Routes)
                    if (r.FromCity >= 0 && r.FromCity < World.Cities.Count && r.ToCity >= 0 && r.ToCity < World.Cities.Count)
                        r.PathMercator = RouteBetween(r.FromCity, r.ToCity, r.IsRailway).PathMercator;

            RefreshCountryColors();
            RebuildPlayerInfrastructure();
        }

        // Loads the satellite basemap (StreamingAssets/basemap/satellite.jpg) — the always-
        // available offline backdrop, replaced by live-streamed tiles (SatelliteTileLoader)
        // once zoomed in close enough for them to add real detail. Starts hidden; SetMode
        // reveals it and hides the flat country fills (they're opaque, so both can't show at
        // once).
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
            // mipChain=true + Trilinear: at world-view zoom the whole 21600px-wide texture is
            // massively minified (without mips that aliases/shimmers); at max zoom-in it's still
            // being magnified (a single flat texture has a hard detail ceiling), so Trilinear
            // gives the smoothest possible interpolation in both directions.
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, true);
            tex.LoadImage(bytes); // auto-sizes the texture to the source image
            // If the source image exceeds SystemInfo.maxTextureSize, Unity silently swaps in an
            // 8x8 placeholder and only logs a native-side warning (easy to miss) — this happened
            // once already (a 21600px-wide basemap on a 16384-max device rendered as solid
            // black with zero managed-side error). Fail loudly instead.
            if (tex.width <= 64 || tex.height <= 64)
                Debug.LogError($"[map] satellite basemap loaded as {tex.width}x{tex.height} — almost certainly exceeded SystemInfo.maxTextureSize ({SystemInfo.maxTextureSize}) and fell back to a placeholder. Re-export the basemap at or below that size.");
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 9;
            var mat = new Material(shader) { mainTexture = tex };
            SatelliteTexture = tex;

            // The source image is a plain equirectangular (linear-in-latitude) photo — a single
            // flat quad would place it at the wrong Y everywhere except the equator now that
            // world space is Mercator-projected. Longitude still maps 1:1 to X in both
            // projections, so only latitude/Y needs subdividing: one thin horizontal strip per
            // row, each row positioned at its correctly Mercator-warped Y while sampling the UV
            // row a plain linear latitude mapping gives (the image's own pixels are never
            // resampled/warped, only where each row of them is placed in world space).
            var mesh = new Mesh { name = "SatelliteQuad" };
            const int rows = 128;
            var verts = new List<Vector3>(2 * (rows + 1));
            var uvs = new List<Vector2>(2 * (rows + 1));
            var tris = new List<int>(6 * rows);
            for (int i = 0; i <= rows; i++)
            {
                float t = (float)i / rows;
                float lat = Mathf.Lerp(-GeoMath.MaxMercatorLatitude, GeoMath.MaxMercatorLatitude, t);
                float y = GeoMath.LonLatToMercator(0f, lat).y;
                float v = (lat + 90f) / 180f; // source image's own linear-in-latitude V
                verts.Add(new Vector3(-180f, y, SatelliteZ));
                verts.Add(new Vector3(180f, y, SatelliteZ));
                uvs.Add(new Vector2(0f, v));
                uvs.Add(new Vector2(1f, v));
                if (i > 0)
                {
                    int b = (i - 1) * 2; // this row's base index
                    int n = i * 2;       // next row's base index
                    tris.Add(b); tris.Add(n + 1); tris.Add(b + 1);
                    tris.Add(b); tris.Add(n); tris.Add(n + 1);
                }
            }
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
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
            borderMeshByCountry = new Mesh[World.Countries.Count];
            for (int ci = 0; ci < World.Countries.Count; ci++)
            {
                var c = World.Countries[ci];
                borderMeshByCountry[ci] = MakeLineMeshObject(c.Name + " (border)", c.OutlineRings, borderColor, BorderZ, transform);
            }
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

        // Airports and seaports — the ne_10m_airports / ne_10m_ports Natural Earth datasets were
        // already being loaded into World.Airports / World.Ports by GeoJsonLoader but never
        // actually rendered. One marker set each (no LOD tiers like cities; MapLayers gates the
        // whole set behind a single zoom threshold instead), built the same constant-screen-size
        // dot way as city markers so they read at a consistent size at any zoom level.
        void BuildInfrastructureMarkers()
        {
            var dotShader = Shader.Find("Meridian/ScreenDot");
            if (dotShader == null)
            {
                Debug.LogWarning("[map] ScreenDot shader not found; skipping airport/port markers");
                return;
            }

            AirportsRoot = BuildPointMarkerSet("Airports", World.Airports, new Color(0.45f, 0.85f, 1f, 0.95f), 3.5f, dotShader);
            PortsRoot = BuildPointMarkerSet("Ports", World.Ports, new Color(1f, 0.70f, 0.25f, 0.95f), 3.5f, dotShader);
            // Military air bases (olive, distinct from civil airports' cyan), oil ports
            // (industrial orange), nuclear plants (radiation yellow-green, slightly bigger so
            // a handful of high-value targets still read clearly at a glance).
            AirBasesRoot = BuildPointMarkerSet("AirBases", World.AirBases, new Color(0.55f, 0.62f, 0.32f, 1f), 3.5f, dotShader);
            OilPortsRoot = BuildPointMarkerSet("OilPorts", World.OilPorts, new Color(0.92f, 0.46f, 0.10f, 1f), 4f, dotShader);
            NuclearPlantsRoot = BuildPointMarkerSet("NuclearPlants", World.NuclearPlants, new Color(0.85f, 0.95f, 0.15f, 1f), 4.5f, dotShader);
            Debug.Log($"[map] built {World.Airports.Count} airport markers, {World.Ports.Count} port markers, " +
                      $"{World.AirBases.Count} air base markers, {World.OilPorts.Count} oil port markers, {World.NuclearPlants.Count} nuclear plant markers");
        }

        // Roads and railways as THICK line meshes (quads, not 1px GL lines — GL_LINES has no
        // width control on most platforms, which is exactly why the original version was
        // indistinguishable from any other thin line). Railways additionally get regularly-
        // spaced cross-tie tick marks overlaid on a fully solid line (see BuildRailwaysRoot) so
        // the two layers read as visually different line *types*, not just different colors.
        //
        // An earlier version tried to tell railways apart by literally punching gaps in the line
        // (a dash pattern with dashLen/gapLen in world units). That looked fine at the one zoom
        // level it was tuned at, but combined badly with switching line width to constant SCREEN
        // pixels: a fixed-world-length dash is a wildly different number of screen pixels at
        // different zooms — either a sub-pixel sliver (reads as noise) or a many-pixels-long,
        // only ~2px-wide needle (reads as broken, not like a railway). Keeping the base line
        // fully solid/continuous (so the existing joint-at-every-vertex fix still closes every
        // bend, same as roads) and drawing ties as a decorative overlay on top sidesteps that
        // mismatch entirely.
        // Cartographic "casing" — a wider, dark base pass under the bright color pass — is what
        // makes real road/rail map lines read as substantial infrastructure instead of a flat
        // colored ribbon; the casing peeking out past the core's edges on both sides is the
        // whole effect. One fixed dark color for both roads and railways (rather than a per-
        // color darkened variant) since it needs to read as an outline/shadow under ANY core
        // color, including railwayColor which is already near-black — a "darker than near-
        // black" casing would be invisible, but a fixed dark-neutral casing still shows through.
        static readonly Color CasingColor = new Color(0.04f, 0.05f, 0.07f, 0.65f);
        const float CasingWidthMultiplier = 1.9f;
        // Sits fractionally further from the camera than the core (more negative Z = closer,
        // see the Z-ordering convention above) so the two passes never z-fight.
        const float CasingZEpsilon = 0.002f;

        void BuildRoadsAndRailways()
        {
            RoadsRoot = BuildLineFeaturesRoot("Roads", World.Roads, roadColor, RoadZ, roadWidth);
            RailwaysRoot = BuildRailwaysRoot(World.Railways, RailZ, railwayWidth);
            Debug.Log($"[map] built {World.Roads.Count} road features, {World.Railways.Count} railway features");
        }

        GameObject BuildLineFeaturesRoot(string rootName, List<LineFeature> features, Color color, float z, float pixelHalfWidth)
        {
            var root = new GameObject(rootName);
            root.transform.SetParent(transform, false);
            root.SetActive(false); // MapLayers reveals this once zoomed in enough

            BuildLineMesh(root.transform, rootName + "_Casing", features, CasingColor, z + CasingZEpsilon, pixelHalfWidth * CasingWidthMultiplier);
            BuildLineMesh(root.transform, rootName + "_Core", features, color, z, pixelHalfWidth);
            return root;
        }

        // Builds one line-feature mesh (all features' lines merged into a single draw call) as
        // a child of `parent`. Shared by both the casing and core passes, and by the railway
        // core (which additionally interleaves tie marks — see BuildRailwaysRoot).
        GameObject BuildLineMesh(Transform parent, string name, List<LineFeature> features, Color color, float z, float pixelHalfWidth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var verts = new List<Vector3>();
            var tris = new List<int>();
            var cols = new List<Color>();
            var uvs = new List<Vector2>();
            foreach (var feat in features)
                foreach (var line in feat.Lines)
                    AppendThickLine(verts, tris, cols, uvs, line, z, color);
            if (verts.Count == 0) return go;

            var mesh = new Mesh { name = name };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MakeScreenLineMaterial(pixelHalfWidth);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        // Rebuilds the whole player-built road/rail mesh from Infrastructure.Routes — called
        // once whenever a route completes (a rare event; at the scale a single game can produce
        // — dozens, not thousands — a full rebuild from scratch is trivially cheap, no need for
        // incremental append) and once after a save loads. Slightly bolder width and drawn a
        // hair closer to the camera than natural roads/rail so a player's own construction
        // visibly reads as "yours" even where it runs alongside/over existing infrastructure.
        // Terrain-following route planner (Map/TerrainRouter), built lazily off the satellite
        // basemap the first time the player previews or commits a build. Null-safe: if the
        // basemap never loaded, the router itself falls back to straight lines.
        TerrainRouter terrainRouter;
        public TerrainRouter.Route RouteBetween(int fromCity, int toCity, bool rail)
        {
            terrainRouter ??= new TerrainRouter(SatelliteTexture);
            return terrainRouter.Plan(World.Cities[fromCity].Pos, World.Cities[toCity].Pos, rail);
        }

        public void RebuildPlayerInfrastructure()
        {
            bool wasActive = PlayerInfrastructureRoot != null && PlayerInfrastructureRoot.activeSelf;
            if (PlayerInfrastructureRoot != null) Destroy(PlayerInfrastructureRoot);
            PlayerInfrastructureRoot = new GameObject("PlayerInfrastructure");
            PlayerInfrastructureRoot.transform.SetParent(transform, false);

            var roadFeatures = new List<LineFeature>();
            var railFeatures = new List<LineFeature>();
            if (Infrastructure != null)
            {
                foreach (var r in Infrastructure.Routes)
                {
                    if (!r.Completed) continue;
                    if (r.FromCity < 0 || r.FromCity >= World.Cities.Count || r.ToCity < 0 || r.ToCity >= World.Cities.Count) continue;
                    // Draw the terrain-following path if it has one (new routes); fall back to a
                    // straight city-to-city line for routes from pre-terrain-routing saves.
                    var line = r.PathMercator != null && r.PathMercator.Count >= 2
                        ? new List<Vector2>(r.PathMercator)
                        : new List<Vector2> { World.Cities[r.FromCity].Pos, World.Cities[r.ToCity].Pos };
                    var feat = new LineFeature { Name = $"{r.FromName}-{r.ToName}", Lines = new List<List<Vector2>> { line } };
                    (r.IsRailway ? railFeatures : roadFeatures).Add(feat);
                }
            }

            const float playerZBias = -0.0005f; // closer to camera than the natural layer
            const float playerWidthBoost = 1.15f;
            if (roadFeatures.Count > 0)
            {
                BuildLineMesh(PlayerInfrastructureRoot.transform, "PlayerRoads_Casing", roadFeatures, CasingColor, RoadZ + CasingZEpsilon + playerZBias, roadWidth * CasingWidthMultiplier * playerWidthBoost);
                BuildLineMesh(PlayerInfrastructureRoot.transform, "PlayerRoads_Core", roadFeatures, roadColor, RoadZ + playerZBias, roadWidth * playerWidthBoost);
            }
            if (railFeatures.Count > 0)
            {
                BuildLineMesh(PlayerInfrastructureRoot.transform, "PlayerRail_Casing", railFeatures, CasingColor, RailZ + CasingZEpsilon + playerZBias, railwayWidth * CasingWidthMultiplier * playerWidthBoost);
                BuildLineMesh(PlayerInfrastructureRoot.transform, "PlayerRail_Core", railFeatures, railwayColor, RailZ + playerZBias, railwayWidth * playerWidthBoost);
            }

            PlayerInfrastructureRoot.SetActive(wasActive);
        }

        // Railways: a dark casing pass (see BuildLineFeaturesRoot), then a solid line body (same
        // joined-quad technique as roads) plus cross-tie tick marks at fixed arc-length spacing
        // — the classic "railroad" cartography symbol — sharing one mesh/material so ties
        // automatically inherit the same constant-screen-pixel scaling as the line body (see
        // AppendTie: tie dimensions are just larger multiples of the same per-vertex UV offset
        // the line quads/joints already use, not a separate width control). Ties therefore have
        // to stay in the CORE mesh at the core's pixelHalfWidth, not the wider casing pass.
        GameObject BuildRailwaysRoot(List<LineFeature> features, float z, float pixelHalfWidth)
        {
            var root = new GameObject("Railways");
            root.transform.SetParent(transform, false);
            root.SetActive(false);

            BuildLineMesh(root.transform, "Railways_Casing", features, CasingColor, z + CasingZEpsilon, pixelHalfWidth * CasingWidthMultiplier);

            var coreGo = new GameObject("Railways_Core");
            coreGo.transform.SetParent(root.transform, false);

            var verts = new List<Vector3>();
            var tris = new List<int>();
            var cols = new List<Color>();
            var uvs = new List<Vector2>();
            foreach (var feat in features)
            {
                foreach (var line in feat.Lines)
                {
                    AppendThickLine(verts, tris, cols, uvs, line, z, railwayColor);
                    AppendTies(verts, tris, cols, uvs, line, z, railTieColor, railTieSpacing);
                }
            }
            if (verts.Count == 0) return root;

            var mesh = new Mesh { name = "Railways_Core" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            coreGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = coreGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MakeScreenLineMaterial(pixelHalfWidth);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return root;
        }

        // Material for the constant-screen-width line shader (see ScreenLine.shader) — falls
        // back to the plain vertex-color fill shader if ScreenLine is missing (e.g. stripped from
        // a build), which still renders something (just back to world-space-sized geometry)
        // rather than nothing.
        Material MakeScreenLineMaterial(float pixelHalfWidth)
        {
            var shader = Shader.Find("Meridian/ScreenLine");
            if (shader == null)
            {
                Debug.LogWarning("[map] ScreenLine shader not found; roads/railways will use raw (unscaled) geometry");
                return fillMaterial;
            }
            var mat = new Material(shader) { name = $"ScreenLine_{pixelHalfWidth}" };
            mat.SetFloat("_PixelHalfWidth", pixelHalfWidth);
            return mat;
        }

        // Appends a polyline as a chain of thin quads (each segment = one rectangle) PLUS a
        // small square "joint" at every interior vertex to close the gap a bend would otherwise
        // leave (without it, a line reads as a chain of disconnected rectangles — at 10m
        // road-data resolution this was visible almost everywhere, not just at sharp corners).
        // Vertices store the RAW line point (no width baked in) plus an offset DIRECTION in UV0;
        // ScreenLine.shader multiplies that direction by the material's _PixelHalfWidth and the
        // camera's current world-units-per-pixel every frame, so the rendered width is always
        // the same number of screen pixels regardless of zoom.
        static void AppendThickLine(List<Vector3> verts, List<int> tris, List<Color> cols, List<Vector2> uvs, List<Vector2> line, float z, Color color)
        {
            if (line.Count < 2) return;
            for (int i = 0; i + 1 < line.Count; i++)
                AppendQuad(verts, tris, cols, uvs, line[i], line[i + 1], z, color);
            for (int i = 1; i + 1 < line.Count; i++)
                AppendJoint(verts, tris, cols, uvs, line[i], z, color);
        }

        // Cross-tie tick marks at fixed arc-length spacing (world/Mercator units — this is what
        // controls how dense the ties look, analogous to the old dash period, but since these sit
        // ON TOP of an always-solid line rather than punching gaps in it, there's no failure mode
        // where the underlying line itself goes missing or reads as noise at the wrong zoom).
        // Each tie is a short quad perpendicular to the rail direction at that point, built with
        // the same "store raw position + UV offset direction" scheme as AppendQuad/AppendJoint —
        // its UV magnitudes are just larger multiples of pixelHalfWidth (see TieLengthUnits/
        // TieThicknessUnits) so it scales together with the line body from the same material.
        const float TieLengthUnits = 2.6f;    // half-length across the rail, in multiples of pixelHalfWidth
        const float TieThicknessUnits = 0.8f; // half-thickness along the rail, in multiples of pixelHalfWidth

        static void AppendTies(List<Vector3> verts, List<int> tris, List<Color> cols, List<Vector2> uvs, List<Vector2> line, float z, Color color, float spacing)
        {
            if (line.Count < 2 || spacing <= 0f) return;
            float distToNext = spacing * 0.5f; // offset the first tie half a period in
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 a = line[i], b = line[i + 1];
                float segLen = Vector2.Distance(a, b);
                if (segLen < 1e-6f) continue;
                Vector2 dir = (b - a) / segLen;

                float t = 0f;
                while (distToNext <= segLen - t)
                {
                    t += distToNext;
                    AppendTie(verts, tris, cols, uvs, a + dir * t, dir, z, color);
                    distToNext = spacing;
                }
                distToNext -= (segLen - t);
            }
        }

        static void AppendTie(List<Vector3> verts, List<int> tris, List<Color> cols, List<Vector2> uvs, Vector2 p, Vector2 alongRail, float z, Color color)
        {
            Vector2 crossRail = new Vector2(-alongRail.y, alongRail.x) * TieLengthUnits;
            Vector2 thick = alongRail * TieThicknessUnits;
            int baseIdx = verts.Count;
            var pos = new Vector3(p.x, p.y, z);
            verts.Add(pos); uvs.Add(-crossRail - thick);
            verts.Add(pos); uvs.Add(-crossRail + thick);
            verts.Add(pos); uvs.Add(crossRail + thick);
            verts.Add(pos); uvs.Add(crossRail - thick);
            cols.Add(color); cols.Add(color); cols.Add(color); cols.Add(color);
            tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
            tris.Add(baseIdx); tris.Add(baseIdx + 3); tris.Add(baseIdx + 2);
        }

        static void AppendQuad(List<Vector3> verts, List<int> tris, List<Color> cols, List<Vector2> uvs, Vector2 a, Vector2 b, float z, Color color)
        {
            Vector2 dir = (b - a).normalized;
            Vector2 n = new Vector2(-dir.y, dir.x); // unit perpendicular; shader scales this by pixel width
            int baseIdx = verts.Count;
            verts.Add(new Vector3(a.x, a.y, z)); uvs.Add(-n);
            verts.Add(new Vector3(a.x, a.y, z)); uvs.Add(n);
            verts.Add(new Vector3(b.x, b.y, z)); uvs.Add(n);
            verts.Add(new Vector3(b.x, b.y, z)); uvs.Add(-n);
            cols.Add(color); cols.Add(color); cols.Add(color); cols.Add(color);
            tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
            tris.Add(baseIdx); tris.Add(baseIdx + 3); tris.Add(baseIdx + 2);
        }

        // An axis-aligned square centered on a shared vertex between two segments. Not a proper
        // miter join (which would need the two segments' actual angle), but a square this size
        // always fully covers where two same-width segment ends meet regardless of the angle
        // between them, which is all that's needed to close the visible gap — a cheap, safe
        // over-approximation rather than exact miter geometry.
        static void AppendJoint(List<Vector3> verts, List<int> tris, List<Color> cols, List<Vector2> uvs, Vector2 p, float z, Color color)
        {
            int baseIdx = verts.Count;
            var pos = new Vector3(p.x, p.y, z);
            verts.Add(pos); uvs.Add(new Vector2(-1, -1));
            verts.Add(pos); uvs.Add(new Vector2(-1, 1));
            verts.Add(pos); uvs.Add(new Vector2(1, 1));
            verts.Add(pos); uvs.Add(new Vector2(1, -1));
            cols.Add(color); cols.Add(color); cols.Add(color); cols.Add(color);
            tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
            tris.Add(baseIdx); tris.Add(baseIdx + 3); tris.Add(baseIdx + 2);
        }

        // Border-crossing markers: one dot per computed crossing (see
        // GeoJsonLoader.ComputeBorderCrossings), same constant-screen-size ScreenDot approach as
        // city/airport markers. Gated by MapLayers alongside the roads layer they're derived from.
        void BuildBorderCrossingMarkers()
        {
            var dotShader = Shader.Find("Meridian/ScreenDot");
            if (dotShader == null) { Debug.LogWarning("[map] ScreenDot shader not found; skipping border crossing markers"); return; }
            var points = World.BorderCrossings.ConvertAll(bc => new PointFeature { Name = $"{bc.CountryA} / {bc.CountryB}", Pos = bc.Pos });
            BorderCrossingsRoot = BuildPointMarkerSet("BorderCrossings", points, borderCrossingColor, 4f, dotShader);
        }

        // Water crossings: both a thick line (the causeway/bridge itself) and a marker at its
        // midpoint (an easier click target than a thin line at world-view zoom). Small, curated
        // set (see GeoJsonLoader.WaterCrossingsSeed) so it gets its own always-a-few-dozen-max
        // strategic-tier visibility rather than being gated behind the crowded roads layer.
        void BuildWaterCrossings()
        {
            WaterCrossingLinesRoot = new GameObject("WaterCrossingLines");
            WaterCrossingLinesRoot.transform.SetParent(transform, false);
            WaterCrossingLinesRoot.SetActive(false);

            var verts = new List<Vector3>();
            var tris = new List<int>();
            var cols = new List<Color>();
            var uvs = new List<Vector2>();
            foreach (var wc in World.WaterCrossings)
                AppendThickLine(verts, tris, cols, uvs, wc.Line, RoadZ - 0.01f, waterCrossingColor);
            if (verts.Count > 0)
            {
                var mesh = new Mesh { name = "WaterCrossingLines" };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(verts);
                mesh.SetColors(cols);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                WaterCrossingLinesRoot.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = WaterCrossingLinesRoot.AddComponent<MeshRenderer>();
                mr.sharedMaterial = MakeScreenLineMaterial(roadWidth * 1.4f);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            var dotShader = Shader.Find("Meridian/ScreenDot");
            if (dotShader != null)
            {
                var points = World.WaterCrossings.ConvertAll(wc => new PointFeature
                {
                    Name = wc.Name,
                    Pos = (wc.Line[0] + wc.Line[wc.Line.Count - 1]) * 0.5f,
                });
                WaterCrossingsRoot = BuildPointMarkerSet("WaterCrossingMarkers", points, waterCrossingColor, 4.5f, dotShader);
            }
            Debug.Log($"[map] built {World.WaterCrossings.Count} water crossings, {World.BorderCrossings.Count} border crossing markers");
        }

        GameObject BuildPointMarkerSet(string name, List<PointFeature> points, Color color, float pixelRadius, Shader dotShader)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            root.SetActive(false); // MapLayers reveals this once zoomed in enough
            if (points.Count == 0) return root;

            var verts = new List<Vector3>(points.Count * 4);
            var cols = new List<Color>(points.Count * 4);
            var uvs = new List<Vector2>(points.Count * 4);
            var tris = new List<int>(points.Count * 6);
            foreach (var p in points)
            {
                int b = verts.Count;
                var center = new Vector3(p.Pos.x, p.Pos.y, CityZ);
                verts.Add(center); verts.Add(center); verts.Add(center); verts.Add(center);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                cols.Add(color); cols.Add(color); cols.Add(color); cols.Add(color);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }

            var mesh = new Mesh { name = name };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            var b2 = mesh.bounds;
            b2.Expand(5f);
            mesh.bounds = b2;

            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = root.AddComponent<MeshRenderer>();
            var dotMat = new Material(dotShader) { name = $"Dot_{name}" };
            dotMat.SetFloat("_PixelRadius", pixelRadius);
            mr.sharedMaterial = dotMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return root;
        }

        // Builds a line-list mesh GameObject from a set of lon/lat rings, parented under `parent`.
        // Returns the built Mesh (or null if there was nothing to build) so callers that need to
        // recolor it later — see RefreshCountryColors — don't have to re-find it via the GameObject.
        Mesh MakeLineMeshObject(string name, List<List<Vector2>> rings, Color color, float z, Transform parent)
        {
            if (rings == null || rings.Count == 0) return null;

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
            if (verts.Count == 0) return null;

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
            return mesh;
        }

        void BuildCountryMeshes()
        {
            fillMeshByCountry = new Mesh[World.Countries.Count];
            for (int ci = 0; ci < World.Countries.Count; ci++)
            {
                var c = World.Countries[ci];
                if (c.MeshVerts.Count == 0 || c.MeshIndices.Count == 0) continue;

                // Pre-game fallback (no player country chosen yet, so "relation to the player"
                // is meaningless) — the same hash-based "no two neighbors share a tint" palette
                // this map always used. RefreshCountryColors repaints everything to the real
                // relation gradient the moment a game actually starts.
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
                fillMeshByCountry[ci] = mesh;
            }
            Debug.Log("[map] built country fill meshes");
        }

        // Recolors every country's fill (Political mode) and border (both modes, but especially
        // meaningful in Satellite mode where there's no fill to look at) by its diplomatic
        // relation to the player's own country. Cheap to call often — this only replaces each
        // mesh's per-vertex Color[] in place, no geometry rebuild — so it's called once whenever
        // the player's country is (re)assigned (new game / loaded save) and once per sim day
        // while playing, so relation drift and diplomacy actions show up on the map promptly.
        // Before a game has started (no player country yet), falls back to leaving whatever
        // colors BuildCountryMeshes/BuildCountryBorders already painted (the hash-based palette).
        public void RefreshCountryColors()
        {
            if (fillMeshByCountry == null || borderMeshByCountry == null) return;
            int me = PlayerState.CountryIndex;

            for (int ci = 0; ci < World.Countries.Count; ci++)
            {
                // No player country (start screen / game-over "play again") — reset to the same
                // hash-based palette BuildCountryMeshes painted at boot, so the map doesn't sit
                // there showing a previous game's relation colors while no game is in progress.
                Color c = me < 0
                    ? Palette[(int)(HashName(World.Countries[ci].Name) % (ulong)Palette.Length)]
                    : RelationColor(ci, me);
                ApplyMeshColor(fillMeshByCountry[ci], c);
                ApplyMeshColor(borderMeshByCountry[ci], me < 0 ? borderColor : c);
            }
        }

        Color RelationColor(int countryIndex, int playerIndex)
        {
            if (countryIndex == playerIndex) return selfColor;
            if (Diplomacy == null) return neutralRelationColor;
            float rel = Diplomacy.GetRelation(playerIndex, countryIndex); // 0-100
            return rel <= 50f
                ? Color.Lerp(hostileRelationColor, neutralRelationColor, rel / 50f)
                : Color.Lerp(neutralRelationColor, friendlyRelationColor, (rel - 50f) / 50f);
        }

        static void ApplyMeshColor(Mesh mesh, Color color)
        {
            if (mesh == null) return;
            int n = mesh.vertexCount;
            var colors = new Color[n];
            for (int i = 0; i < n; i++) colors[i] = color;
            mesh.colors = colors;
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
