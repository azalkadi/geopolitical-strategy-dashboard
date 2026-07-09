use egui::{Color32, CornerRadius};

use crate::components;
use crate::state::{AppState, NationCategory};
use crate::theme::{sans_medium, Theme};

/// The bottom-center ministry bar — one icon+label button per government function, the
/// Victoria3/HOI4-style convention this whole redesign is aiming to match. Only visible
/// once a nation is selected, since these are "manage this nation" categories.
pub(super) fn draw_ministry_bar(ctx: &egui::Context, theme: &Theme, state: &mut AppState) {
    egui::Area::new(egui::Id::new("ministry_bar"))
        .anchor(egui::Align2::CENTER_BOTTOM, egui::vec2(0.0, -14.0))
        .order(egui::Order::Middle)
        .show(ctx, |ui| {
            components::glass_frame(theme).show(ui, |ui| {
                ui.horizontal(|ui| {
                    for cat in NationCategory::ALL {
                        let active = state.active_category == cat;
                        let resp = ui.add(
                            egui::Button::new(
                                egui::RichText::new(format!("{}  {}", cat.icon(), cat.label()))
                                    .font(sans_medium(12.0))
                                    .color(if active { theme.bg_0 } else { theme.text_dim }),
                            )
                            .fill(if active { theme.accent } else { Color32::TRANSPARENT })
                            .corner_radius(CornerRadius::same(7)),
                        );
                        if resp.clicked() {
                            state.active_category = cat;
                        }
                        ui.add_space(2.0);
                    }
                });
            });
        });
}
