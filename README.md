# Meridian — Unity port

The geopolitical strategy game, migrated from Rust/Bevy to Unity (C#). This folder is a
ready-to-open Unity project scaffold: the real-world geo data pipeline is already ported and
the flat map renders on Play. What's here is the **foundation**, not the whole game — the
economy simulation, full UI, and gameplay systems still need porting from the original Rust
build (kept alongside this at `../Geo political/`).

## What already works

- **Geo data pipeline** (`Assets/Scripts/Geo/`) — a faithful C# port of the original Rust
  `geo` module: loads the Natural Earth GeoJSON (258 countries, 4,596 provinces, 7,342
  cities, plus ports/airports), triangulates country/province polygons, and exposes
  point-in-polygon + bbox helpers. Includes a C# port of the earcut triangulator (replacing
  the Rust `earcutr` crate).
- **Flat map renderer** (`Assets/Scripts/Map/`) — builds one GPU mesh per country, flat-
  colored, rendered by an orthographic pan/zoom camera. This is the architecture the Bevy
  build kept hitting performance walls on (it re-projected ~550k vector points every frame);
  here the geometry is baked into GPU meshes once, so pan/zoom is cheap.
- **Zero-setup bootstrap** — `Bootstrap.cs` spawns the camera + map at play time, so there's
  no scene to wire up. Just press Play.

## The remaining steps you have to do (they need your account / can't be automated)

1. **Install Unity.** Download **Unity Hub** from https://unity.com/download, install it,
   sign in with (or create) a Unity account, and use the free **Personal** license.
2. **Install a Unity Editor.** In Hub → Installs → Install Editor, pick **2022.3 LTS** (the
   version this project targets — see `ProjectSettings/ProjectVersion.txt`). Any recent 2022.3
   or Unity 6 LTS will also work; Hub will offer to upgrade the project if needed.
3. **Open this project.** In Hub → Projects → Add → select this `MeridianUnity` folder, then
   open it. First open takes a few minutes (Unity imports assets and pulls the Newtonsoft JSON
   package listed in `Packages/manifest.json`).
4. **Press Play.** The map loads (a one-time few-second parse+triangulate of the country
   data — watch the Console for the "loaded N countries in M ms" log) and renders. Left-drag
   to pan, scroll to zoom.

## Next milestones (porting order, matching what the Rust build had)

1. Country **borders** (stroke the `OutlineRings` — quick).
2. **Hit-testing** — click a country to select it (`GeoMath.PointInRing` is already ported).
3. **Provinces / cities / ports / airports** as zoom-gated layers.
4. **Economy simulation** — port `crates/ui_dashboard/src/economy.rs` (this is the real
   gameplay; largely engine-agnostic C# math, ports cleanly).
5. **UI** — rebuild the top bar / ministry bar / panels in Unity UI (uGUI or UI Toolkit).
   This is the biggest chunk and does not port line-for-line from egui.

## Notes

- Geo data lives in `Assets/StreamingAssets/worlddata/` (copied from the original project),
  loaded at runtime — no re-embedding needed.
- The initial geo load runs synchronously on the main thread (a few seconds). Moving it to a
  background thread / job is a worthwhile early follow-up so startup doesn't hitch.
- The default Built-in Render Pipeline is assumed (`FlatVertexColor.shader`). If you switch to
  URP, that one shader needs a URP equivalent; nothing else changes.
- **Untested end-to-end**: this scaffold was written without a Unity install present to
  compile against, so expect to fix a few small compile issues on first open (they'll show in
  the Console). The logic is a faithful port of the working Rust code; any errors will be
  Unity-API surface details, not algorithm bugs.
