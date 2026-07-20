---
tags: [vision, game-design, military]
---

# Conflicts, Terrorism and Military Realism

Part of [[Vision Overview|the vision]]. [[War System]] today is generic — any two countries can
fight, with no connection to the actual geopolitical situation in 2026. The ask is to root the
game in the real current conflict map and add depth [[War System|the current war mechanic]]
doesn't have: terrorism, base types, real equipment, and nuclear proliferation.

## Real 2026 scenario conflicts
The game should start with the real conflicts that exist right now baked in, not a blank slate.
The player named Russia–Ukraine, Iran, Israel–Palestine, and Syria as examples — the reasoning
("this is a geopolitical game, this is how it'll be fun") applies to the real conflict map as a
whole, not just those four. A 2026 scenario start should seed [[War System|active wars]]/tension
and [[Diplomacy System|depressed relations]] matching reality across every rough category of
real current conflict, not one flat list:

- **Major interstate war**: Russia–Ukraine.
- **Regional power posture / proxy tension**: Iran's regional footprint, Israel–Iran tension,
  Israel–Palestine, the broader Israel–Hezbollah/Lebanon axis.
- **Civil war / internal fragmentation**: Syria, Sudan, Yemen, Myanmar.
- **Insurgency-driven instability** (feeds directly into the terrorism mechanic below rather
  than interstate war): the Sahel region (Mali, Burkina Faso, Niger), Somalia, Nigeria's
  northeast.
- **Frozen or unresolved territorial disputes**: Armenia–Azerbaijan (Nagorno-Karabakh),
  India–Pakistan (Kashmir), the Korean DMZ.
- **Great-power competition backdrop** (tension without open war): Taiwan Strait / US–China,
  South China Sea territorial claims.

Seeding this breadth — not just four countries — is what makes the world feel like *this* world
rather than a generic one with a few hot spots painted on.

> [!success] Status: ✅ First slice built — the real early-2026 world map now seeds at game start
> (`Sim/WorldAlignments.cs`, applied in `MapRenderer` right after `DiplomacySystem.Seed`):
> - **14 real blocs** as relation floors with correct Jan-2026 membership: NATO's 32 (Finland
>   2023, Sweden 2024), EU 27, GCC, CSTO *minus Armenia* (participation frozen 2024), ASEAN
>   *including Timor-Leste* (admitted Oct 2025), Five Eyes, Nordic, Baltic, Benelux, Visegrad
>   (floor lowered — cohesion eroded over Ukraine), Mercosur *with Bolivia* (full member 2024),
>   Turkic States, Alliance of Sahel States, CARICOM.
> - **~120 curated bilateral pairs**: severe hostilities (Iran–Israel post-June-2025 war,
>   US–Iran, both Koreas, China–Taiwan, India–Pakistan post-Sindoor, Armenia–Azerbaijan
>   post-Karabakh, Serbia–Kosovo, Morocco–Algeria, Ethiopia–Eritrea, DRC–Rwanda,
>   Venezuela–Guyana...), tense-but-functional pairs (US–China, Greece–Turkey overriding their
>   shared NATO floor, GERD, US–Canada/Denmark 2025 ruptures...), special allies beyond any bloc
>   (US–Israel/Japan/Korea, China–Pakistan, Russia–Belarus/North-Korea, Turkey–Azerbaijan...),
>   and friendships (Abraham-Accords pairs, Iran–Russia, Taiwan's seven remaining formal
>   diplomatic partners...). Post-Assad Syria is modeled correctly: Iran–Syria hostile,
>   Turkey–Syria friendly.
> - **Russia–Ukraine seeds as an actual active war** at day 0 — started 1,407 days before the
>   Jan 1 2026 epoch, modest attacker score, both sides worn to near-stalemate exhaustion.
> - Gaza is seeded as severe hostility rather than an active interstate war, reflecting the
>   Oct 2025 ceasefire holding at the Jan 2026 snapshot.
> - Curated via a multi-agent research workflow with an adversarial verification pass per
>   domain; the verifiers' corrections (TLS in ASEAN, BOL in Mercosur, US–Taiwan as strategic
>   ambiguity not treaty ally, ISR-PSE war→severe, Houthi stand-down, post-Assad flips) are all
>   applied in the committed data. Verified live: boot log confirms 942 pair applications, the
>   seeded war, France's EU floor (which also proves the Natural Earth `ISO_A3="-99"` fallback
>   fix), and Greece–Turkey's curated 30 overriding NATO's 70.
> - Still open in this section: civil wars/internal fragmentation as a mechanic (Sudan, Myanmar
>   are relation-seeded but their internal wars aren't modeled), and tension escalating into
>   AI-declared wars from these seeds.

## Terrorism as an internal mechanic
Distinct from interstate [[War System|war]]: a terrorist/insurgent organization can exist and
grow *inside* a country (the example given: an org growing inside Saudi Arabia). The player
needs ways to fight it internally — options to attack/degrade the organization — and the
organization itself needs to be a real threat: capable of attacking places, attacking
businesses, not just a passive stat. This is a new system, not a reskin of interstate war.

Model this by **type/region**, not by naming specific real active organizations — the real
insurgency-driven instability zones named above (Sahel jihadist insurgencies, Somalia,
Nigeria's northeast, and similar real patterns elsewhere) are the honest reference for what
"growing internal threat" should feel like, without the game needing to model any specific real
group's actual current operational details.

> [!success] Status: ✅ Built (`Sim/TerrorismSystem`). A per-country `TerrorThreat` (0-100, on
> `NationalState` so it serializes) that **grows from real grievance** — political repression
> (low `FreedomSpeech`), a miserable population (low `PublicMood`), and mass unemployment — held
> down by security capacity (defence spending + readiness). Above the attack threshold it
> **periodically strikes**: each attack dents growth, drains the treasury, and knocks public mood
> and approval, surfacing as a Security news toast (escalating from ~monthly at the threshold to
> ~weekly at maximum threat). Fought via a **counter-terror operation** on the Military tab
> (INTERNAL SECURITY card) that spends treasury to cut the threat now — but with the real
> counterinsurgency nuance the vision asks for: a **heavy hand in a low-freedom state works less
> well and breeds fresh grievance**, so force alone can't hold it; the durable fix is freedoms,
> jobs and mood. Modelled by grievance/region, not by naming real groups. Verified live (forced
> scenario: FS=8, unemployment 20%, mood 25 → grievance 54): threat sustained 45-59, 10 attacks
> fired over ~130 days visibly hitting growth and mood, counter-op cut threat 59→50 and flagged
> the heavy-handed backlash. **Still open:** a named/visible org with a location on the map,
> territory it contests, and cross-border spillover.

## Bases, equipment, and nuclear realism
- **Base types**: air bases, naval bases, ground bases, and *foreign* bases (a country hosting
  another country's military presence — real and geopolitically significant, e.g. US bases
  abroad). A [[Supranational Unions|military alliance]] membership (NATO-style) is the natural
  reason a foreign base exists on allied soil, not an isolated fact.
- **Real equipment, by category** — the player named F-35s/F-16s and "fast missiles, low
  missiles" as examples of wanting named real hardware instead of an abstract `ReadinessIndex`
  number; the fuller real taxonomy this points at:
  - **Fighter aircraft, by generation**: 5th-gen (F-35, J-20, Su-57), 4.5-gen (F-16, Rafale,
    Eurofighter Typhoon, Gripen), legacy (MiG-29, F-4-era airframes still in service in some
    air forces).
  - **Missiles, by class**: ballistic (short/medium/intercontinental range), cruise,
    hypersonic, and air-defense/interceptor systems — distinct roles, not one "missile" stat.
  - **Naval**: aircraft carriers, destroyers, frigates, and submarines (conventional vs.
    nuclear-powered — a real, consequential distinction for range/stealth).
  - **Ground**: main battle tanks, artillery, and modern air-defense networks.
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
