use egui::Color32;

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum ThemeMode {
    Dark,
    Light,
}

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum Accent {
    Amber,
    Blue,
    Teal,
}

impl Accent {
    pub fn all() -> [Accent; 3] {
        [Accent::Amber, Accent::Blue, Accent::Teal]
    }

    pub fn label(&self) -> &'static str {
        match self {
            Accent::Amber => "Amber",
            Accent::Blue => "Blue",
            Accent::Teal => "Teal",
        }
    }
}

fn hex(h: u32) -> Color32 {
    Color32::from_rgb(((h >> 16) & 0xFF) as u8, ((h >> 8) & 0xFF) as u8, (h & 0xFF) as u8)
}

fn hex_a(h: u32, a: u8) -> Color32 {
    Color32::from_rgba_unmultiplied(((h >> 16) & 0xFF) as u8, ((h >> 8) & 0xFF) as u8, (h & 0xFF) as u8, a)
}

/// Design tokens lifted 1:1 from the Meridian Console design handoff
/// (docs/architecture design_import/design_handoff_meridian_console/README.md).
#[derive(Clone, Copy, Debug)]
pub struct Theme {
    pub bg_0: Color32,
    pub bg_1: Color32,
    pub bg_2: Color32,
    pub bg_2_t: Color32,
    pub bg_3: Color32,
    pub border: Color32,
    pub border_soft: Color32,
    pub grid_line: Color32,
    pub text: Color32,
    pub text_dim: Color32,
    pub text_faint: Color32,
    pub scrim: Color32,

    pub accent: Color32,
    pub accent_dim: Color32,
    pub accent_ring: Color32,

    pub good: Color32,
    pub good_bg: Color32,
    pub warn: Color32,
    pub warn_bg: Color32,
    pub bad: Color32,
    pub bad_bg: Color32,
    pub info: Color32,
    pub info_bg: Color32,
}

impl Theme {
    pub fn new(mode: ThemeMode, accent: Accent) -> Self {
        match mode {
            ThemeMode::Dark => Self {
                bg_0: hex(0x07090c),
                bg_1: hex(0x0e1116),
                bg_2: hex(0x151920),
                bg_2_t: hex_a(0x151920, 230),
                bg_3: hex(0x1b212a),
                border: hex(0x262d3a),
                border_soft: hex(0x1b212b),
                grid_line: hex_a(0xffffff, 9),
                text: hex(0xe7eaef),
                text_dim: hex(0x8d95a5),
                text_faint: hex(0x5a6170),
                scrim: hex_a(0x040507, 153),
                accent: match accent {
                    Accent::Amber => hex(0xd99a45),
                    Accent::Blue => hex(0x4c86e0),
                    Accent::Teal => hex(0x3f9e8f),
                },
                accent_dim: match accent {
                    Accent::Amber => hex_a(0xd99a45, 41),
                    Accent::Blue => hex_a(0x4c86e0, 41),
                    Accent::Teal => hex_a(0x3f9e8f, 41),
                },
                accent_ring: match accent {
                    Accent::Amber => hex_a(0xd99a45, 115),
                    Accent::Blue => hex_a(0x4c86e0, 115),
                    Accent::Teal => hex_a(0x3f9e8f, 115),
                },
                good: hex(0x6fa787),
                good_bg: hex_a(0x6fa787, 36),
                warn: hex(0xc99a4a),
                warn_bg: hex_a(0xc99a4a, 36),
                bad: hex(0xc1685c),
                bad_bg: hex_a(0xc1685c, 36),
                info: hex(0x6f90b8),
                info_bg: hex_a(0x6f90b8, 36),
            },
            ThemeMode::Light => Self {
                bg_0: hex(0xeef0f3),
                bg_1: hex(0xf5f6f8),
                bg_2: hex(0xffffff),
                bg_2_t: hex_a(0xffffff, 235),
                bg_3: hex(0xeef0f4),
                border: hex(0xdde1e8),
                border_soft: hex(0xe7eaf0),
                grid_line: hex_a(0x141923, 13),
                text: hex(0x191d24),
                text_dim: hex(0x5b6272),
                text_faint: hex(0x8991a1),
                scrim: hex_a(0x141820, 89),
                accent: match accent {
                    Accent::Amber => hex(0xb9791f),
                    Accent::Blue => hex(0x2f5fc4),
                    Accent::Teal => hex(0x2c7d70),
                },
                accent_dim: match accent {
                    Accent::Amber => hex_a(0xb9791f, 26),
                    Accent::Blue => hex_a(0x2f5fc4, 26),
                    Accent::Teal => hex_a(0x2c7d70, 26),
                },
                accent_ring: match accent {
                    Accent::Amber => hex_a(0xb9791f, 77),
                    Accent::Blue => hex_a(0x2f5fc4, 77),
                    Accent::Teal => hex_a(0x2c7d70, 77),
                },
                good: hex(0x3f8462),
                good_bg: hex_a(0x3f8462, 26),
                warn: hex(0x9c7a2e),
                warn_bg: hex_a(0x9c7a2e, 26),
                bad: hex(0xa8483a),
                bad_bg: hex_a(0xa8483a, 26),
                info: hex(0x3f5f8a),
                info_bg: hex_a(0x3f5f8a, 26),
            },
        }
    }

    /// Status color + its tinted background for a "direction": positive/neutral/negative.
    pub fn delta_color(&self, delta: f32) -> Color32 {
        if delta > 0.0 {
            self.good
        } else if delta < 0.0 {
            self.bad
        } else {
            self.text_dim
        }
    }
}

/// Registers IBM Plex Sans (UI text) and IBM Plex Mono (all numeric/data values) with egui,
/// and wires up a text style scale matching the handoff's type scale.
pub fn install_fonts(ctx: &egui::Context) {
    let mut fonts = egui::FontDefinitions::default();

    macro_rules! add_font {
        ($fonts:expr, $name:expr, $bytes:expr) => {
            $fonts.font_data.insert(
                $name.to_owned(),
                std::sync::Arc::new(egui::FontData::from_static($bytes)),
            );
        };
    }

    add_font!(fonts, "plex_sans_regular", include_bytes!("../../../assets/fonts/IBMPlexSans-Regular.ttf"));
    add_font!(fonts, "plex_sans_medium", include_bytes!("../../../assets/fonts/IBMPlexSans-Medium.ttf"));
    add_font!(fonts, "plex_sans_semibold", include_bytes!("../../../assets/fonts/IBMPlexSans-SemiBold.ttf"));
    add_font!(fonts, "plex_sans_bold", include_bytes!("../../../assets/fonts/IBMPlexSans-Bold.ttf"));
    add_font!(fonts, "plex_mono_regular", include_bytes!("../../../assets/fonts/IBMPlexMono-Regular.ttf"));
    add_font!(fonts, "plex_mono_medium", include_bytes!("../../../assets/fonts/IBMPlexMono-Medium.ttf"));
    add_font!(fonts, "plex_mono_semibold", include_bytes!("../../../assets/fonts/IBMPlexMono-SemiBold.ttf"));

    fonts
        .families
        .entry(egui::FontFamily::Proportional)
        .or_default()
        .insert(0, "plex_sans_regular".to_owned());
    fonts
        .families
        .entry(egui::FontFamily::Monospace)
        .or_default()
        .insert(0, "plex_mono_regular".to_owned());

    fonts.families.insert(
        egui::FontFamily::Name("sans_medium".into()),
        vec!["plex_sans_medium".to_owned(), "plex_sans_regular".to_owned()],
    );
    fonts.families.insert(
        egui::FontFamily::Name("sans_semibold".into()),
        vec!["plex_sans_semibold".to_owned(), "plex_sans_regular".to_owned()],
    );
    fonts.families.insert(
        egui::FontFamily::Name("sans_bold".into()),
        vec!["plex_sans_bold".to_owned(), "plex_sans_regular".to_owned()],
    );
    fonts.families.insert(
        egui::FontFamily::Name("mono_medium".into()),
        vec!["plex_mono_medium".to_owned(), "plex_mono_regular".to_owned()],
    );
    fonts.families.insert(
        egui::FontFamily::Name("mono_semibold".into()),
        vec!["plex_mono_semibold".to_owned(), "plex_mono_regular".to_owned()],
    );

    ctx.set_fonts(fonts);
}

pub fn sans(size: f32) -> egui::FontId {
    egui::FontId::new(size, egui::FontFamily::Proportional)
}
pub fn sans_medium(size: f32) -> egui::FontId {
    egui::FontId::new(size, egui::FontFamily::Name("sans_medium".into()))
}
pub fn sans_semibold(size: f32) -> egui::FontId {
    egui::FontId::new(size, egui::FontFamily::Name("sans_semibold".into()))
}
pub fn sans_bold(size: f32) -> egui::FontId {
    egui::FontId::new(size, egui::FontFamily::Name("sans_bold".into()))
}
pub fn mono(size: f32) -> egui::FontId {
    egui::FontId::new(size, egui::FontFamily::Monospace)
}
pub fn mono_medium(size: f32) -> egui::FontId {
    egui::FontId::new(size, egui::FontFamily::Name("mono_medium".into()))
}
pub fn mono_semibold(size: f32) -> egui::FontId {
    egui::FontId::new(size, egui::FontFamily::Name("mono_semibold".into()))
}
