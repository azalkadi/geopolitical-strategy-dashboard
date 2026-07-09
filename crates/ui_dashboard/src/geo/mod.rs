//! Real-world geography: countries, provinces, cities, and (later) infrastructure and
//! landmarks, sourced from Natural Earth (public domain, no attribution required — see
//! data/worlddata/ for the raw GeoJSON). Loaded and triangulated once at startup; the map
//! screen re-projects the cached lon/lat geometry to screen space every frame (cheap)
//! rather than re-triangulating (expensive).
//!
//! Split one file per layer so a growing feature set stays easy to find and debug:
//! `mesh` (shared triangulation/property helpers), `countries`, `provinces`, `cities`.

mod cities;
mod countries;
mod infrastructure;
mod landmarks;
mod mesh;
mod provinces;

pub use cities::{CityGeo, CityTier};
pub use countries::CountryGeo;
pub use infrastructure::{AirportGeo, PortGeo};
pub use landmarks::{LandmarkGeo, LandmarkKind};
pub use provinces::ProvinceGeo;

/// [longitude, latitude] in degrees.
pub type LonLat = [f32; 2];

pub struct GeoWorld {
    pub countries: Vec<CountryGeo>,
    pub provinces: Vec<ProvinceGeo>,
    pub cities: Vec<CityGeo>,
    pub ports: Vec<PortGeo>,
    pub airports: Vec<AirportGeo>,
    pub landmarks: Vec<LandmarkGeo>,
}

impl GeoWorld {
    pub fn load() -> Self {
        let countries = countries::load_countries(include_str!("../../../../data/worlddata/ne_10m_admin_0_countries.geojson"));
        let provinces = provinces::load_provinces(include_str!("../../../../data/worlddata/ne_10m_admin_1_states_provinces.geojson"));
        let cities = cities::load_cities(include_str!("../../../../data/worlddata/ne_10m_populated_places.geojson"));
        let ports = infrastructure::load_ports(include_str!("../../../../data/worlddata/ne_10m_ports.geojson"));
        let airports = infrastructure::load_airports(include_str!("../../../../data/worlddata/ne_10m_airports.geojson"));
        let landmarks = landmarks::load();
        Self { countries, provinces, cities, ports, airports, landmarks }
    }
}

/// Ray-casting point-in-polygon test against a single ring.
pub fn point_in_ring(pt: LonLat, ring: &[LonLat]) -> bool {
    let n = ring.len();
    if n < 3 {
        return false;
    }
    let mut inside = false;
    let mut j = n - 1;
    for i in 0..n {
        let (xi, yi) = (ring[i][0], ring[i][1]);
        let (xj, yj) = (ring[j][0], ring[j][1]);
        if ((yi > pt[1]) != (yj > pt[1])) && (pt[0] < (xj - xi) * (pt[1] - yi) / (yj - yi) + xi) {
            inside = !inside;
        }
        j = i;
    }
    inside
}

pub fn bbox_contains(bbox_min: LonLat, bbox_max: LonLat, pt: LonLat) -> bool {
    pt[0] >= bbox_min[0] && pt[0] <= bbox_max[0] && pt[1] >= bbox_min[1] && pt[1] <= bbox_max[1]
}

/// Do two axis-aligned lon/lat boxes overlap? Used to cull provinces/cities outside the
/// current viewport before doing anything more expensive with them.
pub fn bbox_overlaps(a_min: LonLat, a_max: LonLat, b_min: LonLat, b_max: LonLat) -> bool {
    a_min[0] <= b_max[0] && a_max[0] >= b_min[0] && a_min[1] <= b_max[1] && a_max[1] >= b_min[1]
}
