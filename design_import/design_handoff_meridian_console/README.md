# Handoff: Meridian — Grand Strategy Geopolitical Simulation Console

## Overview
A data-dense, professional dashboard UI for a grand-strategy geopolitical simulation
game. The player governs a nation (President/PM/King/Dictator/Junta/Parliament) and
monitors economy, politics, society, military, and diplomacy in a persistent,
continuously-running simulation (not turn-based). The core design principle is
**causality before raw numbers**: every metric is paired with a one-line "why" — what
changed and what caused it — rather than showing bare figures.

## About the Design Files
The bundled file, `Meridian Console.dc.html`, is a **design reference prototype**,
built in an internal HTML component format (it loads `support.js`, a runtime shim
specific to the design tool it was authored in — that file is included only so the
prototype remains inspectable/runnable for reference, not because it belongs in your
stack). **Do not import or ship these files directly.** Your task is to recreate this
design in the target codebase's existing environment (React, Vue, SwiftUI, native,
etc.) using its established component patterns, state management, and libraries. If no
environment exists yet, choose the framework best suited to a long-lived,
real-time-updating desktop dashboard (React + a real drag/resize-panel library is a
reasonable default — see "Dockable panels" note below).

## Fidelity
**High-fidelity.** Colors, typography, spacing, component chrome, and interaction
behavior are final-intent and should be recreated precisely. All copy in the mock
(country names, minister names, event text) is placeholder narrative content for a
fictional world — keep the structure and tone, swap in real game data.

---

## Design Tokens

### Typography
- UI text: **IBM Plex Sans**, weights 400/500/600/700 (Google Fonts).
- All numeric / data values: **IBM Plex Mono**, weights 400/500/600 — used for stat
  values, table numerics, timestamps, sparkline labels, the ⌘K shortcut chip, and the
  top-bar wordmark. This mono/sans split is a deliberate "terminal" cue; keep it.
- Scale in use: 26px/600 (dashboard stat value) · 24px/600 (tab stat value) · 22px/600
  (election countdown) · 20px/600 (approval trend headline) · 16px/700 (country name)
  · 15px/600 (wordmark, letter-spacing 1.5px) · 13–13.5px (body/tabs) · 12–12.5px
  (table cells, panel titles) · 11–11.5px (labels, deltas, why-lines, minister/sector
  note text) · 10–10.5px (uppercase table headers, timestamps, faint captions) · 9–9.5px
  (severity/status badges).
- Table headers: 10.5px, uppercase, letter-spacing ~0.4px, `text-faint` color.
- "Why" causal lines always: 11px, `text-dim` color, prefixed with a "↳" glyph, sitting
  below a `border-top` divider inside the same card.

### Color — Dark theme (default)
```
--bg-0:  #07090c   page background (outside cards)
--bg-1:  #0e1116   app shell / map surface background
--bg-2:  #151920   solid panel/card background
--bg-2-t: rgba(21,25,32,0.90)   translucent panel bg (glass overlay cards over the map)
--bg-3:  #1b212a   elevated / header strip background
--border:      #262d3a
--border-soft: #1b212b   (dividers inside a card — subtler than --border)
--grid-line:   rgba(255,255,255,0.035)   faint map graticule lines
--text:      #e7eaef
--text-dim:  #8d95a5
--text-faint:#5a6170
--shadow: 0 16px 40px rgba(0,0,0,0.5)
--scrim:  rgba(4,5,7,0.6)   modal/palette backdrop
```

Accent (single sharp interactive color — 3 curated options, pick one, don't mix):
```
amber (default): #d99a45   dim/tint: rgba(217,154,69,0.16)   ring: rgba(217,154,69,0.45)
blue:             #4c86e0   dim/tint: rgba(76,134,224,0.16)
teal:             #3f9e8f   dim/tint: rgba(63,158,143,0.16)
```

Status colors — intentionally desaturated, never pure red/green:
```
--good: #6fa787   bg: rgba(111,167,135,0.14)   (positive trend / ally / clear status)
--warn: #c99a4a   bg: rgba(201,154,74,0.14)    (caution / watch status)
--bad:  #c1685c   bg: rgba(193,104,92,0.14)    (negative trend / rival / scandal)
--info: #6f90b8   bg: rgba(111,144,184,0.14)   (neutral informational)
```

### Color — Light theme
```
--bg-0:#eef0f3  --bg-1:#f5f6f8  --bg-2:#ffffff  --bg-2-t:rgba(255,255,255,0.92)  --bg-3:#eef0f4
--border:#dde1e8  --border-soft:#e7eaf0  --grid-line:rgba(20,25,35,0.05)
--text:#191d24  --text-dim:#5b6272  --text-faint:#8991a1
--shadow: 0 10px 28px rgba(20,25,35,0.12)   --scrim: rgba(20,24,32,0.35)
accent amber:#b9791f   blue:#2f5fc4   teal:#2c7d70
--good:#3f8462 (bg 0.10)   --warn:#9c7a2e (bg 0.10)   --bad:#a8483a (bg 0.10)   --info:#3f5f8a (bg 0.10)
```
Implementation note: in the prototype every token above is a CSS custom property set
once at the app root and consumed via `var(--token)` everywhere else — this is how
theme switching and accent switching restyle the whole app instantly. Recreate this as
CSS variables (or your framework's theme-token equivalent) rather than hardcoding hex
values per component.

### Spacing & shape
- Card/panel radius: 10px. Toolbar/tab-bar container radius: 9px. Buttons/chips: 5–8px.
  Severity/status badges: 4px. Dots/swatches: fully round.
- Page gutter: 12px (grid/flex `gap` and outer `padding`). Card inner padding: 13–14px
  (stat tiles), 12–14px (list/table panels), 14–16px (page headers).
- Borders are always 1px solid `--border` (card outlines) or `--border-soft` (internal
  dividers between rows/sections) — never a heavier weight, never a colored left-accent
  stripe.

---

## Screens / Views

### 1. Top Bar (persistent, 52px height, all screens)
Fixed height flex row, `background: var(--bg-2)`, bottom border `--border`.
Left→right: wordmark lockup ("MERIDIAN" in mono 15px/600, letter-spacing 1.5px, plus a
small "COUNCIL" caption divided by a 1px vertical rule) → home-nation swatch (16×16
accent-colored rounded square) + nation name + sim clock ("14 MAR 2043 · 09:41 UTC",
mono, faint) → a centered, click-to-open search bar standing in for the command
palette trigger (420px wide, rounded 7px, shows a "⌘K" chip on the right) → theme
toggle button (moon/sun glyph) → notification bell with unread-count badge (opens an
inbox popover, 340px wide, anchored top-right, listing recent alerts with colored
severity dots).

### 2. Global Dashboard (home)
Map-first layout. The world map is a **full-bleed base layer** filling the entire
content area (12px inset from the viewport edges, rounded 10px, 1px border). Everything
else floats **as glass overlay panels on top of the map**, not as grid siblings:
- **Overlay cluster** (top-left by default, shifts side with the feed dock): a slim
  toolbar strip (drag-handle glyph, "World Map" label, 7-item mode switcher as
  pill-tabs, zoom −/percentage/+ controls, an eye-glyph "hide overlays" toggle) stacked
  above a row of **4 stat tiles** (Economy/GDP growth, Approval, Unrest Index, Military
  Readiness). Both use `background: var(--bg-2-t)` + `backdrop-filter: blur(10px)` so
  the map is faintly visible through them — this is the one deliberate "glass" texture
  in the system; every other surface in the app is a flat, opaque card.
- **Event & Notification Feed**: a floating panel, full height minus 24px top/bottom
  margins, 360px wide, docked right by default. Header has a drag handle, title, a
  dock-side toggle (⇋, swaps it to the left edge), and a close (×) button. Below that,
  8 filter chips (All/Economy/Trade/Politics/Diplomacy/Military/Unrest/Climate,
  single-select). Body is a scrollable list of event rows (see "Event feed row"
  component below).
- Closing the feed panel replaces it with a small dashed "Feed closed · Restore" chip
  at the same dock position. Hiding the overlay cluster replaces it with a "👁 Show
  overlays" chip pinned top-left. This closable/restorable pattern should be
  implemented as a real, working affordance in the target app, not just visual — same
  for the map's zoom and the panels' native resize (the prototype uses CSS `resize` on
  scrollable panel bodies as a stand-in for real drag-resize; a production build should
  use a proper dockable-panel library, see note below).

### 3. Country Detail
Reached by clicking any node on the map. Structure, top to bottom, all within a 12px-
padded flex column:
- **Header card**: back-to-dashboard button, country color swatch, name (16px/700) +
  region/government-type/capital caption, an alliance status badge (HOME NATION / ALLY
  / NEUTRAL / RIVAL, colored via the status tokens), then — right-aligned, each
  separated by a thin left border — 4 headline stats (GDP, Approval, Unrest, Readiness).
- **Tab bar**: pill-style segmented control, 5 tabs — Economy, Politics, Society,
  Military, Diplomacy. Active tab is solid `--accent` with `--bg-0` text; inactive tabs
  are text-only.
- **Tab body** (scrollable):
  - *Economy*: 4 stat tiles (GDP, Inflation, Trade Balance, Unemployment) → a 2-column
    row: a sortable **Economic Sectors** data table (8 rows: sector name, % of GDP,
    growth %, employment, causal note; click "Share" or "Growth" column header to
    sort, active column shows a ▲/▼ arrow) alongside a **Budget Allocation** card (6
    categories as label + thin progress bar + %, with a delta and one causal note for
    the biggest mover) and a **Tax Policy** card (3 live range sliders — Corporate/
    Income/VAT — that recompute and display a "Projected annual revenue" line as they
    move, and fire a toast confirming the recalculation).
  - *Politics*: an **Approval Trend** card (14-point line chart, current value called
    out large, 3 marker dots on notable inflection points, one causal note) beside a
    **Next Election** card (countdown + a segmented projected-seat-share bar). Below
    that, a sortable **Cabinet Ministers** table (7 rows: name, portfolio, party,
    approval — sortable — and a status badge: CLEAR/WATCH/SCANDAL) beside a **Coalition
    Composition** list (party dot, name, stance, seat count) and a **Corruption &
    Scandal Flags** list (severity badge + title + one-line description).
  - *Society*: 3 stat tiles (Population Growth, Unrest Index, HDI Composite) + an
    **Unrest Hotspots** list (location, causal note, colored unrest-value readout).
  - *Military*: 3 stat tiles (Overall Readiness, Active Personnel, Modernization) + a
    **Branch Readiness** table (4 rows: branch, personnel, readiness %, note) beside an
    **Active Deployments** list (name, location + personnel, status).
  - *Diplomacy*: a **Bilateral Relations** table (11 rows — every other nation: status
    badge, trend arrow +label, a "View →" link that navigates to that country) beside
    an **Active Treaties** list (name, parties, status).

## Reusable Component System
Applied consistently everywhere rather than styled bespoke per screen:
1. **Panel/card chrome** — solid variant (`--bg-2`, 1px `--border`, 10px radius, for
   country-detail sections) and glass-overlay variant (`--bg-2-t` + `backdrop-filter:
   blur(10px)`, same radius/border, used only for the 3 dashboard-overlay surfaces:
   toolbar, stat tiles, feed panel). Panels with a header show a drag-handle glyph
   (⋮⋮), title, and — where applicable — collapse/dock/close controls.
2. **Stat tile** — label (uppercase, faint) + right-aligned delta badge (mono, colored
   good/warn/bad by direction) on one row; big mono value + inline SVG sparkline on the
   next; a `border-top`-divided causal "↳ why" line at the bottom. This is the single
   most-repeated component — economy, dashboard, society, and military stats all use
   the exact same shape.
3. **Data table** — dense rows (12px body / 11px secondary text), uppercase 10.5px
   column headers, `border-top: 1px solid var(--border-soft)` between rows (no zebra
   fill), numeric columns right-aligned in mono, sortable columns show a ▲/▼ next to
   the header label when active.
4. **Causal-annotation line** — a "↳" glyph + `--text-dim` sentence, always the last
   element in a stat tile or table-adjacent card, always describing *why* the number
   moved, never restating the number itself.
5. **Severity/status badge** — small pill, bold 9.5px label (CRIT/WARN/NOTE/INFO or
   CLEAR/WATCH/SCANDAL or ALLY/NEUTRAL/RIVAL/HOME NATION), background = status color at
   ~12–14% alpha, text = full-strength status color.
6. **Event feed row** — severity badge + title (12px/500) + causal "↳ why" line +
   timestamp/category caption, with a chevron that expands a **numbered causal chain**
   (each step is a short past-tense sentence with a date) in an indented sub-card, plus
   an optional "Jump to {country} →" link when the event is tied to a nation.
7. **Command palette** — `⌘/Ctrl K` opens a centered modal (scrim backdrop, 560px card,
   autofocused input, `ESC` chip). Results are grouped by tag (NATION/SCREEN/PANEL/
   ACTION), filtered by substring match against the query, keyboard-navigable
   (↑/↓/Enter), and mouse-hoverable/clickable.
8. **Toast** — left-accented (3px, status color) card, top-right stack, slide-in
   animation, auto-dismiss after 6s or manual ×.

## Interactions & Behavior
- **⌘/Ctrl+K** anywhere → opens command palette; typing filters; ↑/↓ move selection;
  Enter activates; Esc or backdrop click closes.
- **Map**: hover a node → tooltip card (name, region, gov type, GDP/growth/approval/
  readiness) anchored to the node. Click a node → navigate to Country Detail, Economy
  tab, closing any open tooltip/palette.
- **Map mode switcher** (Political/Economic/Military/Trade/Resources/Climate/Election)
  recolors every node per a per-mode rule (see source `modeFill()` for exact
  thresholds — e.g. economic mode: green if growth ≥3%, blue-grey 0–3%, red-brown <0%)
  and swaps the bottom-left legend. **Trade mode** additionally draws thin accent lines
  from the home nation to each ally. **Election mode** pulses a ring around nations
  with an election within ~12 months.
- **Zoom +/−** scale the map layer 0.7×–2.0× in 0.15 steps around its center; a live
  percentage readout sits between the buttons.
- **Feed filter chips** are single-select; **event rows** toggle their own causal-chain
  expansion independently (multiple can be open at once).
- **Feed panel**: × closes it (replaced by a restore chip in the same dock slot); ⇋
  flips its dock side between left/right (the dashboard's overlay cluster
  automatically re-flows to occupy the opposite side).
- **Overlay cluster** (toolbar + stat tiles): eye-glyph toggle hides it entirely,
  replaced by a "Show overlays" chip top-left, so the map can be viewed unobstructed.
- **Sortable table columns** (Sectors: Share/Growth; Ministers: Approval) toggle
  ascending/descending on repeat clicks; only one column sorts at a time.
- **Tax sliders** are native range inputs (0–55); moving one live-recomputes a derived
  "projected annual revenue" figure and fires a toast — this demonstrates
  policy-change → recalculated-consequence causality inline.
- **Theme toggle** swaps every CSS variable dark↔light instantly, app-wide.
- **Toasts**: one fires automatically ~1.8s after load (demoing the alert system); tax
  changes also fire one; each auto-dismisses after 6s or can be dismissed manually.
- **Notification bell**: badge shows unread count; opening the popover clears it (or
  wire "mark all read" explicitly, matching the prototype).
- Every screen must keep working from a 4K multi-monitor layout down to a single
  1920×1080 window — the prototype uses `minmax()` grid tracks and `auto-fit` stat-tile
  grids specifically so tiles reflow/wrap rather than overflow at narrow widths;
  preserve that behavior rather than fixed pixel grids.

## State Management
Top-level state to reproduce (shape is illustrative — adapt to your state library):
- `theme`: 'dark' | 'light' — default 'dark'
- `accentTheme`: 'amber' | 'blue' | 'teal' — default 'amber' (global visual flag)
- `screen`: 'dashboard' | 'country'; `selectedCountryId`; `countryTab`: one of
  economy/politics/society/military/diplomacy
- `mapMode`: one of political/economic/military/trade/resources/climate/election;
  `mapZoom`: number 0.7–2.0; `hoveredCountryId`
- `feedFilter`: one of all/economy/trade/politics/diplomacy/military/unrest/climate;
  `expandedEventIds`: set/map of event id → expanded bool
- `paletteOpen`, `paletteQuery`, `paletteIndex` (keyboard-selected result)
- `notifOpen`, `notifUnreadCount`
- `toasts`: array of `{id, severity, title, message}`, each with its own dismiss timer
- `panelClosed.feed` (bool), `overlayHidden` (bool), `feedDockSide`: 'left' | 'right'
- `sectorSort` / `ministerSort`: `{key, direction}`
- `taxRates`: `{corporate, income, vat}` (0–55 each) → derived `projectedRevenue`

### Data model (replace with live simulation data)
- **Nations** (12 in the mock, incl. home nation "Velmoria"): id, name, code, region,
  government type, capital, population, GDP, GDP growth %, approval %, unrest index,
  military readiness %, climate-risk index, primary resource type, alliance status
  (home/ally/neutral/rival), days-to-next-election (nullable), diplomatic trend
  (improving/stable/worsening), and a map x/y position (percentage coordinates on an
  abstract 0–100 canvas — **not** real-world lat/long; the mock uses a fictional world).
- **Events/timeline**: id, severity (info/notice/warning/critical), category (economy/
  trade/politics/diplomacy/military/unrest/climate), timestamp label, title, one-line
  "why", related-nation id, and an ordered **causal chain** (array of dated,
  past-tense sentences — this is the field that makes the simulation legible; every
  event should carry one).
- Supporting tables per nation: cabinet ministers, economic sectors, budget categories,
  coalition parties, corruption flags, society/military stat blocks, military branches,
  active deployments, treaties, bilateral relations.

## Assets
No image/icon assets — every glyph in the mock (☾/☀ theme toggle, ⌕ search, ⌘ shortcut
chip, ⋮⋮ drag handle, ⇋ dock-swap, 👁 hide-overlay, ▲/▼ sort arrows, ↳ causal-line
marker) is a plain Unicode character, purely as prototyping shorthand. **Replace these
with a proper icon set** (e.g. Lucide, Feather, or your design system's existing icons)
in production — do not ship Unicode glyphs as final icons.
Fonts: **IBM Plex Sans** + **IBM Plex Mono**, loaded from Google Fonts in the mock;
self-host or use your existing font pipeline in production.

## Dockable panels — implementation note
The brief called for dockable/resizable/closable panels. The prototype demonstrates the
*behavior* (close → restore chip, dock-side swap, native CSS `resize` on scrollable
panel bodies) at a fidelity appropriate for a design mock, but does not implement true
drag-to-rearrange/drag-to-resize physics. For production, use a real panel-docking
library appropriate to your framework (e.g. a golden-layout-style or react-mosaic-style
system for React) so users can freely rearrange, split, and resize panels rather than
being limited to the two fixed dock slots (map overlay cluster + feed) shown here.

## Files
- `Meridian Console.dc.html` — the full design reference (single file, all screens,
  both themes, all interactions wired with mock data).
- `support.js` — runtime shim required only to run the reference file in a browser for
  inspection; not part of the design and not needed in your codebase.
- `screenshots/` — reference captures: `dashboard-dark.png` (global dashboard, dark),
  `country-economy-dark.png` / `country-economy-light.png` (Country Detail, Economy
  tab, both themes), `country-politics-light.png` (Country Detail, Politics tab,
  light). These are for quick visual reference only — the HTML file is the source of
  truth for exact spacing/behavior.
