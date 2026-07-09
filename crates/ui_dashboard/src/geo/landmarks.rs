//! Hand-curated real-world points of interest — straits, canals, and famous landmarks.
//! Natural Earth has no vector layer for these, so unlike every other layer in `geo/`, this
//! one isn't downloaded; it's a small, honestly-scoped list of well-known, publicly
//! documented locations and coordinates, not an attempt at an exhaustive landmark database
//! (that would need something like OpenStreetMap's POI data — a much bigger follow-up).

use super::LonLat;

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum LandmarkKind {
    Strait,
    Canal,
    Landmark,
}
impl LandmarkKind {
    pub fn icon(&self) -> &'static str {
        match self {
            LandmarkKind::Strait => "〰",
            LandmarkKind::Canal => "⛴",
            LandmarkKind::Landmark => "🏛",
        }
    }
}

pub struct LandmarkGeo {
    pub name: String,
    pub kind: LandmarkKind,
    pub pos: LonLat,
}

macro_rules! landmark {
    ($name:expr, $kind:expr, $lon:expr, $lat:expr) => {
        LandmarkGeo { name: $name.to_string(), kind: $kind, pos: [$lon, $lat] }
    };
}

pub fn load() -> Vec<LandmarkGeo> {
    use LandmarkKind::*;
    vec![
        // Strategic maritime chokepoints — the world's most consequential straits.
        landmark!("Strait of Hormuz", Strait, 56.25, 26.57),
        landmark!("Bab-el-Mandeb", Strait, 43.33, 12.58),
        landmark!("Strait of Gibraltar", Strait, -5.60, 35.95),
        landmark!("Strait of Malacca", Strait, 100.50, 3.00),
        landmark!("Bosphorus", Strait, 29.07, 41.12),
        landmark!("Dardanelles", Strait, 26.40, 40.20),
        landmark!("Strait of Dover", Strait, 1.50, 51.00),
        landmark!("Taiwan Strait", Strait, 119.50, 24.50),
        landmark!("Strait of Magellan", Strait, -70.00, -52.60),
        landmark!("Torres Strait", Strait, 142.20, -10.50),
        landmark!("Bering Strait", Strait, -169.00, 65.75),
        // Major shipping canals.
        landmark!("Panama Canal", Canal, -79.68, 9.08),
        landmark!("Suez Canal", Canal, 32.30, 30.50),
        landmark!("Kiel Canal", Canal, 9.70, 54.32),
        landmark!("Corinth Canal", Canal, 22.99, 37.94),
        // A representative (not exhaustive) set of world-famous landmarks.
        landmark!("Eiffel Tower", Landmark, 2.2945, 48.8584),
        landmark!("Great Pyramid of Giza", Landmark, 31.1342, 29.9792),
        landmark!("Statue of Liberty", Landmark, -74.0445, 40.6892),
        landmark!("Great Wall of China", Landmark, 116.0197, 40.3584),
        landmark!("Taj Mahal", Landmark, 78.0421, 27.1751),
        landmark!("Christ the Redeemer", Landmark, -43.2105, -22.9519),
        landmark!("Colosseum", Landmark, 12.4922, 41.8902),
        landmark!("Sydney Opera House", Landmark, 151.2153, -33.8568),
        landmark!("Big Ben", Landmark, -0.1246, 51.5007),
        landmark!("Mount Everest", Landmark, 86.9250, 27.9881),
        landmark!("Machu Picchu", Landmark, -72.5450, -13.1631),
        landmark!("Golden Gate Bridge", Landmark, -122.4783, 37.8199),
        landmark!("Burj Khalifa", Landmark, 55.2744, 25.1972),
        landmark!("Petra", Landmark, 35.4444, 30.3285),
        landmark!("Angkor Wat", Landmark, 103.8670, 13.4125),
    ]
}
