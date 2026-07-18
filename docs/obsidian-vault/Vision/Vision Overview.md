---
tags: [vision, game-design]
---

# Vision Overview

Captured 2026-07-17 from a long voice-driven design session. This is the **long-term
ambition** for Meridian — not a sprint, a marathon the player intends to keep pushing deeper
indefinitely. The [[Development Roadmap.canvas|roadmap canvas]]'s stage 4.5 tracks this pillar
at the build-order level; this folder is where the actual design content lives.

**Reading rule for this whole folder** (added at the player's explicit correction): things the
player named — EU/GCC/UN, F-35s/F-16s, oil/fruit sectors, the four conflicts — were **examples
of a pattern, not exhaustive lists**. Every pillar page generalizes from the named examples to
the full real category and its real distinct functions; when building, implement the category,
not just the named instances.

> "I want this game to be super fucking realistic, like crazy realistic, and you can fulfill
> your passion about politics in this game." — the player, on why this matters to them

The player cannot write or read code themselves (see the `user-profile-meridian` memory) — live
coding through Claude is their only path to building this. **Standing instruction: if something
is realistic and improves the simulation, add it without asking permission first.** Only stop to
ask when there's a genuine architectural fork (which system to build first, not whether to add
a realistic detail).

## The six pillars

1. [[Government, Legislature and Real Taxes]] — the biggest one. Real regime types per country
   (monarchy vs. multi-party republic vs. one-party state...), real named political parties,
   a bill system where monarchies decree and parliaments vote, and real seeded tax data instead
   of generic sliders.
2. [[Economic Sectors and Companies]] — the economy as real industries and real companies, not
   one GDP number. Public/private/mixed ownership, per-sector manpower you can reallocate.
3. [[Conflicts, Terrorism and Military Realism]] — the real 2026 conflict map across every
   category (interstate war, proxy tension, civil wars, insurgencies, frozen disputes,
   great-power competition — Iran/Russia-Ukraine/Israel-Palestine/Syria were the named
   examples), internal terrorism as a mechanic, realistic military equipment/bases/nuclear
   programs, and combat visuals (missiles, strikes).
4. [[Supranational Unions]] — the full spectrum of real multilateral organizations by
   *function*: political/economic unions (EU, GCC), military alliances (NATO), regional blocs
   (ASEAN, AU, Arab League), cartels (OPEC+), the UN, informal blocs (BRICS) — plus federal
   internal structure (US states) as the inverse layer inside one country.
5. [[Map and UI Realism]] — right-click context actions, a minimap, tiered city/infrastructure
   icons that actually look like what they represent, city/province click interactions, train
   and cargo movement, bigger flags, and an overall "looks like a real game" polish bar.
6. [[Data Accuracy Audit]] — the geo datasets already have real gaps (a rail line marked
   present that doesn't actually run, a capital missing from its own country's network) that
   need auditing against reality, not just against the raw source files.

## Why this order
[[Government, Legislature and Real Taxes]] is first because regime type gates almost everything
else — how a bill passes, what "declare war" even means diplomatically, how the world reacts to
freedom-of-speech/religion changes, what the right-click menu should even offer. Build the
gate, then build what it gates.
