use egui::{Context, CornerRadius, Margin, Stroke};

use crate::state::{AppState, MapMode, PaletteAction, Screen};
use crate::theme::{mono, sans, sans_medium, Theme};

fn build_results(state: &AppState) -> Vec<(&'static str, String, PaletteAction)> {
    let mut out: Vec<(&'static str, String, PaletteAction)> = Vec::new();

    out.push(("SCREEN", "Dashboard".to_string(), PaletteAction::GoDashboard));

    for n in &state.world.nations {
        out.push(("NATION", n.name.to_string(), PaletteAction::GoCountry(n.id)));
    }

    for m in MapMode::ALL {
        out.push((
            "ACTION",
            format!("Switch map to {} mode", m.label()),
            PaletteAction::SetMapMode(m),
        ));
    }
    out.push(("ACTION", "Reset map zoom".to_string(), PaletteAction::ResetZoom));
    out.push(("ACTION", "Open notifications".to_string(), PaletteAction::OpenNotifications));

    out
}

fn apply(state: &mut AppState, action: &PaletteAction) {
    match action {
        PaletteAction::GoDashboard => state.screen = Screen::Dashboard,
        PaletteAction::GoCountry(id) => state.select_country(id),
        PaletteAction::SetMapMode(m) => {
            state.map_mode = *m;
            state.screen = Screen::Dashboard;
        }
        PaletteAction::ResetZoom => {
            state.map_zoom = 1.2;
            state.map_center = [10.0, 15.0];
        }
        PaletteAction::OpenNotifications => state.notif_open = true,
    }
    state.palette_open = false;
}

pub fn show(ctx: &Context, theme: &Theme, state: &mut AppState) {
    let screen_rect = ctx.viewport_rect();

    // Scrim
    egui::Area::new(egui::Id::new("palette_scrim"))
        .order(egui::Order::Foreground)
        .fixed_pos(screen_rect.min)
        .show(ctx, |ui| {
            let resp = ui.allocate_response(screen_rect.size(), egui::Sense::click());
            ui.painter().rect_filled(screen_rect, 0.0, theme.scrim);
            if resp.clicked() {
                state.palette_open = false;
            }
        });

    let all = build_results(state);
    let query = state.palette_query.to_lowercase();
    let filtered: Vec<&(&'static str, String, PaletteAction)> = all
        .iter()
        .filter(|(_, label, _)| query.is_empty() || label.to_lowercase().contains(&query))
        .take(9)
        .collect();

    if state.palette_index >= filtered.len() {
        state.palette_index = filtered.len().saturating_sub(1);
    }

    let up = ctx.input(|i| i.key_pressed(egui::Key::ArrowUp));
    let down = ctx.input(|i| i.key_pressed(egui::Key::ArrowDown));
    let enter = ctx.input(|i| i.key_pressed(egui::Key::Enter));
    if down && !filtered.is_empty() {
        state.palette_index = (state.palette_index + 1).min(filtered.len() - 1);
    }
    if up {
        state.palette_index = state.palette_index.saturating_sub(1);
    }
    let mut chosen: Option<PaletteAction> = None;
    if enter {
        if let Some((_, _, action)) = filtered.get(state.palette_index) {
            chosen = Some(action.clone());
        }
    }

    egui::Area::new(egui::Id::new("palette_window"))
        .order(egui::Order::Foreground)
        .anchor(egui::Align2::CENTER_TOP, egui::vec2(0.0, 120.0))
        .show(ctx, |ui| {
            egui::Frame::new()
                .fill(theme.bg_2)
                .stroke(Stroke::new(1.0, theme.border))
                .corner_radius(CornerRadius::same(10))
                .inner_margin(Margin::same(14))
                .shadow(egui::Shadow {
                    offset: [0, 16],
                    blur: 40,
                    spread: 0,
                    color: egui::Color32::from_black_alpha(120),
                })
                .show(ui, |ui| {
                    ui.set_width(560.0);
                    ui.horizontal(|ui| {
                        ui.label(egui::RichText::new("⌕").color(theme.text_faint));
                        let resp = ui.add(
                            egui::TextEdit::singleline(&mut state.palette_query)
                                .desired_width(460.0)
                                .hint_text("Search countries, panels, actions...")
                                .frame(egui::Frame::NONE)
                                .font(sans(14.0)),
                        );
                        resp.request_focus();
                        ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                            egui::Frame::new()
                                .fill(theme.bg_3)
                                .corner_radius(CornerRadius::same(4))
                                .inner_margin(Margin::symmetric(5, 2))
                                .show(ui, |ui| {
                                    ui.label(egui::RichText::new("ESC").font(mono(10.0)).color(theme.text_dim));
                                });
                        });
                    });
                    ui.add_space(8.0);
                    ui.separator();
                    ui.add_space(4.0);

                    egui::ScrollArea::vertical().max_height(360.0).show(ui, |ui| {
                        let mut last_tag = "";
                        for (i, (tag, label, action)) in filtered.iter().enumerate() {
                            if *tag != last_tag {
                                ui.label(egui::RichText::new(*tag).font(sans_medium(10.0)).color(theme.text_faint));
                                last_tag = tag;
                            }
                            let selected = i == state.palette_index;
                            let row = egui::Frame::new()
                                .fill(if selected { theme.accent_dim } else { egui::Color32::TRANSPARENT })
                                .corner_radius(CornerRadius::same(6))
                                .inner_margin(Margin::symmetric(8, 6))
                                .show(ui, |ui| {
                                    ui.set_width(ui.available_width());
                                    let resp = ui.add(
                                        egui::Label::new(
                                            egui::RichText::new(label.as_str())
                                                .font(sans(13.0))
                                                .color(if selected { theme.accent } else { theme.text }),
                                        )
                                        .sense(egui::Sense::click()),
                                    );
                                    resp
                                });
                            if row.inner.hovered() {
                                state.palette_index = i;
                            }
                            if row.inner.clicked() {
                                chosen = Some(action.clone());
                            }
                        }
                        if filtered.is_empty() {
                            ui.label(egui::RichText::new("No results").font(sans(13.0)).color(theme.text_faint));
                        }
                    });
                });
        });

    if let Some(action) = chosen {
        apply(state, &action);
    }
}
