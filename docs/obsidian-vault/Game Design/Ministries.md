---
tags: [game-design]
---

# Ministries

The 8 categories in the bottom ministry bar (`NationCategory` — see [[UI System]] in the
architecture map), each with its own accent color and topic list. Selecting one opens the side
panel to that ministry.

| Ministry | Covers | Mechanics note |
|---|---|---|
| **Economy** | GDP, growth, unemployment, inflation, custom taxes | See [[Economy Mechanics]] |
| **Budget** | The four core tax rates + interest rate + spend levers | Part of [[Economy Mechanics]] |
| **Trade** | Exports/imports/trade balance | Boosted by [[Diplomacy Mechanics|trade agreements]] |
| **Politics** | Approval rating, international standing, public mood | Drives [[Elections]] |
| **Diplomacy** | Bilateral relations with every other country | See [[Diplomacy Mechanics]] |
| **Military** | Defense spending, readiness, active wars | See [[War Mechanics]] |
| **Society** | Public mood, healthcare spend | Derived from [[Economy Mechanics]] |
| **Technology** | Research spend, innovation index | Derived from [[Economy Mechanics]] |

Each ministry's numbers are computed by exactly one [[Simulation Overview|Sim/ system]] — there's
no ministry with its own independent hidden simulation. See the architecture-side
[[National State]] note for how Politics/Military/Society/Technology are actually derived
(mostly from Economy inputs plus one adjustable lever each).
