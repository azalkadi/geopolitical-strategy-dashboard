use egui::Context;

use crate::components;
use crate::state::AppState;
use crate::theme::Theme;

pub fn show(ctx: &Context, theme: &Theme, state: &mut AppState) {
    state.toasts.retain(|t| !t.is_expired());

    let mut dismiss: Option<u64> = None;

    egui::Area::new(egui::Id::new("toast_stack"))
        .order(egui::Order::Tooltip)
        .anchor(egui::Align2::RIGHT_TOP, egui::vec2(-14.0, 60.0))
        .show(ctx, |ui| {
            ui.vertical(|ui| {
                for toast in &state.toasts {
                    ui.horizontal(|ui| {
                        components::toast_card(ui, theme, toast.severity, &toast.title, &toast.message);
                        if ui.small_button("×").clicked() {
                            dismiss = Some(toast.id);
                        }
                    });
                    ui.add_space(6.0);
                }
            });
        });

    if let Some(id) = dismiss {
        state.toasts.retain(|t| t.id != id);
    }
}
