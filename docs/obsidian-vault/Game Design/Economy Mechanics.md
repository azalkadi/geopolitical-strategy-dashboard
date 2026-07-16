---
tags: [game-design]
---

# Economy Mechanics

The player-facing rules behind the Economy/Budget/Trade ministries. Simulated for every country
in the game, not just yours — see [[Economy System]] in the architecture map for the code.

## What you control
- **Four tax rates**: income, corporate, VAT, tariff — plus you can define your own custom taxes
  with their own name and rate.
- **Interest rate**.
- **Budget spending levers**: education, healthcare, infrastructure, each as a % of GDP. These
  aren't cosmetic — they genuinely feed growth, innovation ([[Ministries|Technology]]), and
  public mood ([[Ministries|Society]]).

## What moves as a result
Growth rate reacts to tax/interest-rate drag versus spending boost, plus a small amount of
noise. GDP, unemployment, inflation, and treasury update from that every simulated day. Cross
a meaningful threshold (e.g. into recession) and you'll get a toast explaining *why* — the
economy narrates its own state changes rather than leaving you to guess.

## Trade
Exports/imports are driven by an export/import propensity per country, boosted permanently by
any [[Diplomacy Mechanics|trade agreement]] you sign. Trade balance, annual revenue/expenditure/
deficit, public debt, and debt-to-GDP are all derived and shown on the Trade tab.

## Where the seed numbers come from
Real countries seed from real Natural Earth GDP/population data where available (see
[[Natural Earth Datasets]]); everywhere else falls back to a placeholder distribution so no
country starts at zero.
