use serde_json::Value;

use super::mesh::{point_coords, prop_i64, prop_str};
use super::LonLat;

/// Size tiers by real population estimate — the "categorize cities by size" mechanic.
/// Thresholds match common real-world usage (UN "megacity" = 10M+, etc.), not a guess.
#[derive(Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Debug)]
pub enum CityTier {
    Town,
    City,
    MajorCity,
    Megacity,
}
impl CityTier {
    pub fn label(&self) -> &'static str {
        match self {
            CityTier::Town => "Town",
            CityTier::City => "City",
            CityTier::MajorCity => "Major City",
            CityTier::Megacity => "Megacity",
        }
    }
    /// Marker radius in screen pixels, before the capital-star bump.
    pub fn radius(&self) -> f32 {
        match self {
            CityTier::Town => 1.5,
            CityTier::City => 2.0,
            CityTier::MajorCity => 2.75,
            CityTier::Megacity => 3.5,
        }
    }
    fn from_pop(pop_max: i64) -> Self {
        if pop_max >= 10_000_000 {
            CityTier::Megacity
        } else if pop_max >= 1_000_000 {
            CityTier::MajorCity
        } else if pop_max >= 100_000 {
            CityTier::City
        } else {
            CityTier::Town
        }
    }
}

pub struct CityGeo {
    pub name: String,
    pub country: String,
    pub pos: LonLat,
    pub pop_max: i64,
    pub is_capital: bool,
    pub tier: CityTier,
}

pub(super) fn load_cities(json: &str) -> Vec<CityGeo> {
    let v: Value = serde_json::from_str(json).expect("parse cities geojson");
    let features = v["features"].as_array().cloned().unwrap_or_default();
    let mut out = Vec::with_capacity(features.len());
    for f in &features {
        let props = &f["properties"];
        let Some(pos) = point_coords(&f["geometry"]) else { continue };
        let pop_max = prop_i64(props, "POP_MAX");
        out.push(CityGeo {
            name: prop_str(props, "NAME"),
            country: prop_str(props, "ADM0NAME"),
            pos,
            pop_max,
            is_capital: prop_i64(props, "ADM0CAP") == 1,
            tier: CityTier::from_pop(pop_max),
        });
    }
    out
}
