---
tags: [architecture, ui]
---

# UI System

`Assets/Scripts/UI/` — the entire HUD, built **entirely in C#** via UI Toolkit
(`UIDocument`/`VisualElement`). No `.uxml`/`.uss` assets, no UI Builder, no built-in
Button/Slider controls (those need a theme stylesheet unavailable without the Editor UI Builder)
— every button and slider is a hand-rolled `VisualElement` with manual pointer-event handling.

## GameUIRoot.cs (~1,700 lines) — the whole game screen

Structural regions, in file order:

- **Awake()** — loads `GamePanelSettings` from Resources (required for text to render at all in a
  build — see [[Code Architecture]] gotchas), builds every region once, schedules `Refresh()`
  every 100ms.
- **Top bar** — MERIDIAN title, day counter, speed buttons, [[Map Modes and Coloring|map-mode]]
  toggle, SAVE button, player badge (country/approval/election countdown), 4 live stat tiles
  (growth/unemployment/inflation/treasury) for whatever country is selected.
- **Ministry bar** — 8 colored nameplate buttons, one per [[Ministries|NationCategory]]; hover
  reveals a topic dropdown.
- **Side panel** — the main content panel, rebuilt only when selection/category/topic/war-state
  actually changes (`builtFor*` dirty-tracking guards avoid rebuilding on every 100ms tick).
  Per-category draw methods: `DrawEconomy`+`DrawTaxSection`, `DrawBudget`, `DrawTrade`,
  `DrawPolitics`, `DrawMilitary`+`DrawOwnWars`+`DrawForeignMilitary`, `DrawDiplomacy`+
  `DrawBilateralDiplomacy`+`DrawDiplomacyOverview`, `DrawSociety`, `DrawTechnology`. Includes a
  private `Sparkline` class — a custom `generateVisualContent`/`Painter2D` line chart reading a
  [[History and World Feed|HistorySeries]] directly.
- **Toasts** — top-left fading notification stack (max 4), fed by `CheckForNewEvents()` (diffs
  every country's `EconomyState.LastWhy` for threshold-crossing narration) and by draining
  [[History and World Feed|WorldFeed]].
- **Start screen** — searchable country list (`BeginGame`) plus a CONTINUE button gated on
  [[Save Load]] validity.
- **Decision-event modal** — shown while `EventSystem.Pending != null` ([[Decision Events]]); has
  deliberately no dismiss button — governing means deciding.
- **Game-over screen** — `PlayerState.LastResultMessage`, term count, PLAY AGAIN (resets to the
  start screen and repaints the map back to neutral colors — see [[Map Modes and Coloring]]).
- **Refresh()** — the central 100ms tick: handles dev-only `MERIDIAN_AUTOSTART`/
  `MERIDIAN_LOADSAVE` env vars, toggles screens by `PlayerState.State`, updates live stats/
  sliders/sparklines in place, and only calls the expensive `RebuildSidePanel()` when something
  structural actually changed.

## FeaturePanel.cs
A separate, self-contained panel for **clicked map features** (city/road/border crossing/water
crossing) — independent of the country ministry panel, sharing the same `UIDocument` root.
Left-anchored, rebuild-on-change, mirrors `GameUIRoot`'s own pattern. See [[Camera and Input]]
for how a click resolves to one of these four feature types.

## Shared small files
- `GameTheme.cs` — the "UN Security Council chamber" institutional palette: deep UN-blue
  backgrounds, gold ceremonial accents, muted flag-inspired category colors. Helpers: `Shade`
  (darken for button 3D rim), `Muted` (blend toward panel background for resting-state buttons),
  `Delta` (Positive/Negative color by sign).
- `NationCategory.cs` — see [[Ministries]].
- `UIState.cs` — `ActiveCategory`, `ActiveTopic` (null = category overview), `PanelOpen` (the
  side panel is opt-in — selecting a country alone does not open it, only choosing a ministry
  does).

## Consumers / dependencies
Reads from everything: `MapRenderer` (World/Economy/National/Wars/Diplomacy/CountryNames/
CurrentMode), `MapInteraction` (Selected/SimDay/daysPerSecond), and every [[Simulation Overview|Sim/ system]] directly for display and player actions (tax sliders, diplomacy buttons,
war declarations, event choices).
