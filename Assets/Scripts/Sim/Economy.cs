using System.Collections.Generic;
using Meridian.Geo;

namespace Meridian.Sim
{
    // Started as a faithful C# port of crates/ui_dashboard/src/economy.rs — a deterministic
    // ticking economy for EVERY country (not just the one on screen), seeded from Natural
    // Earth's real POP_EST/GDP_MD where available. Pure math, engine-agnostic. Has since
    // grown past the Rust original: adjustable budget spending levers (education/healthcare/
    // infrastructure) and diplomacy-driven trade-agreement bonuses are Unity-side additions.

    public class EconomyState
    {
        // All state fields are public (including ones only Tick() itself uses, like the PRNG
        // word and seed-time constants) so the save system can serialize an EconomyState
        // wholesale with Newtonsoft and get back an exact replica — no hidden state, no drift
        // between a loaded game and one that never quit.
        public double Gdp;                 // Billions USD
        public float GrowthRate;           // % annualized, smoothed
        public float BaseGrowthTarget;     // long-run trend growth (set at seed)
        public float Unemployment;         // %
        public float Inflation;            // %
        public float TaxIncome;            // four independent player-adjustable levers (%)
        public float TaxCorporate;
        public float TaxVat;
        public float TaxTariff;
        public float InterestRate;         // central bank policy rate, %
        public double Treasury;            // Billions USD, cumulative (negative = deficit)
        public uint Rng;
        public string LastWhy;             // null == None
        public bool HasRealBaseline;       // false when GDP/pop were placeholders
        public bool HasRealTaxProfile;     // true when seeded from CountryProfiles' real curated rates

        // First slice of the "Economic Sectors and Companies" vision pillar — real named
        // companies (see Sim/Companies.cs), mutable per-game so ownership changes (see
        // LegislatureSystem, BillKind.CompanyOwnership) never touch the shared static
        // CountryProfiles seed data. Empty for every uncurated country.
        public List<Company> Companies = new();

        // Distributable profit as a share of a company's OutputBillions (revenue). One shared
        // number rather than per-sector detail — anchored so a fully state-owned Aramco yields
        // dividends the same order of magnitude as its real state payouts (see Tick).
        public const double CompanyProfitMargin = 0.10;

        // Player-defined taxes beyond the four core levers — freely add/rename/rate/remove
        // (e.g. "Plastic Bag Tax"). Narrower in scope than the core levers, so each one carries
        // a much smaller revenue and growth-drag coefficient (see Tick()).
        public List<CustomTax> CustomTaxes = new();

        // Budget spending levers (% of GDP). Defaults sum with SpendBase to the same flat 20%
        // the sim used before these were adjustable, so the pre-existing fiscal balance is
        // unchanged until the player actually moves something. SpendBase is the immovable rest
        // of government (administration, pensions, debt service, everything not broken out).
        public float SpendEducation = 4.5f;
        public float SpendHealthcare = 6.0f;
        public float SpendInfrastructure = 3.0f;
        public const float SpendBase = 6.5f;
        public float TotalSpendingRate => SpendBase + SpendEducation + SpendHealthcare + SpendInfrastructure;

        // Sum of active trade-agreement export bonuses (set by DiplomacySystem; each agreement
        // adds a small slice of extra export propensity for BOTH signatories).
        public float TradeAgreementExportBonus;

        // Trade openness (exports/imports as a fraction of GDP), set once at seed from the
        // same GDP-per-capita tiering as growth — smaller/less-diversified economies tend to
        // run more trade relative to GDP. Deliberately simplified (real bilateral trade flows
        // between specific country pairs are a bigger future system); this gives every country
        // an honest, simulated, tariff-reactive trade balance rather than a placeholder.
        public float ExportPropensity;
        public float ImportPropensity;

        public double Exports => Gdp * (ExportPropensity + TradeAgreementExportBonus);
        // Higher tariffs suppress imports (each 10 points of tariff cuts import propensity ~5%).
        public double Imports => Gdp * ImportPropensity * (1.0 - TaxTariff / 100.0 * 0.5);
        public double TradeBalance => Exports - Imports;

        // Budget: derived from the same effective-tax/spending assumptions Tick() already uses
        // for Treasury, just expressed as annualized figures instead of a daily accumulator.
        public double AnnualRevenue => Gdp * EffectiveTaxRate() / 100.0;
        public double AnnualExpenditure => Gdp * TotalSpendingRate / 100.0;
        public double AnnualDeficit => AnnualExpenditure - AnnualRevenue; // positive == deficit
        public double PublicDebt => System.Math.Max(0.0, -Treasury);
        public double DebtToGdp => Gdp > 0.01 ? PublicDebt / Gdp * 100.0 : 0.0;

        // Dynamic population (seeded from Natural Earth POP_EST, then grows/shrinks with living
        // conditions — see NationalState.Tick, which owns the mood/healthcare inputs).
        public double Population;
        public float PopulationGrowth; // %/yr, smoothed
        public double GdpPerCapita => Population > 1 ? Gdp * 1e9 / Population : 0.0;

        // Sovereign credit: markets price government debt off the debt load. The premium is
        // added to the policy rate when servicing debt (see Tick) — sustained deficits walk the
        // rating down the tiers and the interest bill up, the classic debt-spiral mechanic.
        // Thresholds are debt-to-GDP percentages, loosely modeled on real rating-agency bands.
        public string CreditRatingLabel =>
            DebtToGdp < 30 ? "AAA" : DebtToGdp < 60 ? "AA" : DebtToGdp < 90 ? "A" :
            DebtToGdp < 120 ? "BBB" : DebtToGdp < 150 ? "BB" : DebtToGdp < 200 ? "B" : "C";
        public float CreditRiskPremium =>
            DebtToGdp < 30 ? 0f : DebtToGdp < 60 ? 0.3f : DebtToGdp < 90 ? 0.8f :
            DebtToGdp < 120 ? 1.5f : DebtToGdp < 150 ? 2.5f : DebtToGdp < 200 ? 4f : 6f;
        public double AnnualDebtService => PublicDebt * (InterestRate + CreditRiskPremium) / 100.0;
        // Last rating seen by Tick, for downgrade/upgrade event detection. Serialized so a
        // loaded game doesn't fire a spurious rating-change toast on its first tick.
        public string LastCreditRating = "AAA";

        public static EconomyState Seed(Country c, uint salt)
        {
            bool hasReal = c.GdpMd > 0 && c.PopEst > 0;
            double gdp = hasReal
                ? c.GdpMd / 1000.0
                : System.Math.Max(c.PopEst, 10_000L) * 3_000.0 / 1e9;
            double gdpPerCapita = c.PopEst > 0 ? (gdp * 1e9) / c.PopEst : 3_000.0;
            float baseGrowth =
                gdpPerCapita < 5_000.0 ? 4.5f :
                gdpPerCapita < 15_000.0 ? 3.0f :
                gdpPerCapita < 30_000.0 ? 2.0f : 1.2f;

            // Real headline tax rates for a curated set of countries (see CountryProfiles) —
            // the generic 25/22/15/5 figures below are the simulated fallback for everywhere
            // else, not a claim that they're accurate for a specific real country.
            var profile = CountryProfiles.Get(c.IsoA3);

            var state = new EconomyState
            {
                Gdp = gdp,
                GrowthRate = baseGrowth,
                BaseGrowthTarget = baseGrowth,
                Unemployment = 7.0f,
                Inflation = 2.5f,
                TaxIncome = profile?.TaxIncome ?? 25.0f,
                TaxCorporate = profile?.TaxCorporate ?? 22.0f,
                TaxVat = profile?.TaxVat ?? 15.0f,
                TaxTariff = profile?.TaxTariff ?? 5.0f,
                InterestRate = 4.0f,
                Treasury = 0.0,
                Rng = HashSeed(c.IsoA3, salt),
                LastWhy = null,
                HasRealBaseline = hasReal,
                HasRealTaxProfile = profile != null,
                Population = System.Math.Max(c.PopEst, 10_000L),
                PopulationGrowth = 1.0f,
            };

            if (profile?.Companies != null)
                foreach (var seed in profile.Companies)
                    state.Companies.Add(new Company { Name = seed.Name, Sector = seed.Sector, Ownership = seed.Ownership, OutputBillions = seed.OutputBillions });

            float baseOpenness =
                gdpPerCapita < 5_000.0 ? 0.35f :
                gdpPerCapita < 15_000.0 ? 0.28f :
                gdpPerCapita < 30_000.0 ? 0.24f : 0.20f;
            // Noise() needs `Rng`, which is only set once the object above exists.
            state.ExportPropensity = baseOpenness * (1.0f + state.Noise() * 0.15f);
            state.ImportPropensity = baseOpenness * (1.0f + state.Noise() * 0.15f);

            return state;
        }

        // Blended effective tax burden across the four levers (income/corporate weigh more
        // than VAT/tariffs).
        public float EffectiveTaxRate() =>
            TaxIncome * 0.4f + TaxCorporate * 0.3f + TaxVat * 0.2f + TaxTariff * 0.1f;

        // Advances the economy by exactly one simulated day.
        public void Tick()
        {
            float prevGrowth = GrowthRate;

            // Custom taxes add a small extra drag on top of the four core levers — scaled way
            // down since a niche tax (e.g. a plastic bag tax) shouldn't move GDP the way income
            // tax does.
            float customDragTotal = 0f;
            foreach (var t in CustomTaxes) customDragTotal += t.Rate * 0.01f;

            float effectiveTax = EffectiveTaxRate();
            float taxDrag = (effectiveTax - 25.0f) * 0.04f + customDragTotal;
            float rateDrag = (InterestRate - 4.0f) * 0.05f;
            // Productive spending pays off in growth: infrastructure is the most direct
            // (roads/ports/grid capacity), education a slower but real second. Both measured
            // as deviation from their defaults so the baseline economy is unchanged. Trade
            // agreements add a small openness dividend on top.
            float spendBoost = (SpendInfrastructure - 3.0f) * 0.10f
                             + (SpendEducation - 4.5f) * 0.05f
                             + TradeAgreementExportBonus * 4.0f;
            float target = BaseGrowthTarget - taxDrag - rateDrag + spendBoost + Noise() * 0.15f;
            GrowthRate = Clampf(GrowthRate * 0.98f + target * 0.02f, -15.0f, 15.0f);

            Gdp *= 1.0 + GrowthRate / 100.0 / 365.0;
            if (Gdp < 0.01) Gdp = 0.01;

            // Unemployment/inflation must equilibrate around THIS country's own trend growth
            // (BaseGrowthTarget), not a universal constant — BaseGrowthTarget ranges from 1.2%
            // (rich countries) to 4.5% (poor ones) depending on GDP-per-capita tier (see Seed).
            // A hardcoded "2.0" reference here was a real bug: any country whose trend growth
            // sits below 2.0 (i.e. every high-income country, the ones players are most likely
            // to pick) had unemployment rise every single day with no equilibrium, forever,
            // regardless of tax policy — confirmed by 660 days of observed play where a rich
            // nation's unemployment climbed 7.0% to 11.1% with no sign of leveling off.
            Unemployment = Clampf(Unemployment + (BaseGrowthTarget - GrowthRate) * 0.01f, 2.0f, 35.0f);
            Inflation = Clampf(
                Inflation + (GrowthRate - BaseGrowthTarget) * 0.005f - (InterestRate - 4.0f) * 0.03f + Noise() * 0.02f,
                -3.0f, 40.0f);

            Treasury += Gdp * effectiveTax / 100.0 / 365.0 - Gdp * TotalSpendingRate / 100.0 / 365.0;

            // Debt service: outstanding debt accrues interest at the policy rate PLUS the
            // market's credit-risk premium — running big debts at a junk rating compounds fast.
            if (Treasury < 0)
                Treasury -= AnnualDebtService / 365.0;

            // Rating transitions surface through the same LastWhy channel as every other
            // threshold event (checked AFTER today's interest, so the number is current).
            // The actual message is deferred into the if/else-if cascade below so a same-day
            // rating change can't be silently clobbered by a recession/unemployment/inflation
            // message overwriting LastWhy after it — LastCreditRating itself still always
            // advances here so a transition is never detected twice.
            string rating = CreditRatingLabel;
            bool ratingChanged = rating != LastCreditRating;
            bool ratingDowngrade = ratingChanged && RatingRank(rating) > RatingRank(LastCreditRating);
            LastCreditRating = rating;

            // Custom taxes are a much smaller slice of GDP per point of rate than the core
            // levers (they're narrow/specific, not broad-based) — roughly a 10% custom tax
            // yields ~0.1% of GDP annually, versus the core levers' ~20-25% combined.
            foreach (var t in CustomTaxes)
                Treasury += Gdp * (t.Rate / 100.0) * 0.01 / 365.0;

            // State-owned enterprise dividends: a nationalized company pays its profits into
            // the treasury daily, stake-weighted for Mixed ownership. Margin sanity check
            // against the real anchor: Saudi Aramco at ~$480B output × 10% ≈ $48B/yr to the
            // state — same order as (conservative vs.) Aramco's real dividend+royalty flows.
            // The flip side of the deal: state-run firms carry an efficiency drag (~1.5%/yr at
            // full state ownership), so a nationalized company's output — and with it future
            // dividends AND its eventual re-privatization sale price — compounds slower than
            // the economy around it. Private companies pay nothing directly here; their
            // contribution is already inside the macro tax base above.
            foreach (var co in Companies)
            {
                double stake = co.Ownership == Ownership.Public ? 1.0 : co.Ownership == Ownership.Mixed ? 0.5 : 0.0;
                if (stake > 0.0)
                    Treasury += co.OutputBillions * CompanyProfitMargin * stake / 365.0;
                double drift = GrowthRate / 100.0 - stake * 0.015;
                co.OutputBillions = System.Math.Max(0.1, co.OutputBillions * (1.0 + drift / 365.0));
            }

            // Same "crossed a threshold this tick" event logic as the Rust (reconstructs the
            // pre-update value by subtracting the delta). Faithful to the original, including
            // its inflation reconstruction only accounting for the growth term.
            if (ratingChanged)
                LastWhy = ratingDowngrade
                    ? $"Credit rating downgraded to {rating} — debt servicing costs rise"
                    : $"Credit rating upgraded to {rating} — borrowing gets cheaper";
            else if (prevGrowth >= 0.0f && GrowthRate < 0.0f)
                LastWhy = "GDP growth turned negative — economy entering recession";
            else if (prevGrowth < 0.0f && GrowthRate >= 0.0f)
                LastWhy = "GDP growth turned positive — recession easing";
            else if (Unemployment > 12.0f && Unemployment - (2.0f - GrowthRate) * 0.01f <= 12.0f)
                LastWhy = "Unemployment crossed 12% amid weak growth";
            else if (Inflation > 8.0f && Inflation - (GrowthRate - 2.0f) * 0.005f <= 8.0f)
                LastWhy = "Inflation crossed 8% as growth accelerates";
            // else: leave LastWhy unchanged (the Rust's self.last_why.take() is a no-op reassign).
        }

        // --- deterministic PRNG (xorshift32) + FNV-1a seed hash ------------------------

        uint Xorshift32()
        {
            uint x = Rng;
            if (x == 0) x = 0x9e3779b9;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            Rng = x;
            return x;
        }

        // Roughly uniform in [-1, 1].
        float Noise() => (Xorshift32() / (float)uint.MaxValue) * 2.0f - 1.0f;

        static uint HashSeed(string s, uint salt)
        {
            unchecked
            {
                uint h = 0x811c9dc5 ^ salt;
                foreach (char ch in s)
                {
                    h ^= (byte)ch;
                    h *= 0x01000193;
                }
                return h;
            }
        }

        static float Clampf(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);

        public static int RatingRank(string r) => r switch
        {
            "AAA" => 0, "AA" => 1, "A" => 2, "BBB" => 3, "BB" => 4, "B" => 5, _ => 6,
        };
    }

    // A player-defined tax beyond the four core levers — freely named, rated, and removable.
    public class CustomTax
    {
        public string Name = "";
        public float Rate; // percent, 0-50

        public CustomTax() { }
        public CustomTax(string name, float rate) { Name = name; Rate = rate; }
    }

    public class EconomySystem
    {
        public List<EconomyState> States = new();

        public static EconomySystem Seed(IReadOnlyList<Country> countries)
        {
            var sys = new EconomySystem();
            for (int i = 0; i < countries.Count; i++)
                sys.States.Add(EconomyState.Seed(countries[i], (uint)i));
            return sys;
        }

        // Ticks every country's economy once — always all of them, never LOD'd to just the
        // one on screen (Phase 1 Challenge 4).
        public void TickAll()
        {
            foreach (var s in States) s.Tick();
        }
    }
}
