using System.Collections.Generic;
using Meridian.Geo;

namespace Meridian.Sim
{
    // Faithful C# port of crates/ui_dashboard/src/economy.rs — a deterministic ticking
    // economy for EVERY country (not just the one on screen), seeded from Natural Earth's
    // real POP_EST/GDP_MD where available. Pure math, engine-agnostic; this is the real
    // gameplay core and ports 1:1 from the Rust. Kept bug-for-bug faithful to the original.

    public class EconomyState
    {
        public double Gdp;                 // Billions USD
        public float GrowthRate;           // % annualized, smoothed
        float baseGrowthTarget;            // long-run trend growth (set at seed)
        public float Unemployment;         // %
        public float Inflation;            // %
        public float TaxIncome;            // four independent player-adjustable levers (%)
        public float TaxCorporate;
        public float TaxVat;
        public float TaxTariff;
        public float InterestRate;         // central bank policy rate, %
        public double Treasury;            // Billions USD, cumulative (negative = deficit)
        uint rng;
        public string LastWhy;             // null == None
        public bool HasRealBaseline;       // false when GDP/pop were placeholders

        // Player-defined taxes beyond the four core levers — freely add/rename/rate/remove
        // (e.g. "Plastic Bag Tax"). Narrower in scope than the core levers, so each one carries
        // a much smaller revenue and growth-drag coefficient (see Tick()).
        public List<CustomTax> CustomTaxes = new();

        // Trade openness (exports/imports as a fraction of GDP), set once at seed from the
        // same GDP-per-capita tiering as growth — smaller/less-diversified economies tend to
        // run more trade relative to GDP. Deliberately simplified (real bilateral trade flows
        // between specific country pairs are a bigger future system); this gives every country
        // an honest, simulated, tariff-reactive trade balance rather than a placeholder.
        float exportPropensity;
        float importPropensity;

        public double Exports => Gdp * exportPropensity;
        // Higher tariffs suppress imports (each 10 points of tariff cuts import propensity ~5%).
        public double Imports => Gdp * importPropensity * (1.0 - TaxTariff / 100.0 * 0.5);
        public double TradeBalance => Exports - Imports;

        // Budget: derived from the same effective-tax/spending assumptions Tick() already uses
        // for Treasury, just expressed as annualized figures instead of a daily accumulator.
        public double AnnualRevenue => Gdp * EffectiveTaxRate() / 100.0;
        public double AnnualExpenditure => Gdp * 0.20;
        public double AnnualDeficit => AnnualExpenditure - AnnualRevenue; // positive == deficit
        public double PublicDebt => System.Math.Max(0.0, -Treasury);
        public double DebtToGdp => Gdp > 0.01 ? PublicDebt / Gdp * 100.0 : 0.0;

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

            var state = new EconomyState
            {
                Gdp = gdp,
                GrowthRate = baseGrowth,
                baseGrowthTarget = baseGrowth,
                Unemployment = 7.0f,
                Inflation = 2.5f,
                TaxIncome = 25.0f,
                TaxCorporate = 22.0f,
                TaxVat = 15.0f,
                TaxTariff = 5.0f,
                InterestRate = 4.0f,
                Treasury = 0.0,
                rng = HashSeed(c.IsoA3, salt),
                LastWhy = null,
                HasRealBaseline = hasReal,
            };

            float baseOpenness =
                gdpPerCapita < 5_000.0 ? 0.35f :
                gdpPerCapita < 15_000.0 ? 0.28f :
                gdpPerCapita < 30_000.0 ? 0.24f : 0.20f;
            // Noise() needs `rng`, which is only set once the object above exists.
            state.exportPropensity = baseOpenness * (1.0f + state.Noise() * 0.15f);
            state.importPropensity = baseOpenness * (1.0f + state.Noise() * 0.15f);

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
            float target = baseGrowthTarget - taxDrag - rateDrag + Noise() * 0.15f;
            GrowthRate = Clampf(GrowthRate * 0.98f + target * 0.02f, -15.0f, 15.0f);

            Gdp *= 1.0 + GrowthRate / 100.0 / 365.0;
            if (Gdp < 0.01) Gdp = 0.01;

            Unemployment = Clampf(Unemployment + (2.0f - GrowthRate) * 0.01f, 2.0f, 35.0f);
            Inflation = Clampf(
                Inflation + (GrowthRate - 2.0f) * 0.005f - (InterestRate - 4.0f) * 0.03f + Noise() * 0.02f,
                -3.0f, 40.0f);

            Treasury += Gdp * effectiveTax / 100.0 / 365.0 - Gdp * 0.20 / 365.0;

            // Custom taxes are a much smaller slice of GDP per point of rate than the core
            // levers (they're narrow/specific, not broad-based) — roughly a 10% custom tax
            // yields ~0.1% of GDP annually, versus the core levers' ~20-25% combined.
            foreach (var t in CustomTaxes)
                Treasury += Gdp * (t.Rate / 100.0) * 0.01 / 365.0;

            // Same "crossed a threshold this tick" event logic as the Rust (reconstructs the
            // pre-update value by subtracting the delta). Faithful to the original, including
            // its inflation reconstruction only accounting for the growth term.
            if (prevGrowth >= 0.0f && GrowthRate < 0.0f)
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
            uint x = rng;
            if (x == 0) x = 0x9e3779b9;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            rng = x;
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
