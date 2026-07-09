use egui::Ui;

use crate::components;
use crate::state::{AppState, NationCategory};
use crate::theme::{mono, mono_medium, sans, sans_medium, sans_semibold, Theme};

use super::{format_count, row};

pub(super) fn draw_real_country_panel(ctx: &egui::Context, theme: &Theme, state: &mut AppState, idx: usize) {
    if state.geo.countries.get(idx).is_none() || state.economies.states.get(idx).is_none() {
        return;
    }

    let mut close = false;
    let mut play_clicked = false;

    egui::Area::new(egui::Id::new("real_country_panel"))
        .anchor(egui::Align2::RIGHT_BOTTOM, egui::vec2(-14.0, -86.0))
        .order(egui::Order::Middle)
        .show(ctx, |ui| {
            components::solid_frame(theme).show(ui, |ui| {
                ui.set_width(320.0);
                let is_player = state.player_country == Some(idx);
                {
                    let c = &state.geo.countries[idx];
                    let capital = state.geo.cities.iter().find(|city| city.is_capital && city.country == c.name);

                    ui.horizontal(|ui| {
                        ui.vertical(|ui| {
                            ui.horizontal(|ui| {
                                ui.label(egui::RichText::new(&c.name).font(sans_semibold(15.0)).color(theme.text));
                                if is_player {
                                    ui.add_space(6.0);
                                    components::badge(ui, "PLAYING", theme.accent, theme.accent_dim);
                                }
                            });
                            if !c.name_long.is_empty() && c.name_long != c.name {
                                ui.label(egui::RichText::new(&c.name_long).font(sans(11.0)).color(theme.text_dim));
                            }
                        });
                        ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                            if ui.add(egui::Button::new("×").frame(false)).clicked() {
                                close = true;
                            }
                        });
                    });
                    ui.add_space(6.0);

                    if !is_player {
                        if ui
                            .add(egui::Button::new(egui::RichText::new("▶  Play as this nation").font(sans_medium(12.0)).color(theme.bg_0)).fill(theme.accent))
                            .clicked()
                        {
                            play_clicked = true;
                        }
                        ui.add_space(6.0);
                    }

                    ui.separator();
                    ui.add_space(6.0);

                    row(ui, theme, "Continent", &format!("{} · {}", c.continent, c.subregion));
                    row(ui, theme, "ISO codes", &format!("{} / {}", c.iso_a2, c.iso_a3));
                    row(ui, theme, "Population (est.)", &format_count(c.pop_est));
                    if let Some(cap) = capital {
                        row(ui, theme, "Capital", &cap.name);
                    }
                }

                ui.add_space(4.0);
                ui.separator();
                ui.add_space(4.0);

                match state.active_category {
                    NationCategory::Economy => draw_economy_category(ui, theme, state, idx),
                    other => draw_placeholder_category(ui, theme, other),
                }
            });
        });

    if close {
        state.select_real_country(None);
    }
    if play_clicked {
        state.player_country = Some(idx);
        let name = state.geo.countries[idx].name.clone();
        state.push_toast(crate::data::Severity::Info, "Now playing", format!("You are now governing {name}"));
    }
}

/// One labeled slider row; returns the new value if the user moved it this frame.
fn tax_slider(ui: &mut Ui, theme: &Theme, label: &str, value: f32, max: f32) -> Option<f32> {
    let mut v = value;
    ui.horizontal(|ui| {
        ui.label(egui::RichText::new(label).font(sans(11.5)).color(theme.text_dim));
        ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
            ui.label(egui::RichText::new(format!("{value:.0}%")).font(mono_medium(11.5)).color(theme.accent));
        });
    });
    let changed = ui.add(egui::Slider::new(&mut v, 0.0..=max).show_value(false)).changed();
    changed.then_some(v)
}

fn draw_economy_category(ui: &mut Ui, theme: &Theme, state: &mut AppState, idx: usize) {
    let new_income;
    let new_corporate;
    let new_vat;
    let new_tariff;
    let new_rate;
    {
        let eco = &state.economies.states[idx];
        ui.horizontal(|ui| {
            components::section_title(ui, theme, &format!("Simulated Economy — Day {}", state.sim_day));
        });
        ui.add_space(4.0);
        row(ui, theme, "GDP", &format!("${:.1}B", eco.gdp));
        ui.horizontal(|ui| {
            ui.label(egui::RichText::new("Growth").font(sans(11.5)).color(theme.text_dim));
            ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                ui.label(
                    egui::RichText::new(format!("{:+.2}%/yr", eco.growth_rate))
                        .font(mono(12.0))
                        .color(theme.delta_color(eco.growth_rate)),
                );
            });
        });
        ui.add_space(4.0);
        row(ui, theme, "Unemployment", &format!("{:.1}%", eco.unemployment));
        row(ui, theme, "Inflation", &format!("{:.1}%", eco.inflation));
        ui.horizontal(|ui| {
            ui.label(egui::RichText::new("Treasury").font(sans(11.5)).color(theme.text_dim));
            ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                ui.label(
                    egui::RichText::new(format!("${:.1}B", eco.treasury))
                        .font(mono(12.0))
                        .color(theme.delta_color(eco.treasury as f32)),
                );
            });
        });

        ui.add_space(6.0);
        ui.separator();
        ui.add_space(4.0);
        components::table_header_plain(ui, theme, "Tax & Monetary Policy");
        ui.add_space(4.0);
        new_income = tax_slider(ui, theme, "Income tax", eco.tax_income, 55.0);
        new_corporate = tax_slider(ui, theme, "Corporate tax", eco.tax_corporate, 55.0);
        new_vat = tax_slider(ui, theme, "VAT / sales tax", eco.tax_vat, 30.0);
        new_tariff = tax_slider(ui, theme, "Import tariffs", eco.tax_tariff, 40.0);
        new_rate = tax_slider(ui, theme, "Interest rate", eco.interest_rate, 20.0);
        ui.add_space(2.0);
        row(ui, theme, "Blended tax burden", &format!("{:.1}%", eco.effective_tax_rate()));

        if let Some(why) = &eco.last_why {
            components::causal_line(ui, theme, why);
        } else {
            components::causal_line(ui, theme, "Baseline simulation running — no notable shifts yet");
        }

        ui.add_space(6.0);
        let note = if eco.has_real_baseline {
            "Population and GDP baseline are real (Natural Earth). Growth/unemployment/inflation/treasury evolve from a simplified simulation, not live real-world data."
        } else {
            "No source GDP estimate for this country — economy seeded from a nominal placeholder baseline, then simulated forward."
        };
        ui.label(egui::RichText::new(note).font(sans(10.0)).color(theme.text_faint));
    }

    let eco = &mut state.economies.states[idx];
    if let Some(v) = new_income {
        eco.tax_income = v;
    }
    if let Some(v) = new_corporate {
        eco.tax_corporate = v;
    }
    if let Some(v) = new_vat {
        eco.tax_vat = v;
    }
    if let Some(v) = new_tariff {
        eco.tax_tariff = v;
    }
    if let Some(v) = new_rate {
        eco.interest_rate = v;
    }
}

fn draw_placeholder_category(ui: &mut Ui, theme: &Theme, cat: NationCategory) {
    ui.horizontal(|ui| {
        ui.label(egui::RichText::new(cat.icon()).font(sans(16.0)));
        ui.add_space(4.0);
        components::section_title(ui, theme, cat.label());
    });
    ui.add_space(6.0);
    ui.label(
        egui::RichText::new("Not simulated yet")
            .font(sans_medium(11.5))
            .color(theme.warn),
    );
    ui.add_space(4.0);
    ui.label(egui::RichText::new(cat.coming_soon_description()).font(sans(11.5)).color(theme.text_dim));
}
