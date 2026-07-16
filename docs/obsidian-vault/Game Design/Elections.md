---
tags: [game-design]
---

# Elections

Your term is 4 years (1,460 simulated days). When it ends, your approval rating — the exact
same number shown on the Politics tab throughout your term, not a separate hidden "win" stat —
decides whether you keep governing. See [[Player State and Elections]] in the architecture map
for the code.

## The check
- **50% approval or higher** → safely re-elected, next term begins immediately
- **Below 35%** → voted out, game over
- **Between 35% and 50%** → a weighted coin flip, so the outcome isn't a hard cliff-edge right at
  the 50% line

## What actually moves your approval rating
Everything you do, indirectly: [[Economy Mechanics|economic]] performance (growth, unemployment,
inflation), your choices in [[Decision Events|decision events]], [[Diplomacy
Mechanics|denouncing]] a rival (a quick, cheap boost), and how a [[War Mechanics|war]] you're
in goes. There's no separate campaigning mechanic — good governance *is* the campaign.

## Game over and replay
Losing an election ends the run with your final message and term count shown. PLAY AGAIN resets
everything — including the map's [[Map Modes and Coloring|relation coloring]], which goes back
to neutral until you pick your next country — and drops you back at the start screen to choose
a new country.
