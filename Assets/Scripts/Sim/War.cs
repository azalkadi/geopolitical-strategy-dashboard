using System.Collections.Generic;

namespace Meridian.Sim
{
    // Abstract interstate war: no unit-pushing, no territory exchange (borders are real Natural
    // Earth data and stay fixed) — a war is a contest of military strength and national
    // endurance, resolved through a drifting war score and mounting exhaustion, ending in a
    // negotiated peace. Same honest-simulation principle as everything else in Sim/: military
    // strength derives from the numbers the player already manages (GDP, defense spending,
    // readiness), war costs flow back into those same numbers (growth, treasury, approval,
    // mood), and nothing is a separate bolted-on "war stat".
    public class War
    {
        public int Attacker;         // country index
        public int Defender;
        public long StartDay;
        // Positive favors the attacker, negative the defender. ±100 is total collapse; peace
        // demands unlock at ±40 (see CanDemandConcessions).
        public float Score;
        public float ExhaustionAttacker; // 0-100, rises every day at war
        public float ExhaustionDefender;
        public long LastPlayerPeaceOfferDay = long.MinValue;
    }

    public class WarSystem
    {
        public List<War> Active = new();
        uint rng = 0x77aa11bb;

        // Set once by MapRenderer after the union registry is built. When present, declaring war
        // on a military-alliance member turns that member's whole bloc against the aggressor
        // (mutual defence) — see Declare. Not serialized; rebuilt with the registry on load.
        [Newtonsoft.Json.JsonIgnore] public UnionSystem Unions;

        public const float DeclareRelationCeiling = 35f; // can only declare on countries you're cold with
        public const float ConcessionScoreThreshold = 40f;
        public const double ReparationsGdpFraction = 0.02;
        public const long PeaceOfferCooldownDays = 60;

        // Effective military strength — annualized military budget (GDP × defense share)
        // scaled by readiness. Square-rooted so a 10x economy is ~3x the war weight: giants
        // still win, but not instantly, and a well-funded middle power can bleed one.
        public static double Strength(EconomyState e, NationalState n) =>
            System.Math.Sqrt(System.Math.Max(0.01, e.Gdp * (n.DefenseSpending / 100.0))) * (0.5 + n.ReadinessIndex / 100.0);

        public bool AtWar(int country)
        {
            foreach (var w in Active)
                if (w.Attacker == country || w.Defender == country) return true;
            return false;
        }

        public War WarBetween(int a, int b)
        {
            foreach (var w in Active)
                if ((w.Attacker == a && w.Defender == b) || (w.Attacker == b && w.Defender == a)) return w;
            return null;
        }

        public List<War> WarsOf(int country)
        {
            var list = new List<War>();
            foreach (var w in Active)
                if (w.Attacker == country || w.Defender == country) list.Add(w);
            return list;
        }

        // Declares a war. Re-validates eligibility itself (not just at the caller) — a UI
        // button built when the declaration was legal can be clicked after relations warmed or
        // an agreement was signed, and must fail closed rather than start an illegal war.
        public War Declare(int attacker, int defender, long day, DiplomacySystem dip, NationalSystem nat)
        {
            if (!CanDeclare(attacker, defender, dip)) return null;
            var w = new War { Attacker = attacker, Defender = defender, StartDay = day };
            Active.Add(w);

            // Diplomatic fallout: relations to the floor, aggressor loses standing worldwide.
            dip.ChangeRelation(attacker, defender, -100f);
            var aggressorNat = nat.States[attacker];
            aggressorNat.InternationalStanding = Clampf(aggressorNat.InternationalStanding - 6f, 0f, 100f);

            // Mutual defence: attacking a military-alliance member turns that member's whole bloc
            // against the aggressor — every ally's relations with the aggressor drop hard, and the
            // aggressor takes an extra worldwide standing hit for striking an allied nation. This
            // is why invading a NATO/CSTO member is a very different proposition than invading a
            // non-aligned one.
            if (Unions != null)
            {
                var allies = Unions.MilitaryAlliesOf(defender);
                foreach (int ally in allies)
                    if (ally != attacker) dip.ChangeRelation(attacker, ally, -25f);
                if (allies.Count > 0)
                    aggressorNat.InternationalStanding = Clampf(aggressorNat.InternationalStanding - 4f, 0f, 100f);
            }

            // Rally-round-the-flag: both governments get a short-term approval bump; the
            // exhaustion mechanics take it all back (and more) if the war drags on.
            aggressorNat.ApprovalRating = Clampf(aggressorNat.ApprovalRating + 3f, 0f, 100f);
            var defenderNat = nat.States[defender];
            defenderNat.ApprovalRating = Clampf(defenderNat.ApprovalRating + 5f, 0f, 100f);
            return w;
        }

        public bool CanDeclare(int attacker, int defender, DiplomacySystem dip) =>
            attacker != defender
            && WarBetween(attacker, defender) == null
            && dip.GetRelation(attacker, defender) < DeclareRelationCeiling
            && !dip.HasAgreement(attacker, defender);

        // Advances every active war one day. Returns messages for wars that ENDED this tick
        // (AI-negotiated peaces), for the UI toast feed.
        public List<string> TickAll(EconomySystem econ, NationalSystem nat, DiplomacySystem dip, GeoWorldNames names, long day)
        {
            var ended = new List<string>();
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var w = Active[i];
                var ea = econ.States[w.Attacker]; var na = nat.States[w.Attacker];
                var ed = econ.States[w.Defender]; var nd = nat.States[w.Defender];

                double sa = Strength(ea, na), sd = Strength(ed, nd);
                float advantage = (float)((sa - sd) / System.Math.Max(0.01, sa + sd));
                w.Score = Clampf(w.Score + advantage * 0.45f + Noise() * 0.25f, -100f, 100f);

                // Exhaustion accrues daily, faster for the losing side; readiness erodes.
                w.ExhaustionAttacker = Clampf(w.ExhaustionAttacker + 0.05f + (w.Score < 0 ? 0.04f : 0f), 0f, 100f);
                w.ExhaustionDefender = Clampf(w.ExhaustionDefender + 0.05f + (w.Score > 0 ? 0.04f : 0f), 0f, 100f);
                na.ReadinessIndex = Clampf(na.ReadinessIndex - 0.02f, 5f, 100f);
                nd.ReadinessIndex = Clampf(nd.ReadinessIndex - 0.02f, 5f, 100f);

                // War economy: growth drags, war spending bleeds the treasury daily
                // (~1.5% GDP annualized on top of normal budgets), morale sags with exhaustion.
                ea.GrowthRate = Clampf(ea.GrowthRate - 0.004f, -15f, 15f);
                ed.GrowthRate = Clampf(ed.GrowthRate - 0.004f, -15f, 15f);
                ea.Treasury -= ea.Gdp * 0.015 / 365.0;
                ed.Treasury -= ed.Gdp * 0.015 / 365.0;
                na.PublicMood = Clampf(na.PublicMood - w.ExhaustionAttacker * 0.0012f, 0f, 100f);
                nd.PublicMood = Clampf(nd.PublicMood - w.ExhaustionDefender * 0.0012f, 0f, 100f);
                na.ApprovalRating = Clampf(na.ApprovalRating - w.ExhaustionAttacker * 0.0010f, 0f, 100f);
                nd.ApprovalRating = Clampf(nd.ApprovalRating - w.ExhaustionDefender * 0.0010f, 0f, 100f);

                // AI-side auto-resolution (only when the PLAYER isn't a belligerent — player
                // wars end only through the player's own peace actions, or total collapse):
                bool playerInvolved = w.Attacker == PlayerState.CountryIndex || w.Defender == PlayerState.CountryIndex;
                if (!playerInvolved)
                {
                    // Decisive score, or both sides worn out → negotiated end.
                    if (w.Score >= ConcessionScoreThreshold + 20f || w.Score <= -(ConcessionScoreThreshold + 20f))
                    {
                        bool attackerWon = w.Score > 0;
                        ApplyConcessions(w, attackerWon, econ, nat, dip);
                        ended.Add($"{names.Name(attackerWon ? w.Attacker : w.Defender)} has won its war against {names.Name(attackerWon ? w.Defender : w.Attacker)} — reparations imposed.");
                        Active.RemoveAt(i);
                        continue;
                    }
                    if (w.ExhaustionAttacker > 70f && w.ExhaustionDefender > 70f)
                    {
                        EndWhitePeace(w, dip);
                        ended.Add($"The war between {names.Name(w.Attacker)} and {names.Name(w.Defender)} has ended in an exhausted stalemate.");
                        Active.RemoveAt(i);
                        continue;
                    }
                }
                else
                {
                    // Total collapse ends even a player war without consent — losing 100-0 is
                    // not a negotiation.
                    if (w.Score >= 100f || w.Score <= -100f)
                    {
                        bool attackerWon = w.Score > 0;
                        ApplyConcessions(w, attackerWon, econ, nat, dip);
                        ended.Add($"Total victory: {names.Name(attackerWon ? w.Attacker : w.Defender)} has crushed {names.Name(attackerWon ? w.Defender : w.Attacker)}.");
                        Active.RemoveAt(i);
                        continue;
                    }
                }
            }
            return ended;
        }

        // Player peace actions ------------------------------------------------------------

        public bool PlayerCanDemandConcessions(War w)
        {
            bool playerIsAttacker = w.Attacker == PlayerState.CountryIndex;
            float scoreFromPlayerSide = playerIsAttacker ? w.Score : -w.Score;
            return scoreFromPlayerSide >= ConcessionScoreThreshold;
        }

        // Winner-takes-reparations peace, initiated by the player from a winning position.
        // The Active.Contains guard matters: the UI holds a captured War reference, and TickAll
        // can auto-resolve (and remove) that exact war before the click lands — without the
        // guard, concessions would apply a second time to an already-ended war.
        public string PlayerDemandConcessions(War w, EconomySystem econ, NationalSystem nat, DiplomacySystem dip, GeoWorldNames names)
        {
            if (!Active.Contains(w)) return null;
            if (!PlayerCanDemandConcessions(w)) return null;
            bool playerIsAttacker = w.Attacker == PlayerState.CountryIndex;
            ApplyConcessions(w, playerIsAttacker, econ, nat, dip);
            Active.Remove(w);
            int loser = playerIsAttacker ? w.Defender : w.Attacker;
            return $"{names.Name(loser)} capitulates — reparations paid, the war is over.";
        }

        // White peace, initiated by the player. The AI side accepts if it isn't clearly
        // winning, or if it's simply worn out. 60-day cooldown between offers.
        public string PlayerOfferWhitePeace(War w, DiplomacySystem dip, GeoWorldNames names, long day)
        {
            if (!Active.Contains(w)) return null; // same stale-UI-reference guard as above
            if (day - w.LastPlayerPeaceOfferDay < PeaceOfferCooldownDays) return null;
            w.LastPlayerPeaceOfferDay = day;

            bool playerIsAttacker = w.Attacker == PlayerState.CountryIndex;
            float aiScore = playerIsAttacker ? -w.Score : w.Score;   // positive = AI winning
            float aiExhaustion = playerIsAttacker ? w.ExhaustionDefender : w.ExhaustionAttacker;
            bool accepts = aiScore < ConcessionScoreThreshold * 0.75f || aiExhaustion > 55f;
            if (!accepts)
                return "Peace offer REJECTED — they believe they can win this.";

            EndWhitePeace(w, dip);
            Active.Remove(w);
            int other = playerIsAttacker ? w.Defender : w.Attacker;
            return $"White peace signed with {names.Name(other)}. No victor, no reparations.";
        }

        void ApplyConcessions(War w, bool attackerWon, EconomySystem econ, NationalSystem nat, DiplomacySystem dip)
        {
            int winner = attackerWon ? w.Attacker : w.Defender;
            int loser = attackerWon ? w.Defender : w.Attacker;
            var ew = econ.States[winner]; var el = econ.States[loser];
            var nw = nat.States[winner]; var nl = nat.States[loser];

            double reparations = el.Gdp * ReparationsGdpFraction;
            el.Treasury -= reparations;
            ew.Treasury += reparations;
            nw.ApprovalRating = Clampf(nw.ApprovalRating + 8f, 0f, 100f);
            nl.ApprovalRating = Clampf(nl.ApprovalRating - 10f, 0f, 100f);
            nl.PublicMood = Clampf(nl.PublicMood - 6f, 0f, 100f);
            // Postwar relations: hostile but no longer at-war-floor; time heals via drift.
            dip.ChangeRelation(w.Attacker, w.Defender, +15f);
        }

        void EndWhitePeace(War w, DiplomacySystem dip)
        {
            dip.ChangeRelation(w.Attacker, w.Defender, +20f);
        }

        float Noise()
        {
            uint x = rng;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            rng = x;
            return (x / (float)uint.MaxValue) * 2f - 1f;
        }

        static float Clampf(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }

    // Tiny indirection so WarSystem can produce human-readable end-of-war messages without
    // depending on Meridian.Geo (Sim/ stays engine- and geo-agnostic).
    public class GeoWorldNames
    {
        readonly System.Func<int, string> lookup;
        public GeoWorldNames(System.Func<int, string> lookup) { this.lookup = lookup; }
        public string Name(int idx) => lookup(idx);
    }
}
