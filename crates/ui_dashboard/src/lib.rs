pub mod basemap;
pub mod components;
pub mod data;
pub mod economy;
pub mod geo;
pub mod palette;
pub mod screens;
pub mod state;
pub mod theme;

use egui::Context;
use state::{AppState, Screen};
use theme::Theme;

/// Top-level entry point, called once per frame by the `app` binary crate.
/// Call `theme::install_fonts(ctx)` once at startup before the first call to this.
pub fn ui(ctx: &Context, state: &mut AppState) {
    let theme = Theme::new(state.theme, state.accent);
    apply_visuals(ctx, &theme);

    // Fire the one demo toast ~1.8s after launch, matching the reference prototype.
    if !state.startup_toast_fired && state.startup_at.elapsed().as_secs_f32() > 1.8 {
        state.startup_toast_fired = true;
        state.push_toast(
            data::Severity::Warning,
            "Steel export volume -12%",
            "New tariff regime detected — see Trade feed for details",
        );
    }

    handle_global_shortcuts(ctx, state);

    let dt = ctx.input(|i| i.stable_dt);
    state.advance_sim(dt);
    if state.sim_speed != state::SimSpeed::Paused {
        ctx.request_repaint();
    }

    // egui 0.35 removed the Context-based `Panel::show(ctx, ...)` entry point in favor of a
    // Ui-based one uniformly; the outermost Ui for a frame is built explicitly like this
    // (mirrors bevy_egui's own `ui.rs` example for 0.35+).
    let mut viewport_ui = egui::Ui::new(
        ctx.clone(),
        egui::Id::new("viewport"),
        egui::UiBuilder::new()
            .layer_id(egui::LayerId::background())
            .max_rect(ctx.viewport_rect()),
    );

    egui::Panel::top("top_bar")
        .exact_size(52.0)
        .frame(egui::Frame::new().fill(theme.bg_2).inner_margin(egui::Margin::symmetric(14, 0)))
        .show(&mut viewport_ui, |ui| {
            screens::topbar::show(ui, &theme, state);
        });

    // No inner margin on the Dashboard: the map should fill the screen edge-to-edge like a
    // game world, with panels floating on top of it — not sit inset inside a card like a
    // dashboard widget. The Country Detail screen (scrolling content, not a map) keeps a
    // margin since it reads as a document, not a world.
    let margin = match state.screen {
        Screen::Dashboard => egui::Margin::ZERO,
        Screen::Country => egui::Margin::same(12),
    };
    egui::CentralPanel::default()
        .frame(egui::Frame::new().fill(theme.bg_0).inner_margin(margin))
        .show(&mut viewport_ui, |ui| match state.screen {
            Screen::Dashboard => screens::dashboard::show(ui, &theme, state),
            Screen::Country => screens::country::show(ui, &theme, state),
        });

    if state.palette_open {
        palette::show(ctx, &theme, state);
    }

    screens::toasts::show(ctx, &theme, state);

    if !state.toasts.is_empty() {
        ctx.request_repaint_after(std::time::Duration::from_millis(200));
    }
}

fn handle_global_shortcuts(ctx: &Context, state: &mut AppState) {
    let toggle = ctx.input(|i| {
        i.modifiers.command && i.key_pressed(egui::Key::K)
    });
    if toggle {
        state.palette_open = !state.palette_open;
        state.palette_query.clear();
        state.palette_index = 0;
    }
    if state.palette_open && ctx.input(|i| i.key_pressed(egui::Key::Escape)) {
        state.palette_open = false;
    }
}

fn apply_visuals(ctx: &Context, theme: &Theme) {
    let mut visuals = match state_mode(theme) {
        theme::ThemeMode::Dark => egui::Visuals::dark(),
        theme::ThemeMode::Light => egui::Visuals::light(),
    };
    visuals.override_text_color = Some(theme.text);
    visuals.panel_fill = theme.bg_0;
    visuals.window_fill = theme.bg_2;
    visuals.window_stroke = egui::Stroke::new(1.0, theme.border);
    visuals.widgets.noninteractive.bg_fill = theme.bg_2;
    visuals.widgets.inactive.bg_fill = theme.bg_3;
    visuals.widgets.hovered.bg_fill = theme.bg_3;
    visuals.widgets.active.bg_fill = theme.accent_dim;
    visuals.selection.bg_fill = theme.accent_dim;
    visuals.selection.stroke = egui::Stroke::new(1.0, theme.accent);
    visuals.hyperlink_color = theme.accent;
    ctx.set_visuals(visuals);
}

// Small helper so apply_visuals doesn't need to store ThemeMode redundantly; Theme itself
// doesn't retain which mode it was built from, so we re-derive a dark/light base from
// perceived background luminance (bg_0 is near-black in dark, near-white in light).
fn state_mode(theme: &Theme) -> theme::ThemeMode {
    let lum = theme.bg_0.r() as u32 + theme.bg_0.g() as u32 + theme.bg_0.b() as u32;
    if lum < 384 {
        theme::ThemeMode::Dark
    } else {
        theme::ThemeMode::Light
    }
}
