//! Satellite map mode's basemap: NASA's "Blue Marble: Next Generation" whole-earth
//! topography/bathymetry composite (public domain — a work of the U.S. government), a
//! single equirectangular JPEG. Decoded lazily the first time Satellite mode is opened,
//! since decoding a 5400×2700 JPEG has a real one-time cost not worth paying at every
//! startup for players who never use this mode.

const BASEMAP_JPEG: &[u8] = include_bytes!("../../../assets/basemap/world_topo_bathy.jpg");

pub fn decode() -> egui::ColorImage {
    let img = image::load_from_memory(BASEMAP_JPEG).expect("decode bundled basemap JPEG").to_rgba8();
    let (w, h) = img.dimensions();
    egui::ColorImage::from_rgba_unmultiplied([w as usize, h as usize], img.as_raw())
}

/// Lazily decodes+uploads the basemap texture the first time it's needed, returning a
/// cheap-to-clone handle thereafter.
pub fn ensure_loaded(ctx: &egui::Context, slot: &mut Option<egui::TextureHandle>) -> egui::TextureHandle {
    slot.get_or_insert_with(|| ctx.load_texture("satellite_basemap", decode(), egui::TextureOptions::LINEAR)).clone()
}
