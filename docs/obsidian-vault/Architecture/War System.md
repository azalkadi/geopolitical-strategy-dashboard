---
tags: [architecture, sim]
---

# War System

`Assets/Scripts/Sim/War.cs` — an **abstract** interstate war system: no unit movement, no
territory exchange. A war is a drifting war-score/exhaustion contest between two countries'
derived military strength, ending via negotiated peace or collapse. Costs flow straight back
into the same [[Economy System|economy]] and [[National State|national]] numbers everything else
uses. See [[War Mechanics]] for the player-facing version.

## War (one active conflict)
`Attacker`/`Defender` (country indices), `StartDay`, `Score` (±100, which side is "winning"),
`ExhaustionAttacker`/`ExhaustionDefender`, `LastPlayerPeaceOfferDay`.

## WarSystem
- `Strength(EconomyState, NationalState)` = `sqrt(GDP × defenseShare) × readinessFactor` — a
  single static formula used for both belligerents, player or AI alike.
- Constants: `DeclareRelationCeiling = 35` (relations must already be this bad or worse),
  `ConcessionScoreThreshold = 40`, `ReparationsGdpFraction = 0.02`,
  `PeaceOfferCooldownDays = 60`.
- `CanDeclare(a, b, diplomacy)` / `Declare(a, b, day, diplomacy, national)` — validates the
  relation ceiling, applies diplomatic/approval fallout on declaration.
- `TickAll(econ, national, diplomacy, names, day)` — advances score/exhaustion/growth-drag/
  treasury-drain daily for every active war. **AI-vs-AI wars auto-resolve** via a decisive score
  or mutual exhaustion; **player wars only end via player action or total collapse** — the player
  is never auto-defeated or auto-victorious by the tick loop alone.
- `PlayerCanDemandConcessions`/`PlayerDemandConcessions` — once `Score` crosses
  `ConcessionScoreThreshold` in the player's favor, extracts `ReparationsGdpFraction` of the
  loser's GDP.
- `PlayerOfferWhitePeace` — the AI side accepts if it isn't winning or is sufficiently exhausted.

## GeoWorldNames
A thin `Func<int,string>`-backed lookup wrapper so `Sim/` stays entirely unaware of `Geo/` —
`WarSystem` needs country **names** for headline text but must not depend on `GeoWorld` directly.

## Consumers
- [[World AI]] — declares AI-vs-AI wars via `CanDeclare`/`Declare`, checked every tick
- [[National State]] — readiness feeds strength; war outcomes write back approval/standing/mood
- [[UI System]] — `GameUIRoot.DrawMilitary`+`DrawOwnWars`+`DrawForeignMilitary` (declare/demand
  concessions/offer white peace buttons)
- [[History and World Feed|WorldFeed]] — every declaration/peace/collapse pushes a headline
- [[Save Load]] — the whole `WarSystem` (a `List<War>`) serializes directly
- [[Camera and Input]] — the `MERIDIAN_DIAG_WAR` self-test in `MapInteraction.cs` exercises
  declare/tick/concession/white-peace end to end
