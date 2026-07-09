use crate::components;
use crate::state::AppState;
use crate::theme::{sans, sans_semibold, Theme};

use super::{format_count, row};

/// Info panel for a clicked province or city — sits where the map legend normally does
/// (mutually exclusive with it), since both a legend and a place panel open at once would
/// be clutter, not clarity.
pub(super) fn draw_selected_place_panel(ctx: &egui::Context, theme: &Theme, state: &mut AppState) {
    if let Some(cidx) = state.selected_city {
        draw_city_panel(ctx, theme, state, cidx);
    } else if let Some(pidx) = state.selected_province {
        draw_province_panel(ctx, theme, state, pidx);
    }
}

fn draw_city_panel(ctx: &egui::Context, theme: &Theme, state: &mut AppState, idx: usize) {
    let Some(city) = state.geo.cities.get(idx) else {
        state.selected_city = None;
        return;
    };
    let mut close = false;
    egui::Area::new(egui::Id::new("place_panel"))
        .anchor(egui::Align2::LEFT_BOTTOM, egui::vec2(14.0, -14.0))
        .order(egui::Order::Middle)
        .show(ctx, |ui| {
            components::glass_frame(theme).show(ui, |ui| {
                ui.set_width(240.0);
                ui.horizontal(|ui| {
                    ui.label(egui::RichText::new(&city.name).font(sans_semibold(14.0)).color(theme.text));
                    ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                        if ui.add(egui::Button::new("×").frame(false)).clicked() {
                            close = true;
                        }
                    });
                });
                if city.is_capital {
                    components::badge(ui, "CAPITAL", theme.accent, theme.accent_dim);
                    ui.add_space(4.0);
                }
                row(ui, theme, "Country", &city.country);
                row(ui, theme, "Population (est.)", &format_count(city.pop_max));
                row(ui, theme, "Size tier", city.tier.label());
            });
        });
    if close {
        state.selected_city = None;
    }
}

fn draw_province_panel(ctx: &egui::Context, theme: &Theme, state: &mut AppState, idx: usize) {
    let Some(p) = state.geo.provinces.get(idx) else {
        state.selected_province = None;
        return;
    };
    let mut close = false;
    egui::Area::new(egui::Id::new("place_panel"))
        .anchor(egui::Align2::LEFT_BOTTOM, egui::vec2(14.0, -14.0))
        .order(egui::Order::Middle)
        .show(ctx, |ui| {
            components::glass_frame(theme).show(ui, |ui| {
                ui.set_width(240.0);
                ui.horizontal(|ui| {
                    ui.label(egui::RichText::new(&p.name).font(sans_semibold(14.0)).color(theme.text));
                    ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                        if ui.add(egui::Button::new("×").frame(false)).clicked() {
                            close = true;
                        }
                    });
                });
                row(ui, theme, "Country", &p.admin_country);
                if !p.type_en.is_empty() {
                    row(ui, theme, "Type", &p.type_en);
                }
                ui.label(egui::RichText::new("Provincial economic data not modeled yet").font(sans(10.0)).color(theme.text_faint));
            });
        });
    if close {
        state.selected_province = None;
    }
}

