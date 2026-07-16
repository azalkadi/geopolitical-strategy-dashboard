---
tags: [game-design]
---

# Gameplay Overview

Pick a real country. Govern it. Watch its economy simulate day by day. Manage 8 ministries.
Win or lose elections on approval rating. Inspired by *Geo-Political Simulator 5*, rebuilt from
first principles — see [[Code Architecture]] for how.

## The core loop
1. **Start screen** — search and pick any of 258 real countries to govern.
2. **Time passes** — the sim clock advances at a chosen speed (paused / 1× / 3× / 10×); every
   country in the world ticks its economy, not just yours.
3. **You govern via 8 [[Ministries]]** — adjust tax rates, spending levers, defense/research
   budgets; conduct diplomacy; declare or end wars.
4. **The world reacts** — other countries drift, occasionally go to AI-vs-AI war or sign trade
   deals ([[World AI]] in the architecture map), and your own [[Diplomacy Mechanics|relations]]
   with each of them show up directly on the map (see [[Map Modes and Coloring]]).
5. **Random [[Decision Events|decision events]]** land every 150-360 days — a scandal, a flood, a
   banking wobble — each with a real, immediate consequence depending on which option you pick.
6. **[[Elections]]** — every 4 years, your approval rating (the same number on the Politics tab
   the whole time, not a hidden separate stat) decides if you keep governing.
7. **Game over** — voted out, or keep winning terms indefinitely. PLAY AGAIN resets to the start
   screen with a fresh country choice.

## What makes a country's numbers move
Nothing is decorative. Tax rates and interest rates genuinely drive growth/unemployment/
inflation ([[Economy Mechanics]]). Defense/research spending genuinely move readiness/
innovation. Diplomacy actions genuinely cost treasury or raise relations. War genuinely drags
down growth and treasury for both sides. Approval rating is computed from all of the above, and
it's the one number that ends your game.

## Save/continue
One save slot, autosaved on quit mid-game; CONTINUE on the start screen picks up exactly where
you left off — see [[Save Load]] in the architecture map.
