# Phase 1 — Software Architecture
**Project:** (working title) *Sovereign* — a continuous, emergent grand-strategy geopolitical simulator
**Engine:** Bevy (Rust) — confirmed 2026-07-08
**Status:** Draft for review. Do not proceed to Phase 2 until this is approved or amended.

---

## 1. How to read this document

This is the architecture, not the implementation. There is no game code in this phase — only the skeleton every later phase must build inside. Where the original brief contains a design decision I think is weak or underspecified, I've called it out explicitly under **"Challenge"** rather than silently working around it. You need to rule on those before Phase 2, because they change folder structure, data schemas, and the roadmap order.

---

## 2. Engine reality check: what Bevy gives you and what it doesn't

Bevy is the right choice for this project's stated philosophy ("simulation first, data-oriented, modular, multi-threaded") — but it is a **game engine's rendering/app framework wrapped around an ECS**, not a simulation framework, not a UI toolkit, not a networking stack, and not a geospatial engine. For a project this size, "which engine" really means "which five or six libraries around bevy_ecs." Being explicit about the gaps now avoids discovering them in Phase 9.

| Area | What Bevy gives you | What's missing / what we add |
|---|---|---|
| **Core simulation** | `bevy_ecs`: archetype-based ECS, usable **standalone** without the renderer/App/windowing layers. This is the single biggest reason Bevy fits: we can run the entire world simulation headless, as a plain Rust library, with no GPU, no window, no render loop. | Nothing — this is the load-bearing decision. See §4. |
| **Scheduling / parallelism** | `Schedule` + system sets auto-parallelize systems with non-conflicting data access; conflicting systems are ordered automatically. | Ambiguous ordering (two systems that *could* run in either order and both matter for determinism, e.g. via `Commands` or events) is **not** resolved automatically — Bevy's ambiguity detector flags it, but we must explicitly `.before()/.after()` or merge them. This is a determinism requirement we own, not one Bevy provides. |
| **UI** | `bevy_ui` — flexbox-like layout, fine for HUDs and simple menus. | It has **no docking system**, weak support for dense data tables, virtualization, or the "intelligence dashboard" aesthetic you specified. Building that natively would be a multi-month sub-project. **Decision: use `bevy_egui`** (immediate-mode GUI embedded in Bevy) for all dashboard/panel/chart UI, with `egui_dock` for dockable panels. Bevy's own renderer is reserved for the map and any 3D/2D world view. This is a hard architectural split, not a style choice — see §6. |
| **Map rendering** | Nothing geospatial at all. Bevy has no concept of projections, vector tiles, or borders. | We build a custom map-rendering crate: geographic polygon data (provinces/countries) is preprocessed offline into GPU-friendly meshes (triangulated polygons + simplified LOD levels per zoom), rendered as a custom Bevy render layer, with map *modes* (political/terrain/economic/etc.) as data overlays, not separate meshes. This is a real sub-engineering-project in itself — budget accordingly in the roadmap (§10). |
| **Save/serialization** | `bevy_reflect` + `DynamicScene` can (de)serialize component data generically. | Not sufficient alone for versioned, deterministic, moddable, diff-friendly saves at this scale. We need a hand-designed save schema with explicit versioning/migration — reflection is a tool inside that, not the system itself. Detailed in a later phase; outline in §8. |
| **Networking** | None built in. | Out of scope until Phase 14 by your own roadmap. Noted now only because §5's determinism strategy is written so it doesn't foreclose multiplayer later (server-authoritative sync, not lockstep — see Challenge 3). |
| **Scripting / modding** | None built in. Bevy's asset system supports hot-reloading of data files, which we do want. | Mods are pure **data** (RON/JSON/TOML + asset packs) for anything statistical (countries, leaders, economic constants, event definitions). For mod *logic* (custom AI behaviors, custom event triggers) we integrate an embedded scripting layer (`mlua`/Lua, or `rhai`) in Phase 13 — not now. Data-driven-only modding ships much earlier than scriptable modding. |
| **Determinism / RNG** | None built in; `std` RNG and float ops are not guaranteed reproducible. | We own this fully: seeded PRNG (`rand_chacha`) per domain, explicit system ordering, careful f64 usage. Detailed in §5. |
| **Platform targets** | Windows/macOS/Linux native compilation is a first-class, well-trodden Bevy path. Steam is routine (thousands of shipped Bevy/Rust titles use it). Epic Games Store is less trodden but is "just" another distribution target once Steam packaging works — no engine blocker. | Nothing extra needed at Phase 1. |

**Bottom line:** Bevy is not "the engine that renders our game," it's "the ECS + scheduler that runs our simulation, with a renderer bolted on for the map, and egui bolted on for the UI." That reframing drives the whole architecture below.

---

## 3. Challenges to the brief (read before approving)

These are places where the brief's language, taken literally, would produce a worse or unbuildable system. I'm flagging them rather than quietly deciding for you.

### Challenge 1 — "Millions of simulated entities" as literal individual people
Simulating 8 billion humans as ECS entities is not feasible on any hardware, and isn't what makes GPS5-style games feel real anyway. **Recommendation:** population is represented as **demographic cohorts** — one component bundle per (province × age band × income class × ethnicity × religion) bucket, evolved statistically (birth/death/migration/employment as rates applied to cohort size, with noise). This is standard practice in every real economic/demographic model (World Bank, UN population models work this way) and it's what "real GDP, real population" actually requires to be *accurate*, not just numerous.

Individual named **entities** are reserved for people who matter causally: politicians, generals, central bank governors, business tycoons, terrorist/rebel leaders, the player's cabinet. That's thousands, not billions — a real ECS workload, not a fantasy one. This satisfies "millions of simulated entities" in the sense of *simulated population* (cohorts aggregate to real headcounts) while keeping the entity count tractable.

### Challenge 2 — "No fake turns," "world evolves continuously"
True continuous-time simulation (differential equations integrated in real time) is not how any credible grand strategy game works, and isn't necessary for realism. **Recommendation:** a fixed simulation tick (proposed: 1 tick = 1 in-game day, with hourly sub-resolution for military/breaking-news events), decoupled from render framerate via Bevy's `FixedUpdate` schedule, with player-controlled speed (paused / 1x / 2x / 3x / max) — the same model EU4/HOI4/GPS5 actually use under the hood. "No fake turns" is satisfied because there's no player-facing turn structure and every nation is simulated every tick, not just the player's — that's the real distinction worth preserving, and I've kept it as a hard requirement in §5.

### Challenge 3 — Deterministic saves + Ironman + Replay + optional multiplayer, together
Bit-exact determinism across different CPUs/compilers (true lockstep multiplayer) is a genuinely expensive engineering commitment — it usually means fixed-point math everywhere, which is invasive and slows early development a lot. But single-machine determinism (same binary, same save, same result — enough for Ironman and Replay) is much cheaper: seeded RNG + explicit system ordering + consistent f64 usage is sufficient.

**Recommendation:** commit now to single-machine determinism (cheap, needed regardless for Ironman/Replay). Defer the lockstep-vs-server-authoritative decision for multiplayer to Phase 14, and default to **server-authoritative state sync** (the server runs the one true simulation, clients render a replicated view) rather than lockstep — it's dramatically simpler, doesn't require fixed-point math, and matches how most modern strategy-adjacent multiplayer actually ships. This keeps Phase 14 optional in practice, not just in name.

### Challenge 4 — Simulation fidelity for non-focused countries
Nothing in the brief should be read as license to cheapen AI nations the player isn't looking at — "every country has an autonomous AI government" making "believable long-term decisions" is GPS5's actual selling point over Paradox games (which do heavily abstract distant AI). **Recommendation:** all 200+ countries run the *same* simulation systems at the *same* tick rate, always — no LOD on simulation correctness. Where we do apply LOD is presentation: we don't compute or render city-level detail, camera-facing UI widgets, or narrative flavor text for a country the player isn't looking at; we still fully simulate its economy, politics, military, and population. This is a performance/detail distinction, not a simulation-quality one, and it's the thing to protect if a future performance crunch tempts corner-cutting.

### Challenge 5 — Open data licensing
"Use open public datasets" and "never rely on proprietary databases" is right, but the datasets you'll actually want (Natural Earth for boundaries, World Bank/IMF for economic baselines, REST Countries / UN for demographics) carry different licenses — Natural Earth is public domain, OpenStreetMap boundary data is ODbL (share-alike + attribution if redistributed), World Bank data is CC-BY 4.0. This needs a tracked decision per dataset (an attribution/license manifest shipped with the game), not a blanket assumption. I'll build that manifest in Phase 2 (World Data) — flagging it now so it's budgeted, not discovered late.

---

## 4. High-level architecture

Four layers, strictly one-directional dependency (lower layers never import upward):

```
┌─────────────────────────────────────────────────────────────┐
│  Layer 4 — Presentation                                      │
│  bevy render app · map renderer · bevy_egui dashboard UI     │
│  reads simulation state, sends player Intents, never          │
│  mutates simulation state directly                            │
└───────────────────────────▲───────────────────────────────────┘
                             │ read-only state + event stream
┌───────────────────────────┴───────────────────────────────────┐
│  Layer 3 — AI                                                  │
│  per-country decision agents, utility AI over Layer 2 state,   │
│  emits the same Intents a human player would                   │
└───────────────────────────▲───────────────────────────────────┘
                             │ Intents in, World events out
┌───────────────────────────┴───────────────────────────────────┐
│  Layer 2 — Simulation Core (headless, deterministic)           │
│  bevy_ecs world, one Bevy Plugin per domain (economy, politics, │
│  society, military, diplomacy, intel, tech, climate, resources, │
│  internal security), FixedUpdate tick, event bus between them   │
└───────────────────────────▲───────────────────────────────────┘
                             │ loads/validates into
┌───────────────────────────┴───────────────────────────────────┐
│  Layer 1 — Data                                                │
│  static reference data (RON/TOML) · mod overlay resolution ·   │
│  save/load · schema versioning                                 │
└─────────────────────────────────────────────────────────────┘
```

**Why this shape:** Layer 2 has no idea a renderer or a UI exists — it's a plain library crate that can be unit-tested, fuzzed, and run in CI with zero GPU. This is what makes "deterministic saves," "replay mode," and (eventually) a dedicated multiplayer server possible without a rewrite: they're all just "run Layer 2 headless with different drivers." Layer 3 (AI) is peer to the player, not special-cased inside Layer 2 — both produce the same `Intent` events, which is what "every country has an autonomous AI government" architecturally requires: the AI must be *usable* by any country, including one a human player hands off from mid-game.

---

## 5. Simulation Core — the part that matters most

### 5.1 Entity model

- `Country` — one entity per sovereign state. Components: identity, treasury, government structure, ideology stance, diplomatic relation table (references to other Country entities), aggregate indices (GDP, unemployment %, unrest, etc. — cached rollups, recomputed from provinces each tick, never hand-edited).
- `Province` (real administrative regions, e.g. US states, French départements, etc.) — components: geography, resource deposits, infrastructure level, garrison, local unrest, owning `Country` reference.
- `PopulationCohort` — many per province (age band × income class × ethnicity × religion). This is the actual population simulation substrate (Challenge 1).
- `City` — subset of a province for major urban centers only (population above a threshold), with its own infrastructure/crime/housing components layered on top of its parent province's cohorts.
- `Person` — named individual entities only: politicians, cabinet members, generals, central bankers, opposition leaders, notable oligarchs/terrorists. Components: personality (ambition, risk tolerance, corruption, ideology, negotiation style, health, stress — as specified in the brief), relationships graph, career state.
- `MilitaryUnit` — army/navy/air/special-forces formations, owned by Country, stationed at Province.

### 5.2 Domain modules as Bevy Plugins

Each brief section (Economy, Politics, Society, Diplomacy, Military, Intelligence, Technology, Climate, Resources, Internal Security) becomes one crate, one Bevy `Plugin`, with:
- its own components (owns its slice of state — Economy owns treasury/inflation/trade components, Politics owns approval/party/coalition components, etc.)
- its own systems, registered into `FixedUpdate`
- **no direct function calls into other domain crates.** Cross-domain influence (tax increase → unrest → crime → coup, per your worked example) happens exclusively through a **typed event bus** (Bevy `Events<T>`): Economy emits `TaxPolicyChanged`, Society's unrest system reads it and emits `UnrestIncreased`, Politics reads that and emits `ProtestStarted`, Military reads sustained unrest + emits `LoyaltyShifted`, etc.

This is the concrete mechanism that satisfies "no duplicated logic" and "no scripted events" simultaneously: the causal chain in your brief isn't a scripted sequence, it's an emergent path through independently-owned reactive systems that only agree on event *shapes*, not on each other's internals. It also means a modder or a later engineer can add a new domain (say, a Media/Information module) without touching existing crates — they just subscribe to and emit events.

### 5.3 Time & determinism

- Tick = 1 in-game day, run in `FixedUpdate`, decoupled from render framerate. Sub-day resolution (hourly) reserved for fast-moving events (military engagements, breaking news, market flash crashes) via a secondary finer schedule that only wakes relevant systems.
- Every domain crate gets its own seeded `ChaCha8Rng` stream (seeded from the save's master seed + domain id), never `thread_rng()`. Same save + same seed ⇒ same trajectory.
- System ordering within and across domains is explicit (`.chain()` / `.before()/.after()` system sets per domain, domains themselves ordered by a documented dependency list) so Bevy's parallel scheduler can still run non-conflicting systems concurrently without introducing order-dependent nondeterminism. We turn on Bevy's system-ambiguity lints in CI so an unordered-but-conflicting pair fails the build, not a player's save.
- No `HashMap` iteration in any path that affects simulation outcome — `IndexMap`/`BTreeMap` or explicit sort keys only, in Layer 2.

### 5.4 Player and AI symmetry

Neither the player nor an AI country mutates simulation state directly. Both submit `Intent` events (`RaiseTax { country, amount }`, `DeclareWar { ... }`, `SignTreaty { ... }`) into the same queue; Layer 2 validates and applies them identically regardless of source. This is what makes "hand a country to/from AI control" (e.g. if a player's country falls, or they switch nations) a non-event architecturally — there is no special player code path to remove.

---

## 6. Presentation layer

- **World/map rendering:** a dedicated `map_render` crate using Bevy's renderer directly (wgpu). Boundary polygons preprocessed offline (build-time tool, not runtime) from source geodata into simplified multi-LOD triangle meshes keyed by zoom level; map "modes" (political/terrain/economic/military/election/climate/resource/etc., per your list) are implemented as swappable fragment-shader colorings/overlays driven by Layer 2 data snapshots, not as separate geometry — this is what makes 10+ map modes tractable instead of a 10x asset problem.
- **Dashboard UI:** `bevy_egui` + `egui_dock` for every panel, chart, table, timeline, and the command palette. Charts via `egui_plot`. This is a deliberate split from the map renderer (§2) — egui is immediate-mode and excellent for dense, dockable, data-heavy tool UI; Bevy's own UI is not there yet.
- **Read-only contract:** Presentation subscribes to a snapshot/event-stream API exposed by Layer 2 and never touches ECS components directly. This boundary is what keeps "run headless for CI/dedicated-server/replay" true — if UI code reached into the ECS directly this would quietly break the first time someone takes a shortcut under deadline pressure, so it's enforced at the crate-visibility level (Layer 2 exposes a query/event API crate; raw component types aren't `pub` outside it).

---

## 7. Repository / workspace structure

Cargo workspace, one crate per architectural box above:

```
geo-sim/
├── Cargo.toml                    # workspace root
├── crates/
│   ├── sim_core/                 # ECS world, tick scheduler, event bus, Intent system
│   ├── sim_economy/               # Plugin: GDP, inflation, trade, taxation, banking, markets
│   ├── sim_society/                # Plugin: population cohorts, migration, health, education, crime
│   ├── sim_politics/               # Plugin: parties, elections, approval, cabinet, corruption
│   ├── sim_military/                # Plugin: forces, logistics, production, war resolution
│   ├── sim_diplomacy/               # Plugin: treaties, alliances, IOs, sanctions, UN voting
│   ├── sim_intelligence/            # Plugin: espionage, cyber, info warfare
│   ├── sim_technology/              # Plugin: research tree, tech effects on other domains
│   ├── sim_climate/                 # Plugin: weather, disasters, resource/food feedback
│   ├── sim_resources/               # Plugin: extraction, reserves, resource markets
│   ├── sim_security/                 # Plugin: police, prisons, organized crime, martial law
│   ├── ai_core/                      # per-country decision agents, personality-driven utility AI
│   ├── data_schema/                   # static reference data types + RON/TOML loaders
│   ├── data_worldgen/                  # build-time: geodata → province/country baseline dataset
│   ├── save_system/                     # save/load, versioning, migration, ironman, replay/timeline
│   ├── mod_loader/                       # mod manifest resolution, data overlay merging
│   ├── map_render/                        # geodata → GPU meshes, map modes, camera
│   ├── ui_dashboard/                       # bevy_egui panels, charts, command palette
│   └── app/                                 # binary crate: wires Layers 1–4 together, windowing
├── data/
│   ├── worlddata/                    # source open datasets + license manifest (Challenge 5)
│   └── mods/                          # local dev mods, loaded by mod_loader
├── tools/
│   └── worldgen_pipeline/              # offline geodata → data_worldgen preprocessing CLI
├── tests/
│   └── integration/                    # headless multi-tick determinism & scenario tests
└── docs/
    └── architecture/                    # this document and future phase docs
```

`sim_core` and every `sim_*` crate depend only on `data_schema` and each other's **event types** (put in a thin shared `sim_events` crate to avoid `sim_economy` depending on all of `sim_politics` just to see its events) — never on `ai_core`, `map_render`, `ui_dashboard`, or `app`. `app` is the only crate allowed to depend on everything.

---

## 8. Save system (outline — full schema in a later phase)

- Two save layers: **World State** (full ECS snapshot: all countries/provinces/cohorts/persons — the expensive part) and **Event Log** (the Intent/event stream since the last World State snapshot). Autosave writes an event-log delta; periodic full snapshots bound replay-reconstruction time.
- **Replay/Timeline** falls out of the Event Log for free: replaying = re-running Layer 2 headless from a snapshot through the logged Intents, deterministically (§5.3).
- **Ironman** = autosave-only, single save slot, no manual reload — a UI/save-policy constraint on top of the same mechanism, not a separate system.
- Schema is explicitly versioned from day one (every serialized struct carries a schema version; migrations are written, not assumed) — this is non-negotiable for a "runs forever" game that will outlive its own save format many times over.

---

## 9. Modding architecture (outline)

- Tier 1 (ships early): pure data mods — countries, leaders, economic constants, starting conditions, event *definitions* (not event *logic*) as RON/TOML/JSON, hot-reloaded via Bevy's asset system, merged over base data via `mod_loader`'s override rules (last-loaded-wins per key, with an explicit load-order manifest — not a silent merge).
- Tier 2 (Phase 13): embedded scripting (Lua via `mlua`) for custom AI behaviors and custom event *logic*, sandboxed against a defined host API — not raw ECS access.
- No recompilation required for either tier, per the brief.

---

## 10. Revised roadmap

Your original 15-phase roadmap is directionally right; two changes given the above:

1. **Phase 9 (Map engine) needs to start earlier than "after Politics/Military/Diplomacy."** It's a genuinely large sub-project (§2, §6) with no dependency on those domains being finished — only on Phase 2's world data existing. Recommend running a *minimal* map renderer (borders + one map mode) in parallel starting around Phase 4, so it's available as a debugging/visualization tool for every subsequent simulation phase instead of being a black box until Phase 9. Full map-mode richness stays a later milestone.
2. **AI (Phase 11) should not wait for every domain to be complete.** Because Player and AI are symmetric Intent producers (§5.4), a minimal AI ("do nothing" / "random legal Intent") should exist as early as Phase 4 purely so every domain can be integration-tested with all 200 countries active, not just the player's. "Good" AI decision-making is still a late-roadmap milestone; a *present* AI is not.

| Phase | Scope | Depends on | Key exit criteria |
|---|---|---|---|
| 1 | Architecture (this doc) | — | Approved by you |
| 2 | World data | 1 | Country/province baseline dataset + license manifest, `data_schema` + `data_worldgen` crates, loads into an empty `sim_core` world |
| 3 | Simulation engine core | 2 | `sim_core` tick loop, event bus, Intent pipeline, headless — no domain logic yet, just plumbing + determinism test harness |
| 4 | Economy (+ minimal AI, + minimal map) | 3 | Tax/GDP/inflation/trade loop running for all 200+ countries headless; stub AI issuing legal Intents; borders visible on screen |
| 5 | Population/Society | 3,4 | Cohort model live, feeds/reads Economy |
| 6 | Politics | 4,5 | Elections, approval, coalitions; the brief's worked causal chain (tax→unrest→...→coup) demonstrably emergent, not scripted |
| 7 | Military | 4,6 | Forces, logistics, war resolution |
| 8 | Diplomacy | 6,7 | Treaties, alliances, IOs, sanctions |
| 9 | Map engine (full) | started in 4 | All map modes, zoom LOD, camera |
| 10 | UI (full dashboard) | 4–9 producing real data | egui panels/charts/timeline/command palette against live data |
| 11 | AI (full) | 4–10 | Personality-driven utility AI replacing stub, tuned per ideology/government type |
| 12 | Performance optimization | 4–11 | 200+ countries, thousands of cities, target cohort counts, sustained tick rate under budget |
| 13 | Modding tools | 12 | Data-mod pipeline shipped; Lua scripting layer for AI/events |
| 14 | Multiplayer (optional) | 12 | Server-authoritative sync per Challenge 3 |
| 15 | Steam release | 13 (14 optional) | Packaging, store integration |

---

## 11. Open decisions needed before Phase 2

1. **World data scope for v1**: all 200+ sovereign states with real provinces from day one, or a smaller initial roster (e.g. G20 + regional powers at full fidelity, rest abstracted) that expands over time? This materially changes Phase 2 effort.
2. **Real-world leader/party names**: use real current officeholders and parties (factual, not copyrighted, but politically sensitive and will go stale) vs. procedurally generated leaders/parties from day one? This affects `data_schema` and the AI personality generator.
3. Confirm the **cohort-based population model** (Challenge 1) and **server-authoritative multiplayer** (Challenge 3) as accepted, since they're the two biggest deviations from a literal reading of the brief.

Once you've reviewed this — agreed, amended, or challenged back — we move to Phase 2 (World Data): concrete schema for `Country`/`Province`/`PopulationCohort`, the data sourcing/license manifest, and the worldgen pipeline design.
