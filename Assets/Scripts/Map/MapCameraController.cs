using UnityEngine;

namespace Meridian.Map
{
    // Orthographic pan/zoom for the flat equirectangular map. Attach to the Main Camera
    // (which must be Orthographic). lon/lat map to world XY, so the camera just moves in XY
    // and changes orthographic size — no projection math, no per-frame reprojection, none of
    // the drag-rotation complexity the 3D-globe attempt fought with. Left-drag pans; scroll
    // zooms toward the cursor.
    [RequireComponent(typeof(Camera))]
    public class MapCameraController : MonoBehaviour
    {
        public float minOrthoSize = 4f;    // most zoomed-in
        public float maxOrthoSize = 100f;  // whole-world view (lat range is 180 -> size ~90)
        public float zoomSpeed = 0.15f;
        public float latitudeClamp = 88f;

        Camera cam;
        Vector3 dragOriginWorld;
        bool dragging;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Clamp(90f, minOrthoSize, maxOrthoSize);
            // Start centered on the map. Keep the camera in front of the z=0 map plane.
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
