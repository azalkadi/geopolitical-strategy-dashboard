use egui::{Color32, CornerRadius, Margin, Stroke};

use crate::components::{self, StatTile};
use crate::state::{AppState, DockSide, FeedFilter, MapMode};
use crate::theme::{mono_medium, sans, sans_medium, Theme};

fn opposite(side: DockSide) -> DockSide {
    match side {
        DockSide::Left => DockSide::Right,
        DockSide::Right => DockSide::Left,
    }
}

pub(super) fn draw_overlay_cluster(ctx: &egui::Context, theme: &Theme, state: &mut AppState) {
    let side = opposite(state.feed_dock_side);
    let (anchor, offset) = match side {
        DockSide::Left => (egui::Align2::LEFT_TOP, egui::vec2(12.0, 64.0)),
        DockSide::Right => (egui::Align2::RIGHT_TOP, egui::vec2(-12.0, 64.0)),
    };

    egui::Area::new(egui::Id::new("overlay_cluster"))
        .anchor(anchor, offset)
        .order(egui::Order::Middle)
        .show(ctx, |ui| {
            ui.set_max_width(560.0);
            components::glass_frame(theme).show(ui, |ui| {
                ui.horizontal(|ui| {
                    ui.label(egui::RichText::new("⋮⋮").color(theme.text_faint));
                    ui.add_space(6.0);
                    ui.label(egui::RichText::new("World Map").font(sans_medium(12.0)).color(theme.text));
                    ui.add_space(10.0);
                    for m in MapMode::ALL {
                        let active = state.map_mode == m;
                        let resp = ui.add(
                            egui::Button::new(egui::RichText::new(m.label()).font(sans(11.0)).color(if active {
                                theme.accent
                            } else {
                                theme.text_dim
                            }))
                            .fill(if active { theme.accent_dim } else { Color32::TRANSPARENT })
                            .corner_radius(CornerRadius::same(5))
                            .frame(true),
                        );
                        if resp.clicked() {
                            state.map_mode = m;
                        }
                    }
                    ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                        if ui.add(egui::Button::new("👁").frame(false)).clicked() {
                            state.overlay_hidden = true;
                        }
                        ui.add_space(8.0);
                        if ui.small_button("+").clicked() {
                            state.map_zoom = (state.map_zoom * 1.3).min(60.0);
                        }
                        ui.label(egui::RichText::new(format!("{:.1}x", state.map_zoom)).font(mono_medium(11.0)).color(theme.text_dim));
                        if ui.small_button("-").clicked() {
                            state.map_zoom = (state.map_zoom / 1.3).max(1.0);
                        }
                    });
                });
            });
            ui.add_space(8.0);
            ui.horizontal_wrapped(|ui| {
                let home = state.world.nation(state.world.home_id());
                if let Some(home) = home {
                    let tiles = [
                        StatTile { label: "GDP Growth", value: "1.8%", delta_text: "-0.3pp WoW", delta: -0.3, spark: &state.world.gdp_series, why: "Slowing on steel export drop following Kesh tariff retaliation" },
                        StatTile { label: "Approval", value: "47%", delta_text: "-2pp WoW", delta: -2.0, spark: &state.world.approval_series_dash, why: "Coalition strain and transit strike weigh on approval" },
                        StatTile { label: "Unrest Index", value: "38", delta_text: "+9 WoW", delta: -9.0, spark: &state.world.unrest_series, why: "Transit strike entering day 4 in the capital" },
                        StatTile { label: "Military Readiness", value: "71%", delta_text: "+3pp WoW", delta: 3.0, spark: &state.world.readiness_series, why: "Reserve call-up offsets equipment backlog" },
                    ];
                    let _ = home;
                    for t in &tiles {
                        components::stat_tile(ui, theme, 168.0, t);
                    }
                }
            });
        });
}

pub(super) fn draw_show_overlays_chip(ctx: &egui::Context, theme: &Theme, state: &mut AppState) {
    egui::Area::new(egui::Id::new("show_overlays_chip"))
        .anchor(egui::Align2::LEFT_TOP, egui::vec2(12.0, 64.0))
        .order(egui::Order::Middle)
        .show(ctx, |ui| {
            egui::Frame::new()
                .fill(theme.bg_2_t)
                .stroke(Stroke::new(1.0, theme.border))
                .corner_radius(CornerRadius::same(7))
                .inner_margin(Margin::symmetric(10, 6))
                .show(ui, |ui| {
                    if ui
                        .add(egui::Label::new(egui::RichText::new("👁 Show overlays").font(sans(12.0)).color(theme.text_dim)).sense(egui::Sense::click()))
                        .clicked()
                    {
                        state.overlay_hidden = false;
                    }
                });
        });
}

pub(super) fn draw_feed_restore_chip(ctx: &egui::Context, theme: &Theme, state: &mut AppState) {
    let (anchor, offset) = match state.feed_dock_side {
        DockSide::Right => (egui::Align2::RIGHT_TOP, egui::vec2(-12.0, 64.0)),
        DockSide::Left => (egui::Align2::LEFT_TOP, egui::vec2(12.0, 64.0)),
    };
    egui::Area::new(egui::Id::new("feed_restore_chip"))
        .anchor(anchor, offset)
        .order(egui::Order::Middle)
        .show(ctx, |ui| {
            egui::Frame::new()
                .fill(theme.bg_2_t)
                .stroke(Stroke::new(1.0, theme.border))
                .corner_radius(CornerRadius::same(7))
                .inner_margin(Margin::symmetric(10, 6))
                .show(ui, |ui| {
                    if ui
                        .add(egui::Label::new(egui::RichText::new("Feed closed · Restore").font(sans(12.0)).color(theme.text_dim)).sense(egui::Sense::click()))
                        .clicked()
                    {
                        state.feed_panel_closed = false;
                    }
                });
        });
}

pub(super) fn draw_feed_panel(ctx: &egui::Context, theme: &Theme, state: &mut AppState) {
    let (anchor, offset) = match state.feed_dock_side {
        DockSide::Right => (egui::Align2::RIGHT_TOP, egui::vec2(-12.0, 64.0)),
        DockSide::Left => (egui::Align2::LEFT_TOP, egui::vec2(12.0, 64.0)),
    };

    egui::Area::new(egui::Id::new("feed_panel"))
        .anchor(anchor, offset)
        .order(egui::Order::Middle)
        .show(ctx, |ui| {
            ui.set_width(360.0);
            components::glass_frame(theme).show(ui, |ui| {
                ui.set_width(336.0);
                ui.horizontal(|ui| {
                    ui.label(egui::RichText::new("⋮⋮").color(theme.text_faint));
                    ui.add_space(6.0);
                    components::section_title(ui, theme, "Event & Notification Feed");
                    ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                        if ui.add(egui::Button::new("×").frame(false)).clicked() {
                            state.feed_panel_closed = true;
                        }
                        if ui.add(egui::Button::new("⇋").frame(false)).clicked() {
                            state.feed_dock_side = opposite(state.feed_dock_side);
                        }
                    });
                });
                ui.add_space(6.0);
                ui.horizontal_wrapped(|ui| {
                    for f in FeedFilter::ALL {
                        let active = state.feed_filter == f;
                        let resp = ui.add(
                            egui::Button::new(egui::RichText::new(f.label()).font(sans(10.5)).color(if active {
                                theme.accent
                            } else {
                                theme.text_dim
                            }))
                            .fill(if active { theme.accent_dim } else { theme.bg_1 })
                            .corner_radius(CornerRadius::same(5)),
                        );
                        if resp.clicked() {
                            state.feed_filter = f;
                        }
                    }
                });
                ui.add_space(6.0);
                ui.separator();

                let mut toggle_id: Option<&'static str> = None;
                let mut jump_to: Option<&'static str> = None;

                egui::ScrollArea::vertical().max_height(560.0).show(ui, |ui| {
                    for e in state.world.events.iter().filter(|e| state.feed_filter.matches(e.category)) {
                        let expanded = state.expanded_event_ids.contains(e.id);
                        ui.add_space(6.0);
                        ui.horizontal(|ui| {
                            components::severity_badge(ui, theme, e.severity);
                            ui.vertical(|ui| {
                                ui.set_width(240.0);
                                ui.label(egui::RichText::new(e.title).font(sans_medium(12.0)).color(theme.text));
                                ui.label(egui::RichText::new(format!("↳ {}", e.why)).font(sans(11.0)).color(theme.text_dim));
                                ui.label(
                                    egui::RichText::new(format!("{}  ·  {}", e.ts, e.category.label()))
                                        .font(crate::theme::mono(10.0))
                                        .color(theme.text_faint),
                                );
                            });
                            ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                                let chevron = if expanded { "▲" } else { "▼" };
                                if ui.add(egui::Button::new(chevron).frame(false)).clicked() {
                                    toggle_id = Some(e.id);
                                }
                            });
                        });
                        if expanded {
                            ui.indent(("chain", e.id), |ui| {
                                for (i, step) in e.chain.iter().enumerate() {
                                    ui.label(egui::RichText::new(format!("{}. {}", i + 1, step)).font(sans(11.0)).color(theme.text_dim));
                                }
                                if let Some(country) = e.country {
                                    if let Some(n) = state.world.nation(country) {
                                        if ui
                                            .add(egui::Label::new(egui::RichText::new(format!("Jump to {} →", n.name)).font(sans(11.0)).color(theme.accent)).sense(egui::Sense::click()))
                                            .clicked()
                                        {
                                            jump_to = Some(country);
                                        }
                                    }
                                }
                            });
                        }
                        ui.separator();
                    }
                });

                if let Some(id) = toggle_id {
                    if state.expanded_event_ids.contains(id) {
                        state.expanded_event_ids.remove(id);
                    } else {
                        state.expanded_event_ids.insert(id);
                    }
                }
                if let Some(c) = jump_to {
                    state.select_country(c);
                }
            });
        });
}
