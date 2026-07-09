use serde_json::Value;

use super::mesh::{build_mesh_from_geometry, prop_i64, prop_str};
use super::LonLat;

pub struct CountryGeo {
    pub name: String,
    pub name_long: String,
    pub iso_a2: String,
    pub iso_a3: String,
    pub continent: String,
    pub subregion: String,
    pub pop_est: i64,
    pub gdp_md: i64,
    pub centroid: LonLat,
    pub bbox_min: LonLat,
    pub bbox_max: LonLat,
    /// Triangulated fill mesh, in lon/lat space.
    pub mesh_verts: Vec<LonLat>,
    pub mesh_indices: Vec<u32>,
    /// All rings (outer + holes, every polygon part) — for border stroke rendering.
    pub outline_rings: Vec<Vec<LonLat>>,
    /// Outer ring only, one per polygon part — for point-in-polygon hit testing.
    pub outer_rings: Vec<Vec<LonLat>>,
}

pub(super) fn load_countries(json: &str) -> Vec<CountryGeo> {
    let v: Value = serde_json::from_str(json).expect("parse countries geojson");
    let features = v["features"].as_array().cloned().unwrap_or_default();
    let mut out = Vec::with_capacity(features.len());
    for f in &features {
        let props = &f["properties"];
        let mesh = build_mesh_from_geometry(&f["geometry"]);
        out.push(CountryGeo {
            name: prop_str(props, "NAME"),
            name_long: prop_str(props, "NAME_LONG"),
            iso_a2: prop_str(props, "ISO_A2"),
            iso_a3: prop_str(props, "ISO_A3"),
            continent: prop_str(props, "CONTINENT"),
            subregion: prop_str(props, "SUBREGION"),
            pop_est: prop_i64(props, "POP_EST"),
            gdp_md: prop_i64(props, "GDP_MD"),
            centroid: mesh.centroid,
            bbox_min: mesh.bbox_min,
            bbox_max: mesh.bbox_max,
            mesh_verts: mesh.verts,
            mesh_indices: mesh.indices,
            outline_rings: mesh.outline_rings,
            outer_rings: mesh.outer_rings,
        });
    }
    out
}
