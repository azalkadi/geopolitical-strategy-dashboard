---
tags: [architecture, sim]
---

# Save Load

`Assets/Scripts/Sim/SaveLoad.cs` — a whole-simulation JSON snapshot written to
`Application.persistentDataPath/meridian_save.json`. Since every [[Simulation Overview|Sim/
system]] uses plain public fields, this is a **dumb, complete serialization** — a loaded game is
bit-identical to one that was never quit. Geography itself is never saved (it reloads fresh from
`StreamingAssets` every launch via [[Geo Pipeline]]) — only mutable simulation state is
persisted.

## SaveGame (the serialized shape)
`Version`, `SavedAtUtc`, `CountryCount` (guards against loading a save from a different geo
dataset), `SimDay`, `DaysPerSecond`, every [[Player State and Elections|PlayerState]] field,
`NextEventDay` (see [[Decision Events]]), `List<EconomyState>`, `List<NationalState>`, the whole
`DiplomacySystem` object, the whole `WarSystem` object, and a
`Dictionary<string, float[]>` for [[History and World Feed|PlayerHistory]]'s six series.

## API
- `SaveExists()` / `SavePath`
- `Save(simDay, daysPerSecond, econ, national, diplomacy, wars)` — write-then-rename for crash
  safety, via `Newtonsoft.Json.JsonConvert`
- `TryRead(expectedCountryCount)` — validates the country count and completeness; returns `null`
  on **any** problem (missing file, corrupt JSON, mismatched country count) rather than throwing,
  so callers can just check for null
- `Apply(save, econ, national)` — replaces `econ.States`/`national.States` wholesale, restores
  every `PlayerState` static, clears `EventSystem.Pending`/`WorldFeed`, reloads
  `PlayerHistory` from the saved series

## Call sites
- `GameUIRoot.cs` — the SAVE button, `ContinueSavedGame()` (start screen), and an autosave on
  `OnApplicationQuit` (only if `PlayerState.State == Playing`, so quitting mid-game doesn't cost
  the player their run)
- `MapRenderer.ApplySave(save)` — swaps the freshly-seeded simulation for the saved one
  (geography/meshes stay as already built by `Start()`); also triggers
  `RefreshCountryColors()` (see [[Map Modes and Coloring]]) so relation colors are correct the
  instant a save loads, not just after the first tick

## Dev-only self-test
`MERIDIAN_DIAG_SAVE=1` (checked in `MapInteraction.cs`) saves at day 60 and immediately
re-reads the file, comparing key deserialized values (GDP, treasury, PRNG state, a diplomacy
relation, approval rating, active war count) against the live simulation — verifies
serialization fidelity in-process. Pair with `MERIDIAN_LOADSAVE=1` on a **second** launch to
verify the full cross-process load path.
