using System.Collections.Generic;
using Meridian.Geo;

namespace Meridian.Sim
{
    // Bilateral diplomacy: a symmetric relations score (0-100) for every country pair, seeded
    // from real geography (shared continent/subregion reads as familiarity) plus deterministic
    // per-pair noise, drifting slowly back toward its seeded baseline so player actions matter
    // but don't permanently rewrite the world with one aid check.
    //
    // Player-facing actions (aid / trade agreement / denounce) route through here and touch the
    // same EconomyState/NationalState numbers everything else uses — an agreement genuinely
    // raises both signatories' exports, aid genuinely costs treasury.
    public class DiplomacySystem
    {
        // Public (and settable) so the save system can serialize/deserialize the whole object
        // with Newtonsoft and get an exact replica — see Sim/SaveLoad.cs.
        public int Count { get; set; }
        public float[] Relations;      // upper-triangle packed symmetric matrix
        public float[] Baselines;      // what each pair drifts back toward
        public HashSet<long> Agreements = new(); // packed (min,max) pairs with an active trade agreement

        public const float AgreementThreshold = 65f;
        public const float AgreementExportBonus = 0.015f; // per agreement, both sides

        // Cooldown bookkeeping so the player can't spam +12 aid every tick. Keyed by pair.
        public Dictionary<long, long> LastActionDay = new();
        public const long ActionCooldownDays = 90;

        public static DiplomacySystem Seed(IReadOnlyList<Country> countries)
        {
            int n = countries.Count;
            var sys = new DiplomacySystem
            {
                Count = n,
                Relations = new float[n * (n - 1) / 2],
                Baselines = new float[n * (n - 1) / 2],
            };

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    float r = 50f;
                    if (!string.IsNullOrEmpty(countries[i].Continent) && countries[i].Continent == countries[j].Continent) r += 8f;
                    if (!string.IsNullOrEmpty(countries[i].Subregion) && countries[i].Subregion == countries[j].Subregion) r += 10f;
                    // Deterministic per-pair noise from the ISO codes, ±15.
                    r += (PairHash(countries[i].IsoA3, countries[j].IsoA3) % 3100u) / 100f - 15f;
                    r = Clampf(r, 5f, 95f);
                    int idx = sys.PackIndex(i, j);
                    sys.Relations[idx] = r;
                    sys.Baselines[idx] = r;
                }
            }
            return sys;
        }

        public float GetRelation(int a, int b)
        {
            if (a == b || a < 0 || b < 0 || a >= Count || b >= Count) return 100f;
            return Relations[PackIndex(a, b)];
        }

        public void ChangeRelation(int a, int b, float delta)
        {
            if (a == b || a < 0 || b < 0 || a >= Count || b >= Count) return;
            int idx = PackIndex(a, b);
            Relations[idx] = Clampf(Relations[idx] + delta, 0f, 100f);
        }

        public bool HasAgreement(int a, int b) => Agreements.Contains(PairKey(a, b));

        public bool CanAct(int a, int b, long day) =>
            !LastActionDay.TryGetValue(PairKey(a, b), out long last) || day - last >= ActionCooldownDays;

        public void MarkActed(int a, int b, long day) => LastActionDay[PairKey(a, b)] = day;

        // --- player actions -------------------------------------------------------------

        // Foreign aid: 0.05% of the donor's GDP (min $0.2B). Warmth is bought, standing earned.
        public string SendAid(int from, int to, EconomyState donor, NationalState donorNat, long day)
        {
            double cost = System.Math.Max(0.2, donor.Gdp * 0.0005);
            donor.Treasury -= cost;
            ChangeRelation(from, to, +12f);
            donorNat.InternationalStanding = Clampf(donorNat.InternationalStanding + 1.5f, 0f, 100f);
            MarkActed(from, to, day);
            return $"Aid package delivered (${cost:0.0}B) — relations improved.";
        }

        // Trade agreement: needs warm relations, permanent (until game reset), boosts BOTH
        // economies' exports via EconomyState.TradeAgreementExportBonus.
        public string SignAgreement(int a, int b, EconomyState ea, EconomyState eb, long day)
        {
            if (HasAgreement(a, b)) return "An agreement is already in force.";
            if (GetRelation(a, b) < AgreementThreshold) return null;
            Agreements.Add(PairKey(a, b));
            ea.TradeAgreementExportBonus += AgreementExportBonus;
            eb.TradeAgreementExportBonus += AgreementExportBonus;
            ChangeRelation(a, b, +5f);
            MarkActed(a, b, day);
            return "Trade agreement signed — exports rise on both sides.";
        }

        // Denounce: cheap domestic applause, real international cost.
        public string Denounce(int from, int to, NationalState fromNat, long day)
        {
            ChangeRelation(from, to, -15f);
            fromNat.ApprovalRating = Clampf(fromNat.ApprovalRating + 1.5f, 0f, 100f);
            fromNat.InternationalStanding = Clampf(fromNat.InternationalStanding - 1f, 0f, 100f);
            MarkActed(from, to, day);
            return "Denunciation delivered — the base loves it; embassies do not.";
        }

        // Daily decay toward baseline. Only the player's own rows matter for gameplay right
        // now, but the full matrix stays coherent for future AI-vs-AI diplomacy.
        public void TickAll()
        {
            for (int i = 0; i < Relations.Length; i++)
                Relations[i] += (Baselines[i] - Relations[i]) * 0.001f;
        }

        // Top-N friendliest / frostiest countries from `a`'s perspective (excluding itself).
        public List<(int index, float relation)> RankedFor(int a, bool friendliest, int topN)
        {
            var list = new List<(int, float)>(Count - 1);
            for (int j = 0; j < Count; j++)
                if (j != a) list.Add((j, GetRelation(a, j)));
            list.Sort((x, y) => friendliest ? y.Item2.CompareTo(x.Item2) : x.Item2.CompareTo(y.Item2));
            if (list.Count > topN) list.RemoveRange(topN, list.Count - topN);
            return list;
        }

        int PackIndex(int a, int b)
        {
            if (a > b) { (a, b) = (b, a); }
            // Row-major upper triangle: index = a*(2n-a-1)/2 + (b-a-1)
            return a * (2 * Count - a - 1) / 2 + (b - a - 1);
        }

        static long PairKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        static uint PairHash(string x, string y)
        {
            unchecked
            {
                uint h = 0x811c9dc5;
                // Order-independent: hash both orderings and combine symmetrically.
                foreach (char c in x) { h ^= (byte)c; h *= 0x01000193; }
                uint h2 = 0x811c9dc5;
                foreach (char c in y) { h2 ^= (byte)c; h2 *= 0x01000193; }
                return h ^ h2;
            }
        }

        static float Clampf(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
