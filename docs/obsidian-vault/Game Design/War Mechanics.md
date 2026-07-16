---
tags: [game-design]
---

# War Mechanics

Abstract, numbers-driven war — no units to move, no territory to paint on the map. Two
countries' military strength (derived from GDP, defense spending, and readiness) push a war
score back and forth every day until someone wins, someone quits, or both sides exhaust
themselves into peace. See [[War System]] in the architecture map for the code.

## Declaring war
Only possible against a country whose [[Diplomacy Mechanics|relations]] with you have already
fallen to 35 or below — you can't declare war on a friend out of nowhere. Declaring costs
relations and has knock-on diplomatic/approval effects.

## While at war
Every simulated day, the war score drifts based on relative strength, and both sides accumulate
exhaustion. Being at war is a genuine drag on your economy (growth suffers, treasury bleeds).

## Ending a war (as the player)
- **Demand concessions** — once the score has swung far enough in your favor, extract a
  reparations payment (a % of the loser's GDP) and end the war.
- **Offer white peace** — end the war with no payment either way; the other side accepts if
  they aren't winning or are exhausted enough.
- A war never auto-resolves *against* the player the way it can between two AI countries — you
  always get to choose how to end (or continue) a war you're personally in.

## AI-vs-AI wars
The rest of the world occasionally fights without you — see [[World AI]]. Those wars **can**
auto-resolve on their own via a decisive score or mutual exhaustion, since neither side is a
human waiting to decide.
