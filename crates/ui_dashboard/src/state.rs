use std::collections::HashSet;
use std::time::{Duration, Instant};

use crate::data::{Category, Severity, World};
use crate::economy::EconomySystem;
use crate::geo::GeoWorld;
use crate::theme::{Accent, ThemeMode};

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum SimSpeed {
    Paused,
    Normal,
    Fast,
    Fastest,
}
impl SimSpeed {
    pub fn days_per_real_second(&self) -> f32 {
        match self {
            SimSpeed::Paused => 0.0,
            SimSpeed::Normal => 1.0,
            SimSpeed::Fast => 5.0,
            SimSpeed::Fastest => 20.0,
        }
    }
    pub fn label(&self) -> &'static str {
        match self {
            SimSpeed::Paused => "II",
            SimSpeed::Normal => "1x",
            SimSpeed::Fast => "5x",
            SimSpeed::Fastest => "20x",
        }
    }
    pub fn next(&self) -> Self {
        match self {
            SimSpeed::Paused => SimSpeed::Normal,
            SimSpeed::Normal => SimSpeed::Fast,
            SimSpeed::Fast => SimSpeed::Fastest,
            SimSpeed::Fastest => SimSpeed::Paused,
        }
    }
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum Screen {
    Dashboard,
    Country,
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum CountryTab {
    Economy,
    Politics,
    Society,
    Military,
    Diplomacy,
}
impl CountryTab {
    pub const ALL: [CountryTab; 5] = [
        CountryTab::Economy,
        CountryTab::Politics,
        CountryTab::Society,
        CountryTab::Military,
        CountryTab::Diplomacy,
    ];
    pub fn label(&self) -> &'static str {
        match self {
            CountryTab::Economy => "Economy",
            CountryTab::Politics => "Politics",
            CountryTab::Society => "Society",
            CountryTab::Military => "Military",
            CountryTab::Diplomacy => "Diplomacy",
        }
    }
}

/// The bottom ministry bar's categories — matches the Victoria3/HOI4-style "one bar per
/// government function" convention the user asked to mimic. Only `Economy` has a real
/// simulation behind it right now; the rest are honest placeholders naming what they'll
/// cover (Phase 1's domain list), not faked content.
#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum NationCategory {
    Economy,
    Budget,
    Trade,
    Politics,
    Diplomacy,
    Military,
    Society,
    Technology,
}
impl NationCategory {
    pub const ALL: [NationCategory; 8] = [
        NationCategory::Economy,
        NationCategory::Budget,
        NationCategory::Trade,
        NationCategory::Politics,
        NationCategory::Diplomacy,
        NationCategory::Military,
        NationCategory::Society,
        NationCategory::Technology,
    ];
    pub fn label(&self) -> &'static str {
        match self {
            NationCategory::Economy => "Economy",
            NationCategory::Budget => "Budget",
            NationCategory::Trade => "Trade",
            NationCategory::Politics => "Politics",
            NationCategory::Diplomacy => "Diplomacy",
            NationCategory::Military => "Military",
            NationCategory::Society => "Society",
            NationCategory::Technology => "Technology",
        }
    }
    pub fn icon(&self) -> &'static str {
        match self {
            NationCategory::Economy => "📈",
            NationCategory::Budget => "🏛",
            NationCategory::Trade => "🚢",
            NationCategory::Politics => "🗳",
            NationCategory::Diplomacy => "🤝",
            NationCategory::Military => "⚔",
            NationCategory::Society => "👥",
            NationCategory::Technology => "🔬",
        }
    }
    /// What each not-yet-simulated category will eventually cover, shown honestly instead
    /// of faked content — pulled from the Phase 1 architecture doc's domain list.
    pub fn coming_soon_description(&self) -> &'static str {
        match self {
            NationCategory::Economy => "",
            NationCategory::Budget => "Government spending allocation, debt issuance, deficits — spun out of the Economy tax model.",
            NationCategory::Trade => "Import/export flows, trade agreements, embargoes, sector-level shortages and surpluses.",
            NationCategory::Politics => "Parties, coalitions, elections, approval ratings, corruption, and constitutional reform.",
            NationCategory::Diplomacy => "Treaties, alliances, international organizations, sanctions, and foreign aid.",
            NationCategory::Military => "Forces, readiness, logistics, procurement, and deployments.",
            NationCategory::Society => "Population, migration, education, healthcare, crime, and public unrest.",
            NationCategory::Technology => "Research tree effects across the other domains — energy, defense, medicine, AI.",
        }
    }
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum MapMode {
    Terrain,
    Satellite,
    Political,
    Economic,
    Military,
    Trade,
    Resources,
    Climate,
    Election,
}
impl MapMode {
    pub const ALL: [MapMode; 9] = [
        MapMode::Terrain,
        MapMode::Satellite,
        MapMode::Political,
        MapMode::Economic,
        MapMode::Military,
        MapMode::Trade,
        MapMode::Resources,
        MapMode::Climate,
        MapMode::Election,
    ];
    pub fn label(&self) -> &'static str {
        match self {
            MapMode::Terrain => "Terrain",
            MapMode::Satellite => "Satellite",
            MapMode::Political => "Political",
            MapMode::Economic => "Economic",
            MapMode::Military => "Military",
            MapMode::Trade => "Trade",
            MapMode::Resources => "Resources",
            MapMode::Climate => "Climate",
            MapMode::Election => "Election",
        }
    }
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum FeedFilter {
    All,
    Economy,
    Trade,
    Politics,
    Diplomacy,
    Military,
    Unrest,
    Climate,
}
impl FeedFilter {
    pub const ALL: [FeedFilter; 8] = [
        FeedFilter::All,
        FeedFilter::Economy,
        FeedFilter::Trade,
        FeedFilter::Politics,
        FeedFilter::Diplomacy,
        FeedFilter::Military,
        FeedFilter::Unrest,
        FeedFilter::Climate,
    ];
    pub fn label(&self) -> &'static str {
        match self {
            FeedFilter::All => "All",
            FeedFilter::Economy => "Economy",
            FeedFilter::Trade => "Trade",
            FeedFilter::Politics => "Politics",
            FeedFilter::Diplomacy => "Diplomacy",
            FeedFilter::Military => "Military",
            FeedFilter::Unrest => "Unrest",
            FeedFilter::Climate => "Climate",
        }
    }
    pub fn matches(&self, cat: Category) -> bool {
        match self {
            FeedFilter::All => true,
            FeedFilter::Economy => cat == Category::Economy,
            FeedFilter::Trade => cat == Category::Trade,
            FeedFilter::Politics => cat == Category::Politics,
            FeedFilter::Diplomacy => cat == Category::Diplomacy,
            FeedFilter::Military => cat == Category::Military,
            FeedFilter::Unrest => cat == Category::Unrest,
            FeedFilter::Climate => cat == Category::Climate,
        }
    }
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum DockSide {
    Left,
    Right,
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum SortDir {
    Asc,
    Desc,
}
impl SortDir {
    pub fn flip(self) -> Self {
        match self {
            SortDir::Asc => SortDir::Desc,
            SortDir::Desc => SortDir::Asc,
        }
    }
    pub fn arrow(self) -> &'static str {
        match self {
            SortDir::Asc => "▲",
            SortDir::Desc => "▼",
        }
    }
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum SectorSortKey {
    Share,
    Growth,
}
#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum MinisterSortKey {
    Approval,
}

pub struct Toast {
    pub id: u64,
    pub severity: Severity,
    pub title: String,
    pub message: String,
    pub spawned: Instant,
}
impl Toast {
    pub fn is_expired(&self) -> bool {
        self.spawned.elapsed() > Duration::from_secs(6)
    }
}

pub struct PaletteResult {
    pub tag: &'static str,
    pub label: String,
    pub hint: String,
    pub action: PaletteAction,
}

#[derive(Clone, Debug)]
pub enum PaletteAction {
    GoDashboard,
    GoCountry(&'static str),
    SetMapMode(MapMode),
    ResetZoom,
    OpenNotifications,
}

pub struct AppState {
    pub world: World,
    pub geo: GeoWorld,
    /// One live economy per `geo.countries` entry, same index — always ticking for every
    /// country, not just whichever one is on screen (Phase 1 Challenge 4).
    pub economies: EconomySystem,
    pub sim_day: u64,
    pub sim_speed: SimSpeed,
    /// Accumulates fractional simulated days between frames so the tick rate is decoupled
    /// from render framerate (Phase 1 §5.3).
    sim_accum_days: f32,

    pub theme: ThemeMode,
    pub accent: Accent,

    pub screen: Screen,
    pub selected_country_id: Option<&'static str>,
    pub country_tab: CountryTab,

    pub map_mode: MapMode,
    /// Roughly "world-widths visible": 1.0 shows the whole 360° of longitude across the
    /// map's width; higher values zoom in. Range enforced by the zoom +/- controls.
    pub map_zoom: f32,
    /// Map center, in [longitude, latitude] degrees.
    pub map_center: [f32; 2],
    pub hovered_country_id: Option<&'static str>,
    /// Index into `geo.countries` for the real-world map layer (separate from the
    /// fictional demo-world `*_country_id` fields above, which drive the mock dashboard).
    pub hovered_real_country: Option<usize>,
    pub selected_real_country: Option<usize>,
    /// The nation the user has chosen to play as — distinct from `selected_real_country`,
    /// which is just "currently being inspected." Set via the "Play as this nation" button
    /// in the country panel.
    pub player_country: Option<usize>,
    /// Which ministry-bar category is open for the selected nation.
    pub active_category: NationCategory,
    /// Set when hovering a province on the map (zoom-gated, like cities).
    pub hovered_province: Option<usize>,
    /// Set when hovering a city on the map (zoom-gated).
    pub hovered_city: Option<usize>,
    pub selected_city: Option<usize>,
    pub selected_province: Option<usize>,

    pub feed_filter: FeedFilter,
    pub expanded_event_ids: HashSet<&'static str>,

    pub palette_open: bool,
    pub palette_query: String,
    pub palette_index: usize,

    pub notif_open: bool,
    pub notif_unread_count: i32,

    pub toasts: Vec<Toast>,
    toast_seq: u64,
    pub startup_toast_fired: bool,
    pub startup_at: Instant,

    pub feed_panel_closed: bool,
    pub overlay_hidden: bool,
    pub feed_dock_side: DockSide,

    pub sector_sort: (SectorSortKey, SortDir),
    pub minister_sort: (MinisterSortKey, SortDir),

    pub tax_corporate: f32,
    pub tax_income: f32,
    pub tax_vat: f32,

    /// Lazily loaded on first use of Satellite map mode (decoding the basemap JPEG takes
    /// real time, so it isn't paid at startup unless that mode is actually opened).
    pub basemap_texture: Option<egui::TextureHandle>,
}

impl Default for AppState {
    fn default() -> Self {
        let geo = GeoWorld::load();
        let economies = EconomySystem::seed(&geo.countries);
        Self {
            world: World::mock(),
            geo,
            economies,
            sim_day: 0,
            sim_speed: SimSpeed::Normal,
            sim_accum_days: 0.0,
            theme: ThemeMode::Dark,
            accent: Accent::Amber,
            screen: Screen::Dashboard,
            selected_country_id: None,
            country_tab: CountryTab::Economy,
            map_mode: MapMode::Terrain,
            map_zoom: 1.2,
            map_center: [10.0, 15.0],
            hovered_country_id: None,
            hovered_real_country: None,
            selected_real_country: None,
            player_country: None,
            active_category: NationCategory::Economy,
            hovered_province: None,
            hovered_city: None,
            selected_city: None,
            selected_province: None,
            feed_filter: FeedFilter::All,
            expanded_event_ids: HashSet::new(),
            palette_open: false,
            palette_query: String::new(),
            palette_index: 0,
            notif_open: false,
            notif_unread_count: 3,
            toasts: Vec::new(),
            toast_seq: 1,
            startup_toast_fired: false,
            startup_at: Instant::now(),
            feed_panel_closed: false,
            overlay_hidden: false,
            feed_dock_side: DockSide::Right,
            sector_sort: (SectorSortKey::Share, SortDir::Desc),
            minister_sort: (MinisterSortKey::Approval, SortDir::Desc),
            tax_corporate: 24.0,
            tax_income: 32.0,
            tax_vat: 18.0,
            basemap_texture: None,
        }
    }
}

impl AppState {
    pub fn select_country(&mut self, id: &'static str) {
        self.selected_country_id = Some(id);
        self.country_tab = CountryTab::Economy;
        self.screen = Screen::Country;
        self.palette_open = false;
    }

    /// Selects (or deselects, with `None`) a real country on the map, always resetting the
    /// ministry bar back to Economy so a fresh selection doesn't land on a stale category.
    pub fn select_real_country(&mut self, idx: Option<usize>) {
        self.selected_real_country = idx;
        if idx.is_some() {
            self.active_category = NationCategory::Economy;
        }
    }

    pub fn push_toast(&mut self, severity: Severity, title: impl Into<String>, message: impl Into<String>) {
        let id = self.toast_seq;
        self.toast_seq += 1;
        self.toasts.push(Toast {
            id,
            severity,
            title: title.into(),
            message: message.into(),
            spawned: Instant::now(),
        });
    }

    pub fn projected_annual_revenue(&self) -> f32 {
        self.world
            .projected_annual_revenue(self.tax_corporate, self.tax_income, self.tax_vat)
    }

    /// Called once per frame with the real elapsed time; advances the simulation by however
    /// many whole days that amount of (speed-scaled) time covers. Decoupling the tick from
    /// the render framerate this way means the sim runs at the same rate at 30fps or 300fps.
    pub fn advance_sim(&mut self, dt_seconds: f32) {
        self.sim_accum_days += dt_seconds * self.sim_speed.days_per_real_second();
        while self.sim_accum_days >= 1.0 {
            self.sim_accum_days -= 1.0;
            self.sim_day += 1;
            self.economies.tick_all();
        }
    }
}
