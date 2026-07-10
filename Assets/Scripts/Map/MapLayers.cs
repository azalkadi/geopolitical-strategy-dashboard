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
        public float labelsBelow = 45f;

        MapRenderer map;
        Camera cam;
        // Cities worth labelling (MajorCity+), cached so OnGUI isn't scanning all 7,342.
        readonly List<City> labelCities = new();
        GUIStyle labelStyle;

        void Start()
        {
            map = FindObjectOfType<MapRenderer>();
            cam = Camera.main;
        }

        void Update()
        {
            if (map == null || cam == null || map.CityTierRoots == null) return;

            // Populate the label cache once the world has loaded.
            if (labelCities.Count == 0 && map.World != null)
                foreach (var c in map.World.Cities)
                    if (c.Tier == CityTier.Megacity || c.Tier == CityTier.MajorCity)
                        labelCities.Add(c);

            float z = cam.orthographicSize;

            // Drives ScreenDot.shader's constant-screen-size math for every city marker.
            Shader.SetGlobalFloat("_OrthoSize", z);

            if (map.ProvincesRoot != null) map.ProvincesRoot.SetActive(z < provincesBelow);

            SetTier(CityTier.Megacity, true);            // always visible
            SetTier(CityTier.MajorCity, z < majorCityBelow);
            SetTier(CityTier.City, z < cityBelow);
            SetTier(CityTier.Town, z < townBelow);
        }

        void SetTier(CityTier t, bool on)
        {
            var root = map.CityTierRoots[(int)t];
            if (root != null && root.activeSelf != on) root.SetActive(on);
        }

        void OnGUI()
        {
            if (map == null || cam == null || map.World == null) return;
            if (cam.orthographicSize >= labelsBelow) return;

            if (labelStyle == null)
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };

            GUI.color = Color.white;
            foreach (var c in labelCities)
            {
                Vector3 sp = cam.WorldToScreenPoint(new Vector3(c.Pos.x, c.Pos.y, 0f));
                if (sp.z < 0f || sp.x < 0f || sp.x > Screen.width || sp.y < 0f || sp.y > Screen.height) continue;
                // GUI y-origin is top-left; camera screen y-origin is bottom-left.
                float gy = Screen.height - sp.y;
                GUI.Label(new Rect(sp.x - 60f, gy - 20f, 120f, 16f), c.Name, labelStyle);
            }
        }
    }
}
