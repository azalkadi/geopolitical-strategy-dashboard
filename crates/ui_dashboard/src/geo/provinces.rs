use serde_json::Value;

use super::mesh::{build_mesh_from_geometry, prop_str};
use super::LonLat;

pub struct ProvinceGeo {
    pub name: String,
    pub admin_country: String,
    pub adm0_a3: String,
    /// Natural Earth's own admin-1 type label (Province/State/Territory/Region/...) — real
    /// data, not a guess.
    pub type_en: String,
    pub centroid: LonLat,
    pub bbox_min: LonLat,
    pub bbox_max: LonLat,
    pub mesh_verts: Vec<LonLat>,
    pub mesh_indices: Vec<u32>,
    pub outline_rings: Vec<Vec<LonLat>>,
}

pub(super) fn load_provinces(json: &str) -> Vec<ProvinceGeo> {
    let v: Value = serde_json::from_str(json).expect("parse provinces geojson");
    let features = v["features"].as_array().cloned().unwrap_or_default();
    let mut out = Vec::with_capacity(features.len());
    for f in &features {
        let props = &f["properties"];
        let mesh = build_mesh_from_geometry(&f["geometry"]);
        out.push(ProvinceGeo {
            name: prop_str(props, "name"),
            admin_country: prop_str(props, "admin"),
            adm0_a3: prop_str(props, "adm0_a3"),
            type_en: prop_str(props, "type_en"),
            centroid: mesh.centroid,
            bbox_min: mesh.bbox_min,
            bbox_max: mesh.bbox_max,
            mesh_verts: mesh.verts,
            mesh_indices: mesh.indices,
            outline_rings: mesh.outline_rings,
        });
    }
    out
}
