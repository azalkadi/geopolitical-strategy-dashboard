using System.Collections.Generic;
using Meridian.Geo;

namespace Meridian.Sim
{
    // Lightweight derived/single-lever systems for the 5 ministry categories that previously
    // showed "coming soon" placeholders. Follows the same honest-simulation principle as
    // EconomyState: every number here is computed from real inputs (GDP rank, growth,
    // unemployment, trade, spending levers) rather than fabricated. Each category gets at most
    // one adjustable lever (mirroring Economy's tax/rate sliders); everything else is a
    // slow-drifting derived index so changes feel like consequences, not random noise.
    public class NationalState
    {
        public float ApprovalRating;        // Politics: 0-100, government approval
        public float DefenseSpending;       // Military: % of GDP, adjustable lever
        public float ReadinessIndex;        // Military: 0-100, drifts toward a spending-driven target
        public float InternationalStanding; // Diplomacy: 0-100 composite
        public float PublicMood;            // Society: 0-100, daily-life conditions (distinct from approval)
        public float ResearchSpending;      // Technology: % of GDP, adjustable lever
        public float InnovationIndex;       // Technology: 0-100, drifts toward a spending/GDP-driven target

        // Real government type for a curated set of countries (see CountryProfiles); the first
        // slice of the "Government, Legislature and Real Taxes" vision pillar — see
        // docs/obsidian-vault/Vision/Government, Legislature and Real Taxes.md. Unspecified
        // everywhere a real classification hasn't been researched yet.
        public GovernmentType Government;

        // Civil liberties — speech, religion, internet — each 0-100 (0 = totally restricted,
        // 100 = fully free). Proposed as bills through LegislatureSystem, same as taxes; see
        // docs/obsidian-vault/Vision/Government, Legislature and Real Taxes.md. No per-country
        // real index is curated yet (that's its own Freedom-House-scale research project) —
        // seeded from a government-type bucket instead, an honest coarse heuristic, not a claim
        // of researched accuracy for any specific country.
        public float FreedomSpeech;
        public float FreedomReligion;
        public float FreedomInternet;

        // Passive bonuses from supranational-union membership (see Sim/UnionSystem), baked in at
        // world seed. Military/intelligence blocs add standing (collective clout); military blocs
        // also add readiness. Folded into the drift targets in Tick so they persist rather than
        // decaying away. 0 for non-aligned countries.
        public float AllianceStandingBonus;
        public float AllianceReadinessBonus;

        // Live, per-game parliament — a mutable COPY of CountryProfiles.Parties made at seed, so
        // elections reshuffle THIS game's seat shares without corrupting the shared static data
        // (same copy-on-seed rule as EconomyState.Companies). Null/empty for countries without
        // curated parties (they route bills through the decree path). SeatShare is a fraction
        // summing to ~1.0; a bill passes at >0.5. See RunElection.
        public List<PartyProfile> Parties;

        // A general election reshuffles seat shares by economic conditions + noise: parties whose
        // economic ideology matches the moment gain (the right gains on strong growth / low
        // inflation, the left on high unemployment), then everything renormalises to sum ~1.0.
        // Deterministic per (seed) so a save/reload reproduces the same result. Returns the
        // biggest gainer's name for a headline, or null if this country has no parties. Making
        // seat shares move is what lets AI legislation (and the player's vote math) evolve across
        // a game instead of sitting frozen at the seeded balance forever.
        public string RunElection(EconomyState e, uint seed)
        {
            if (Parties == null || Parties.Count == 0) return null;
            float rightSwing = (e.GrowthRate - 2f) * 0.010f
                             - (e.Unemployment - 7f) * 0.008f
                             - System.Math.Max(0f, e.Inflation - 4f) * 0.006f;
            float total = 0f, bestGain = -999f; string winner = null;
            foreach (var p in Parties)
            {
                uint h = Hash(seed, (uint)(p.Name.Length * 131 + (int)(p.EconLean * 100f)));
                float noise = (h % 1000u) / 1000f * 0.06f - 0.03f; // ±0.03 seat fraction
                float delta = p.EconLean * rightSwing + noise;
                float ns = System.Math.Max(0.01f, p.SeatShare + delta);
                if (ns - p.SeatShare > bestGain) { bestGain = ns - p.SeatShare; winner = p.Name; }
                p.SeatShare = ns;
                total += ns;
            }
            if (total > 0f) foreach (var p in Parties) p.SeatShare /= total;
            return winner;
        }

        static uint Hash(uint a, uint b)
        {
            unchecked { uint h = 0x811c9dc5u; h = (h ^ a) * 0x01000193u; h = (h ^ b) * 0x01000193u; h ^= h >> 15; return h; }
        }

        public static NationalState Seed(GovernmentType government = GovernmentType.Unspecified)
        {
            float baseFreedom = government switch
            {
                GovernmentType.OneServiceState => 15f,
                GovernmentType.AbsoluteMonarchy => 25f,
                GovernmentType.PresidentialRepublic => 60f,
                GovernmentType.ConstitutionalMonarchy => 65f,
                GovernmentType.ParliamentaryRepublic => 70f,
                _ => 50f,
            };
            return new NationalState
            {
                ApprovalRating = 50f,
                DefenseSpending = 2.0f,
                ReadinessIndex = 50f,
                InternationalStanding = 50f,
                PublicMood = 50f,
                ResearchSpending = 1.5f,
                InnovationIndex = 40f,
                Government = government,
                FreedomSpeech = baseFreedom,
                FreedomReligion = baseFreedom,
                FreedomInternet = baseFreedom,
            };
        }

        // Advances one simulated day. gdpRankPercentile in [0,1], 1 == largest economy in the
        // world this tick (see NationalSystem.TickAll, which computes it once across all countries).
        public void Tick(EconomyState e, double gdpRankPercentile)
        {
            float approvalTarget = Clampf(50f + e.GrowthRate * 4f - (e.Unemployment - 7f) * 2f - System.Math.Max(0f, e.Inflation - 4f) * 3f, 0f, 100f);
            ApprovalRating = Clampf(ApprovalRating + (approvalTarget - ApprovalRating) * 0.01f, 0f, 100f);

            float readinessTarget = Clampf(DefenseSpending * 20f + AllianceReadinessBonus, 0f, 100f);
            ReadinessIndex = Clampf(ReadinessIndex + (readinessTarget - ReadinessIndex) * 0.02f, 0f, 100f);

            // Education spending compounds with direct research spending — a strong school
            // system raises the ceiling on what research money can produce.
            // Money (ResearchSpending, SpendEducation) AND people (research/education manpower)
            // both drive innovation — funding a lab with no staff, or staff with no funding,
            // each underperforms. Manpower terms are zero at their defaults (3% research, 7%
            // education) so this is unchanged until the player reallocates.
            float innovationTarget = Clampf((float)(gdpRankPercentile * 60.0) + ResearchSpending * 15f + (e.SpendEducation - 4.5f) * 3f
                + (e.ManpowerResearch - 3f) * 2.5f + (e.ManpowerEducation - 7f) * 1.5f, 0f, 100f);
            InnovationIndex = Clampf(InnovationIndex + (innovationTarget - InnovationIndex) * 0.01f, 0f, 100f);

            double openness = e.Gdp > 0.01 ? e.Exports / e.Gdp : 0.0;
            float standingTarget = Clampf((float)(gdpRankPercentile * 50.0) + (float)System.Math.Min(20.0, openness * 40.0) + ApprovalRating * 0.3f + AllianceStandingBonus, 0f, 100f);
            InternationalStanding = Clampf(InternationalStanding + (standingTarget - InternationalStanding) * 0.01f, 0f, 100f);

            // Healthcare spending is the daily-life lever: people feel underfunded hospitals
            // long before they feel a GDP decimal point.
            // Healthcare mood depends on funding (SpendHealthcare) and staffing (ManpowerHealthcare)
            // together — a well-funded but understaffed system still leaves people waiting.
            float moodTarget = Clampf(70f - (e.Unemployment - 7f) * 2.5f - System.Math.Max(0f, e.Inflation - 4f) * 2f
                + (e.SpendHealthcare - 6.0f) * 2.5f + (e.ManpowerHealthcare - 10f) * 1.2f, 0f, 100f);
            PublicMood = Clampf(PublicMood + (moodTarget - PublicMood) * 0.015f, 0f, 100f);

            // Population dynamics live here (not in EconomyState.Tick) because the drivers —
            // mood and healthcare — are this class's numbers. Good living conditions pull
            // growth toward ~2%/yr, misery toward decline; GDP per capita then moves with the
            // ratio of the two growth curves.
            float popTarget = Clampf(0.8f + (PublicMood - 50f) * 0.02f + (e.SpendHealthcare - 6.0f) * 0.06f + (e.ManpowerHealthcare - 10f) * 0.02f, -0.8f, 2.5f);
            e.PopulationGrowth = Clampf(e.PopulationGrowth + (popTarget - e.PopulationGrowth) * 0.005f, -0.8f, 2.5f);
            e.Population *= 1.0 + e.PopulationGrowth / 100.0 / 365.0;
        }

        static float Clampf(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }

    public class NationalSystem
    {
        public List<NationalState> States = new();

        public static NationalSystem Seed(IReadOnlyList<Country> countries)
        {
            var sys = new NationalSystem();
            foreach (var c in countries)
            {
                var profile = CountryProfiles.Get(c.IsoA3);
                var ns = NationalState.Seed(profile?.Government ?? GovernmentType.Unspecified);
                // Deep-copy the curated parties into per-game state so elections mutate this
                // game's parliament, never the shared static CountryProfiles data.
                if (profile?.Parties != null)
                {
                    ns.Parties = new List<PartyProfile>(profile.Parties.Count);
                    foreach (var p in profile.Parties)
                        ns.Parties.Add(new PartyProfile(p.Name, p.EconLean, p.SeatShare));
                }
                sys.States.Add(ns);
            }
            return sys;
        }

        // Ticks every country once. Computes each country's GDP-rank percentile fresh each call
        // (cheap at 258 countries) since InnovationIndex/InternationalStanding are relative to
        // the rest of the world, not absolute.
        public void TickAll(EconomySystem economy)
        {
            int n = economy.States.Count;
            var gdps = new double[n];
            for (int i = 0; i < n; i++) gdps[i] = economy.States[i].Gdp;

            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            System.Array.Sort(order, (a, b) => gdps[a].CompareTo(gdps[b]));

            var percentile = new double[n];
            for (int rank = 0; rank < n; rank++)
                percentile[order[rank]] = n > 1 ? (double)rank / (n - 1) : 0.5;

            for (int i = 0; i < States.Count && i < n; i++)
                States[i].Tick(economy.States[i], percentile[i]);
        }
    }
}
