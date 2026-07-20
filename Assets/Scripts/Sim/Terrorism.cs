using System.Collections.Generic;
using UnityEngine;

namespace Meridian.Sim
{
    // Internal terrorism / insurgency — the "terrorism as an internal mechanic" piece of the
    // Conflicts vision pillar (docs/obsidian-vault/Vision/Conflicts, Terrorism and Military
    // Realism.md). Deliberately NOT a reskin of interstate war: this is a domestic threat that
    // GROWS inside a country from real grievances and is fought internally, not a second country
    // to shoot at.
    //
    // The honest causal model: threat rises with grievance (political repression → low
    // FreedomSpeech, a miserable population → low PublicMood, mass joblessness → high
    // unemployment) and is held down by security capacity (defence spending + readiness) and the
    // player's counter-terror operations. When it's high it periodically strikes — hitting
    // growth, the treasury, public mood and approval. The nuance the vision asks for: a
    // heavy-handed crackdown in a low-freedom state is LESS effective and breeds fresh grievance,
    // exactly the real counterinsurgency trap — so the durable fix is addressing root causes
    // (freedoms, jobs, mood), not just force.
    //
    // State lives on NationalState.TerrorThreat (so it serializes with everything else); this
    // system is a stateless ticker over the national/economic states.
    public class TerrorismSystem
    {
        public const float AttackThreshold = 35f;

        // Running count of attacks fired this game (all countries) — diagnostics read it to
        // confirm the attack path executes, since attacks surface as UI toasts, not log lines.
        public int TotalAttacks;

        // How combustible a country is right now, 0-100. Repression is the dominant driver (an
        // unfree state manufacturing grievance), then a miserable population and mass
        // joblessness. A free, content, employed society sits near zero.
        public static float Grievance(EconomyState e, NationalState n)
        {
            float g = (60f - n.FreedomSpeech) * 0.5f
                    + (55f - n.PublicMood) * 0.35f
                    + Mathf.Max(0f, e.Unemployment - 8f) * 1.5f;
            return Mathf.Clamp(g, 0f, 100f);
        }

        // Seed each country's starting threat from its initial grievance, so genuinely unstable
        // countries begin with a simmering insurgency rather than everyone starting at zero.
        public static void SeedThreat(EconomySystem econ, NationalSystem nat)
        {
            for (int i = 0; i < nat.States.Count && i < econ.States.Count; i++)
                nat.States[i].TerrorThreat = Mathf.Clamp(Grievance(econ.States[i], nat.States[i]) * 0.4f, 0f, 60f);
        }

        // Advances every country's threat one day and fires attacks. Returns headlines for
        // attacks worth surfacing (the player's own country, or a major economy).
        public List<string> TickAll(EconomySystem econ, NationalSystem nat, GeoWorldNames names, int playerIndex, long day)
        {
            List<string> headlines = null;
            for (int i = 0; i < nat.States.Count && i < econ.States.Count; i++)
            {
                var e = econ.States[i];
                var n = nat.States[i];

                float grievance = Grievance(e, n);
                float security = n.DefenseSpending * 2f + n.ReadinessIndex * 0.05f;
                float target = Mathf.Clamp(grievance - security, 0f, 100f);
                n.TerrorThreat = Mathf.Clamp(n.TerrorThreat + (target - n.TerrorThreat) * 0.010f, 0f, 100f);

                if (n.TerrorThreat <= AttackThreshold) continue;

                // Attack odds scale with how far past the threshold the threat sits: ~2%/day
                // just over the line (an attack every ~7 weeks), rising to ~10%/day at maximum
                // threat (roughly weekly) — a real, escalating danger, not a rare curiosity.
                uint h = Hash((uint)i, (uint)day);
                if (h % 1000u < (uint)((n.TerrorThreat - AttackThreshold) * 4f))
                {
                    e.GrowthRate = Clampf(e.GrowthRate - 0.3f, -15f, 15f);
                    e.Treasury -= e.Gdp * 0.002;
                    n.PublicMood = Clampf(n.PublicMood - 3f, 0f, 100f);
                    n.ApprovalRating = Clampf(n.ApprovalRating - 2f, 0f, 100f);
                    n.TerrorThreat = Clampf(n.TerrorThreat + 2f, 0f, 100f); // a successful strike emboldens the group
                    TotalAttacks++;
                    if (i == playerIndex || e.Gdp > 400.0)
                        (headlines ??= new List<string>()).Add(
                            $"{names.Name(i)}: a militant attack strikes the economy and rattles the public.");
                }
            }
            return headlines ?? Empty;
        }
        static readonly List<string> Empty = new();

        // Player counter-terror operation: spend treasury to cut the threat now. Effectiveness
        // scales with legitimacy (rule of law): a well-governed, free state degrades the network
        // cleanly; a repressive one relies on brute force that works less well and stings the
        // public, nudging grievance back up — the deliberate real-world nuance.
        public static string LaunchOperation(EconomyState e, NationalState n)
        {
            double cost = System.Math.Max(0.5, e.Gdp * 0.004);
            e.Treasury -= cost;
            float legitimacy = Mathf.Clamp01(n.FreedomSpeech / 60f);
            float cut = 8f + legitimacy * 10f; // 8 (repressive) .. 18 (legitimate)
            n.TerrorThreat = Clampf(n.TerrorThreat - cut, 0f, 100f);
            bool heavyHanded = legitimacy < 0.4f;
            if (heavyHanded) n.PublicMood = Clampf(n.PublicMood - 2f, 0f, 100f);
            return $"Counter-terror operation (${cost:0.0}B): threat cut by {cut:0}. " +
                   (heavyHanded ? "Heavy-handed tactics unsettle the public — force alone won't hold it down."
                                : "Conducted within the law; addressing grievances keeps it down for good.");
        }

        static float Clampf(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        static uint Hash(uint a, uint b)
        {
            unchecked { uint h = 0x811c9dc5u; h = (h ^ a) * 0x01000193u; h = (h ^ b) * 0x01000193u; h ^= h >> 15; return h; }
        }
    }
}
