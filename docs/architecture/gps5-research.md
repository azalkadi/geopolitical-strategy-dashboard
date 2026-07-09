# Research: Geo-Political Simulator 5 (competitive analysis)

**Purpose:** understand what GPS5 (Eversim, formerly branded *Power & Revolution*) actually claims to simulate, what it gets right, and — more importantly — what a decade of player feedback says is wrong with it, so Phase 2+ can be pointed at the actual gap instead of re-deriving GPS5's mistakes from scratch. No assets, text, or code referenced here; this is mechanics/lessons only, per the no-copy constraint.

---

## 1. What GPS5 is

Published by Eversim (French studio), GPS5 is the latest entry in a series that's been running under two names — *Geo-Political Simulator* and *Power & Revolution* — for roughly a decade, releasing a new paid "Edition" nearly every year (GPS5's own store page already advertises a "2025 Edition" and a "2026 Edition" upgrade). The pitch: "a simulation of our current world... across economic, political, military, social, financial, environmental, energetic, transportation" domains, playable as a head of state, an opposition figure, or the president of a multinational corporation. This is the closest existing product to what you're describing — same pitch, same ambition, same scope.

**Scale claims** (marketing-stated, not verified): 175 playable countries, 600+ tracked data points per country, 3,000+ modeled multinational corporations, 150,000 facts/figures in the underlying dataset, ~30 tax types, 130+ economic sectors, several hundred to 1,000+ playable actions, 50+ international organizations, 20+ scenario starts.

Sources: [GPS5 official feature page](https://www.geo-political-simulator-5.com/new_features.php?langue=en), [GPS5 presentation page](https://www.geo-political-simulator-5.com/presentation.php?langue=en), [Steam store page](https://store.steampowered.com/app/3107770/GeoPolitical_Simulator_5/)

## 2. Feature inventory (for gap analysis, not for copying)

| System | What GPS5 does |
|---|---|
| Map | 3D world map, ~2M polygons, 10M+ grid cells, zoom to street level, modeled rivers/bridges/tunnels/60+ building types |
| Economy | "Macro-economic engine" simulating thousands of individual companies contributing to national GDP; sector-level trade, price variation, shortages/surplus, embargoes |
| Corporations | Player-manageable multinationals with production/marketing/HR/R&D/cybersecurity/pricing decisions, including illegal tactics (tax evasion, price fixing) as explicit options |
| Politics | Party AI that forms/dissolves coalitions, changes leaders, spawns new parties; election scenarios (including a US election mode); union AI that responds to labor conditions and can trigger strikes |
| Military | Land/sea/air/ICBM-scale units; terrain affects movement (rivers/mountains slow, roads/highways speed up) |
| Espionage | Funding foreign political parties, stealing technology for your country/companies |
| UI | Redesigned for GPS5, scalable for 4K, but see §3 — this is the single most-criticized surface |

## 3. What ten years of player feedback actually says

This is the useful part. Steam aggregate: **"Mostly Negative," ~37/100 player score** on the base GPS5 listing (213 positive / 358 negative of 571 reviews at time of research). Recurring, specific complaints across reviews and community discussions:

1. **Bugs that break the core loop, not just polish** — GDP stops calculating, coalition-building becomes literally impossible, crashes and random freezes mid-session.
2. **AI that breaks immersion at the mechanical level, not just "feels dumb"** — dependency relationships on other nations are AI-controlled in ways players describe as not making mechanical sense; diplomatic interactions are called "laughably unrealistic."
3. **Save system reliability** — reports of no dependable save/autosave, to the point of players being told to leave the PC running continuously rather than risk a reload.
4. **UI as a complexity tax, not a complexity aid** — "important information buried under layers of unnecessary complexity," described as clunky and outdated despite the GPS5 redesign. The depth is real but the interface actively fights the player trying to access it.
5. **Performance** — poor framerate/responsiveness reported even on high-end hardware.
6. **The business model compounds all of the above** — because a new paid "Edition" ships roughly yearly, bugs from one edition are widely reported as carrying straight into the next unfixed, and a Steam petition exists specifically asking Valve to review the series for this pattern. The practical effect: depth and system count grew every year, but reliability didn't, because each edition cycle was too short to pay down the previous one's debt.

Sources: [Steam reviews (via Steambase aggregation)](https://steambase.io/games/geo-political-simulator-5/reviews), [Steam Community discussion: "As bad as the reviews suggest?"](https://steamcommunity.com/app/3107770/discussions/0/4701287772393163106/), [Change.org petition re: Eversim edition pattern](https://www.change.org/p/make-valve-review-eversim-s-geopolitical-simulator-games), [Steam store page](https://store.steampowered.com/app/3107770/GeoPolitical_Simulator_5/)

## 4. The actual gap (this is the "best approach" answer)

GPS5's problem was never ambition — the scope inventory in §2 is genuinely close to what you asked for in the master prompt, and by most accounts the *simulation depth is real*. Its problem is that **depth was built faster than reliability, determinism, and legibility**, every single year, for a decade, without ever stopping to pay it down. That is a direct, evidence-backed argument for decisions Phase 1 already made — and a few it didn't go far enough on:

| GPS5's failure mode | Phase 1 decision that already defends against it | Additional action this research justifies |
|---|---|---|
| GDP silently stops calculating; coalition logic gets into an impossible state | Headless, deterministic `sim_core` (§4–5 of Phase 1), testable without a renderer | **Add:** every domain crate ships property-based/invariant tests ("GDP is always computable," "a coalition search always terminates") as a Phase-3 exit criterion, not a someday nice-to-have. GPS5's specific failures are a ready-made invariant checklist. |
| Unreliable saves, "leave the PC running" | Versioned, event-log-based save system (§8) | **Add:** autosave reliability and save/load round-trip determinism become an explicit CI-gated test from Phase 3 onward — i.e. "can we save and reload without state divergence" is checked every commit, not tested manually before ship. |
| AI dependency behavior that "doesn't make mechanical sense" | Player/AI symmetry via shared `Intent` events (§5.4) — AI can't have hidden authority the player doesn't, because it's the same code path | **Add:** explicitly test cross-country dependency scenarios (e.g., trade/resource dependency, alliance obligations) for legibility — the player should always be able to see *why* an AI-controlled dependency behaved the way it did, via the event log. This is a UI requirement, not just a sim one: surface the causal chain, don't hide it. |
| UI depth buried under unnecessary complexity | `bevy_egui` + docking, but Phase 1 didn't specify an information-architecture principle | **Add (new):** adopt a stated UI principle now — every panel must answer "what changed and why" before "what is the current value," since GPS5's failure was volume of data without a legible causal story. This favors your dashboard/timeline/notification-driven design over GPS5's static-panel-per-ministry approach. Concretely: the event bus (§5.2) that already exists for simulation causality is also the natural feed for a "why did this happen" UI trail — reuse it, don't build a second explanation system later. |
| Yearly edition cadence outruns bug-fixing capacity | N/A — you're not committed to a yearly-edition model | **Add (roadmap discipline):** resist adding new domain crates faster than existing ones reach the invariant/test bar above. If ambition list (§ "Simulation Systems" in the master prompt) tempts scope growth phase-over-phase, treat "does the last domain we shipped still pass its invariants" as a gate before starting the next one — this is the single most avoidable failure in GPS5's history and it's a process discipline, not an architecture one. |

## 5. Where GPS5 is a reasonable model to follow, not just a cautionary tale

- **Domain breadth** (§2) is a solid checklist against your master prompt's system list — nothing in GPS5's inventory is missing from your brief, and nothing in your brief looks unreasonable in light of what GPS5 already ships (so scope itself isn't the risk; sequencing and reliability are).
- **Player/opposition/corporation as three playable "seats"** on the same simulated world is a good concrete pattern worth adopting deliberately: it's really just "which Intent-issuing entity is human-controlled this session," which Phase 1's player/AI symmetry (§5.4) already supports for free. Worth stating explicitly as a Phase 6 (Politics) / Phase 11 (AI) target rather than rediscovering it late: **head-of-state, opposition leader, and corporation CEO should all be selectable player seats from the same world simulation**, not three different game modes.
- **Illegal/gray-market corporate actions** (tax evasion, price fixing, embargo-busting) as explicit, simulated (not narrative-flavor) options is a good realism signal worth keeping on the Economy/Society domain backlog.

## 6. Recommendation

No change to the Phase 1 architecture's shape — it already anticipates GPS5's specific failure modes better than GPS5 itself did. What this research changes is **acceptance criteria**: Phase 2 onward should treat "would this specific GPS5 complaint be possible in our system" as a literal checklist item at the end of every phase, not just a general aspiration to "be more reliable." I've folded the concrete additions above into what Phase 3's exit criteria should include; recommend we bake §4's table into the Phase 3 doc directly when we get there rather than trusting it'll be remembered from this research doc.
