using System.Collections.Generic;
using UnityEngine;
using Meridian.Geo;

namespace Meridian.Map
{
    // Zoom-gated level-of-detail for the map: reveals province borders and progressively
    // smaller city tiers as the orthographic camera zooms in, and draws name labels for the
    // notable cities currently on screen. Same "detail appears as you zoom" idea the old Bevy
    // build had, done here by toggling the pre-built layer roots on MapRenderer (Unity
    // frustum-culls the geometry) and drawing labels in immediate-mode GUI for only the
    // on-screen few.
    public class MapLayers : MonoBehaviour
    {
        [Header("Orthographic size thresholds (smaller = more zoomed in)")]
        public float provincesBelow = 35f;
        public float majorCityBelow = 55f;
        public float cityBelow = 22f;
        public float townBelow = 9f;
        public float infrastructureBelow = 30f;
        public float roadsBelow = 25f;
        public float railwaysBelow = 25f;
        // Oil ports / nuclear plants / air bases are sparse (dozens, not hundreds) — safe to
        // reveal at a much wider zoom than the crowded civil airports/ports layer.
        public float strategicBelow = 65f;

        [Header("Label thresholds — every tier gets a name once you're zoomed in enough for it")]
        public float megacityLabelsBelow = 90f; // effectively "always" — biggest cities read as landmarks
        public float majorCityLabelsBelow = 55f;
        public float cityLabelsBelow = 22f;
        public float townLabelsBelow = 9f;
        public float infrastructureLabelsBelow = 14f;
        public float strategicLabelsBelow = 40f;

        MapRenderer map;
        Camera cam;
        // Cities bucketed by tier once the world has loaded, so OnGUI only ever walks the tier(s)
        // actually on screen at the current zoom instead of scanning all 7,342 every frame.
        readonly List<City>[] citiesByTier = new List<City>[4];
        bool bucketed;
        GUIStyle labelStyle;
        GUIStyle infraLabelStyle;

        void Start()
        {
            map = FindObjectOfType<MapRenderer>();
            cam = Camera.main;
        }

        void Update()
        {
            if (map == null || cam == null || map.CityTierRoots == null) return;

            if (!bucketed && map.World != null)
            {
                for (int t = 0; t < citiesByTier.Length; t++) citiesByTier[t] = new List<City>();
                foreach (var c in map.World.Cities) citiesByTier[(int)c.Tier].Add(c);
                bucketed = true;
            }

            float z = cam.orthographicSize;

            // Drives ScreenDot.shader's constant-screen-size math for every point marker.
            Shader.SetGlobalFloat("_OrthoSize", z);

            if (map.ProvincesRoot != null) map.ProvincesRoot.SetActive(z < provincesBelow);

            SetTier(CityTier.Megacity, true);            // always visible
            SetTier(CityTier.MajorCity, z < majorCityBelow);
            SetTier(CityTier.City, z < cityBelow);
            SetTier(CityTier.Town, z < townBelow);

            SetActive(map.AirportsRoot, z < infrastructureBelow);
            SetActive(map.PortsRoot, z < infrastructureBelow);
            SetActive(map.RoadsRoot, z < roadsBelow);
            SetActive(map.RailwaysRoot, z < railwaysBelow);
            SetActive(map.AirBasesRoot, z < strategicBelow);
            SetActive(map.OilPortsRoot, z < strategicBelow);
            SetActive(map.NuclearPlantsRoot, z < strategicBelow);
            // Border crossings are derived from the roads layer, so they share its threshold.
            SetActive(map.BorderCrossingsRoot, z < roadsBelow);
            // Water crossings are a handful of real, notable structures — worth showing at the
            // same wide zoom as other strategic sites rather than hiding them behind the
            // crowded roads threshold.
            SetActive(map.WaterCrossingsRoot, z < strategicBelow);
            SetActive(map.WaterCrossingLinesRoot, z < strategicBelow);
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        void SetTier(CityTier t, bool on)
        {
            var root = map.CityTierRoots[(int)t];
            if (root != null && root.activeSelf != on) root.SetActive(on);
        }

        void OnGUI()
        {
            if (map == null || cam == null || map.World == null || !bucketed) return;

            if (labelStyle == null)
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            if (infraLabelStyle == null)
                infraLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };

            float z = cam.orthographicSize;

            // Every city tier gets its name drawn once you're zoomed in enough for that tier —
            // megacities read as landmarks almost immediately, towns only once you're in close.
            GUI.color = Color.white;
            if (z < megacityLabelsBelow) DrawCityLabels(citiesByTier[(int)CityTier.Megacity]);
            if (z < majorCityLabelsBelow) DrawCityLabels(citiesByTier[(int)CityTier.MajorCity]);
            if (z < cityLabelsBelow) DrawCityLabels(citiesByTier[(int)CityTier.City]);
            if (z < townLabelsBelow) DrawCityLabels(citiesByTier[(int)CityTier.Town]);

            if (z < infrastructureLabelsBelow)
            {
                GUI.color = new Color(0.7f, 0.92f, 1f, 0.95f);
                DrawPointLabels(map.World.Airports, infraLabelStyle);
                GUI.color = new Color(1f, 0.82f, 0.55f, 0.95f);
                DrawPointLabels(map.World.Ports, infraLabelStyle);
            }

            // Strategic sites label earlier (wider zoom) than the crowded civil layers above —
            // there are only dozens of these, so they don't clutter the view the way thousands
            // of towns/airports would.
            if (z < strategicLabelsBelow)
            {
                GUI.color = new Color(0.75f, 0.80f, 0.55f, 0.95f);
                DrawPointLabels(map.World.AirBases, infraLabelStyle);
                GUI.color = new Color(1f, 0.65f, 0.30f, 0.95f);
                DrawPointLabels(map.World.OilPorts, infraLabelStyle);
                GUI.color = new Color(0.90f, 0.97f, 0.40f, 0.95f);
                DrawPointLabels(map.World.NuclearPlants, infraLabelStyle);

                GUI.color = new Color(0.55f, 0.95f, 1f, 1f);
                var wcPoints = map.World.WaterCrossings.ConvertAll(wc => new Meridian.Geo.PointFeature
                {
                    Name = wc.Name,
                    Pos = (wc.Line[0] + wc.Line[wc.Line.Count - 1]) * 0.5f,
                });
                DrawPointLabels(wcPoints, infraLabelStyle);
            }
        }

        void DrawCityLabels(List<City> cities)
        {
            foreach (var c in cities)
            {
                Vector3 sp = cam.WorldToScreenPoint(new Vector3(c.Pos.x, c.Pos.y, 0f));
                if (sp.z < 0f || sp.x < 0f || sp.x > Screen.width || sp.y < 0f || sp.y > Screen.height) continue;
                // GUI y-origin is top-left; camera screen y-origin is bottom-left.
                float gy = Screen.height - sp.y;
                GUI.Label(new Rect(sp.x - 60f, gy - 20f, 120f, 16f), c.Name, labelStyle);
            }
        }

        void DrawPointLabels(List<Meridian.Geo.PointFeature> points, GUIStyle style)
        {
            foreach (var p in points)
            {
                if (string.IsNullOrEmpty(p.Name)) continue;
                Vector3 sp = cam.WorldToScreenPoint(new Vector3(p.Pos.x, p.Pos.y, 0f));
                if (sp.z < 0f || sp.x < 0f || sp.x > Screen.width || sp.y < 0f || sp.y > Screen.height) continue;
                float gy = Screen.height - sp.y;
                GUI.Label(new Rect(sp.x - 70f, gy - 18f, 140f, 14f), p.Name, style);
            }
        }
    }
}
