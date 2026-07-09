//! Real ports and airports (Natural Earth 10m, point data — small and cheap to load).
//! Roads and railways are deliberately NOT included yet: Natural Earth's 10m road and
//! railway networks are ~50MB and ~40MB of raw GeoJSON respectively. Parsing that at every
//! startup (on top of the ~73MB we already load for countries/provinces/cities) would push
//! load time well past what's reasonable for a "clean, game-like" launch — the kind of
//! problem a proper offline preprocessing/baking step (Phase 1's planned `data_worldgen`
//! crate) solves, but that doesn't exist yet. Adding them without that step would trade one
//! complaint (dashboard look) for another (slow startup), so they're queued, not silently
//! dropped.

use serde_json::Value;

use super::mesh::{point_coords, prop_str};
use super::LonLat;

pub struct PortGeo {
    pub name: String,
    pub pos: LonLat,
}

pub struct AirportGeo {
    pub name: String,
    pub iata_code: String,
    pub pos: LonLat,
}

pub(super) fn load_ports(json: &str) -> Vec<PortGeo> {
    let v: Value = serde_json::from_str(json).expect("parse ports geojson");
    let features = v["features"].as_array().cloned().unwrap_or_default();
    let mut out = Vec::with_capacity(features.len());
    for f in &features {
        let Some(pos) = point_coords(&f["geometry"]) else { continue };
        out.push(PortGeo { name: prop_str(&f["properties"], "name"), pos });
    }
    out
}

pub(super) fn load_airports(json: &str) -> Vec<AirportGeo> {
    let v: Value = serde_json::from_str(json).expect("parse airports geojson");
    let features = v["features"].as_array().cloned().unwrap_or_default();
    let mut out = Vec::with_capacity(features.len());
    for f in &features {
        let props = &f["properties"];
        let Some(pos) = point_coords(&f["geometry"]) else { continue };
        out.push(AirportGeo { name: prop_str(props, "name"), iata_code: prop_str(props, "iata_code"), pos });
    }
    out
}
