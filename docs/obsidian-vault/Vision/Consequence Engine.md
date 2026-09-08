---
tags: [vision, game-design, north-star]
---

# The Consequence Engine

**This is the north-star design brief.** It supersedes nothing already built, but it re-aims the
project: everything from here should serve one of its four pillars, and §14 lists things to
actively *not* build — including one thing Meridian already has (a single reputation bar).

## STACK (filled for Meridian)

```
Engine / framework:  Unity 6000.5.3f1, UI Toolkit (C#-only, no UXML), custom mesh map rendering
Language:            C# — engine-agnostic Sim/ layer (pure math), Unity only at Map/ and UI/
Map implementation:  2D flat Web Mercator (not a 3D globe) — Natural Earth vectors + satellite
                     basemap, orthographic pan/zoom camera
Turn length:         1 tick = 1 simulated day, real-time at 1-10 days/sec (no discrete turns)
Save format:         Newtonsoft JSON, whole-object dumps of public-field Sim classes
Current state:       258 countries; real early-2026 geopolitics seeded; economy, sectors,
                     companies, diplomacy, war, legislature + elections, unions, terrorism live
```

## The brief in one line

The reference product (Geo-Political Simulator 2026: 175 countries, ~600 fields each, 150k facts)
is criticised in its own reviews as *surface level* despite all that data. **The gap is not data,
it is consequence.** Model a narrower world, resolve actions deeply, slowly, and with memory. A
decision in year one should still be costing the player in year forty — and the player must be
able to trace exactly why.

> **Design sentence: breadth is solved. Build depth.**

## The four pillars

1. **Legitimacy is the real currency.** Capability (money, armies, weapons) is easy to accumulate.
   What the player is always short of is *the right to use it*. Every state, institution and
   population holds a **separate** opinion, never aggregated.
2. **Consent scales, force does not.** A state taken by force is held by force forever; a state
   that joins voluntarily brings its army, institutions and legitimacy. Force is fast and cheap
   now, consent is slow and expensive now, **and the ratio inverts after ~15 years** — so the game
   must be playable for 100+ years for the player to feel the inversion.
3. **Costs arrive late.** Almost nothing resolves within a decade. Crises should routinely trace
   back to a decision made long ago, and the game must be able to *show the causal chain*.
4. **Institutions bind the player.** The most rewarding move is building a rule that constrains
   yourself and then living under it; the most catastrophic is breaking it. **Reward
   self-limitation more than conquest.**

## §3 The legitimacy ledger — the most important system

**No single reputation/approval number.** Legitimacy is held separately per observer, and the same
action moves different observers in opposite directions. The game must **never average them**.

| Observer | What moves it |
|---|---|
| Own population | Pride, prosperity, defiance of stronger powers, delivery on promises |
| Foreign populations | The same things — the observer players most often forget they have |
| Foreign governments | Predictability, respect for sovereignty, keeping agreements |
| Religious / moral authority | Whether actions are defensible in the tradition the player claims |
| Bloc member states | Whether the player obeys the rules the bloc itself wrote |
| Own military | Pay, purpose, and whether it is used for things it believes in |

A player at 94% with foreign populations and rejected by every foreign government is a specific,
unstable, extremely interesting position — model it, don't smooth it.

**Religious / moral authority is a first-class actor**, not a modifier: it can recognise, refuse,
condemn, or **go silent** (silence is distinct and often most damaging); it can split internally
and resolve over months/years; it is **not captured by conquering its territory**; reversing itself
costs it some of its own authority. It should be able to end a reign with no state declaring war.

## §4 Consent, accession, and the crowd

Invitation carries terms (`army_retained`, `flag_retained`, `law_retained`, `guarantees_preserved`,
`vote_weight`) and a derived `implied_coercion`.

- Targets evaluate over N ticks — small states answer fast, large states take 1-3 years.
- **Refusal is permanent memory.** Re-inviting a refuser gets harder each time — not a cooldown.
- `implied_coercion` rises with troop movements during evaluation, simultaneous economic pressure,
  public deadlines, inevitability framing. High coercion raises short-term acceptance and
  **permanently lowers legitimacy with every observing state**, including uninvited ones.
- **Preserved guarantees are the strongest lever in the game** — and a genuine concession that
  limits the player later.

**The crowd mechanic.** Above a threshold of legitimacy with *foreign populations*, populations act
independently of their governments — crossing borders, massing at frontiers, forcing their own
governments' hands. Three rules: (1) the player cannot direct it or call it off cleanly; (2) force
used against it is catastrophic for whoever fires, so crowds cross lines armies cannot; (3) a crowd
sent against a defended line produces casualties **blamed on the player, by their own population**.

## §5 Institutions and the overrule cycle — the spine of the long game

| `times_overruled` | Effect |
|---|---|
| 0 | Decisions carry real weight; members invest |
| 1 | Treated as advisory; delegations withdraw; bloc-member legitimacy drops sharply |
| 2 | Dissolves. All legitimacy accumulated from its existence is lost |
| Restored | Costs more than creating it did, and members demand **binding** power as the price |

**The restoration play should be the highest-value move in the game**: break it, admit it publicly,
restore it with *more* power, submit to it — and recover more than was lost. Give institutions a
removal clause over the player; a leader who survives a removal vote is *more* legitimate after.

## §6 Budget and convergence

- **PRODUCTION_SHARE** — provinces keep what they generate; cheap, stable, gap never closes.
- **PER_CAPITA** — equal spending per person; enormously expensive, and the single most effective
  legitimacy instrument in the game.

Track `wealth_gap_ratio` = richest ÷ poorest **at province level** (national averages hide
neglected interiors). **The absorption constraint shifts**: money binds for ~12-15 years, then
*administrative capacity* (trained civil servants) binds and money hits sharp diminishing returns.
Discoverable, not a tooltip.

## §7 Energy, chokepoints, leverage

- **Membership tariff**: price energy by political status, not market. Works exactly as long as the
  player is winning — that failure mode is the point.
- **Chokepoints** are two-sided: closing cracks opposing coalitions in months *and* stops the
  player's own imports (famine if food-import-dependent). **Selective opening** is the correct play
  and should be discoverable; it degrades as flags of convenience appear, and enforcing needs a navy.
- **Demand destruction is permanent.** Holding a high price floor for ~3 years finances your own
  replacement.
- Track **nominal vs real** honestly (a revaluation doubling nominal GDP on 18% real growth must
  show both).

## §8 Military

- **The sub-threshold gap** — the most important military design point. Strategic weapons and a
  conventional army answer nothing that is deniable, small and below both thresholds (a killed
  scientist, a sabotaged transformer, an unclaimed drone). Adversaries should deliberately work
  inside it; filling it takes years (covert capability, proxies, or a proportionate strike complex).
- **Pay and loyalty**: an army paid to be loyal is loyal to *the pay* — i.e. to whoever controls the
  payroll, which in a distant theatre is the local commander. Model **slow drift**, no coup event: a
  far, well-funded command fighting a long war quietly stops forwarding revenue, reversible only by
  going there in person.
- **Irregular/volunteer formations**: cheap and effective, loyal to their recruiter; within ~18
  months they commit reprisals the regular army avoided (religious condemnation), grow the
  insurgencies they fight, find their own funding and clerics, and develop a view on who should rule.
- **Strategic weapons**: custody ≠ ownership (and observers can see it); declaring an arsenal you
  lack works for weeks then resolves against you; testing proves it *and* locates production;
  **ambiguity is an asset** — parades and inventories convert it into targeting data; a state
  holding sites of universal religious importance cannot credibly threaten mutual destruction.
  Never let them resolve sub-threshold, domestic, or legitimacy problems.

## §9 Territory, disputes, the freeze

Most disputes should be **unresolvable and survivable**. The **Freeze Clause**: standstill (no
construction/settlement/administrative change/demographic movement); a unanimity council with
rotating chair; automatic 10-year review; the bloc takes no position and time creates no
presumption; the dispute may not be raised elsewhere.

**The upgrade**: replace *status* reviews with **development reviews** — income, sanitation,
schools, freedom of movement, displacement (must be zero), by district, certified by both parties.
Funding is conditional on the disputed population's numbers improving; **a party whose districts
fall behind loses control of spending there, but the territory keeps its funding.** This doesn't
solve disputes — it makes waiting non-oppressive and converts sovereignty arguments into
statistical ones winnable over decades.

**Interest substitution**: model *positions* and *underlying interests* separately. Occupations
usually serve a function (early warning, water, a corridor, a buffer); deliver it another way and
the occupation becomes negotiable. Tuning: explicit **acceptable-refusal clauses** beat ultimatums;
offers carried by a **small member** beat offers from the hegemon; **no ceremony** means far less
domestic backlash; phased withdrawals overrun by 2-3×, always.

## §10 Named actors and unanswered questions

Each state generates 1-3 persistent named officials with positions, memory and a lifespan. Actors
ask questions at councils and **some must go unanswered** — stored, re-raised for decades, by that
actor or a successor after their death. **The highest-value narrative object is a question asked
early, ignored, and answered forty years later by someone who was a child when it was asked.**
Weight actors from the poorest member to become the bloc's moral spokespeople over time.

## §11 Rival blocs and grievance

Any state refused admission, admitted on worse terms than a comparable state, or subjected to a
rule it did not vote for gets a **grievance record**. Grievance-holders with capability form
**permanent** counter-blocs (not hostile, not inside); counter-blocs copy the player's institutions
and sometimes implement them better; **there is no reconciliation event**. **Consistency memory**:
refuse A on grounds X then admit B who fails worse on X, and every observer records it permanently
— generate the accusation text.

## §12 The narrative layer

After each significant decision, generate a **capital-by-capital reaction** — 4-8 capitals, 1-2
lines each, differentiated by known interests and prior positions. **Include capitals with no vote
and large populations** (inside the sphere, outside the bloc) — theirs is the emotionally loaded
reaction. Reactions are the primary feedback channel; numbers are secondary. Tone: consequences,
not analysis — what happened the next morning, not whether it was wise.

**Also generate a causal trace on request**: when a crisis fires, the player asks *why is this
happening* and gets the chain of decisions that produced it, with years attached.

## §13 Implementation order

1. **Legitimacy ledger (§3) + invitation pipeline (§4)** — nothing else means anything without
   these. **Headless, text log.**
2. **Institutions and the overrule cycle (§5)** — where the long game lives.
3. **Budget and convergence (§6)** — where difficulty lives.
4. **Military (§8) and energy (§7).**
5. **Freeze clause (§9), actors (§10), rival blocs (§11).**
6. **Narrative layer (§12)**, last, over everything.

> Build 1-3 headless with a text log first. **If the consent-and-legitimacy loop is not interesting
> in a text log, a 3D map will not save it.** That is precisely the reference product's failure mode.

## §14 What NOT to build

- **No single approval/reputation bar** — the whole design is that observers disagree.
- **No "annex"/"puppet" verb for bloc members** — conquest is a separate loop.
- **No tech tree** — institutional capacity is a stock that depreciates, not a tree.
- **No event popups that resolve a system** — systems resolve through institutions or not at all.
- **No score screen.** The measures are: wealth gap ratio, membership count, whether the exit clause
  was ever used, and how many times the player overruled their own institutions.
- **Do not out-data GPS.** Twenty deeply modelled states with fifty years of memory beat 175 shallow ones.

---

## How this maps onto Meridian as it stands (2026-07-21)

**Direct conflicts with what's already built — these are the migration targets, not oversights:**

| Brief requires | Meridian today | Plan |
|---|---|---|
| No single reputation bar (§14) | `NationalState.InternationalStanding` — one bar, used by war, diplomacy, unions, regime change, freedoms | Build the ledger **alongside**, migrate consumers one at a time. Ripping it out in one commit breaks every system at once with nothing verifiable. |
| Permanent memory (Pillar 3) | `LegislatureSystem.TickAll` **prunes resolved bills >60 days** | Memory must move to a permanent, never-pruned event log keyed per state. |
| 20 deep states (§14) | 258 states, breadth-first | Keep 258 for the map; the Consequence Engine operates deeply on the player + states they actually touch. Depth where it's felt, breadth where it's scenery. |
| Institutions bind the player (Pillar 4) | Unions give passive bonuses; the legislature can't overrule the player | Unions/legislature need `binding`, `can_remove_leader`, `times_overruled`. |
| Consent pipeline (§4) | No accession/invitation mechanic at all | Net new. |

**Already aligned:** the daily tick is fine for 100+ year games; `Sim/` is engine-agnostic and
headless-verifiable (`build.ps1` + `Player.log` + `MERIDIAN_DIAG_*`), which is exactly the "headless
with a text log" the brief demands; real curated data (parties, companies, blocs, conflicts) gives
the depth model something true to bite on.

**Build status:** see CLAUDE.md's Current status for what of this is actually implemented. Nothing
in this file should be read as built unless it says so there.
