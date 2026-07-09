# Changelog

All notable work on this project, in chronological order. This is a pre-release prototype —
entries are grouped by work session, not version numbers.

## [Unreleased]

### Added
- **Bottom ministry/category bar** — Victoria3/HOI4-style bottom-center bar (Economy, Budget,
  Trade, Politics, Diplomacy, Military, Society, Technology) for the selected nation.
- **Expanded economy model** — separate Income/Corporate/VAT/Tariff tax rates plus a central
  bank interest rate, each independently player-adjustable.
- **Province and city click/hover stats** — real name, admin country, and (for cities)
  population and capital status, surfaced once zoom LOD reveals them.
- Full-bleed map presentation and game-HUD-style panel chrome, replacing the inset
  dashboard-card look around the map.

### Changed
- Visual style moved further from a data-dashboard aesthetic toward a game-HUD aesthetic.

---

## Session: Real-world map, Terrain mode, first economy mechanic

### Added
- Real-world geography (`geo.rs`): 258 countries, 4,596 provinces (253 countries with
  subdivisions), 7,342 cities — Natural Earth 10m data (public domain, no attribution
  required), triangulated via `earcutr` and cached at load (~2.5s startup).
- Real pan (drag) and zoom (scroll + buttons, 1x–60x) on the map, with point-in-polygon
  hover/click hit-testing against real country borders.
- Zoom-gated LOD: provinces appear past ~4x zoom, cities appear past ~5x zoom with a
  population threshold that relaxes as you zoom in further.
- Real-country info panel on click: name, continent/subregion, ISO codes, population,
  capital — later extended with live simulated economic data (see below).
- **Terrain map mode** (new default): blue ocean, natural per-country land tones instead of
  flat per-continent colors, "engraved" double-stroke borders (soft dark halo + crisp line).
- **First real simulation mechanic**: a continuously-ticking economy for every one of the 258
  countries (not just the one on screen), seeded from real population/GDP estimates —
  GDP, growth rate, unemployment, inflation, and treasury, with a player-adjustable tax rate
  and real time controls (pause/1x/5x/20x) in the top bar. Economic map mode colors by live
  growth rate instead of a static snapshot.

### Changed
- Replaced the old abstract/fictional world map (12 invented nations on a percentage grid)
  with the real-geography map for the dashboard's primary view.
- Fixed a real bug: `components::causal_line` wrapped a single long text label in
  `horizontal_wrapped` (meant for flowing multiple short discrete widgets), which made egui
  size the label to its narrowest word and stack it one word per line, ballooning every stat
  tile to ~800px tall and pushing the map off-screen. Switched to a single wrapping `Label`.

### Known gaps (flagged, not fixed)
- The event feed, dashboard stat tiles, and the original 5-tab "Velmoria" country dashboard
  are still fictional demo content, disconnected from the real map. Making the full
  simulation (politics/military/diplomacy) real-country-driven is future work, not something
  faked to look connected.
- Map projection is simple equirectangular, not Mercator — geographically valid but visibly
  stretches areas near the poles.

---

## Session: Meridian Console UI implementation

### Added
- Cargo workspace (`ui_dashboard` + `app` crates) on Bevy 0.19 + `bevy_egui` 0.41 + egui 0.35.
- IBM Plex Sans/Mono fonts embedded and registered with egui (Apache-2.0, no attribution
  required beyond the license file).
- Full design-token theme system (dark/light, 3 accent options) matching the "Meridian
  Console" design handoff: solid/glass panel chrome, stat tiles with sparklines, causal
  "↳ why" annotation lines, severity/status badges, data tables, toasts, command palette.
- Global Dashboard screen: world map, mode switcher, zoom controls, floating stat-tile
  overlay cluster, dockable/closable event & notification feed with expandable causal chains.
- Country Detail screen: all 5 tabs (Economy with tax sliders, Politics with an approval
  trend chart, Society, Military, Diplomacy) against a fictional 12-nation mock world
  ("Velmoria" and neighbors) — built first as a UI/UX reference implementation before any
  real data existed.
- Working `Ctrl/Cmd+K` command palette, keyboard navigation, dark/light theme toggle.

### Fixed
- egui 0.35 removed the `Context`-based `Panel::show(ctx, ...)` entry point in favor of a
  `Ui`-based one; adjusted the app's top-level frame construction accordingly.
- `set_fonts()` only takes effect starting the next frame's `begin_pass` — installing fonts
  and drawing in the same frame panicked (`FontFamily::Name("sans_medium") is not bound to
  any fonts`). Fixed by skipping the draw on the font-install frame.
- `TextEdit::frame()` takes a `Frame`, not a `bool`, in this egui version (unlike `Button`).

---

## Session: Project architecture and research

### Added
- `docs/architecture/phase-1-architecture.md` — full software architecture for a Bevy-based
  grand-strategy geopolitical simulator: layered design (headless deterministic Simulation
  Core → AI → Presentation), domain-plugin event-bus architecture, cohort-based population
  model, save/mod/AI-symmetry design, and a revised 15-phase roadmap.
- `docs/architecture/gps5-research.md` — competitive research on Geo-Political Simulator 5,
  mapping its specific failure modes (unrecoverable states, unreliable saves, AI legibility,
  UI complexity, yearly-edition technical debt) to concrete acceptance criteria for this
  project's own roadmap.
