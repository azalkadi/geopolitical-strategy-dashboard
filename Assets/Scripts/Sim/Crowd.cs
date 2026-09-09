using System.Collections.Generic;
using UnityEngine;
using Meridian.Geo;

namespace Meridian.Sim
{
    // THE CROWD — Consequence Engine §4, second half.
    // (see docs/obsidian-vault/Vision/Accession and the Crowd.md §6)
    //
    // The most distinctive mechanic in the design and the one most easily ruined, because the
    // obvious implementation — a button that says "organise demonstration" — destroys it entirely.
    // A crowd is not a tool. It is something that HAPPENS, in the gap between a population and its
    // own government, when a foreign power has spent decades earning that population's regard.
    //
    // Three rules, non-negotiable:
    //
    //   1. THE PLAYER CANNOT DIRECT IT OR CALL IT OFF. There is deliberately no UI to start, aim
    //      or stop a crowd anywhere in this codebase. AttemptDisperse() and OrderForceAgainst()
    //      below exist so the COST of those acts is defined and testable — they are not wired to
    //      any button, and must not be. The only lever the player has is the set of conditions,
    //      and the main one (their standing with foreign populations) took decades to build and
    //      cannot be spent twice.
    //
    //   2. FORCE AGAINST A CROWD IS CATASTROPHIC FOR WHOEVER FIRES — the target government or the
    //      player alike. That is precisely why crowds cross lines armies cannot: nobody can afford
    //      to stop them.
    //
    //   3. A CROWD AGAINST A DEFENDED LINE PRODUCES CASUALTIES THE PLAYER IS BLAMED FOR, BY THEIR
    //      OWN POPULATION — even though the player did not fire, did not order it, and could not
    //      have prevented it. The observer they most need is the one that punishes them.
    //
    // Crowds win things armies cannot. They are NOT a substitute for armies: they take no
    // territory and hold none, and a bloc assembled by crowds is still militarily hollow.

    public enum CrowdOutcome { Active, Conceded, FiredUpon, Dispersed, Faded }

    public class Crowd
    {
        public int Toward;              // the state the crowd is FOR — usually the player
        public int In;                  // the state whose streets they are in
        public string TowardIso = "";
        public string InIso = "";
        public long StartedDay;
        public long EndedDay = -1;
        public float Size;              // 0..100
        public float Pressure;          // accumulated acceptance push, capped at +30 by the score
        public int DaysWithoutCause;    // conditions stopped holding this many consecutive days
        public long LastCasualtyDay = -1;
        public int Casualties;
        public CrowdOutcome Outcome = CrowdOutcome.Active;
        public string Ending = "";

        public bool IsActive => Outcome == CrowdOutcome.Active;
    }

    public class CrowdSystem
    {
        public List<Crowd> Crowds = new();     // active and historical, never pruned (Pillar 3)

        // Formation thresholds. The triple condition IS the design sentence — a crowd only appears
        // where a population's regard for a foreign power exceeds its own government's.
        public const float MinForeignPopLegitimacy = 75f;
        public const float MaxGovernmentRelations = 35f;
        public const float MaxPublicMood = 45f;

        public List<Crowd> ActiveCrowds()
        {
            var outp = new List<Crowd>();
            foreach (var c in Crowds) if (c.IsActive) outp.Add(c);
            return outp;
        }

        public Crowd ActiveIn(int country)
        {
            foreach (var c in Crowds) if (c.IsActive && c.In == country) return c;
            return null;
        }

        // Deterministic pseudo-randomness: crowds must replay identically from a save, so nothing
        // here may touch UnityEngine.Random.
        static float Hash01(long day, int a, int b, int salt)
        {
            unchecked
            {
                int h = (int)(day * 486187739) ^ (a * 668265263) ^ (b * 374761393) ^ (salt * 1442695041);
                h ^= h >> 13; h *= 1274126177; h ^= h >> 16;
                return ((uint)h % 100000u) / 100000f;
            }
        }

        // --- conditions ------------------------------------------------------------------

        public bool ConditionsHold(int toward, int inCountry, DiplomacySystem dip, NationalSystem nat,
                                   LegitimacySystem legit)
        {
            if (toward == inCountry) return false;
            if (inCountry < 0 || inCountry >= nat.States.Count) return false;
            var led = legit?.Of(toward);
            if (led == null) return false;
            if (led.Get(Observer.ForeignPopulations) <= MinForeignPopLegitimacy) return false;
            if (dip.GetRelation(toward, inCountry) >= MaxGovernmentRelations) return false;
            if (nat.States[inCountry].PublicMood >= MaxPublicMood) return false;
            return true;
        }

        // --- the tick --------------------------------------------------------------------

        public List<string> TickAll(long day, EconomySystem econ, NationalSystem nat, DiplomacySystem dip,
                                    LegitimacySystem legit, AccessionSystem accession,
                                    IReadOnlyList<Country> countries, GeoWorldNames names)
        {
            List<string> headlines = null;
            if (legit == null || nat == null || dip == null) return Empty;

            // FORMATION. Only states that have genuinely earned foreign regard can pull a crowd,
            // and that is rare — so build that (usually empty) list first and skip the sweep
            // entirely in the common case rather than paying an O(n^2) scan every day.
            if (day % 5 == 0)
            {
                List<int> magnets = null;
                for (int i = 0; i < legit.Ledgers.Count; i++)
                    if (legit.Ledgers[i].Get(Observer.ForeignPopulations) > MinForeignPopLegitimacy)
                        (magnets ??= new List<int>()).Add(i);

                if (magnets != null)
                    foreach (int toward in magnets)
                        for (int inC = 0; inC < nat.States.Count; inC++)
                        {
                            if (ActiveIn(inC) != null) continue;   // one crowd per country at a time
                            if (!ConditionsHold(toward, inC, dip, nat, legit)) continue;

                            var c = new Crowd
                            {
                                Toward = toward, In = inC, StartedDay = day, Size = 8f,
                                TowardIso = toward < countries.Count ? countries[toward].IsoA3 : "",
                                InIso = inC < countries.Count ? countries[inC].IsoA3 : "",
                            };
                            Crowds.Add(c);
                            // The crowd is a legitimacy event for the government it forms against,
                            // charged the moment it appears — before anyone has done anything.
                            legit.Record(inC, day, "Crowds gathered for " + (names?.Name(toward) ?? "a foreign power"),
                                "A population turning toward a foreign power in public is a verdict on its own government",
                                LegitimacySystem.Deltas(ownPopulation: -6f, foreignGovernments: -3f, ownMilitary: -2f));
                            (headlines ??= new List<string>()).Add(
                                $"Crowds gather in {names?.Name(inC)} for {names?.Name(toward)} — the government did not call them and cannot send them home.");
                        }
            }

            // ONGOING.
            foreach (var c in Crowds)
            {
                if (!c.IsActive) continue;
                var gov = nat.States[c.In];
                bool hold = ConditionsHold(c.Toward, c.In, dip, nat, legit);

                if (hold)
                {
                    c.DaysWithoutCause = 0;
                    float pull = legit.Of(c.Toward).Get(Observer.ForeignPopulations) - MinForeignPopLegitimacy;
                    float grievance = MaxPublicMood - gov.PublicMood;
                    c.Size = Mathf.Clamp(c.Size + Mathf.Clamp(0.8f + grievance * 0.08f + pull * 0.04f, 0.2f, 3f), 0f, 100f);
                }
                else c.DaysWithoutCause++;

                // A crowd in the street is a standing indictment. This is a drip, not an event —
                // recording one permanent memory entry per day would drown the causal trace.
                legit.Drift(c.In, Observer.OwnPopulation, -0.3f);
                gov.ApprovalRating = Mathf.Clamp(gov.ApprovalRating - 0.4f, 0f, 100f);

                // Push on any pending invitation from the state the crowd is for. The score caps
                // this at +30 — a crowd tilts a decision, it does not make it.
                c.Pressure = Mathf.Min(30f, c.Pressure + 2f);
                if (accession != null)
                    foreach (var inv in accession.Open)
                        if (inv.To == c.In && inv.From == c.Toward) inv.CrowdPressure = c.Pressure;

                // RULE 3 — casualties at a defended line, blamed on the player who did not fire.
                if (gov.ReadinessIndex > 60f && c.Size > 25f &&
                    (c.LastCasualtyDay < 0 || day - c.LastCasualtyDay > 45) &&
                    Hash01(day, c.In, c.Toward, 7) < (c.Size - 25f) * 0.0006f)
                {
                    c.LastCasualtyDay = day;
                    int dead = 3 + Mathf.RoundToInt(c.Size * 0.4f);
                    c.Casualties += dead;
                    legit.Record(c.Toward, day, "Casualties at a defended line in " + (names?.Name(c.In) ?? "a foreign state"),
                        "They were not sent, not armed and not ordered — and they are still counted against the state they were walking toward",
                        LegitimacySystem.Deltas(ownPopulation: -10f, foreignPopulations: -3f));
                    legit.Record(c.In, day, "Crowd casualties at our own line",
                        "A line held against unarmed people is a line that costs more than it defends",
                        LegitimacySystem.Deltas(ownPopulation: -8f, foreignPopulations: -12f, religiousAuthority: -8f));
                    (headlines ??= new List<string>()).Add(
                        $"{dead} dead at the line in {names?.Name(c.In)}. {names?.Name(c.Toward)} is blamed by its own population for a shot it did not fire.");
                }

                // RESOLUTION. A cornered government either bends or shoots; which one it does is
                // mostly a function of how little legitimacy it has left to lose.
                float govLegit = legit.Of(c.In).Get(Observer.OwnPopulation);

                if (c.Size > 40f && govLegit > 25f && Hash01(day, c.In, c.Toward, 11) < (c.Size - 40f) * 0.0010f)
                {
                    Concede(c, day, dip, legit, accession, names, ref headlines);
                    continue;
                }
                if (c.Size > 55f && govLegit <= 35f && Hash01(day, c.In, c.Toward, 23) < (c.Size - 55f) * 0.0009f)
                {
                    OrderForceAgainst(c, c.In, day, dip, legit, names, ref headlines);
                    continue;
                }
                if (c.DaysWithoutCause > 60)
                {
                    c.Outcome = CrowdOutcome.Faded;
                    c.EndedDay = day;
                    c.Ending = "conditions no longer held; the crowd went home";
                    // Nothing is gained and something is lost: a government that outlasts a crowd
                    // has proved to everyone watching that it can.
                    dip.ChangeRelation(c.Toward, c.In, -5f);
                    (headlines ??= new List<string>()).Add(
                        $"The crowds in {names?.Name(c.In)} have thinned. The government outlasted them.");
                }
            }

            return headlines ?? Empty;
        }

        // --- resolutions ------------------------------------------------------------------

        public void Concede(Crowd c, long day, DiplomacySystem dip, LegitimacySystem legit,
                     AccessionSystem accession, GeoWorldNames names, ref List<string> headlines)
        {
            c.Outcome = CrowdOutcome.Conceded;
            c.EndedDay = day;

            Invitation pending = null;
            if (accession != null)
                foreach (var inv in accession.Open)
                    if (inv.To == c.In && inv.From == c.Toward) { pending = inv; break; }

            if (pending != null)
            {
                // The government yields on the thing the crowd was in the street about. Note this
                // is NOT coercion by the inviter — nobody was pressured, a population moved — so it
                // carries none of §4's coercion penalty. That asymmetry is the entire point.
                pending.GovernmentConceded = true;
                pending.DecisionDay = day;
                c.Ending = "the government conceded accession to " + pending.BlocName;
            }
            else
            {
                dip.ChangeRelation(c.Toward, c.In, +20f);
                c.Ending = "the government gave ground rather than face them";
            }

            legit.Record(c.In, day, "Conceded to a crowd in the street",
                "A government that yields to its own people in public has told everyone where authority actually sits",
                LegitimacySystem.Deltas(ownPopulation: -10f, foreignGovernments: -6f, ownMilitary: -8f));
            legit.Record(c.Toward, day, (names?.Name(c.In) ?? "A state") + " gave ground to its own people",
                "Won without an army, without an order, and without anything the winner can take credit for",
                LegitimacySystem.Deltas(foreignPopulations: +5f, foreignGovernments: -4f));
            (headlines ??= new List<string>()).Add(
                $"The government of {names?.Name(c.In)} concedes — {c.Ending}.");
        }

        // RULE 2. Deliberately NOT wired to any UI — it is called when an AI government breaks, and
        // exists as a public method so that the cost of the player doing it is defined, testable,
        // and identical. Whoever fires pays this.
        public void OrderForceAgainst(Crowd c, int firedBy, long day, DiplomacySystem dip,
                                      LegitimacySystem legit, GeoWorldNames names, ref List<string> headlines)
        {
            c.Outcome = CrowdOutcome.FiredUpon;
            c.EndedDay = day;
            int dead = 20 + Mathf.RoundToInt(c.Size * 1.5f);
            c.Casualties += dead;
            c.Ending = $"the government fired on them; {dead} dead";

            legit.Record(firedBy, day, "Fired on a crowd",
                "There is no version of this that anybody forgets, and no observer that forgives it",
                LegitimacySystem.Deltas(ownPopulation: -15f, foreignPopulations: -25f,
                                        foreignGovernments: -12f, religiousAuthority: -20f,
                                        blocMembers: -10f, ownMilitary: -5f));
            // The state the crowd was walking toward is handed a win it did not ask for and cannot
            // disown — its own population is uneasy about having been the reason.
            if (firedBy != c.Toward)
            {
                legit.Record(c.Toward, day, "A crowd walking toward us was fired on in " + (names?.Name(c.In) ?? "a foreign state"),
                    "A victory delivered by other people's dead is still a victory, and it is still theirs",
                    LegitimacySystem.Deltas(ownPopulation: -4f, foreignPopulations: +10f));
                dip.ChangeRelation(c.Toward, c.In, -25f);
            }
            (headlines ??= new List<string>()).Add(
                $"{names?.Name(c.In)} fires on the crowd. {dead} dead. Nobody who watched it will forget which government gave the order.");
        }

        // RULE 1. Also deliberately unwired. If the player ever gets a button for this, the mechanic
        // is dead — asking a crowd that gathered FOR you to go home reads as exactly what it is.
        public void AttemptDisperse(Crowd c, int by, long day, LegitimacySystem legit, GeoWorldNames names)
        {
            legit.Record(by, day, "Asked a crowd in " + (names?.Name(c.In) ?? "a foreign state") + " to disperse",
                "They were there because of you; telling them to go home is the one thing that reads as betrayal",
                LegitimacySystem.Deltas(ownPopulation: -6f, foreignPopulations: -12f));
            c.Outcome = CrowdOutcome.Dispersed;
            c.EndedDay = day;
            c.Ending = "asked to go home by the state they had gathered for";
        }

        static readonly List<string> Empty = new();
    }
}
