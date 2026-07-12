using UnityEngine;
using UnityEngine.UIElements;
using Meridian.UI;

namespace Meridian.Map
{
    // Zero-setup entry point: spawns the orthographic map camera and the MapRenderer at play
    // time, so the project runs from any empty scene with no manual wiring — just press Play.
    // Once you build out a real scene/UI, you can delete this and place the camera +
    // MapRenderer in the scene by hand instead.
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            // Don't double-spawn if a MapRenderer already exists in the scene.
            if (Object.FindObjectOfType<MapRenderer>() != null) return;

            // Camera
            var camGo = GameObject.FindWithTag("MainCamera");
            if (camGo == null)
            {
                camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                camGo.AddComponent<Camera>();
            }
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 180f; // MapCameraController.Awake() re-sets this anyway; kept in sync for clarity
            cam.transform.position = new Vector3(10f, 20f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            if (camGo.GetComponent<MapCameraController>() == null)
                camGo.AddComponent<MapCameraController>();

            // Map + interaction (economy tick, click-to-select, readout) + zoom-gated layers +
            // live satellite tile streaming (kicks in once zoomed in close, on top of the
            // always-available offline basemap).
            var mapGo = new GameObject("MapRenderer");
            mapGo.AddComponent<MapRenderer>();
            mapGo.AddComponent<MapInteraction>();
            mapGo.AddComponent<MapLayers>();
            mapGo.AddComponent<SatelliteTileLoader>();

            // Game UI (UI Toolkit): top bar, bottom ministry bar, selected-country panel.
            var uiGo = new GameObject("GameUI");
            uiGo.AddComponent<UIDocument>();
            uiGo.AddComponent<GameUIRoot>();
            // Separate small info panel for clicked map features (city/road/border crossing/
            // water crossing) — shares the same UIDocument root, own component/file.
            uiGo.AddComponent<FeaturePanel>();

            Debug.Log("[bootstrap] spawned map camera + renderer + interaction + layers + UI");
        }
    }
}
