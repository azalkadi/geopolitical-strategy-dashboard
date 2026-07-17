---
tags: [vision, game-design, data]
---

# Data Accuracy Audit

Part of [[Vision Overview|the vision]]. The [[Natural Earth Datasets|underlying geo data]] is
real, but "in the source file" and "real in 2026" aren't always the same thing — Natural Earth
is a cartographic dataset, not a live infrastructure registry. Two concrete gaps were flagged
directly by the player and more should be expected once this gets real scrutiny:

- **A rail line is rendered between Syria and Jordan that isn't actually an operating rail
  connection in reality** — the source data has geometry for it, but it doesn't reflect current
  operating status.
- **Riyadh is missing from Saudi Arabia's rail network** in the current render, despite Saudi
  Arabia having a real rail network connecting to/through its capital.

## The general principle
Don't trust "it's in the Natural Earth file" as equivalent to "it's real and currently
operating." Where it matters for gameplay/immersion (a country the player is likely to actually
look closely at), the data needs a real audit against current reality, with corrections layered
on top the same way [[Curated Datasets]] already hand-corrects/supplements gaps Natural Earth
doesn't cover at all (oil ports, nuclear plants, water crossings).

## Suggested approach
Not a one-shot fix — an ongoing audit process:
1. When a player (or Claude, proactively) notices a specific real-world discrepancy, record it
   here with the country/feature and what's actually true.
2. Fix data-level issues either by correcting `GeoJsonLoader`'s output for that record, or by
   adding a small hand-curated override table (same pattern as
   [[Curated Datasets|the border-crossing/water-crossing curation]]) for records where the
   source file is simply wrong or incomplete.
3. Prioritize countries the player actually plays or is likely to scrutinize closely, rather
   than trying to audit all 258 countries' entire infrastructure at once — that's not a
   tractable single task.

## Known issues log
| Country/Region | Feature | Issue | Status |
|---|---|---|---|
| Syria–Jordan | Railway | Rendered as connected/operating; not actually running in reality | Not yet fixed |
| Saudi Arabia | Railway | Riyadh missing from the rendered rail network | Not yet fixed |

Add to this table as more are found — don't let them stay only in conversation history.
