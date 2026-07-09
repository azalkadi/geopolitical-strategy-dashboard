use egui::{Color32, CornerRadius, Pos2, Rect, Stroke, Ui};

use crate::components;
use crate::geo::{self, CountryGeo, LonLat};
use crate::state::{AppState, MapMode};
use crate::theme::{mono, sans, sans_semibold, Theme};

use super::format_count;

/// Simple equirectangular projection: at zoom 1.0 the map's full width spans 360° of
/// longitude. Good enough for a first real-geography pass; a proper Mercator projection
/// (matching what every other map in this genre uses) is a follow-up, not a blocker.
fn project(rect: Rect, center: LonLat, zoom: f32, ll: LonLat) -> Pos2 {
    let scale = rect.width() / 360.0 * zoom;
    egui::pos2(rect.center().x + (ll[0] - center[0]) * scale, rect.center().y - (ll[1] - center[1]) * scale)
}

fn unproject(rect: Rect, center: LonLat, zoom: f32, p: Pos2) -> LonLat {
    let scale = rect.width() / 360.0 * zoom;
    [center[0] + (p.x - rect.center().x) / scale, center[1] - (p.y - rect.center().y) / scale]
}

/// Countries/provinces whose real boundary crosses the ±180° antimeridian (Russia, Fiji,
/// Alaska's Aleutians...) have consecutive ring points that jump from ~+179° to ~-179°
/// longitude. Our projection is a flat linear scale with no wraparound, so a naive draw
/// connects those two points with one giant edge stretching most of the map's width — the
/// "random straight lines" bug. Rather than a full antimeridian-splitting geometry pass,
/// this crops any edge whose *projected* length is implausibly large for a real border
/// segment at the current zoom: real edges are short and dense (10m-resolution vertices),
/// so anything past this fraction of the viewport is provably a wraparound artifact, not a
/// legitimate country boundary.
fn max_plausible_edge(rect: Rect) -> f32 {
    rect.width() * 0.5
}

/// Draws a ring as individual segments (not one closed polyline) so a single wraparound
/// edge can be dropped without discarding the rest of the shape's outline.
fn draw_ring_outline(painter: &egui::Painter, screen_pts: &[Pos2], stroke: Stroke, max_edge: f32) {
    let n = screen_pts.len();
    if n < 2 {
        return;
    }
    for i in 0..n {
        let a = screen_pts[i];
        let b = screen_pts[(i + 1) % n];
        if a.distance(b) <= max_edge {
            painter.line_segment([a, b], stroke);
        }
    }
}

fn visible_bounds(rect: Rect, center: LonLat, zoom: f32) -> (LonLat, LonLat) {
    let a = unproject(rect, center, zoom, rect.left_top());
    let b = unproject(rect, center, zoom, rect.right_bottom());
    ([a[0].min(b[0]), a[1].min(b[1])], [a[0].max(b[0]), a[1].max(b[1])])
}

fn continent_color(theme: &Theme, continent: &str) -> Color32 {
    match continent {
        "Africa" => theme.warn,
        "Asia" => theme.bad,
        "Europe" => theme.accent,
        "North America" => theme.good,
        "South America" => theme.info,
        "Oceania" => theme.good,
        _ => theme.text_faint,
    }
}

/// Cheap FNV-1a over a name, just to get a deterministic-but-varied index per country —
/// no real elevation/land-cover data is loaded, so this fakes the "no two neighboring
/// countries are the exact same flat tint" texture real physical maps have.
fn hash_str(s: &str) -> u64 {
    let mut h: u64 = 0xcbf29ce484222325;
    for b in s.bytes() {
        h ^= b as u64;
        h = h.wrapping_mul(0x100000001b3);
    }
    h
}

const TERRAIN_TONES: [Color32; 6] = [
    Color32::from_rgb(103, 138, 96),
    Color32::from_rgb(118, 148, 103),
    Color32::from_rgb(137, 150, 97),
    Color32::from_rgb(128, 158, 112),
    Color32::from_rgb(148, 142, 92),
    Color32::from_rgb(110, 145, 118),
];

fn terrain_color(c: &CountryGeo) -> Color32 {
    TERRAIN_TONES[(hash_str(&c.name) % TERRAIN_TONES.len() as u64) as usize]
}

/// Ocean tint — a flat near-black/near-white background reads as a chart canvas, not a
/// globe. Derived from `bg_0`'s luminance rather than plumbing `ThemeMode` through, since
/// this is the only place that needs it.
fn ocean_color(theme: &Theme) -> Color32 {
    let luminance = theme.bg_0.r() as u32 + theme.bg_0.g() as u32 + theme.bg_0.b() as u32;
    if luminance < 384 {
        Color32::from_rgb(13, 28, 43)
    } else {
        Color32::from_rgb(175, 205, 224)
    }
}

fn country_fill(theme: &Theme, c: &CountryGeo, mode: MapMode, is_selected: bool, growth_rate: f32) -> Color32 {
    if is_selected {
        return theme.accent.gamma_multiply(0.65);
    }
    match mode {
        MapMode::Terrain => terrain_color(c),
        // Live simulated growth rate, recomputed every tick — this is the one map mode
        // that visibly reflects the running economy sim rather than a static snapshot.
        MapMode::Economic => {
            let base = if growth_rate >= 3.0 {
                theme.good
            } else if growth_rate >= 0.0 {
                theme.warn
            } else {
                theme.bad
            };
            base.gamma_multiply(0.5)
        }
        // Military/Trade/Resources/Climate/Election real data isn't modeled yet (that's
        // Phase 2/6 simulation work) — fall back to the political/continent view rather
        // than fabricate numbers.
        _ => continent_color(theme, &c.continent).gamma_multiply(0.5),
    }
}

pub(super) fn draw_map(ui: &mut Ui, theme: &Theme, state: &mut AppState, rect: Rect) {
    let painter = ui.painter();
    // Full-bleed, no rounded corners/border: the map IS the world, not a chart widget
    // sitting inside a dashboard card.
    painter.rect_filled(rect, CornerRadius::ZERO, ocean_color(theme));

    let prev_clip = ui.clip_rect();
    ui.set_clip_rect(rect.intersect(prev_clip));

    let bg_resp = ui.interact(rect, egui::Id::new("map_bg"), egui::Sense::click_and_drag());

    let scale = rect.width() / 360.0 * state.map_zoom;
    if bg_resp.dragged() {
        let delta = bg_resp.drag_delta();
        state.map_center[0] -= delta.x / scale;
        state.map_center[1] = (state.map_center[1] + delta.y / scale).clamp(-85.0, 85.0);
    }
    if bg_resp.hovered() {
        let scroll = ui.input(|i| i.smooth_scroll_delta.y);
        if scroll.abs() > 0.1 {
            state.map_zoom = (state.map_zoom * (1.0 + scroll * 0.0015)).clamp(1.0, 60.0);
        }
    }

    let (vis_min, vis_max) = visible_bounds(rect, state.map_center, state.map_zoom);
    let pointer_screen = ui.ctx().pointer_hover_pos().filter(|p| rect.contains(*p));
    let pointer_lonlat = pointer_screen.map(|p| unproject(rect, state.map_center, state.map_zoom, p));

    let is_satellite = state.map_mode == MapMode::Satellite;
    if is_satellite {
        draw_satellite_background(ui, state, rect, vis_min, vis_max);
    }

    let mut hovered_idx = None;

    let max_edge = max_plausible_edge(rect);

    for (idx, c) in state.geo.countries.iter().enumerate() {
        if !geo::bbox_overlaps(c.bbox_min, c.bbox_max, vis_min, vis_max) {
            continue;
        }
        let is_selected = state.selected_real_country == Some(idx);
        let growth_rate = state.economies.states.get(idx).map(|e| e.growth_rate).unwrap_or(0.0);

        // In Satellite mode the basemap photo IS the fill — skip the flat color mesh so
        // real imagery shows through, but still draw borders and still hit-test.
        if !is_satellite {
            let fill = country_fill(theme, c, state.map_mode, is_selected, growth_rate);
            let screen_verts: Vec<Pos2> = c.mesh_verts.iter().map(|v| project(rect, state.map_center, state.map_zoom, *v)).collect();
            let mut mesh = egui::Mesh::default();
            for &p in &screen_verts {
                mesh.colored_vertex(p, fill);
            }
            for tri in c.mesh_indices.chunks_exact(3) {
                let (a, b, cc) = (screen_verts[tri[0] as usize], screen_verts[tri[1] as usize], screen_verts[tri[2] as usize]);
                // Skip triangles with an antimeridian-wraparound edge — see `max_plausible_edge`.
                if a.distance(b) > max_edge || b.distance(cc) > max_edge || cc.distance(a) > max_edge {
                    continue;
                }
                mesh.add_triangle(tri[0], tri[1], tri[2]);
            }
            if !mesh.is_empty() {
                ui.painter().add(egui::Shape::mesh(mesh));
            }
        }

        // "Engraved" double-stroke border: a soft dark halo under a crisp thin line reads
        // as a cartographic map rather than a flat data-viz chart outline. In Satellite
        // mode, borders stay subtle so they read as reference lines over the photo.
        let (halo, crisp) = if is_selected {
            (Stroke::new(4.0, theme.accent.gamma_multiply(0.35)), Stroke::new(2.0, theme.accent))
        } else if is_satellite {
            (Stroke::new(0.0, Color32::TRANSPARENT), Stroke::new(0.8, Color32::from_white_alpha(140)))
        } else {
            (Stroke::new(1.6, Color32::from_black_alpha(70)), Stroke::new(0.6, Color32::from_black_alpha(190)))
        };
        for ring in &c.outline_rings {
            let pts: Vec<Pos2> = ring.iter().map(|p| project(rect, state.map_center, state.map_zoom, *p)).collect();
            draw_ring_outline(ui.painter(), &pts, halo, max_edge);
            draw_ring_outline(ui.painter(), &pts, crisp, max_edge);
        }

        if let Some(pl) = pointer_lonlat {
            if geo::bbox_contains(c.bbox_min, c.bbox_max, pl) && c.outer_rings.iter().any(|r| geo::point_in_ring(pl, r)) {
                hovered_idx = Some(idx);
            }
        }
    }

    // A small marker on the player's chosen nation so "who am I playing" is always visible
    // on the map itself, not just inside a panel.
    if let Some(pidx) = state.player_country {
        if let Some(pc) = state.geo.countries.get(pidx) {
            let pos = project(rect, state.map_center, state.map_zoom, pc.centroid) - egui::vec2(0.0, 14.0);
            ui.painter().text(pos, egui::Align2::CENTER_CENTER, "★", egui::FontId::proportional(14.0), theme.accent);
        }
    }

    // Provinces: only past a zoom threshold, and only within the visible viewport —
    // rendering all ~4600 at world-view zoom would both look wrong and cost perf for nothing.
    let mut hovered_province_idx = None;
    if state.map_zoom > 4.0 {
        for (pidx, p) in state.geo.provinces.iter().enumerate() {
            if !geo::bbox_overlaps(p.bbox_min, p.bbox_max, vis_min, vis_max) {
                continue;
            }
            for ring in &p.outline_rings {
                let pts: Vec<Pos2> = ring.iter().map(|pt| project(rect, state.map_center, state.map_zoom, *pt)).collect();
                draw_ring_outline(ui.painter(), &pts, Stroke::new(0.5, theme.text_faint.gamma_multiply(0.5)), max_edge);
            }
            if hovered_province_idx.is_none() {
                if let Some(pl) = pointer_lonlat {
                    // Approximation: tests all rings including any holes, since provinces
                    // don't carry a separate outer-ring-only list the way countries do.
                    if geo::bbox_contains(p.bbox_min, p.bbox_max, pl) && p.outline_rings.iter().any(|r| geo::point_in_ring(pl, r)) {
                        hovered_province_idx = Some(pidx);
                    }
                }
            }
        }
    }

    // Cities: zoom- and tier-gated. Capitals and megacities (real UN-convention 10M+
    // tier) show earliest; major cities need more zoom; towns need the most. Marker size
    // is driven by the same real-population tier (see geo::cities::CityTier).
    let mut hovered_city_idx = None;
    if state.map_zoom > 3.0 {
        let min_pop = (8_000_000.0 / (state.map_zoom / 5.0).max(0.6)).max(0.0) as i64;
        for (cidx, city) in state.geo.cities.iter().enumerate() {
            if !geo::bbox_contains(vis_min, vis_max, city.pos) {
                continue;
            }
            let visible_at_this_zoom = city.is_capital
                || city.tier == geo::CityTier::Megacity
                || (state.map_zoom > 5.0 && city.pop_max >= min_pop);
            if !visible_at_this_zoom {
                continue;
            }
            let pos = project(rect, state.map_center, state.map_zoom, city.pos);
            let r = city.tier.radius() + if city.is_capital { 0.75 } else { 0.0 };
            ui.painter().circle_filled(pos, r, theme.text);
            ui.painter().circle_stroke(pos, r, Stroke::new(1.0, theme.bg_0));
            ui.painter().text(
                pos + egui::vec2(r + 3.0, 0.0),
                egui::Align2::LEFT_CENTER,
                &city.name,
                mono(9.5),
                theme.text_dim,
            );
            if let Some(ps) = pointer_screen {
                if ps.distance(pos) <= r + 5.0 {
                    hovered_city_idx = Some(cidx);
                }
            }
        }
    }

    // Ports and airports: real Natural Earth point data, visible once zoomed in close
    // enough that they'd be individually meaningful rather than overlapping clutter.
    let mut hovered_port_idx = None;
    let mut hovered_airport_idx = None;
    if state.map_zoom > 7.0 {
        for (i, port) in state.geo.ports.iter().enumerate() {
            if !geo::bbox_contains(vis_min, vis_max, port.pos) {
                continue;
            }
            let pos = project(rect, state.map_center, state.map_zoom, port.pos);
            ui.painter().text(pos, egui::Align2::CENTER_CENTER, "⚓", egui::FontId::proportional(11.0), theme.info);
            if let Some(ps) = pointer_screen {
                if ps.distance(pos) <= 8.0 {
                    hovered_port_idx = Some(i);
                }
            }
        }
        for (i, ap) in state.geo.airports.iter().enumerate() {
            if !geo::bbox_contains(vis_min, vis_max, ap.pos) {
                continue;
            }
            let pos = project(rect, state.map_center, state.map_zoom, ap.pos);
            ui.painter().text(pos, egui::Align2::CENTER_CENTER, "✈", egui::FontId::proportional(11.0), theme.warn);
            if let Some(ps) = pointer_screen {
                if ps.distance(pos) <= 8.0 {
                    hovered_airport_idx = Some(i);
                }
            }
        }
    }

    // Landmarks (straits/canals/famous sites): visible fairly early since they're notable
    // by definition, with a label so e.g. "Strait of Hormuz" reads immediately.
    let mut hovered_landmark_idx = None;
    if state.map_zoom > 2.2 {
        for (i, lm) in state.geo.landmarks.iter().enumerate() {
            if !geo::bbox_contains(vis_min, vis_max, lm.pos) {
                continue;
            }
            let pos = project(rect, state.map_center, state.map_zoom, lm.pos);
            ui.painter().text(pos, egui::Align2::CENTER_CENTER, lm.kind.icon(), egui::FontId::proportional(13.0), theme.accent);
            ui.painter().text(
                pos + egui::vec2(0.0, 11.0),
                egui::Align2::CENTER_TOP,
                &lm.name,
                mono(9.0),
                theme.text_dim,
            );
            if let Some(ps) = pointer_screen {
                if ps.distance(pos) <= 9.0 {
                    hovered_landmark_idx = Some(i);
                }
            }
        }
    }

    let clicked = bg_resp.clicked();

    if let Some(i) = hovered_landmark_idx {
        let lm = &state.geo.landmarks[i];
        bg_resp.on_hover_ui(|ui| {
            ui.label(egui::RichText::new(&lm.name).font(sans_semibold(13.0)).color(theme.text));
            let kind_label = match lm.kind {
                geo::LandmarkKind::Strait => "Strait",
                geo::LandmarkKind::Canal => "Canal",
                geo::LandmarkKind::Landmark => "Landmark",
            };
            ui.label(egui::RichText::new(kind_label).font(sans(11.0)).color(theme.text_dim));
        });
    } else if let Some(i) = hovered_airport_idx {
        let ap = &state.geo.airports[i];
        bg_resp.on_hover_ui(|ui| {
            ui.label(egui::RichText::new(&ap.name).font(sans_semibold(13.0)).color(theme.text));
            if !ap.iata_code.is_empty() {
                ui.label(egui::RichText::new(&ap.iata_code).font(mono(11.0)).color(theme.text_dim));
            }
        });
    } else if let Some(i) = hovered_port_idx {
        let port = &state.geo.ports[i];
        bg_resp.on_hover_ui(|ui| {
            ui.label(egui::RichText::new(&port.name).font(sans_semibold(13.0)).color(theme.text));
            ui.label(egui::RichText::new("Port").font(sans(11.0)).color(theme.text_dim));
        });
    } else if let Some(cidx) = hovered_city_idx {
        let city = &state.geo.cities[cidx];
        bg_resp.on_hover_ui(|ui| {
            ui.label(egui::RichText::new(&city.name).font(sans_semibold(13.0)).color(theme.text));
            ui.label(egui::RichText::new(&city.country).font(sans(11.0)).color(theme.text_dim));
            ui.add_space(4.0);
            ui.label(egui::RichText::new(format!("Population (est.)  {}", format_count(city.pop_max))).font(mono(11.0)).color(theme.text));
            if city.is_capital {
                components::badge(ui, "CAPITAL", theme.accent, theme.accent_dim);
            }
        });
    } else if let Some(pidx) = hovered_province_idx {
        let p = &state.geo.provinces[pidx];
        bg_resp.on_hover_ui(|ui| {
            ui.label(egui::RichText::new(&p.name).font(sans_semibold(13.0)).color(theme.text));
            ui.label(egui::RichText::new(&p.admin_country).font(sans(11.0)).color(theme.text_dim));
        });
    } else if let Some(idx) = hovered_idx {
        let c = &state.geo.countries[idx];
        bg_resp.on_hover_ui(|ui| {
            ui.label(egui::RichText::new(&c.name).font(sans_semibold(13.0)).color(theme.text));
            ui.label(egui::RichText::new(format!("{} · {}", c.subregion, c.continent)).font(sans(11.0)).color(theme.text_dim));
            ui.add_space(4.0);
            ui.label(egui::RichText::new(format!("Population  {}", format_count(c.pop_est))).font(mono(11.0)).color(theme.text));
            if c.gdp_md > 0 {
                ui.label(egui::RichText::new(format!("GDP (est.)  ${}B", c.gdp_md / 1000)).font(mono(11.0)).color(theme.text));
            }
        });
    }

    state.hovered_real_country = hovered_idx;
    state.hovered_province = hovered_province_idx;
    state.hovered_city = hovered_city_idx;
    if clicked {
        // City/province clicks open their own info panel without disturbing whatever
        // nation panel is already open (a province click while managing that country's
        // economy shouldn't kick you out of the ministry bar).
        if let Some(cidx) = hovered_city_idx {
            state.selected_city = Some(cidx);
        } else if let Some(pidx) = hovered_province_idx {
            state.selected_province = Some(pidx);
        } else {
            state.select_real_country(hovered_idx);
        }
    }

    ui.set_clip_rect(prev_clip);

    if state.selected_city.is_some() || state.selected_province.is_some() {
        super::place_panel::draw_selected_place_panel(ui.ctx(), theme, state);
    } else {
        draw_legend(ui, theme, state.map_mode);
    }
}

/// Draws NASA's Blue Marble basemap stretched to exactly the currently-visible lon/lat
/// window, so panning/zooming the map moves the photo in lockstep — same UV-rect trick any
/// simple (non-tiled) world-image viewer uses.
fn draw_satellite_background(ui: &mut Ui, state: &mut AppState, rect: Rect, vis_min: LonLat, vis_max: LonLat) {
    let texture = crate::basemap::ensure_loaded(ui.ctx(), &mut state.basemap_texture);
    let uv_min = egui::pos2((vis_min[0] + 180.0) / 360.0, (90.0 - vis_max[1]) / 180.0);
    let uv_max = egui::pos2((vis_max[0] + 180.0) / 360.0, (90.0 - vis_min[1]) / 180.0);
    ui.painter().image(texture.id(), rect, egui::Rect::from_min_max(uv_min, uv_max), Color32::WHITE);
}

fn draw_legend(ui: &mut Ui, theme: &Theme, mode: MapMode) {
    let items: Vec<(&str, Color32)> = match mode {
        MapMode::Terrain | MapMode::Satellite => vec![],
        MapMode::Economic => vec![
            ("Growth ≥3%/yr (live)", theme.good.gamma_multiply(0.5)),
            ("0–3%/yr", theme.warn.gamma_multiply(0.5)),
            ("Recession (<0%)", theme.bad.gamma_multiply(0.5)),
        ],
        _ => vec![
            ("Africa", continent_color(theme, "Africa").gamma_multiply(0.5)),
            ("Asia", continent_color(theme, "Asia").gamma_multiply(0.5)),
            ("Europe", continent_color(theme, "Europe").gamma_multiply(0.5)),
            ("N. America", continent_color(theme, "North America").gamma_multiply(0.5)),
            ("S. America", continent_color(theme, "South America").gamma_multiply(0.5)),
            ("Oceania", continent_color(theme, "Oceania").gamma_multiply(0.5)),
        ],
    };
    let show_note = !matches!(mode, MapMode::Political | MapMode::Economic | MapMode::Terrain | MapMode::Satellite);
    if items.is_empty() && !show_note {
        return;
    }

    egui::Area::new(egui::Id::new("map_legend"))
        .order(egui::Order::Middle)
        .anchor(egui::Align2::LEFT_BOTTOM, egui::vec2(14.0, -14.0))
        .show(ui.ctx(), |ui| {
            components::glass_frame(theme).show(ui, |ui| {
                for (label, color) in &items {
                    ui.horizontal(|ui| {
                        let (rect, _) = ui.allocate_exact_size(egui::vec2(8.0, 8.0), egui::Sense::hover());
                        ui.painter().circle_filled(rect.center(), 4.0, *color);
                        ui.label(egui::RichText::new(*label).font(sans(11.0)).color(theme.text_dim));
                    });
                }
                if show_note {
                    ui.add_space(4.0);
                    ui.label(
                        egui::RichText::new(format!("{} data not modeled yet — showing political view", mode.label()))
                            .font(sans(10.0))
                            .color(theme.text_faint),
                    );
                }
            });
        });
}
