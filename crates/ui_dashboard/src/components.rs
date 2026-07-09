use egui::{Color32, CornerRadius, FontId, Margin, Stroke, Ui};

use crate::data::Severity;
use crate::state::SortDir;
use crate::theme::{mono, mono_medium, sans, sans_medium, sans_semibold, Theme};

pub const RADIUS: u8 = 10;
pub const RADIUS_SM: u8 = 5;

/// Solid card chrome: opaque background, used for Country Detail sections. A real drop
/// shadow (rather than a flat 1px border alone) is what separates a "floating HUD panel"
/// from a flat dashboard card sitting in a page layout.
pub fn solid_frame(theme: &Theme) -> egui::Frame {
    egui::Frame::new()
        .fill(theme.bg_2)
        .stroke(Stroke::new(1.2, theme.border))
        .corner_radius(CornerRadius::same(RADIUS))
        .inner_margin(Margin::same(13))
        .shadow(egui::Shadow {
            offset: [0, 10],
            blur: 24,
            spread: 0,
            color: Color32::from_black_alpha(110),
        })
}

/// Glass-overlay chrome: translucent background over the map. Real backdrop blur isn't
/// exposed by egui's painter, so we approximate with a semi-transparent fill — visually
/// close enough for a design prototype; a production build can add a blur post-pass.
pub fn glass_frame(theme: &Theme) -> egui::Frame {
    egui::Frame::new()
        .fill(theme.bg_2_t)
        .stroke(Stroke::new(1.2, theme.border))
        .corner_radius(CornerRadius::same(RADIUS))
        .inner_margin(Margin::same(12))
        .shadow(egui::Shadow {
            offset: [0, 8],
            blur: 20,
            spread: 0,
            color: Color32::from_black_alpha(130),
        })
}

pub fn severity_colors(theme: &Theme, sev: Severity) -> (Color32, Color32) {
    match sev {
        Severity::Critical => (theme.bad, theme.bad_bg),
        Severity::Warning => (theme.warn, theme.warn_bg),
        Severity::Notice => (theme.info, theme.info_bg),
        Severity::Info => (theme.text_dim, theme.bg_3),
    }
}

/// Small pill badge: bold uppercase label on a tinted background.
pub fn badge(ui: &mut Ui, label: &str, fg: Color32, bg: Color32) {
    egui::Frame::new()
        .fill(bg)
        .corner_radius(CornerRadius::same(4))
        .inner_margin(Margin::symmetric(7, 3))
        .show(ui, |ui| {
            ui.label(egui::RichText::new(label).font(sans_semibold(9.5)).color(fg));
        });
}

pub fn severity_badge(ui: &mut Ui, theme: &Theme, sev: Severity) {
    let (fg, bg) = severity_colors(theme, sev);
    badge(ui, sev.label(), fg, bg);
}

/// The "↳ why" causal-annotation line: always describes *why* a number moved, never the
/// number itself. This is the single load-bearing UX idea in the whole design.
pub fn causal_line(ui: &mut Ui, theme: &Theme, why: &str) {
    ui.add_space(4.0);
    let sep = egui::Separator::default().spacing(0.0);
    ui.add(sep);
    ui.add_space(4.0);
    // A single wrapping Label, not `horizontal_wrapped` with the icon as a separate
    // widget: `horizontal_wrapped` sizes each child to its own minimum width first
    // (for a wrapping Label that's the width of its single longest word), so the text
    // was being squeezed to one word per line instead of wrapping as a paragraph.
    ui.label(egui::RichText::new(format!("↳ {why}")).font(sans(11.0)).color(theme.text_dim));
}

pub struct StatTile<'a> {
    pub label: &'a str,
    pub value: &'a str,
    pub delta_text: &'a str,
    pub delta: f32,
    pub spark: &'a [f32],
    pub why: &'a str,
}

/// The single most-repeated component: label+delta row, big mono value + sparkline,
/// causal "why" line. Used identically on the dashboard, Economy, Society and Military tabs.
pub fn stat_tile(ui: &mut Ui, theme: &Theme, width: f32, tile: &StatTile<'_>) {
    solid_frame(theme).show(ui, |ui| {
        ui.set_width(width);
        ui.horizontal(|ui| {
            ui.label(
                egui::RichText::new(tile.label.to_uppercase())
                    .font(sans_medium(10.5))
                    .color(theme.text_faint),
            );
            ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                ui.label(
                    egui::RichText::new(tile.delta_text)
                        .font(mono(11.0))
                        .color(theme.delta_color(tile.delta)),
                );
            });
        });
        ui.add_space(6.0);
        ui.horizontal(|ui| {
            ui.label(egui::RichText::new(tile.value).font(mono_semi_big()).color(theme.text));
            ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                sparkline(ui, theme, egui::vec2(62.0, 18.0), tile.spark, theme.delta_color(tile.delta));
            });
        });
        causal_line(ui, theme, tile.why);
    });
}

fn mono_semi_big() -> FontId {
    // dashboard stat value scale (26px/600) — see theme::mono_semibold, sized here explicitly
    // because stat_tile is the one place this exact size is used.
    egui::FontId::new(24.0, egui::FontFamily::Name("mono_semibold".into()))
}

/// Small inline SVG-equivalent sparkline drawn straight into the painter.
pub fn sparkline(ui: &mut Ui, _theme: &Theme, size: egui::Vec2, values: &[f32], color: Color32) {
    let (rect, _resp) = ui.allocate_exact_size(size, egui::Sense::hover());
    if values.len() < 2 {
        return;
    }
    let min = values.iter().cloned().fold(f32::INFINITY, f32::min);
    let max = values.iter().cloned().fold(f32::NEG_INFINITY, f32::max);
    let span = (max - min).max(0.001);
    let n = values.len() as f32;
    let points: Vec<egui::Pos2> = values
        .iter()
        .enumerate()
        .map(|(i, v)| {
            let x = rect.left() + (i as f32 / (n - 1.0)) * rect.width();
            let t = (v - min) / span;
            let y = rect.bottom() - t * rect.height();
            egui::pos2(x, y)
        })
        .collect();
    ui.painter().add(egui::Shape::line(points, Stroke::new(1.6, color)));
}

/// Sortable data-table column header: label + arrow when active, click toggles direction.
/// Caller owns the sort state and applies the returned click.
pub fn sortable_header(ui: &mut Ui, theme: &Theme, label: &str, active: bool, dir: SortDir) -> bool {
    let text = if active {
        format!("{label} {}", dir.arrow())
    } else {
        label.to_string()
    };
    let resp = ui.add(
        egui::Label::new(
            egui::RichText::new(text.to_uppercase())
                .font(sans_medium(10.5))
                .color(if active { theme.text } else { theme.text_faint }),
        )
        .sense(egui::Sense::click()),
    );
    resp.clicked()
}

pub fn table_header_plain(ui: &mut Ui, theme: &Theme, label: &str) {
    ui.label(
        egui::RichText::new(label.to_uppercase())
            .font(sans_medium(10.5))
            .color(theme.text_faint),
    );
}

pub fn section_title(ui: &mut Ui, theme: &Theme, title: &str) {
    ui.label(egui::RichText::new(title).font(sans_semibold(12.0)).color(theme.text));
}

/// Thin labeled progress bar used by the Budget Allocation card.
pub fn labeled_bar(ui: &mut Ui, theme: &Theme, label: &str, pct: f32, delta: f32) {
    ui.horizontal(|ui| {
        ui.label(egui::RichText::new(label).font(sans(13.0)).color(theme.text));
        ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
            let sign = if delta > 0.0 { "+" } else { "" };
            ui.label(
                egui::RichText::new(format!("{sign}{delta:.1}pp"))
                    .font(mono(11.0))
                    .color(theme.delta_color(delta)),
            );
            ui.label(egui::RichText::new(format!("{pct}%")).font(mono_medium(13.0)).color(theme.text));
        });
    });
    ui.add_space(3.0);
    let (rect, _) = ui.allocate_exact_size(egui::vec2(ui.available_width(), 6.0), egui::Sense::hover());
    let painter = ui.painter();
    painter.rect_filled(rect, 3.0, theme.bg_3);
    let mut filled = rect;
    filled.set_width(rect.width() * (pct / 100.0).clamp(0.0, 1.0));
    painter.rect_filled(filled, 3.0, theme.accent);
    ui.add_space(8.0);
}

pub struct Toast {
    pub fg: Color32,
    pub bg: Color32,
}

pub fn toast_card(ui: &mut Ui, theme: &Theme, sev: Severity, title: &str, message: &str) {
    let (fg, _bg) = severity_colors(theme, sev);
    egui::Frame::new()
        .fill(theme.bg_2)
        .stroke(Stroke::new(1.0, theme.border))
        .corner_radius(CornerRadius::same(RADIUS_SM as u8))
        .inner_margin(Margin::symmetric(12, 10))
        .show(ui, |ui| {
            ui.set_width(300.0);
            // left accent stripe
            ui.horizontal(|ui| {
                let (rect, _) = ui.allocate_exact_size(egui::vec2(3.0, 34.0), egui::Sense::hover());
                ui.painter().rect_filled(rect, 1.5, fg);
                ui.add_space(4.0);
                ui.vertical(|ui| {
                    ui.label(egui::RichText::new(title).font(sans_semibold(12.5)).color(theme.text));
                    ui.label(egui::RichText::new(message).font(sans(11.0)).color(theme.text_dim));
                });
            });
        });
}
