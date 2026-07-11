using UnityEngine;

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
        // Mercator's Y range at the standard polar cutoff is ±180 (matching X), doubled from
        // equirectangular's ±90 — maxOrthoSize scales up to match so "zoomed all the way out"
        // still frames the whole map with the same margin as before.
        public float maxOrthoSize = 200f;
        public float zoomSpeed = 0.15f;
        public float latitudeClamp = 170f; // keep the camera center within the projected map

        Camera cam;
        Vector3 dragOriginWorld;
        bool dragging;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Clamp(180f, minOrthoSize, maxOrthoSize);
            // Start centered on the map. Keep the camera in front of the z=0 map plane.
            // (10, 20) is still a reasonable starting center post-Mercator — at moderate
            // latitudes like 20° the projection is close to linear, so this barely shifts.
            transform.position = new Vector3(10f, 20f, -10f);
        }

        void Update()
        {
            HandlePan();
            HandleZoom();
            ClampPosition();
        }

        void HandlePan()
        {
            if (Input.GetMouseButtonDown(0))
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

            Vector3 beforeWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            float factor = 1f - scroll * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * factor, minOrthoSize, maxOrthoSize);
            Vector3 afterWorld = cam.ScreenToWorldPoint(Input.mousePosition);

            // Keep the point under the cursor fixed while zooming.
            Vector3 shift = beforeWorld - afterWorld;
            shift.z = 0f;
            transform.position += shift;
        }

        void ClampPosition()
        {
            var p = transform.position;
            p.y = Mathf.Clamp(p.y, -latitudeClamp, latitudeClamp);
            transform.position = p;
        }
    }
}
