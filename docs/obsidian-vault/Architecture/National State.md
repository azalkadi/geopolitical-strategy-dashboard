---
tags: [architecture, sim]
---

# National State

`Assets/Scripts/Sim/NationalState.cs` — lightweight derived indices for the 5 ministries that
aren't [[Economy System|Economy]]: Politics, Military, Diplomacy, Society, Technology. Replaced
what used to be "coming soon" placeholder screens. Each category has **at most one** adjustable
lever; everything else is a slow-drifting derived value computed from real inputs, not its own
independent simulation.

## NationalState (per country)
- `ApprovalRating` — drives [[Elections]]; drifts from growth/unemployment/inflation and (for
  the player) event/diplomacy/war outcomes
- `DefenseSpending` (lever) → `ReadinessIndex` (derived; feeds [[War System]] strength)
- `InternationalStanding` — moved by [[Diplomacy System]] aid/denounce actions and by
  [[War System]] outcomes
- `PublicMood`
- `ResearchSpending` (lever) → `InnovationIndex` (derived)

`Tick(EconomyState e, double gdpRankPercentile)` drifts each index toward a target computed from
growth/unemployment/inflation/spend-levers/GDP-rank.

## NationalSystem
`List<NationalState> States`; `Seed(count)`; `TickAll(EconomySystem)` — recomputes every
country's GDP-rank percentile **fresh each tick** (sorts the full country list by GDP) before
ticking every state, so a country's relative standing moves as the world's economies do, not
just its own.

## Consumers
- [[War System]] — reads `ReadinessIndex` for strength, writes back approval/standing/mood from
  war outcomes
- [[Decision Events]] — event effects mutate `NationalState` directly
- [[Elections]] — reads `ApprovalRating` for the reelection check
- [[UI System]] — Politics/Military/Society/Technology tabs in `GameUIRoot.cs`
- [[Save Load]] — serialized wholesale as `List<NationalState>`
