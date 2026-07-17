using UnityEngine;
using UnityEngine.UIElements;

namespace Meridian.Map
{
    // Orthographic pan/zoom for the flat Web Mercator-projected map. Attach to the Main Camera
    // (which must be Orthographic). World XY is Mercator space (see GeoMath.LonLatToMercator —
    // x is still plain longitude, y is the Mercator-warped latitude, both in degrees-normalized
    // units), so the camera just moves in XY and changes orthographic size — no projection math
    // here, none of the drag-rotation complexity the 3D-globe attempt fought with. Left-drag
    // pans; scroll zooms toward the cursor.
    [RequireComponent(typeof(Camera))]
    public class MapCameraController : MonoBehaviour
    {
        // Deep zoom-in now has real detail to show (live satellite tiles), not just a blurred-out
        // static texture, so this is pushed much closer than the old equirectangular build ever
        // needed.
        public float minOrthoSize = 0.03f; // most zoomed-in (~a few km of view height)
        // The whole projected map spans ±180 on both axes. 180 = the map exactly fills the
        // viewport's height when fully zoomed out; anything larger would show empty void
        // around the world.
        public float maxOrthoSize = 180f;
        public float zoomSpeed = 0.15f;
        const float MapExtent = 180f; // half-extent of the Mercator world square

        Camera cam;
        Vector3 dragOriginWorld;
        bool dragging;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Clamp(180f, minOrthoSize, maxOrthoSize);
            // Start centered on the whole map. At this fully-zoomed-out size the view is as
            // tall as (or, on any landscape aspect, wider than) the map itself, so
            // ClampPosition's very first call would force any off-center start back to 0
            // anyway — start there directly instead of setting a value the clamp immediately
            // discards.
            transform.position = new Vector3(0f, 0f, -10f);
        }

        void Update()
        {
            HandlePan();
            HandleZoom();
            ClampPosition();
        }

        UIDocument uiDoc;

        // Same UI-awareness fix as MapInteraction.PointerOverUI: without it, dragging a panel
        // slider or holding a button also panned the map underneath.
        bool PointerOverUI(Vector2 mouseScreenPos)
        {
            if (uiDoc == null) uiDoc = FindObjectOfType<UIDocument>();
            var panel = uiDoc != null ? uiDoc.rootVisualElement?.panel : null;
            if (panel == null) return false;
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(mouseScreenPos.x, Screen.height - mouseScreenPos.y));
            return panel.Pick(panelPos) != null;
        }

        void HandlePan()
        {
            if (Input.GetMouseButtonDown(0) && !PointerOverUI(Input.mousePosition))
            {
                dragOriginWorld = cam.ScreenToWorldPoint(Input.mousePosition);
                dragging = true;
            }
            if (Input.GetMouseButtonUp(0)) dragging = false;

            if (dragging && Input.GetMouseButton(0))
            {
                Vector3 now = cam.ScreenToWorldPoint(Input.mousePosition);
                Vector3 delta = dragOriginWorld - now;
                delta.z = 0f;
                transform.position += delta;
                // Recompute origin against the moved camera so the grabbed point stays put.
                dragOriginWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            }
        }

        void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.0001f) return;
            // Scrolling over a UI panel (e.g. the start screen's country list) belongs to that
            // panel's own ScrollView, not the map zoom.
            if (PointerOverUI(Input.mousePosition)) return;

            Vector3 beforeWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            float factor = 1f - scroll * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * factor, minOrthoSize, maxOrthoSize);
            Vector3 afterWorld = cam.ScreenToWorldPoint(Input.mousePosition);

            // Keep the point under the cursor fixed while zooming.
            Vector3 shift = beforeWorld - afterWorld;
            shift.z = 0f;
            transform.position += shift;
        }

        // Keeps the VIEW inside the map, not just the camera center: the visible half-extents
        // (orthoSize vertically, orthoSize × aspect horizontally) are subtracted from the map
        // bounds so no pan or zoom can ever show void beyond an edge. On ultrawide monitors
        // the view is wider than the whole map until fairly zoomed in — that axis locks
        // centered (the clamp range inverts, so min/max swap to 0).
        void ClampPosition()
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;

            var p = transform.position;
            p.x = ClampAxis(p.x, halfW);
            p.y = ClampAxis(p.y, halfH);
            transform.position = p;
        }

        static float ClampAxis(float center, float halfView)
        {
            float room = MapExtent - halfView;
            if (room <= 0f) return 0f; // view wider than the map — lock centered
            return Mathf.Clamp(center, -room, room);
        }
    }
}
