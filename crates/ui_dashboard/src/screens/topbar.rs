use egui::{Color32, CornerRadius, Margin, Stroke, Ui};

use crate::components;
use crate::state::AppState;
use crate::theme::{mono, sans, sans_bold, sans_medium, Theme};

pub fn show(ui: &mut Ui, theme: &Theme, state: &mut AppState) {
    ui.horizontal_centered(|ui| {
        // Wordmark
        egui::Frame::new()
            .fill(theme.accent)
            .corner_radius(CornerRadius::same(6))
            .inner_margin(Margin::same(4))
            .show(ui, |ui| {
                ui.set_width(16.0);
                ui.set_height(16.0);
            });
        ui.add_space(8.0);
        ui.label(egui::RichText::new("MERIDIAN").font(mono(15.0)).color(theme.text));
        ui.add_space(6.0);
        ui.label(egui::RichText::new("|").color(theme.border));
        ui.add_space(6.0);
        ui.label(egui::RichText::new("COUNCIL").font(sans_medium(11.0)).color(theme.text_faint));

        ui.add_space(16.0);
        ui.label(egui::RichText::new("|").color(theme.border));
        ui.add_space(16.0);

        // The player's chosen nation (see country_panel's "Play as this nation" button) and
        // a clean, always-visible GDP/deficit/unemployment readout — the "normal game" top
        // bar the interface should read as, not a buried side panel.
        if let Some(pidx) = state.player_country {
            if let (Some(c), Some(eco)) = (state.geo.countries.get(pidx), state.economies.states.get(pidx)) {
                egui::Frame::new()
                    .fill(theme.accent)
                    .corner_radius(CornerRadius::same(4))
                    .show(ui, |ui| {
                        ui.set_width(16.0);
                        ui.set_height(16.0);
                    });
                ui.add_space(6.0);
                ui.label(egui::RichText::new(&c.name).font(sans_bold(13.0)).color(theme.text));
                ui.add_space(14.0);

                let deficit = eco.treasury;
                stat_chip(ui, theme, "GDP", &format!("${:.0}B", eco.gdp), theme.text);
                stat_chip(ui, theme, if deficit < 0.0 { "Deficit" } else { "Surplus" }, &format!("${:.1}B", deficit.abs()), theme.delta_color(deficit as f32));
                stat_chip(ui, theme, "Unemp.", &format!("{:.1}%", eco.unemployment), theme.text);
            }
        } else {
            ui.label(egui::RichText::new("No nation selected — click a country on the map to play as it").font(sans(11.5)).color(theme.text_faint));
        }

        ui.add_space(16.0);
        ui.label(egui::RichText::new("|").color(theme.border));
        ui.add_space(16.0);

        // Real simulation time controls — replaces the old fixed fictional date; this is
        // the actual economy-sim clock (see economy.rs / AppState::advance_sim).
        if ui
            .add(egui::Button::new(egui::RichText::new(state.sim_speed.label()).font(mono(11.0)).color(theme.accent)).fill(theme.bg_1))
            .on_hover_text("Click to cycle simulation speed")
            .clicked()
        {
            state.sim_speed = state.sim_speed.next();
        }
        ui.add_space(8.0);
        ui.label(egui::RichText::new(format!("Day {}", state.sim_day)).font(mono(11.0)).color(theme.text_faint));

        // Centered search / command-palette trigger
        ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
            // Notification bell (rightmost)
            let bell_resp = ui.add(
                egui::Button::new(egui::RichText::new("🔔").font(sans(15.0)))
                    .fill(Color32::TRANSPARENT)
                    .frame(false),
            );
            if state.notif_unread_count > 0 {
                ui.painter().circle_filled(
                    bell_resp.rect.right_top() + egui::vec2(-2.0, 2.0),
                    6.0,
                    theme.bad,
                );
                ui.painter().text(
                    bell_resp.rect.right_top() + egui::vec2(-2.0, 2.0),
                    egui::Align2::CENTER_CENTER,
                    state.notif_unread_count.to_string(),
                    mono(8.5),
                    Color32::WHITE,
                );
            }
            if bell_resp.clicked() {
                state.notif_open = !state.notif_open;
                if state.notif_open {
                    state.notif_unread_count = 0;
                }
            }

            ui.add_space(10.0);

            // Theme toggle
            let icon = match state.theme {
                crate::theme::ThemeMode::Dark => "☾",
                crate::theme::ThemeMode::Light => "☀",
            };
            if ui
                .add(egui::Button::new(egui::RichText::new(icon).font(sans(15.0))).fill(Color32::TRANSPARENT).frame(false))
                .clicked()
            {
                state.theme = match state.theme {
                    crate::theme::ThemeMode::Dark => crate::theme::ThemeMode::Light,
                    crate::theme::ThemeMode::Light => crate::theme::ThemeMode::Dark,
                };
            }

            ui.add_space(14.0);

            // Search / palette trigger bar
            egui::Frame::new()
                .fill(theme.bg_1)
                .stroke(Stroke::new(1.0, theme.border))
                .corner_radius(CornerRadius::same(7))
                .inner_margin(Margin::symmetric(10, 6))
                .show(ui, |ui| {
                    ui.set_width(420.0);
                    ui.horizontal(|ui| {
                        ui.label(egui::RichText::new("⌕").color(theme.text_faint));
                        ui.add_space(4.0);
                        let resp = ui.add(
                            egui::Label::new(
                                egui::RichText::new("Search countries, panels, actions...")
                                    .font(sans(12.5))
                                    .color(theme.text_faint),
                            )
                            .sense(egui::Sense::click()),
                        );
                        if resp.clicked() {
                            state.palette_open = true;
                        }
                        ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                            egui::Frame::new()
                                .fill(theme.bg_3)
                                .corner_radius(CornerRadius::same(4))
                                .inner_margin(Margin::symmetric(5, 2))
                                .show(ui, |ui| {
                                    ui.label(egui::RichText::new("⌘K").font(mono(10.5)).color(theme.text_dim));
                                });
                        });
                    });
                });
        });
    });

    if state.notif_open {
        notif_popover(ui, theme, state);
    }
}

/// A tiny label+value pair for the top bar's compact GDP/deficit/unemployment readout —
/// deliberately no frame/border, so it reads as "clean" rather than another boxed widget.
fn stat_chip(ui: &mut Ui, theme: &Theme, label: &str, value: &str, value_color: Color32) {
    ui.label(egui::RichText::new(label).font(sans(10.5)).color(theme.text_faint));
    ui.add_space(3.0);
    ui.label(egui::RichText::new(value).font(mono(12.0)).color(value_color));
    ui.add_space(12.0);
}

fn notif_popover(ui: &mut Ui, theme: &Theme, state: &mut AppState) {
    egui::Area::new(egui::Id::new("notif_popover"))
        .anchor(egui::Align2::RIGHT_TOP, egui::vec2(-14.0, 54.0))
        .order(egui::Order::Foreground)
        .show(ui.ctx(), |ui| {
            components::solid_frame(theme).show(ui, |ui| {
                ui.set_width(340.0);
                ui.horizontal(|ui| {
                    components::section_title(ui, theme, "Notifications");
                    ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                        if ui.add(egui::Button::new("×").frame(false)).clicked() {
                            state.notif_open = false;
                        }
                    });
                });
                ui.separator();
                egui::ScrollArea::vertical().max_height(320.0).show(ui, |ui| {
                    for e in state.world.events.iter().take(8) {
                        ui.horizontal(|ui| {
                            components::severity_badge(ui, theme, e.severity);
                            ui.vertical(|ui| {
                                ui.label(egui::RichText::new(e.title).font(sans_medium(12.0)).color(theme.text));
                                ui.label(egui::RichText::new(e.ts).font(mono(10.0)).color(theme.text_faint));
                            });
                        });
                        ui.add_space(6.0);
                    }
                });
            });
        });
}

