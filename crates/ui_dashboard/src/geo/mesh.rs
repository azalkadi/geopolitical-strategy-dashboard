//! Shared GeoJSON-property and polygon-triangulation helpers used by every layer
//! (`countries`, `provinces`) that has fill+outline geometry rather than just a point.

use serde_json::Value;

use super::LonLat;

pub(super) fn prop_str(props: &Value, key: &str) -> String {
    props.get(key).and_then(|v| v.as_str()).unwrap_or("").to_string()
}

pub(super) fn prop_i64(props: &Value, key: &str) -> i64 {
    props
        .get(key)
        .and_then(|v| v.as_i64().or_else(|| v.as_f64().map(|f| f as i64)))
        .unwrap_or(0)
}

pub(super) fn point_coords(geom: &Value) -> Option<LonLat> {
    let coords = geom.get("coordinates")?.as_array()?;
    let lon = coords.first()?.as_f64()? as f32;
    let lat = coords.get(1)?.as_f64()? as f32;
    Some([lon, lat])
}

pub(super) struct MeshParts {
    pub verts: Vec<LonLat>,
    pub indices: Vec<u32>,
    pub outline_rings: Vec<Vec<LonLat>>,
    pub outer_rings: Vec<Vec<LonLat>>,
    pub bbox_min: LonLat,
    pub bbox_max: LonLat,
    pub centroid: LonLat,
}

/// Normalizes Polygon/MultiPolygon geometry into triangulated fill geometry plus rings for
/// outline drawing and point-in-polygon hit testing. Triangulation is per polygon-part (an
/// archipelago nation's islands are triangulated independently, then combined into one mesh).
pub(super) fn build_mesh_from_geometry(geom: &Value) -> MeshParts {
    let mut verts: Vec<LonLat> = Vec::new();
    let mut indices: Vec<u32> = Vec::new();
    let mut outline_rings: Vec<Vec<LonLat>> = Vec::new();
    let mut outer_rings: Vec<Vec<LonLat>> = Vec::new();
    let mut bbox_min = [f32::INFINITY, f32::INFINITY];
    let mut bbox_max = [f32::NEG_INFINITY, f32::NEG_INFINITY];
    let mut centroid_sum = [0f64, 0f64];
    let mut centroid_n = 0f64;

    let gtype = geom.get("type").and_then(|v| v.as_str()).unwrap_or("");
    let coords = geom.get("coordinates");
    let Some(coords) = coords else {
        return MeshParts { verts, indices, outline_rings, outer_rings, bbox_min, bbox_max, centroid: [0.0, 0.0] };
    };

    let polygons: Vec<&Value> = if gtype == "MultiPolygon" {
        coords.as_array().map(|a| a.iter().collect()).unwrap_or_default()
    } else {
        vec![coords]
    };

    for poly in polygons {
        let Some(rings) = poly.as_array() else { continue };
        let mut flat: Vec<f32> = Vec::new();
        let mut hole_indices: Vec<usize> = Vec::new();

        for (ri, ring_val) in rings.iter().enumerate() {
            let Some(ring_coords) = ring_val.as_array() else { continue };
            let mut ring_pts: Vec<LonLat> = Vec::with_capacity(ring_coords.len());
            if ri > 0 {
                hole_indices.push(flat.len() / 2);
            }
            for pt in ring_coords {
                let Some(arr) = pt.as_array() else { continue };
                let lon = arr.first().and_then(|v| v.as_f64()).unwrap_or(0.0) as f32;
                let lat = arr.get(1).and_then(|v| v.as_f64()).unwrap_or(0.0) as f32;
                flat.push(lon);
                flat.push(lat);
                ring_pts.push([lon, lat]);
                bbox_min[0] = bbox_min[0].min(lon);
                bbox_min[1] = bbox_min[1].min(lat);
                bbox_max[0] = bbox_max[0].max(lon);
                bbox_max[1] = bbox_max[1].max(lat);
                centroid_sum[0] += lon as f64;
                centroid_sum[1] += lat as f64;
                centroid_n += 1.0;
            }
            if ri == 0 {
                outer_rings.push(ring_pts.clone());
            }
            outline_rings.push(ring_pts);
        }

        if let Ok(tri_indices) = earcutr::earcut(&flat, &hole_indices, 2) {
            let base = verts.len() as u32;
            for chunk in flat.chunks(2) {
                verts.push([chunk[0], chunk[1]]);
            }
            for tri in tri_indices.chunks_exact(3) {
                indices.push(base + tri[0] as u32);
                indices.push(base + tri[1] as u32);
                indices.push(base + tri[2] as u32);
            }
        }
    }

    let centroid = if centroid_n > 0.0 {
        [(centroid_sum[0] / centroid_n) as f32, (centroid_sum[1] / centroid_n) as f32]
    } else {
        [0.0, 0.0]
    };

    MeshParts { verts, indices, outline_rings, outer_rings, bbox_min, bbox_max, centroid }
}
