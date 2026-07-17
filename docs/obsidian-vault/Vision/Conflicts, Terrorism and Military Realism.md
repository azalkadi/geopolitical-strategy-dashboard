---
tags: [vision, game-design, military]
---

# Conflicts, Terrorism and Military Realism

Part of [[Vision Overview|the vision]]. [[War System]] today is generic — any two countries can
fight, with no connection to the actual geopolitical situation in 2026. The ask is to root the
game in the real current conflict map and add depth [[War System|the current war mechanic]]
doesn't have: terrorism, base types, real equipment, and nuclear proliferation.

## Real 2026 scenario conflicts
The game should start with the real conflicts that exist right now baked in, not a blank slate:
Russia–Ukraine, Iran (its regional posture/conflicts), Israel–Palestine, Syria's internal
situation. These aren't just flavor — a 2026 scenario start should seed [[War System|active
wars]] and [[Diplomacy System|depressed relations]] matching reality, so the world feels like
*this* world, not a generic one. "This is how the game will be fun — this is a geopolitical
game" was said explicitly as the reasoning.

## Terrorism as an internal mechanic
Distinct from interstate [[War System|war]]: a terrorist organization can exist and grow
*inside* a country (the example given: a terror org growing inside Saudi Arabia). The player
needs ways to fight it internally — options to attack/degrade the organization — and the
organization itself needs to be a real threat: capable of attacking places, attacking
businesses, not just a passive stat. This is a new system, not a reskin of interstate war.

## Bases, equipment, and nuclear realism
- **Base types**: air bases, naval bases, ground bases, and *foreign* bases (a country hosting
  another country's military presence — real and geopolitically significant, e.g. US bases
  abroad).
- **Real equipment**: named real hardware (F-35s, F-16s, missile classes distinguished by
  speed/type — "fast missiles, low missiles" was the phrasing) instead of an abstract
  `ReadinessIndex` number.
- **Intel fog of war**: the player should only know what their "secret services" would
  plausibly know about another country's military — not full omniscient data on everyone,
  general/public data otherwise.
- **Nuclear proliferation**: countries with nukes can extend a "nuclear umbrella" over allies;
  a country without nukes can run a covert program to develop them; if a major power discovers
  it, **sanctions** are a real consequence — this needs to hook into [[Diplomacy System]] and
  [[Economy System]] (sanctions should actually hurt).

## Combat visuals
When a strike happens, the player wants to *see* it: missiles visibly traveling from launch to
target on the map, an impact/boom effect, not just a toast notification. Ties into
[[Map and UI Realism]]'s broader "make it look and feel like a game" ask.

## Where this plugs into existing code
- `Sim/War.cs`/`Sim/WorldAI.cs` — scenario seeding (start some wars/tensions as already active
  at game start, matching reality) is a relatively contained addition to `WarSystem.Seed`-style
  logic.
- Terrorism is a new system entirely — no existing analog. Needs its own state per country
  (org strength, target selection, player counter-actions) and its own tick.
- Bases/equipment/nuclear are a significant expansion of [[National State]]'s military fields
  (`DefenseSpending`/`ReadinessIndex` today) into something far more granular — likely its own
  file given the scope, referencing [[Curated Datasets]] (air bases already exist as map
  markers) as a starting data source.
- Missile-strike visuals are a [[Map Rendering]] addition — an animated line/projectile from
  source to target, timed to the attack event.
