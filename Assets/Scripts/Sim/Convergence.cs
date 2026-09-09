using System.Collections.Generic;
using UnityEngine;
using Meridian.Geo;

namespace Meridian.Sim
{
    // BUDGET AND CONVERGENCE — Consequence Engine §6.
    //
    // National averages hide neglected interiors, so this system refuses to work in national
    // averages. Every country is split into its real provinces, each with its own wealth, and the
    // number that matters is wealth_gap_ratio = richest province / poorest province.
    //
    // Two doctrines, and the whole section is the difference between them:
    //
    //   PRODUCTION_SHARE  provinces keep what they generate. Cheap, stable, and the gap NEVER
    //                     closes — it drifts toward the raw spread of productive potential, which
    //                     is exactly what geography handed you.
    //   PER_CAPITA        equal spending per person. Enormously expensive, and the single most
    //                     effective legitimacy instrument in the game.
    //
    // THE ABSORPTION CONSTRAINT is the thing worth discovering, and it is deliberately not a
    // tooltip anywhere. For the first decade or so of a per-capita programme MONEY is the binding
    // constraint: spend more, the gap closes faster, and it feels like a solved problem. Then it
    // stops working. What binds after that is ADMINISTRATIVE CAPACITY — trained civil servants who
    // can actually deliver a school, a clinic, a road, in a province that has never had one — and
    // administrative capacity takes ~14 years to build and cannot be bought in a budget cycle.
    //
    // A player who spent those years raising education spending and education manpower will sail
    // through the transition. A player who did not will watch the most expensive programme in
    // their budget stop moving the number entirely, and the only honest signal they get is the
    // world feed telling them the spending went up and the gap did not move.

    public enum BudgetDoctrine { ProductionShare, PerCapita }

    // One province. Wealth and Output are indices where 1.0 is the country's population-weighted
    // average at world seed, so they stay comparable across countries of wildly different size.
    public class ProvinceCell
    {
        public int Country;
        public string Name = "";
        public float PopShare;      // share of its country's population
        public float Output;        // productive potential — what this province generates
        public float Wealth;        // what the people living there actually have
    }

    public class CountryConvergence
    {
        public BudgetDoctrine Doctrine = BudgetDoctrine.ProductionShare;
        public long DoctrineSinceDay;
        // Trained civil servants able to deliver services where they have never been delivered.
        // Grows on a ~14-year half-life from education spending and education manpower. This is
        // the constraint that binds AFTER money stops being the constraint.
        public float AdminCapacity = 20f;
        public float GapRatio = 1f;
        public float SeedGapRatio = 1f;
        public float LastGapRatio = 1f;
        public float LastProgress;    // share of the seeded gap closed so far -- what legitimacy pays on
        public int Start, Count;      // slice into ConvergenceSystem.Cells
        // Diagnostics of the absorption constraint, so the world feed can say something true
        // about it without ever naming the mechanic.
        public bool AdminBound;
        public int StalledSteps;
        public bool StallReported;
    }

    public class ConvergenceSystem
    {
        public List<ProvinceCell> Cells = new();
        public List<CountryConvergence> States = new();

        public const int StepDays = 30;         // convergence is a monthly process, not a daily one

        public CountryConvergence Of(int country) =>
            country >= 0 && country < States.Count ? States[country] : null;

        public IReadOnlyList<ProvinceCell> ProvincesOf(int country)
        {
            var s = Of(country);
            var outp = new List<ProvinceCell>();
            if (s == null) return outp;
            for (int i = s.Start; i < s.Start + s.Count; i++) outp.Add(Cells[i]);
            return outp;
        }

        // --- seeding ----------------------------------------------------------------------

        // Province wealth is seeded from real geography rather than noise: where the cities are,
        // how far the province is from the capital, and whether it has a port. That produces the
        // shape every real country actually has — a rich capital region, a rich coast, and a poor
        // interior — without any per-country authoring.
        public static ConvergenceSystem Seed(GeoWorld world, EconomySystem econ)
        {
            var sys = new ConvergenceSystem();
            int nc = world.Countries.Count;
            var byIso = new Dictionary<string, int>();
            // Cities carry ADM0NAME (a country NAME), provinces carry adm0_a3 (an ISO code). Keying
            // both off the ISO map silently matched no cities at all, which left every province in
            // the world scored on ports alone -- a 1.3x gap everywhere and Chile's richest region
            // coming out as Arica. Both keys are needed, and a name map is the only way in.
            var byName = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < nc; i++)
            {
                sys.States.Add(new CountryConvergence());
                string iso = world.Countries[i].IsoA3;
                if (!string.IsNullOrEmpty(iso) && iso != "-99" && !byIso.ContainsKey(iso)) byIso[iso] = i;
                string nm = world.Countries[i].Name;
                if (!string.IsNullOrEmpty(nm) && !byName.ContainsKey(nm)) byName[nm] = i;
                string nl = world.Countries[i].NameLong;
                if (!string.IsNullOrEmpty(nl) && !byName.ContainsKey(nl)) byName[nl] = i;
            }

            // Group provinces by country.
            var provsOf = new List<List<Province>>(nc);
            for (int i = 0; i < nc; i++) provsOf.Add(new List<Province>());
            foreach (var p in world.Provinces)
                if (!string.IsNullOrEmpty(p.Adm0A3) && byIso.TryGetValue(p.Adm0A3, out int ci))
                    provsOf[ci].Add(p);

            // Assign each city to the nearest province centroid inside its own country, and each
            // port to the nearest province centroid anywhere. Both are one-off costs at world load.
            var urbanMass = new Dictionary<Province, double>();
            var capitalOf = new Dictionary<int, Vector2>();
            var capitalProvince = new HashSet<Province>();
            int matchedCities = 0;
            foreach (var c in world.Cities)
            {
                if (string.IsNullOrEmpty(c.Country)) continue;
                if (!byName.TryGetValue(c.Country, out int ci) && !byIso.TryGetValue(c.Country, out ci)) continue;
                matchedCities++;
                if (c.IsCapital) capitalOf[ci] = c.Pos;
                var list = provsOf[ci];
                Province best = null; float bestD = float.MaxValue;
                foreach (var p in list)
                {
                    float d = (p.Centroid - c.Pos).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = p; }
                }
                if (best != null)
                {
                    urbanMass[best] = (urbanMass.TryGetValue(best, out double m) ? m : 0.0) + System.Math.Max(1000L, c.PopMax);
                    if (c.IsCapital) capitalProvince.Add(best);
                }
            }
            Debug.Log($"[convergence] matched {matchedCities}/{world.Cities.Count} cities to provinces across {byName.Count} country names");

            var hasPort = new HashSet<Province>();
            foreach (var port in world.Ports)
            {
                Province best = null; float bestD = float.MaxValue;
                foreach (var p in world.Provinces)
                {
                    float d = (p.Centroid - port.Pos).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = p; }
                }
                if (best != null && bestD < 4f) hasPort.Add(best);   // ~2 degrees, so it is genuinely coastal
            }

            for (int ci = 0; ci < nc; ci++)
            {
                var list = provsOf[ci];
                var st = sys.States[ci];
                st.Start = sys.Cells.Count;
                if (list.Count == 0)
                {
                    // A country with no province geometry is one undivided cell — the gap ratio is
                    // 1.0 and stays there, which is the honest answer for a city-state.
                    sys.Cells.Add(new ProvinceCell { Country = ci, Name = world.Countries[ci].Name, PopShare = 1f, Output = 1f, Wealth = 1f });
                    st.Count = 1; st.GapRatio = st.SeedGapRatio = st.LastGapRatio = 1f;
                    continue;
                }

                bool haveCapital = capitalOf.TryGetValue(ci, out Vector2 cap);
                double totalUrban = 0.0;
                foreach (var p in list) totalUrban += urbanMass.TryGetValue(p, out double m) ? m : 0.0;

                var raw = new List<float>(list.Count);
                var pop = new List<float>(list.Count);
                foreach (var p in list)
                {
                    double m = urbanMass.TryGetValue(p, out double mm) ? mm : 0.0;
                    float urbanShare = totalUrban > 0 ? (float)(m / totalUrban) : 1f / list.Count;

                    // Closeness to the capital, normalised against the country's own extent so it
                    // means the same thing in Luxembourg and in Russia.
                    float extent = Mathf.Max(1f, (world.Countries[ci].BboxMax - world.Countries[ci].BboxMin).magnitude);
                    float capCloseness = haveCapital ? Mathf.Clamp01(1f - (p.Centroid - cap).magnitude / extent) : 0.5f;

                    // Real province-level gaps are 3-10x (Jakarta against Papua, Antofagasta
                    // against Araucania), not the 1.3x a flatter formula produces. The capital
                    // premium is squared because it is sharply concentrated rather than a gentle
                    // gradient, and the urban term uses ^0.6 rather than sqrt so a genuine
                    // primate city actually dominates its country the way it does in life.
                    float score = 0.25f
                                + 2.20f * Mathf.Pow(urbanShare, 0.6f)
                                + 0.90f * capCloseness * capCloseness
                                + (hasPort.Contains(p) ? 0.30f : 0f)
                                + (capitalProvince.Contains(p) ? 0.55f : 0f);
                    raw.Add(score);
                    // Population follows the cities, with a floor so an empty interior still has people in it.
                    pop.Add(0.25f / list.Count + 0.75f * urbanShare);
                }

                float popSum = 0f; foreach (var v in pop) popSum += v;
                if (popSum <= 0f) popSum = 1f;
                float weighted = 0f;
                for (int k = 0; k < raw.Count; k++) weighted += raw[k] * (pop[k] / popSum);
                if (weighted <= 0.0001f) weighted = 1f;

                for (int k = 0; k < list.Count; k++)
                {
                    float w = raw[k] / weighted;      // 1.0 == this country's average
                    sys.Cells.Add(new ProvinceCell
                    {
                        Country = ci, Name = list[k].Name,
                        PopShare = pop[k] / popSum,
                        Output = w, Wealth = w,
                    });
                }
                st.Count = list.Count;
                st.GapRatio = st.SeedGapRatio = st.LastGapRatio = sys.ComputeGap(st);
            }
            return sys;
        }

        public float ComputeGap(CountryConvergence st)
        {
            if (st.Count <= 1) return 1f;
            float hi = 0f, lo = float.MaxValue;
            for (int i = st.Start; i < st.Start + st.Count; i++)
            {
                float w = Cells[i].Wealth;
                if (w > hi) hi = w;
                if (w < lo) lo = w;
            }
            return hi / Mathf.Max(0.05f, lo);
        }

        public float MeanWealth(CountryConvergence st)
        {
            float sum = 0f, wsum = 0f;
            for (int i = st.Start; i < st.Start + st.Count; i++)
            {
                sum += Cells[i].Wealth * Cells[i].PopShare;
                wsum += Cells[i].PopShare;
            }
            return wsum > 0f ? sum / wsum : 1f;
        }

        // --- doctrine ---------------------------------------------------------------------

        public void SetDoctrine(int country, BudgetDoctrine d, long day, EconomySystem econ,
                                LegitimacySystem legit, GeoWorldNames names)
        {
            var st = Of(country);
            if (st == null || st.Doctrine == d) return;
            st.Doctrine = d;
            st.DoctrineSinceDay = day;

            if (d == BudgetDoctrine.PerCapita)
            {
                // Declaring it is not doing it — the money has to appear in the budget, so this
                // opens the line at a real, visible cost the player then has to defend.
                if (econ.States[country].SpendConvergence < 3f) econ.States[country].SpendConvergence = 3f;
                legit?.Record(country, day, "Adopted per-capita budgeting",
                    "Promising every province the same spending per person is the most expensive promise a state makes to itself, and the one its poorest provinces judge it by",
                    LegitimacySystem.Deltas(ownPopulation: +4f, foreignPopulations: +2f));
            }
            else
            {
                econ.States[country].SpendConvergence = 0f;
                legit?.Record(country, day, "Returned to production-share budgeting",
                    "Provinces keep what they generate, which the rich ones have always preferred and the poor ones have always understood",
                    LegitimacySystem.Deltas(ownPopulation: -6f, blocMembers: +1f));
            }
        }

        // --- the tick ---------------------------------------------------------------------

        public List<string> TickAll(long day, EconomySystem econ, NationalSystem nat,
                                    LegitimacySystem legit, GeoWorldNames names)
        {
            if (day % StepDays != 0) return Empty;
            List<string> headlines = null;

            for (int ci = 0; ci < States.Count && ci < econ.States.Count; ci++)
            {
                var st = States[ci];
                var e = econ.States[ci];
                if (st.Count <= 0) continue;

                // Administrative capacity: slow, boring, and the thing that decides whether the
                // expensive programme keeps working after its first decade. ~14-year half-life.
                float adminTarget = Mathf.Clamp(20f + (e.SpendEducation - 4.5f) * 6f
                                                    + (e.ManpowerEducation - 7f) * 2.5f, 5f, 95f);
                st.AdminCapacity += (adminTarget - st.AdminCapacity) * 0.012f;

                float mean = MeanWealth(st);
                st.LastGapRatio = st.GapRatio;

                if (st.Doctrine == BudgetDoctrine.ProductionShare)
                {
                    // Provinces keep what they generate, so wealth walks toward productive
                    // potential and the gap settles at whatever geography dictated. It never
                    // closes, and it costs nothing, and that is the trade.
                    for (int i = st.Start; i < st.Start + st.Count; i++)
                        Cells[i].Wealth += (Cells[i].Output - Cells[i].Wealth) * 0.010f;
                    st.AdminBound = false;
                    st.StalledSteps = 0;
                }
                else
                {
                    float spend = Mathf.Max(0f, e.SpendConvergence);
                    // Tuned so a 6%-of-GDP programme closes roughly half the provincial gap in
                    // ~13 years. That number is not decoration: it is what puts the crossover
                    // where §6 says it belongs. At 0.0030 the whole gap closed inside two years,
                    // difficulty shot to its ceiling immediately, and administrative capacity was
                    // the binding constraint from month one -- so "money binds first, then
                    // capacity" never actually happened in the run.
                    float moneyRate = spend * 0.00042f;

                    // The easy gains come first. Cash transfers and a road close the first half of
                    // a gap cheaply; the second half is schools, clinics and staff in places that
                    // have never had any, and it costs multiples more per point.
                    float span = Mathf.Max(0.01f, st.SeedGapRatio - 1f);
                    float progress = Mathf.Clamp01(1f - (st.GapRatio - 1f) / span);
                    // SQUARED, not linear. A linear ramp made delivery three times harder by the
                    // time only a third of the gap was closed, which pulled the crossover forward
                    // to year one or two and destroyed the shape §6 asks for. Squaring keeps the
                    // first stretch genuinely easy -- cash and a road -- and makes the last
                    // stretch brutally hard, which is both truer and puts the handover from money
                    // to capacity where it belongs, around year 12.
                    float difficulty = 1f + 9f * progress * progress;

                    // Difficulty belongs on the DELIVERY side, not the money side. Dividing the
                    // money rate by it (the first version) meant the demanded rate only ever fell,
                    // so the administrative ceiling could never bind and §6's whole discovery --
                    // money binds for a decade, then capacity binds -- silently never happened.
                    // What actually gets harder as the easy gains run out is placing staff and
                    // running services in places that have never had them.
                    // 0.00055 puts the handover from money to capacity at year ~12 for a country running
                    // an ordinary education budget, which is the window §6 specifies. At 0.00040 it
                    // landed at year 8 -- the right shape, the wrong decade.
                    float adminCeiling = st.AdminCapacity * 0.00055f / difficulty;
                    float rate = Mathf.Min(moneyRate, adminCeiling);
                    st.AdminBound = moneyRate > adminCeiling + 0.00001f;

                    for (int i = st.Start; i < st.Start + st.Count; i++)
                        Cells[i].Wealth += (mean - Cells[i].Wealth) * rate;
                }

                st.GapRatio = ComputeGap(st);
                float closed = st.LastGapRatio - st.GapRatio;

                // LEGITIMACY. Per-capita budgeting is the strongest instrument in the game, but it
                // pays for RESULTS, not for intent: a programme that has stopped moving the number
                // pays nothing at all, however much it costs.
                if (st.Doctrine == BudgetDoctrine.PerCapita)
                {
                    // Pay on the SHARE of the remaining gap closed, not on the raw change in a
                    // ratio. Paying on the ratio meant a country with a 1.3x gap earned almost
                    // nothing however well the programme ran, while the stall penalty fired on
                    // nearly every step -- so the design's "single most effective legitimacy
                    // instrument in the game" actively made you worse off than doing nothing.
                    float spanNow = Mathf.Max(0.01f, st.SeedGapRatio - 1f);
                    float progressNow = Mathf.Clamp01(1f - (st.GapRatio - 1f) / spanNow);
                    float dProgress = progressNow - st.LastProgress;
                    st.LastProgress = progressNow;

                    if (dProgress > 0.0002f)
                    {
                        legit?.Drift(ci, Observer.OwnPopulation, Mathf.Min(1.5f, dProgress * 40f));
                        st.StalledSteps = 0;
                    }
                    else
                    {
                        st.StalledSteps++;
                        // The penalty is for spending heavily and DELIVERING NOTHING, which is a
                        // real state -- not merely for a programme that is going slowly because
                        // most of the work is already done.
                        if (st.AdminBound && e.SpendConvergence > 4f)
                            legit?.Drift(ci, Observer.OwnPopulation, -0.20f);
                    }
                }
                else if (st.GapRatio > 4f)
                    legit?.Drift(ci, Observer.OwnPopulation, -0.05f);

                // The only signal the player gets about the absorption constraint, and it names
                // nothing: the money went up, the number did not move.
                if (!st.StallReported && st.AdminBound && st.StalledSteps >= 6 && st.Doctrine == BudgetDoctrine.PerCapita)
                {
                    st.StallReported = true;
                    (headlines ??= new List<string>()).Add(
                        $"{names?.Name(ci)}: convergence spending is at {e.SpendConvergence:0.0}% of GDP and the provincial gap has not moved in six months. Ministries report they cannot place the staff.");
                }
            }
            return headlines ?? Empty;
        }

        static readonly List<string> Empty = new();
    }
}
