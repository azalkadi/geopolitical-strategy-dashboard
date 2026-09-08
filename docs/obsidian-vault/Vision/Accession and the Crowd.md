---
tags: [vision, game-design, design-spec, consequence-engine]
---

# Accession and the Crowd — §4 implementation spec

Implementation-ready design for [[Consequence Engine]] §4, the other half of §13 step 1. Written
to be built directly: every structure below uses Meridian's `Sim/` conventions (public fields,
engine-agnostic, Newtonsoft-serializable). **Nothing here is built yet.**

Depends on [[Consequence Engine]] §3's ledger (`Sim/Legitimacy.cs`), which is written but
unverified on branch `consequence-engine/legitimacy-ledger`.

---

## 0. The blocker to fix first: bloc membership is currently immutable

`UnionSystem` derives membership from the static `WorldAlignments.Blocs` array and **rebuilds it
from scratch on load** (`MapRenderer.ApplySave`). Nothing can join or leave a bloc. Accession is
meaningless until that changes.

**Required change before any of §4:**
- `UnionSystem` gains a **mutable, serialized** membership list seeded *from* `WorldAlignments` at
  world seed, instead of being re-derived every load.
- It must go into `SaveGame` (like `Legitimacy` did) rather than being rebuilt.
- Passive effects (`ApplyPassiveEffects`) currently bake in **once at seed**. With mutable
  membership they must be recomputed on every membership change — and because they're baked into
  `TradeAgreementExportBonus` / `AllianceStandingBonus`, recomputation has to be *idempotent*:
  store each state's union-derived contribution separately so it can be subtracted and re-added,
  or it will double on every join.

That last point is a real trap: today's code adds to `TradeAgreementExportBonus` and never
subtracts. Fix it by tracking `UnionDerivedExportBonus` as its own field.

---

## 1. Data model

```csharp
public enum AccessionStatus { Pending, Accepted, Refused, Withdrawn }

// The terms of the offer. Each generous term makes acceptance likelier AND is a genuine,
// permanent concession that limits the bloc later (§4: "it is a genuine concession").
public class AccessionTerms
{
    public bool ArmyRetained;          // target keeps its own army под its own command
    public bool FlagRetained;          // keeps flag/name/symbols
    public bool LawRetained;           // keeps its own legal system
    public bool GuaranteesPreserved;   // keeps OUTSIDE security relationships — strongest lever
    public float VoteWeight;           // 0..1, share of bloc vote vs. a population-fair share
}

public class Invitation
{
    public int From;                   // inviting state (the player, usually)
    public int To;                     // target state
    public string BlocName;
    public long OfferedDay;
    public long DecisionDay;           // when the target answers (see §2)
    public AccessionTerms Terms;
    public bool PublicDeadline;        // framing choice, set by the player at offer time
    public bool FramedAsInevitable;    // framing choice
    public float ImpliedCoercion;      // 0..1, DERIVED — never set directly (see §3)
    public AccessionStatus Status;
    public string Reason;              // why it resolved that way — for memory and narrative
}

// PERMANENT. Never expires, never decays, no cooldown. (§4: "Refusal is permanent memory.")
public class RefusalRecord
{
    public int Inviter;
    public long Day;
    public float CoercionAtOffer;
    public string Why;
}
```

`AccessionSystem` holds `List<Invitation> Open`, `List<Invitation> Resolved` (never pruned), and
`Dictionary<int, List<RefusalRecord>> Refusals` keyed by target.

---

## 2. Evaluation window — small states answer fast, large states take years

```
evalDays = Clamp(90 + 200 * log10(population / 1_000_000), 90, 1095)
```

| Population | Answers in |
|---|---|
| ~1 M | ~90 days |
| ~10 M | ~290 days |
| ~100 M | ~490 days |
| ~1.4 B | ~730 days |

Capped at 3 years. The wait is the point — it is the first place the player feels "consent is slow
and expensive now" (Pillar 2).

---

## 3. `ImpliedCoercion` is derived, never authored

Recomputed **every tick during the evaluation window** — pressure applied *after* the offer still
counts, which is what makes the mechanic honest.

| Signal (Meridian-concrete) | + |
|---|---|
| Inviter at war with the target's neighbour/ally during the window | 0.30 |
| Inviter raised tariffs (`TaxTariff`) since the offer | 0.25 |
| `PublicDeadline` set on the offer | 0.20 |
| `FramedAsInevitable` set on the offer | 0.25 |
| Inviter's readiness rose sharply during the window (mobilisation) | 0.20 |
| Re-invite within 2 years of a prior refusal | 0.15 |

Clamped 0..1.

**Its two effects are deliberately opposed** (this is the whole design):

1. **Short term it works:** `acceptance += ImpliedCoercion * 25`.
2. **Permanently it costs:** on *any* resolution — accept, refuse, or withdraw — record against the
   inviter's ledger, visible to **every observing state, including uninvited ones**:

```csharp
Legitimacy.Record(inviter, day, "Applied pressure during an accession offer",
    "Coercing a state into a union is remembered by every state that watched it happen",
    LegitimacySystem.Deltas(
        foreignPopulations:  -8f * coercion,
        foreignGovernments: -12f * coercion,
        blocMembers:         -6f * coercion,   // your own members wrote rules against this
        religiousAuthority:  -4f * coercion));
```

A player who coerces their way to five members has bought five members and a permanently poisoned
ledger — and the ledger is what §3 says they actually need.

---

## 4. Acceptance score (evaluated once, on `DecisionDay`)

Accept if `score > 50`.

```
score  = 25                                             // base reluctance
       + 0.45 * relations(inviter, target)              // existing Diplomacy relation
       + 0.30 * legitimacy(inviter, ForeignPopulations) // §3 ledger — the target's PEOPLE
       + 0.20 * legitimacy(inviter, ForeignGovernments) // the target's GOVERNMENT
       + prosperityPull                                 // see below
       + termsBonus                                     // see below
       + ImpliedCoercion * 25                           // short-term only
       - 20 * priorRefusalCount(target, inviter)        // PERMANENT, not a cooldown
       - 15 if target holds a grievance record (§11)
       - 10 if any existing bloc member opposes (their ledger view of inviter < 40)
```

**`prosperityPull`** = `Clamp((inviterGdpPerCapita / targetGdpPerCapita - 1) * 8, -10, +20)` —
joining a richer bloc is attractive; a poorer one has to compensate with terms.

**`termsBonus`** — and note the ordering matches the brief's claim that preserved guarantees are
the strongest lever in the game:

| Term | + |
|---|---|
| `GuaranteesPreserved` | 15 |
| `ArmyRetained` | 12 |
| `LawRetained` | 10 |
| `FlagRetained` | 8 |
| `VoteWeight` ≥ fair share | +6, else −10 |

### The concessions must actually bind later

If they don't, this collapses into a free "say yes to everything" button. Each generous term
**removes something from the bloc**:

- `ArmyRetained` → the bloc does **not** gain the member's manpower; the member may **decline bloc
  wars** (checked in `WarSystem` when mutual defence fires).
- `GuaranteesPreserved` → the member keeps outside alliances, so it does **not** receive
  `AllianceStandingBonus`/`AllianceReadinessBonus` from this bloc, and an attack on it does **not**
  trigger the bloc's mutual defence.
- `LawRetained` → union-level legislation (§5) does **not** bind this member.
- `VoteWeight` below fair share → generates a **grievance record** (§11) years later: "admitted on
  worse terms than a comparable state."

So a bloc assembled entirely by consent is large, legitimate, and **militarily hollow**. That
trade-off is Pillar 2, and it should be discoverable, not signposted.

---

## 5. Refusal handling

On refusal: append a permanent `RefusalRecord`, and record the *inviter's* ledger hit — being
publicly turned down is itself a legitimacy event:

```csharp
Legitimacy.Record(inviter, day, $"Accession offer refused",
    "A public refusal reads as a limit on the inviter's authority",
    LegitimacySystem.Deltas(ownPopulation: -3f, foreignGovernments: -2f));
```

Re-invitation is **harder every time** (`-20` per prior refusal, cumulative, permanent) and adds
`+0.15` coercion if attempted within two years. There is deliberately **no cooldown timer** — a
cooldown implies the memory expires, and it must not.

---

## 6. The crowd

The most distinctive mechanic in the design, and the one most likely to be got wrong by making it
controllable.

**Formation conditions**, checked per (inviter, target) pair each tick:

```
inviter's ForeignPopulations legitimacy  > 75
AND target government relations(inviter) < 35     // the people like you, their government doesn't
AND target PublicMood                    < 45     // people have their own reasons to move
```

That triple condition *is* the design sentence: the crowd only appears in the gap between a
population and its own government.

**The three rules, non-negotiable:**

1. **The player cannot direct it or call it off.** There must be **no UI button** to start, aim, or
   stop a crowd. The player can only change the *conditions* — and the main lever (their own
   foreign-population legitimacy) is something they spent decades building and will not want to
   spend. Attempting to disperse it reads as betrayal: `ownPopulation −6`, `foreignPopulations −12`.
2. **Force against it is catastrophic for whoever fires**, target government or player alike:
   `foreignPopulations −25`, `religiousAuthority −20`, `ownPopulation −15`. This is why crowds
   cross lines armies cannot — nobody can afford to stop them.
3. **A crowd against a defended line produces casualties the player is blamed for, by their own
   population** (`ownPopulation −10`), even though the player did not fire and did not order it.
   The observer they most need is the one that punishes them.

**Effects while active:** the target government's legitimacy with its **own** population falls
~0.3/tick; a pending invitation's acceptance score gains `+2/tick` (capped +30); and each tick
there is a small chance the target government either **concedes** (accepts a pending invitation,
or relations jump) or **fires on the crowd** (rule 2 — which is usually terminal for that
government, and hands the player a win they did not ask for and cannot disown).

Crowds are a way of winning things armies cannot. **They are not a substitute for armies** — they
cannot take or hold territory, and a bloc built on them is still militarily hollow (§4).

---

## 7. Where it plugs into Meridian

| Piece | Location |
|---|---|
| `AccessionSystem`, `Invitation`, `AccessionTerms`, `RefusalRecord` | new `Sim/Accession.cs` |
| `CrowdSystem` (or fold into `AccessionSystem`) | new `Sim/Crowd.cs` |
| Mutable membership + idempotent bonus recompute | `Sim/Unions.cs` (§0 — do this first) |
| Daily tick: coercion recompute, decisions, crowd formation | `MapInteraction.TickEconomy` loop |
| Seed + save/load wiring | `MapRenderer`, `SaveGame` |
| Offer UI (terms checkboxes, framing toggles, live coercion readout) | `GameUIRoot`, Diplomacy tab |
| Ledger effects | already available via `Sim/Legitimacy.cs` |

**The offer UI must show `ImpliedCoercion` live as the player toggles framing options** — the
player should be able to *watch* the number rise as they add a public deadline, and understand
they are trading permanent legitimacy for short-term acceptance.

---

## 8. Verification plan (headless, per the project's standard)

`MERIDIAN_DIAG_ACCESSION=1`, four phases, all readable from `Player.log`:

1. **Generous offer to a warm neighbour** — log terms, evaluation window, acceptance score
   breakdown, and the accept. Expect acceptance, and expect the bloc to gain a member who does
   *not* contribute manpower (because `ArmyRetained`).
2. **Coercive offer to a cold state** — set `PublicDeadline` + `FramedAsInevitable`, log the
   derived coercion, the acceptance boost, and then dump the inviter's ledger showing the
   permanent multi-observer hit. **This is the key assertion: accepted AND worse off.**
3. **Refusal, then immediate re-invite** — assert the second offer's score is ~20 lower and its
   coercion ~0.15 higher, proving memory is permanent rather than a cooldown.
4. **Crowd** — force the three conditions, then log formation, per-tick pressure, and (by forcing
   the branch) both resolutions: government concedes, and government fires.

Success criterion, stated up front: **the log alone should be interesting to read.** Per §13, if
the consent loop isn't compelling in a text log, a map will not save it.

---

## 9. Open tuning questions (decide during implementation, from the log)

- Is 3 years too long for the largest states to answer, or exactly the right amount of dread?
- Does `-20` per refusal make second offers effectively impossible? (It might *should*.)
- Should the crowd be able to form toward a state that has **no** pending invitation — i.e. purely
  as a legitimacy phenomenon rather than an accession tool? The brief implies yes, and it would be
  stronger: crowds as a thing that *happens to* the player, not a mechanism they aim.
