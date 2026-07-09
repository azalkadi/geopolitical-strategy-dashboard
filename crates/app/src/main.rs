use bevy::prelude::*;
use bevy_egui::{EguiContexts, EguiPlugin, EguiPrimaryContextPass};
use ui_dashboard::{state::AppState, theme};

fn main() {
    App::new()
        .add_plugins(DefaultPlugins.set(WindowPlugin {
            primary_window: Some(Window {
                title: "Meridian Console".to_string(),
                resolution: (1440u32, 900u32).into(),
                ..default()
            }),
            ..default()
        }))
        .add_plugins(EguiPlugin::default())
        .insert_resource(AppStateRes(AppState::default()))
        .add_systems(Startup, setup)
        .add_systems(EguiPrimaryContextPass, ui_system)
        .run();
}

#[derive(Resource)]
struct AppStateRes(AppState);

fn setup(mut commands: Commands) {
    commands.spawn(Camera2d);
}

fn ui_system(
    mut contexts: EguiContexts,
    mut state: ResMut<AppStateRes>,
    mut fonts_installed: Local<bool>,
) -> Result {
    let ctx = contexts.ctx_mut()?;
    if !*fonts_installed {
        theme::install_fonts(ctx);
        *fonts_installed = true;
        // `set_fonts` only takes effect starting the next `begin_pass`, so drawing
        // anything that uses the new custom font families in this same frame would
        // panic ("FontFamily::Name(...) is not bound to any fonts"). Skip this frame.
        return Ok(());
    }
    ui_dashboard::ui(ctx, &mut state.0);
    Ok(())
}
