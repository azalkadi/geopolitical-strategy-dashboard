//! The Global Dashboard screen, split into one file per concern so a growing feature set
//! (map layers, ministry categories, overlay panels) stays easy to find and debug:
//! - `map` — projection, all map layers (countries/provinces/cities/infrastructure/
//!   landmarks), hit-testing, the legend.
//! - `country_panel` — the selected-nation info card and its ministry-category bodies.
//! - `ministry_bar` — the bottom-center category bar.
//! - `overlays` — the stat-tile cluster and the event feed panel.

mod country_panel;
mod map;
mod ministry_bar;
mod overlays;
mod place_panel;

use egui::Ui;

use crate::state::AppState;
use crate::theme::Theme;

pub fn show(ui: &mut Ui, theme: &Theme, state: &mut AppState) {
    let map_rect = ui.max_rect();
    map::draw_map(ui, theme, state, map_rect);

    if let Some(idx) = state.selected_real_country {
        country_panel::draw_real_country_panel(ui.ctx(), theme, state, idx);
        ministry_bar::draw_ministry_bar(ui.ctx(), theme, state);
    }

    if state.overlay_hidden {
        overlays::draw_show_overlays_chip(ui.ctx(), theme, state);
    } else {
        overlays::draw_overlay_cluster(ui.ctx(), theme, state);
    }

    if state.feed_panel_closed {
        overlays::draw_feed_restore_chip(ui.ctx(), theme, state);
    } else {
        overlays::draw_feed_panel(ui.ctx(), theme, state);
    }
}

/// Shared by `map` (tooltips) and `country_panel` (info rows).
fn format_count(n: i64) -> String {
    if n >= 1_000_000_000 {
        format!("{:.2}B", n as f64 / 1_000_000_000.0)
    } else if n >= 1_000_000 {
        format!("{:.1}M", n as f64 / 1_000_000.0)
    } else if n >= 1_000 {
        format!("{:.0}k", n as f64 / 1_000.0)
    } else {
        n.to_string()
    }
}

/// A label-left, value-right row — the basic building block of every info panel body.
fn row(ui: &mut Ui, theme: &Theme, label: &str, value: &str) {
    ui.horizontal(|ui| {
        ui.label(egui::RichText::new(label).font(crate::theme::sans(11.5)).color(theme.text_dim));
        ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
            ui.label(egui::RichText::new(value).font(crate::theme::mono(12.0)).color(theme.text));
        });
    });
    ui.add_space(4.0);
}
