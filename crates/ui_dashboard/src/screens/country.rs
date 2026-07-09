use egui::{CornerRadius, Ui};
use egui_extras::{Column, TableBuilder};
use egui_plot::{Line, Plot, PlotPoints, Points};

use crate::components::{self, StatTile};
use crate::data::{Alliance, DeploymentStatus, MinisterStatus, Nation, Severity};
use crate::state::{AppState, CountryTab, MinisterSortKey, Screen, SectorSortKey, SortDir};
use crate::theme::{mono, mono_medium, sans, sans_bold, sans_medium, sans_semibold, Theme};

pub fn show(ui: &mut Ui, theme: &Theme, state: &mut AppState) {
    let country_id = state.selected_country_id.unwrap_or_else(|| state.world.home_id());
    let Some(nation) = state.world.nation(country_id).cloned() else {
        state.screen = Screen::Dashboard;
        return;
    };

    egui::ScrollArea::vertical().show(ui, |ui| {
        draw_header(ui, theme, state, &nation);
        ui.add_space(10.0);
        draw_tabs(ui, theme, state);
        ui.add_space(10.0);
        match state.country_tab {
            CountryTab::Economy => draw_economy(ui, theme, state),
            CountryTab::Politics => draw_politics(ui, theme, state),
            CountryTab::Society => draw_society(ui, theme, state),
            CountryTab::Military => draw_military(ui, theme, state),
            CountryTab::Diplomacy => draw_diplomacy(ui, theme, state, &nation),
        }
    });
}

fn alliance_colors(theme: &Theme, alliance: Alliance) -> (egui::Color32, egui::Color32) {
    match alliance {
        Alliance::Home => (theme.accent, theme.accent_dim),
        Alliance::Ally => (theme.info, theme.info_bg),
        Alliance::Neutral => (theme.info, theme.info_bg),
        Alliance::Rival => (theme.bad, theme.bad_bg),
    }
}

fn draw_header(ui: &mut Ui, theme: &Theme, state: &mut AppState, n: &Nation) {
    components::solid_frame(theme).show(ui, |ui| {
        ui.horizontal(|ui| {
            if ui
                .add(egui::Button::new(egui::RichText::new("← Dashboard").font(sans_medium(12.0)).color(theme.text_dim)))
                .clicked()
            {
                state.screen = Screen::Dashboard;
            }
            ui.add_space(10.0);
            egui::Frame::new()
                .fill(theme.accent)
                .corner_radius(CornerRadius::same(8))
                .show(ui, |ui| {
                    ui.set_width(34.0);
                    ui.set_height(34.0);
                });
            ui.add_space(8.0);
            ui.vertical(|ui| {
                ui.label(egui::RichText::new(n.name).font(sans_bold(16.0)).color(theme.text));
                ui.label(
                    egui::RichText::new(format!("{} · {} · Capital: {}", n.region, n.gov_type, n.capital))
                        .font(sans(11.5))
                        .color(theme.text_dim),
                );
            });
            ui.add_space(12.0);
            let (fg, bg) = alliance_colors(theme, n.alliance);
            components::badge(ui, n.alliance.label(), fg, bg);

            ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                for (label, value) in [
                    ("Readiness", format!("{}%", n.readiness)),
                    ("Unrest", n.unrest.to_string()),
                    ("Approval", format!("{}%", n.approval)),
                    ("GDP", format!("${}B", n.gdp)),
                ] {
                    ui.vertical(|ui| {
                        ui.label(egui::RichText::new(label).font(sans_medium(10.0)).color(theme.text_faint));
                        ui.label(egui::RichText::new(value).font(mono_medium(15.0)).color(theme.text));
                    });
                    ui.add_space(14.0);
                    ui.separator();
                    ui.add_space(14.0);
                }
            });
        });
    });
}

fn draw_tabs(ui: &mut Ui, theme: &Theme, state: &mut AppState) {
    ui.horizontal(|ui| {
        for tab in CountryTab::ALL {
            let active = state.country_tab == tab;
            let resp = ui.add(
                egui::Button::new(egui::RichText::new(tab.label()).font(if active {
                    sans_semibold(12.5)
                } else {
                    sans(12.5)
                })
                .color(if active { theme.bg_0 } else { theme.text_dim }))
                .fill(if active { theme.accent } else { egui::Color32::TRANSPARENT })
                .corner_radius(CornerRadius::same(6)),
            );
            if resp.clicked() {
                state.country_tab = tab;
            }
            ui.add_space(4.0);
        }
    });
}

/// Returns (left_width, right_width) for a two-column row. Deliberately does not take
/// closures for the two columns: both would need to be constructed (and hold a mutable
/// borrow of `state`) before either runs, which the borrow checker rejects whenever one
/// side needs `&mut AppState`. Callers instead do two sequential `ui.allocate_ui` calls
/// inline, so each closure is built, run, and dropped before the next is built.
fn two_col_widths(ui: &Ui, left_ratio: f32) -> (f32, f32) {
    let total = ui.available_width();
    let gap = 12.0;
    let left_w = (total - gap) * left_ratio;
    let right_w = (total - gap) - left_w;
    (left_w, right_w)
}

fn draw_economy(ui: &mut Ui, theme: &Theme, state: &mut AppState) {
    ui.horizontal_wrapped(|ui| {
        let tiles = [
            StatTile { label: "GDP", value: "$2140B", delta_text: "+1.8%", delta: 1.8, spark: &state.world.gdp_series, why: "Slowing on steel export drop following Kesh tariff retaliation" },
            StatTile { label: "Inflation", value: "3.1%", delta_text: "+0.0pp", delta: 0.0, spark: &state.world.gdp_series, why: "Central bank holds rate steady at 4.25%" },
            StatTile { label: "Trade Balance", value: "-$34B", delta_text: "-$1B MoM", delta: -1.0, spark: &state.world.gdp_series, why: "Export volume falls on retaliatory tariffs" },
            StatTile { label: "Unemployment", value: "5.4%", delta_text: "+0.2pp", delta: -0.2, spark: &state.world.gdp_series, why: "Transit strike temporarily idles logistics workers" },
        ];
        for t in &tiles {
            components::stat_tile(ui, theme, 210.0, t);
        }
    });
    ui.add_space(12.0);

    let (left_w, right_w) = two_col_widths(ui, 0.58);
    ui.horizontal_top(|ui| {
        ui.allocate_ui(egui::vec2(left_w, 0.0), |ui| {
            ui.set_width(left_w);
            draw_sectors_table(ui, theme, state);
        });
        ui.add_space(12.0);
        ui.allocate_ui(egui::vec2(right_w, 0.0), |ui| {
            ui.set_width(right_w);
            draw_budget_card(ui, theme, state);
            ui.add_space(12.0);
            draw_tax_card(ui, theme, state);
        });
    });
}

fn draw_sectors_table(ui: &mut Ui, theme: &Theme, state: &mut AppState) {
    components::solid_frame(theme).show(ui, |ui| {
        ui.horizontal(|ui| {
            components::section_title(ui, theme, "Economic Sectors");
            ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                ui.label(egui::RichText::new("% of GDP").font(sans(10.5)).color(theme.text_faint));
            });
        });
        ui.add_space(6.0);

        let mut sectors = state.world.sectors.clone();
        let (key, dir) = state.sector_sort;
        sectors.sort_by(|a, b| {
            let ord = match key {
                SectorSortKey::Share => a.gdp_share.partial_cmp(&b.gdp_share).unwrap(),
                SectorSortKey::Growth => a.growth.partial_cmp(&b.growth).unwrap(),
            };
            if dir == SortDir::Desc {
                ord.reverse()
            } else {
                ord
            }
        });

        TableBuilder::new(ui)
            .column(Column::remainder().at_least(120.0))
            .column(Column::exact(70.0))
            .column(Column::exact(70.0))
            .column(Column::exact(60.0))
            .column(Column::remainder().at_least(160.0))
            .header(20.0, |mut header| {
                header.col(|ui| components::table_header_plain(ui, theme, "Sector"));
                header.col(|ui| {
                    if components::sortable_header(ui, theme, "Share", key == SectorSortKey::Share, dir) {
                        toggle_sector_sort(state_ref(state), SectorSortKey::Share);
                    }
                });
                header.col(|ui| {
                    if components::sortable_header(ui, theme, "Growth", key == SectorSortKey::Growth, dir) {
                        toggle_sector_sort(state_ref(state), SectorSortKey::Growth);
                    }
                });
                header.col(|ui| components::table_header_plain(ui, theme, "Employ."));
                header.col(|ui| components::table_header_plain(ui, theme, "Note"));
            })
            .body(|mut body| {
                for s in &sectors {
                    body.row(28.0, |mut row| {
                        row.col(|ui| {
                            ui.label(egui::RichText::new(s.name).font(sans_medium(12.0)).color(theme.text));
                        });
                        row.col(|ui| {
                            ui.label(egui::RichText::new(format!("{:.1}%", s.gdp_share)).font(mono(12.0)).color(theme.text));
                        });
                        row.col(|ui| {
                            ui.label(
                                egui::RichText::new(format!("{:+.1}%", s.growth))
                                    .font(mono(12.0))
                                    .color(theme.delta_color(s.growth)),
                            );
                        });
                        row.col(|ui| {
                            ui.label(egui::RichText::new(s.employ.to_string()).font(mono(12.0)).color(theme.text_dim));
                        });
                        row.col(|ui| {
                            ui.label(egui::RichText::new(s.note).font(sans(11.0)).color(theme.text_dim));
                        });
                    });
                }
            });
    });
}

// Plain reborrow helper: calling this instead of using `state` directly inside nested
// TableBuilder header closures forces a fresh `&mut AppState` reborrow at each call site,
// which keeps the borrow checker happy across the nested `header(...).col(...)` closures
// without restructuring the table into a two-pass (build / apply-clicks-after) shape.
fn state_ref(state: &mut AppState) -> &mut AppState {
    state
}

fn toggle_sector_sort(state: &mut AppState, key: SectorSortKey) {
    if state.sector_sort.0 == key {
        state.sector_sort.1 = state.sector_sort.1.flip();
    } else {
        state.sector_sort = (key, SortDir::Desc);
    }
}

fn toggle_minister_sort(state: &mut AppState, key: MinisterSortKey) {
    if state.minister_sort.0 == key {
        state.minister_sort.1 = state.minister_sort.1.flip();
    } else {
        state.minister_sort = (key, SortDir::Desc);
    }
}

fn draw_budget_card(ui: &mut Ui, theme: &Theme, state: &AppState) {
    components::solid_frame(theme).show(ui, |ui| {
        components::section_title(ui, theme, "Budget Allocation");
        ui.add_space(6.0);
        for b in &state.world.budget {
            components::labeled_bar(ui, theme, b.label, b.pct as f32, b.delta);
        }
    });
}

fn draw_tax_card(ui: &mut Ui, theme: &Theme, state: &mut AppState) {
    components::solid_frame(theme).show(ui, |ui| {
        components::section_title(ui, theme, "Tax Policy");
        ui.add_space(6.0);

        let mut changed = false;
        for (label, val) in [
            ("Corporate", &mut state.tax_corporate),
            ("Income", &mut state.tax_income),
            ("VAT", &mut state.tax_vat),
        ] {
            ui.horizontal(|ui| {
                ui.label(egui::RichText::new(label).font(sans(13.0)).color(theme.text));
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    ui.label(egui::RichText::new(format!("{val:.0}%")).font(mono_medium(13.0)).color(theme.accent));
                });
            });
            let resp = ui.add(egui::Slider::new(val, 0.0..=55.0).show_value(false));
            if resp.changed() {
                changed = true;
            }
            ui.add_space(6.0);
        }

        ui.separator();
        ui.add_space(4.0);
        let revenue = state.projected_annual_revenue();
        ui.horizontal(|ui| {
            ui.label(egui::RichText::new("Projected annual revenue").font(sans(11.5)).color(theme.text_dim));
            ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                ui.label(egui::RichText::new(format!("${revenue:.0}B")).font(mono_medium(14.0)).color(theme.text));
            });
        });

        if changed {
            state.push_toast(Severity::Info, "Tax policy updated", format!("Projected annual revenue recalculated: ${revenue:.0}B"));
        }
    });
}

fn draw_politics(ui: &mut Ui, theme: &Theme, state: &mut AppState) {
    let (left_w, right_w) = two_col_widths(ui, 0.5);
    ui.horizontal_top(|ui| {
        ui.allocate_ui(egui::vec2(left_w, 0.0), |ui| {
            ui.set_width(left_w);
            draw_approval_trend(ui, theme, state);
        });
        ui.add_space(12.0);
        ui.allocate_ui(egui::vec2(right_w, 0.0), |ui| {
            ui.set_width(right_w);
            draw_next_election(ui, theme, state);
        });
    });
    ui.add_space(12.0);

    let (left_w, right_w) = two_col_widths(ui, 0.58);
    ui.horizontal_top(|ui| {
        ui.allocate_ui(egui::vec2(left_w, 0.0), |ui| {
            ui.set_width(left_w);
            draw_ministers_table(ui, theme, state);
        });
        ui.add_space(12.0);
        ui.allocate_ui(egui::vec2(right_w, 0.0), |ui| {
            ui.set_width(right_w);
            draw_coalition_card(ui, theme, state);
            ui.add_space(12.0);
            draw_corruption_card(ui, theme, state);
        });
    });
}

fn draw_approval_trend(ui: &mut Ui, theme: &Theme, state: &AppState) {
    components::solid_frame(theme).show(ui, |ui| {
        ui.horizontal(|ui| {
            components::section_title(ui, theme, "Approval Trend");
            ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                ui.label(egui::RichText::new("47%").font(mono_medium(20.0)).color(theme.text));
            });
        });
        ui.add_space(6.0);

        let series = &state.world.approval_series;
        let points: PlotPoints = series.iter().enumerate().map(|(i, v)| [i as f64, *v as f64]).collect();
        let line = Line::new("Approval", points).color(theme.accent).width(2.0);

        let marker_pts: Vec<[f64; 2]> = state
            .world
            .approval_marker_idx
            .iter()
            .filter_map(|&i| series.get(i).map(|v| [i as f64, *v as f64]))
            .collect();
        let markers = Points::new("Markers", PlotPoints::from(marker_pts)).radius(3.5).color(theme.accent);

        Plot::new("approval_trend_plot")
            .height(160.0)
            .show_axes([false, false])
            .show_grid([false, false])
            .allow_drag(false)
            .allow_scroll(false)
            .allow_zoom(false)
            .show(ui, |plot_ui| {
                plot_ui.line(line);
                plot_ui.points(markers);
            });

        components::causal_line(ui, theme, "Coalition strain and transit strike weigh on approval; central bank hold offered modest stabilization");
    });
}

fn draw_next_election(ui: &mut Ui, theme: &Theme, state: &AppState) {
    components::solid_frame(theme).show(ui, |ui| {
        components::section_title(ui, theme, "Next Election");
        ui.add_space(6.0);
        let home = state.world.nation(state.world.home_id());
        let days = home.and_then(|h| h.election_days).unwrap_or(0);
        ui.label(egui::RichText::new(format!("{days} days")).font(mono_medium(22.0)).color(theme.accent));
        ui.add_space(8.0);
        ui.label(egui::RichText::new("PROJECTED SEAT SHARE").font(sans_medium(10.0)).color(theme.text_faint));
        ui.add_space(4.0);

        let total: f32 = state.world.parties.iter().map(|p| p.seats as f32).sum();
        let (rect, _) = ui.allocate_exact_size(egui::vec2(ui.available_width(), 20.0), egui::Sense::hover());
        let mut x = rect.left();
        let painter = ui.painter();
        for p in &state.world.parties {
            let w = rect.width() * (p.seats as f32 / total);
            let seg = egui::Rect::from_min_size(egui::pos2(x, rect.top()), egui::vec2(w, rect.height()));
            let color = party_color(theme, p);
            painter.rect_filled(seg, 0.0, color);
            x += w;
        }
    });
}

fn party_color(theme: &Theme, p: &crate::data::Party) -> egui::Color32 {
    if p.is_accent {
        theme.accent
    } else {
        match p.stance {
            "Coalition" => theme.good,
            "Opposition" => theme.bad,
            _ => theme.info,
        }
    }
}

fn draw_ministers_table(ui: &mut Ui, theme: &Theme, state: &mut AppState) {
    components::solid_frame(theme).show(ui, |ui| {
        components::section_title(ui, theme, "Cabinet Ministers");
        ui.add_space(6.0);

        let mut ministers = state.world.ministers.clone();
        let (key, dir) = state.minister_sort;
        ministers.sort_by(|a, b| {
            let ord = match key {
                MinisterSortKey::Approval => a.approval.cmp(&b.approval),
            };
            if dir == SortDir::Desc {
                ord.reverse()
            } else {
                ord
            }
        });

        TableBuilder::new(ui)
            .column(Column::remainder().at_least(110.0))
            .column(Column::exact(110.0))
            .column(Column::exact(90.0))
            .column(Column::exact(70.0))
            .column(Column::exact(70.0))
            .header(20.0, |mut header| {
                header.col(|ui| components::table_header_plain(ui, theme, "Minister"));
                header.col(|ui| components::table_header_plain(ui, theme, "Portfolio"));
                header.col(|ui| components::table_header_plain(ui, theme, "Party"));
                header.col(|ui| {
                    if components::sortable_header(ui, theme, "Approval", key == MinisterSortKey::Approval, dir) {
                        toggle_minister_sort(state_ref(state), MinisterSortKey::Approval);
                    }
                });
                header.col(|ui| components::table_header_plain(ui, theme, "Status"));
            })
            .body(|mut body| {
                for m in &ministers {
                    body.row(26.0, |mut row| {
                        row.col(|ui| {
                            ui.label(egui::RichText::new(m.name).font(sans_medium(12.0)).color(theme.text));
                        });
                        row.col(|ui| {
                            ui.label(egui::RichText::new(m.portfolio).font(sans(11.5)).color(theme.text_dim));
                        });
                        row.col(|ui| {
                            ui.label(egui::RichText::new(m.party).font(sans(11.5)).color(theme.text_dim));
                        });
                        row.col(|ui| {
                            ui.label(egui::RichText::new(format!("{}%", m.approval)).font(mono(12.0)).color(theme.text));
                        });
                        row.col(|ui| {
                            let (label, fg, bg) = match m.status {
                                MinisterStatus::Clear => ("CLEAR", theme.good, theme.good_bg),
                                MinisterStatus::Watch => ("WATCH", theme.warn, theme.warn_bg),
                                MinisterStatus::Scandal => ("SCANDAL", theme.bad, theme.bad_bg),
                            };
                            components::badge(ui, label, fg, bg);
                        });
                    });
                }
            });
    });
}

fn draw_coalition_card(ui: &mut Ui, theme: &Theme, state: &AppState) {
    components::solid_frame(theme).show(ui, |ui| {
        components::section_title(ui, theme, "Coalition Composition");
        ui.add_space(6.0);
        for p in &state.world.parties {
            ui.horizontal(|ui| {
                let (rect, _) = ui.allocate_exact_size(egui::vec2(8.0, 8.0), egui::Sense::hover());
                ui.painter().circle_filled(rect.center(), 4.0, party_color(theme, p));
                ui.label(egui::RichText::new(p.name).font(sans(12.5)).color(theme.text));
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    ui.label(egui::RichText::new(p.seats.to_string()).font(mono(12.0)).color(theme.text));
                    ui.label(egui::RichText::new(p.stance).font(sans(11.0)).color(theme.text_faint));
                });
            });
            ui.add_space(4.0);
        }
    });
}

fn draw_corruption_card(ui: &mut Ui, theme: &Theme, state: &AppState) {
    components::solid_frame(theme).show(ui, |ui| {
        components::section_title(ui, theme, "Corruption & Scandal Flags");
        ui.add_space(6.0);
        for c in &state.world.corruption {
            ui.horizontal(|ui| {
                components::severity_badge(ui, theme, c.severity);
                ui.vertical(|ui| {
                    ui.label(egui::RichText::new(c.title).font(sans_medium(12.0)).color(theme.text));
                    ui.label(egui::RichText::new(c.desc).font(sans(11.0)).color(theme.text_dim));
                });
            });
            ui.add_space(6.0);
        }
    });
}

fn draw_society(ui: &mut Ui, theme: &Theme, state: &AppState) {
    ui.horizontal_wrapped(|ui| {
        for s in &state.world.society_stats {
            simple_stat_tile(ui, theme, 220.0, s.label, s.value, s.why);
        }
    });
    ui.add_space(12.0);
    components::solid_frame(theme).show(ui, |ui| {
        components::section_title(ui, theme, "Unrest Hotspots");
        ui.add_space(6.0);
        for h in &state.world.hotspots {
            ui.horizontal(|ui| {
                ui.label(egui::RichText::new(h.name).font(sans_medium(12.5)).color(theme.text));
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    ui.label(egui::RichText::new(h.value.to_string()).font(mono_medium(13.0)).color(unrest_color(theme, h.value)));
                });
            });
            ui.label(egui::RichText::new(h.note).font(sans(11.0)).color(theme.text_dim));
            ui.add_space(6.0);
            ui.separator();
        }
    });
}

fn unrest_color(theme: &Theme, value: i32) -> egui::Color32 {
    if value >= 60 {
        theme.bad
    } else if value >= 35 {
        theme.warn
    } else {
        theme.good
    }
}

fn simple_stat_tile(ui: &mut Ui, theme: &Theme, width: f32, label: &str, value: &str, why: &str) {
    components::solid_frame(theme).show(ui, |ui| {
        ui.set_width(width);
        ui.label(egui::RichText::new(label.to_uppercase()).font(sans_medium(10.5)).color(theme.text_faint));
        ui.add_space(4.0);
        ui.label(egui::RichText::new(value).font(mono_medium(22.0)).color(theme.text));
        components::causal_line(ui, theme, why);
    });
}

fn draw_military(ui: &mut Ui, theme: &Theme, state: &AppState) {
    ui.horizontal_wrapped(|ui| {
        for s in &state.world.military_stats {
            simple_stat_tile(ui, theme, 220.0, s.label, s.value, s.why);
        }
    });
    ui.add_space(12.0);
    let (left_w, right_w) = two_col_widths(ui, 0.55);
    ui.horizontal_top(|ui| {
        ui.allocate_ui(egui::vec2(left_w, 0.0), |ui| {
            ui.set_width(left_w);
            components::solid_frame(theme).show(ui, |ui| {
                components::section_title(ui, theme, "Branch Readiness");
                ui.add_space(6.0);
                TableBuilder::new(ui)
                    .column(Column::remainder().at_least(90.0))
                    .column(Column::exact(70.0))
                    .column(Column::exact(70.0))
                    .column(Column::remainder().at_least(140.0))
                    .header(20.0, |mut header| {
                        header.col(|ui| components::table_header_plain(ui, theme, "Branch"));
                        header.col(|ui| components::table_header_plain(ui, theme, "Personnel"));
                        header.col(|ui| components::table_header_plain(ui, theme, "Ready"));
                        header.col(|ui| components::table_header_plain(ui, theme, "Note"));
                    })
                    .body(|mut body| {
                        for b in &state.world.branches {
                            body.row(26.0, |mut row| {
                                row.col(|ui| {
                                    ui.label(egui::RichText::new(b.name).font(sans_medium(12.0)).color(theme.text));
                                });
                                row.col(|ui| {
                                    ui.label(egui::RichText::new(b.personnel).font(mono(12.0)).color(theme.text_dim));
                                });
                                row.col(|ui| {
                                    ui.label(egui::RichText::new(format!("{}%", b.readiness)).font(mono(12.0)).color(theme.text));
                                });
                                row.col(|ui| {
                                    ui.label(egui::RichText::new(b.note).font(sans(11.0)).color(theme.text_dim));
                                });
                            });
                        }
                    });
            });
        });
        ui.add_space(12.0);
        ui.allocate_ui(egui::vec2(right_w, 0.0), |ui| {
            ui.set_width(right_w);
            components::solid_frame(theme).show(ui, |ui| {
                components::section_title(ui, theme, "Active Deployments");
                ui.add_space(6.0);
                for d in &state.world.deployments {
                    ui.horizontal(|ui| {
                        ui.vertical(|ui| {
                            ui.label(egui::RichText::new(d.name).font(sans_medium(12.0)).color(theme.text));
                            ui.label(egui::RichText::new(format!("{} · {}", d.location, d.personnel)).font(sans(11.0)).color(theme.text_dim));
                        });
                        ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                            let (label, color) = match d.status {
                                DeploymentStatus::Ongoing => ("Ongoing", theme.good),
                                DeploymentStatus::Concluded => ("Concluded", theme.text_faint),
                            };
                            ui.label(egui::RichText::new(label).font(sans_medium(11.0)).color(color));
                        });
                    });
                    ui.add_space(6.0);
                    ui.separator();
                }
            });
        });
    });
}

fn draw_diplomacy(ui: &mut Ui, theme: &Theme, state: &mut AppState, home: &Nation) {
    let (left_w, right_w) = two_col_widths(ui, 0.62);
    let others: Vec<Nation> = state.world.nations.iter().filter(|n| n.id != home.id).cloned().collect();
    let mut jump_to: Option<&'static str> = None;

    ui.horizontal_top(|ui| {
        ui.allocate_ui(egui::vec2(left_w, 0.0), |ui| {
            ui.set_width(left_w);
            components::solid_frame(theme).show(ui, |ui| {
                components::section_title(ui, theme, "Bilateral Relations");
                ui.add_space(6.0);
                TableBuilder::new(ui)
                    .column(Column::remainder().at_least(120.0))
                    .column(Column::exact(90.0))
                    .column(Column::exact(100.0))
                    .column(Column::exact(60.0))
                    .header(20.0, |mut header| {
                        header.col(|ui| components::table_header_plain(ui, theme, "Nation"));
                        header.col(|ui| components::table_header_plain(ui, theme, "Status"));
                        header.col(|ui| components::table_header_plain(ui, theme, "Trend"));
                        header.col(|_ui| {});
                    })
                    .body(|mut body| {
                        for n in &others {
                            body.row(26.0, |mut row| {
                                row.col(|ui| {
                                    ui.label(egui::RichText::new(n.name).font(sans_medium(12.0)).color(theme.text));
                                });
                                row.col(|ui| {
                                    let (fg, bg) = alliance_colors(theme, n.alliance);
                                    components::badge(ui, n.alliance.label(), fg, bg);
                                });
                                row.col(|ui| {
                                    let (color, label) = match n.trend {
                                        crate::data::Trend::Improving => (theme.good, "▲ Improving"),
                                        crate::data::Trend::Worsening => (theme.bad, "▼ Worsening"),
                                        crate::data::Trend::Stable => (theme.text_dim, "– Stable"),
                                    };
                                    ui.label(egui::RichText::new(label).font(sans(11.5)).color(color));
                                });
                                row.col(|ui| {
                                    if ui
                                        .add(
                                            egui::Label::new(egui::RichText::new("View →").font(sans(11.0)).color(theme.accent))
                                                .sense(egui::Sense::click()),
                                        )
                                        .clicked()
                                    {
                                        jump_to = Some(n.id);
                                    }
                                });
                            });
                        }
                    });
            });
        });
        ui.add_space(12.0);
        ui.allocate_ui(egui::vec2(right_w, 0.0), |ui| {
            ui.set_width(right_w);
            components::solid_frame(theme).show(ui, |ui| {
                components::section_title(ui, theme, "Active Treaties");
                ui.add_space(6.0);
                for t in &state.world.treaties {
                    ui.label(egui::RichText::new(t.name).font(sans_medium(12.5)).color(theme.text));
                    ui.label(egui::RichText::new(t.parties).font(sans(11.0)).color(theme.text_dim));
                    ui.label(egui::RichText::new(t.status).font(sans(11.0)).color(theme.text_faint));
                    ui.add_space(8.0);
                    ui.separator();
                    ui.add_space(4.0);
                }
            });
        });
    });

    if let Some(id) = jump_to {
        state.select_country(id);
    }
}
