---
tags: [game-design]
---

# Diplomacy Mechanics

Every country pair in the game has a relations score from 0 (hostile) to 100 (friendly),
seeded from real geography (neighbors and same-region countries start warmer) — see
[[Diplomacy System]] in the architecture map for the code. This score is also what colors the
whole map — see [[Map Modes and Coloring]].

## Actions you can take
- **Send Aid** — costs a small % of your GDP, +12 relations, small boost to your international
  standing. Warmth is bought.
- **Sign Trade Agreement** — needs relations ≥ 65 first. Permanent (until you start a new game),
  boosts both countries' exports. +5 relations on signing.
- **Denounce** — free, −15 relations, small approval-rating boost at home (cheap domestic
  applause), small hit to your international standing (real cost abroad).

Each action has a **90-day cooldown per country pair** — you can't spam aid or denunciations
against the same target back-to-back.

## Drift
Left alone, every relation slowly drifts back toward its original seeded baseline — so one aid
package doesn't permanently buy a friendship, and one denunciation doesn't permanently end one.

## The wider world
Countries you're not involved with occasionally sign trade agreements or go to war with each
other on their own — see [[World AI]] in the architecture map. This keeps happening whether or
not you're paying attention, so the world feels alive rather than frozen outside your own
borders.

## Consequence: war eligibility
Relations have to already be at 35 or below before you (or an AI) can declare war on that
country — see [[War Mechanics]].
