using System.Collections.Generic;
using UnityEngine;
using Meridian.Geo;

namespace Meridian.Sim
{
    // ACCESSION — Consequence Engine §4 (see docs/obsidian-vault/Vision/Accession and the Crowd.md).
    //
    // Pillar 2: consent scales, force does not. A state that joins voluntarily brings its army,
    // institutions and legitimacy; a state taken by force is held by force forever. This is the
    // machinery of joining voluntarily, and its whole design is that the CHEAP path (pressure) and
    // the DURABLE path (generous terms) pull in opposite directions:
    //
    //   - Coercion RAISES short-term acceptance and PERMANENTLY damages the inviter's legitimacy
    //     with every observing state, including ones never invited.
    //   - Generous terms raise acceptance AND genuinely hollow out the bloc later — an army the
    //     bloc doesn't get, a member that can't be called to war, law the union can't touch.
    //
    // So a bloc assembled by consent is large, legitimate and militarily hollow; one assembled by
    // pressure is obedient and friendless. Neither is "correct" — that tension is the game.
    //
    // Refusal is PERMANENT MEMORY, never a cooldown: re-inviting a state that said no is harder
    // every single time, forever.

    public enum AccessionStatus { Pending, Accepted, Refused, Withdrawn }

    public class AccessionTerms
    {
        public bool ArmyRetained;         // keeps its army under its own command
        public bool FlagRetained;         // keeps flag/name/symbols
        public bool LawRetained;          // keeps its own legal system
        public bool GuaranteesPreserved;  // keeps OUTSIDE security relationships — strongest lever
        public float VoteWeight = 1f;     // 1.0 == a population-fair share

        public int GenerosityScore()
        {
            int s = 0;
            if (GuaranteesPreserved) s += 15;
            if (ArmyRetained) s += 12;
            if (LawRetained) s += 10;
            if (FlagRetained) s += 8;
            s += VoteWeight >= 1f ? 6 : -10;
            return s;
        }
    }

    public class Invitation
    {
        public int From;
        public int To;
        public string BlocName = "";
        public string TargetIso = "";
        public long OfferedDay;
        public long DecisionDay;
        public AccessionTerms Terms = new();
        public bool PublicDeadline;
        public bool FramedAsInevitable;

        // Snapshots taken at offer time so "did the inviter apply pressure DURING the evaluation"
        // is measurable rather than guessed.
        public float InviterTariffAtOffer;
        public float InviterReadinessAtOffer;

        public float ImpliedCoercion;     // 0..1, derived every tick — never authored
        // Set by CrowdSystem, not by the inviter — a population in the street is not a lever the
        // player pulls. Capped at +30 by the score: a crowd tilts a decision, it does not make it.
        public float CrowdPressure;
        // A government that yielded to its own people. Accepts regardless of score, and carries
        // NO coercion penalty for the inviter, because the inviter did not do it.
        public bool GovernmentConceded;

        public AccessionStatus Status = AccessionStatus.Pending;
        public string Reason = "";
        public float FinalScore;
    }

    // Permanent. No expiry, no cooldown — the whole point is that it is remembered.
    public class RefusalRecord
    {
        public int Inviter;
        public int Target;
        public long Day;
        public float CoercionAtOffer;
        public string Why = "";
    }

    public class AccessionSystem
    {
        public List<Invitation> Open = new();
        public List<Invitation> Resolved = new();     // never pruned
        public List<RefusalRecord> Refusals = new();  // never pruned

        // --- offering -------------------------------------------------------------------

        // Large states deliberate for years; small ones answer in months. The wait is the first
        // place the player feels that consent is slow (Pillar 2).
        public static long EvaluationDays(double population)
        {
            double m = System.Math.Max(1.0, population / 1_000_000.0);
            return (long)Mathf.Clamp(90f + 200f * Mathf.Log10((float)m), 90f, 1095f);
        }

        public bool HasOpenInvitation(int to) => Open.Exists(i => i.To == to);

        public int RefusalCount(int inviter, int target)
        {
            int n = 0;
            foreach (var r in Refusals) if (r.Inviter == inviter && r.Target == target) n++;
            return n;
        }

        public Invitation Offer(int from, int to, string blocName, string targetIso,
                                AccessionTerms terms, bool publicDeadline, bool framedAsInevitable,
                                long day, EconomyState targetEcon, EconomyState inviterEcon, NationalState inviterNat)
        {
            var inv = new Invitation
            {
                From = from, To = to, BlocName = blocName, TargetIso = targetIso,
                OfferedDay = day,
                DecisionDay = day + EvaluationDays(targetEcon.Population),
                Terms = terms ?? new AccessionTerms(),
                PublicDeadline = publicDeadline,
                FramedAsInevitable = framedAsInevitable,
                InviterTariffAtOffer = inviterEcon.TaxTariff,
                InviterReadinessAtOffer = inviterNat.ReadinessIndex,
            };
            Open.Add(inv);
            return inv;
        }

        // --- coercion (derived, recomputed every tick) -----------------------------------

        public float ComputeCoercion(Invitation inv, EconomyState inviterEcon, NationalState inviterNat,
                                     WarSystem wars, long day)
        {
            float c = 0f;
            if (inv.PublicDeadline) c += 0.20f;
            if (inv.FramedAsInevitable) c += 0.25f;
            if (wars != null && wars.AtWar(inv.From)) c += 0.30f;
            if (inviterEcon.TaxTariff > inv.InviterTariffAtOffer + 0.5f) c += 0.25f;         // economic pressure
            if (inviterNat.ReadinessIndex > inv.InviterReadinessAtOffer + 5f) c += 0.20f;    // mobilisation
            // Re-inviting soon after a refusal is itself read as pressure.
            foreach (var r in Refusals)
                if (r.Inviter == inv.From && r.Target == inv.To && day - r.Day < 730) { c += 0.15f; break; }
            return Mathf.Clamp01(c);
        }

        // --- resolution -------------------------------------------------------------------

        public float AcceptanceScore(Invitation inv, DiplomacySystem dip, LegitimacySystem legit,
                                     EconomyState inviterEcon, EconomyState targetEcon)
        {
            // No free floor. Giving up sovereignty starts at zero and must be EARNED — an early
            // version started at 25 and handed out ~70 points before relations were even
            // consulted, with the result that a hostile rival accepted annexation-by-invitation.
            // Relations are now the dominant term.
            float score = 0f;
            float relations = dip.GetRelation(inv.From, inv.To);
            score += 0.80f * relations;

            // A state that is actively hostile does not join your union at any price.
            if (relations < 25f) score -= 30f;

            var led = legit?.Of(inv.From);
            if (led != null)
            {
                score += 0.15f * led.Get(Observer.ForeignPopulations); // what their PEOPLE think of you
                score += 0.10f * led.Get(Observer.ForeignGovernments); // what their GOVERNMENT thinks
            }
            else score += 12f; // no ledger: neutral stand-in

            double tpc = System.Math.Max(1.0, targetEcon.GdpPerCapita);
            float prosperityPull = Mathf.Clamp((float)((inviterEcon.GdpPerCapita / tpc) - 1.0) * 8f, -10f, 20f);
            score += prosperityPull;

            score += inv.Terms.GenerosityScore();
            score += Mathf.Min(30f, inv.CrowdPressure);          // §6: the street, not the state
            score += inv.ImpliedCoercion * 25f;                  // works NOW, costs forever
            score -= 20f * RefusalCount(inv.From, inv.To);       // permanent, cumulative
            return score;
        }

        // Advances every open invitation. Returns headlines. Accepting actually mutates bloc
        // membership through UnionSystem, which is why §4 step 0 (mutable membership) had to
        // land first.
        public List<string> TickAll(long day, EconomySystem econ, NationalSystem nat, DiplomacySystem dip,
                                    LegitimacySystem legit, UnionSystem unions, WarSystem wars,
                                    IReadOnlyList<Country> countries, GeoWorldNames names)
        {
            List<string> headlines = null;
            for (int i = Open.Count - 1; i >= 0; i--)
            {
                var inv = Open[i];
                if (inv.From < 0 || inv.From >= econ.States.Count || inv.To < 0 || inv.To >= econ.States.Count) { Open.RemoveAt(i); continue; }

                // Pressure applied AFTER the offer still counts — that is what makes this honest.
                inv.ImpliedCoercion = ComputeCoercion(inv, econ.States[inv.From], nat.States[inv.From], wars, day);
                if (day < inv.DecisionDay) continue;

                float score = AcceptanceScore(inv, dip, legit, econ.States[inv.From], econ.States[inv.To]);
                inv.FinalScore = score;
                bool accepted = score > 50f || inv.GovernmentConceded;

                // The permanent price of pressure — charged whatever the answer, and visible to
                // every observing state, not just the one that was pressured.
                if (inv.ImpliedCoercion > 0.05f)
                    legit?.Record(inv.From, day, "Pressured a state during an accession offer",
                        "Coercing a state into a union is remembered by every state that watched it happen",
                        LegitimacySystem.Deltas(
                            foreignPopulations: -8f * inv.ImpliedCoercion,
                            foreignGovernments: -12f * inv.ImpliedCoercion,
                            blocMembers: -6f * inv.ImpliedCoercion,
                            religiousAuthority: -4f * inv.ImpliedCoercion));

                if (accepted)
                {
                    unions?.AddMember(inv.BlocName, inv.TargetIso, countries, econ, nat);
                    // A member admitted with preserved guarantees keeps its outside security
                    // relationships — so the bloc's mutual defence does NOT extend to it. The
                    // concession that bought the yes is the concession that hollows the bloc.
                    if (inv.Terms.GuaranteesPreserved)
                    {
                        var bloc = unions?.FindBloc(inv.BlocName);
                        if (bloc != null && !bloc.GuaranteedMembers.Contains(inv.TargetIso))
                            bloc.GuaranteedMembers.Add(inv.TargetIso);
                    }
                    inv.Status = AccessionStatus.Accepted;
                    inv.Reason = inv.GovernmentConceded
                        ? $"government conceded to a crowd (score was {score:0})"
                        : $"accepted at score {score:0}";
                    dip.ChangeRelation(inv.From, inv.To, +15f);
                    // The reward is for CONSENT, so it is scaled by how freely the yes was given.
                    // A state dragged in under maximum pressure earns the inviter nothing at all —
                    // otherwise coercion is simply profitable, which inverts the entire design.
                    float consent = 1f - inv.ImpliedCoercion;
                    legit?.Record(inv.From, day, $"{names?.Name(inv.To) ?? "A state"} joined by consent",
                        consent > 0.75f
                            ? "A state that joins voluntarily is worth more than one that is taken"
                            : "A state that joined under pressure impresses nobody",
                        LegitimacySystem.Deltas(ownPopulation: +3f * consent, foreignPopulations: +4f * consent,
                                                foreignGovernments: +3f * consent, blocMembers: +4f * consent));
                    (headlines ??= new List<string>()).Add(
                        $"{names?.Name(inv.To)} accepts accession to {inv.BlocName}.");
                }
                else
                {
                    Refusals.Add(new RefusalRecord
                    {
                        Inviter = inv.From, Target = inv.To, Day = day,
                        CoercionAtOffer = inv.ImpliedCoercion,
                        Why = $"refused at score {score:0}",
                    });
                    inv.Status = AccessionStatus.Refused;
                    inv.Reason = $"refused at score {score:0}";
                    legit?.Record(inv.From, day, $"{names?.Name(inv.To) ?? "A state"} refused accession",
                        "A public refusal reads as a limit on the inviter's authority",
                        LegitimacySystem.Deltas(ownPopulation: -3f, foreignGovernments: -2f));
                    (headlines ??= new List<string>()).Add(
                        $"{names?.Name(inv.To)} refuses accession to {inv.BlocName}.");
                }

                Resolved.Add(inv);
                Open.RemoveAt(i);
            }
            return headlines ?? Empty;
        }
        static readonly List<string> Empty = new();
    }
}
