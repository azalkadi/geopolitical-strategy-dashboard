using System.Collections.Generic;

namespace Meridian.Sim
{
    // Sector composition of GDP — the "sectors compose GDP" piece of the Economic Sectors and
    // Companies vision pillar (docs/obsidian-vault/Vision/Economic Sectors and Companies.md).
    //
    // Design choice (deliberate, documented): GDP still GROWS via EconomyState.Tick's balanced
    // macro model — that model is carefully tuned and rebuilding it bottom-up from sectors would
    // destabilize the whole sim. Instead GDP is DECOMPOSED into ten sector shares that sum to
    // 100%, each drifting over time toward the faster-growing sectors (real structural
    // transformation — services/tech gain share, agriculture/mining lose it as a country
    // develops). Shares are seeded by development tier and then bumped up wherever a country has
    // big real curated companies (so Saudi Arabia reads as energy-dominant from Aramco's real
    // output, not the flat tier template). The composition then feeds a small, bounded nudge
    // back into aggregate growth, so a country over-weighted in declining sectors grows slightly
    // slower — a real feedback without touching the tuned macro core.
    public class SectorState
    {
        public Sector Sector;
        public float Share;    // % of GDP (all sectors in a country sum to ~100)
        public double Output;  // = Gdp * Share/100, refreshed each tick for display/dividends

        public string Label => SectorInfo.Label(Sector);
    }

    public static class SectorInfo
    {
        // Enum order (Companies.cs): Energy=0, Agriculture=1, Manufacturing=2, Technology=3,
        // Finance=4, Services=5, Mining=6, Construction=7, Defense=8, Healthcare=9.
        public static readonly Sector[] All =
        {
            Sector.Energy, Sector.Agriculture, Sector.Manufacturing, Sector.Technology,
            Sector.Finance, Sector.Services, Sector.Mining, Sector.Construction,
            Sector.Defense, Sector.Healthcare,
        };

        public static string Label(Sector s) => s switch
        {
            Sector.Energy => "Energy", Sector.Agriculture => "Agriculture", Sector.Manufacturing => "Manufacturing",
            Sector.Technology => "Technology", Sector.Finance => "Finance", Sector.Services => "Services",
            Sector.Mining => "Mining", Sector.Construction => "Construction", Sector.Defense => "Defense",
            _ => "Healthcare",
        };

        // Per-sector distributable profit margin (share of a company's revenue that becomes
        // profit) — replaces the old flat 10%. Anchored to real industry patterns: energy/mining/
        // finance/tech run fat, agriculture/construction/services thin. Used both for SOE
        // dividends (Economy.Tick) and as the notion of sector "productivity" for the growth
        // nudge below.
        public static double ProfitMargin(Sector s) => s switch
        {
            Sector.Finance => 0.18, Sector.Technology => 0.16, Sector.Energy => 0.15, Sector.Mining => 0.14,
            Sector.Healthcare => 0.11, Sector.Defense => 0.10, Sector.Manufacturing => 0.08,
            Sector.Services => 0.07, Sector.Construction => 0.06, _ => 0.04, // Agriculture
        };

        // Long-run structural drift: how a sector's SHARE tends to move as an economy runs.
        // Positive = gains share over time (the tertiarization / high-tech shift real economies
        // undergo), negative = shrinks as a share of GDP. Small numbers — this is a slow drift,
        // not a swing.
        public static float ShareDrift(Sector s) => s switch
        {
            Sector.Technology => 0.9f, Sector.Services => 0.6f, Sector.Finance => 0.5f, Sector.Healthcare => 0.4f,
            Sector.Defense => 0.0f, Sector.Construction => 0.0f, Sector.Energy => -0.2f, Sector.Manufacturing => -0.2f,
            Sector.Mining => -0.4f, _ => -0.6f, // Agriculture shrinks fastest as a share
        };

        // Seed sector shares (%) by development tier, keyed off GDP per capita — the same tiering
        // Economy.Seed uses for growth/openness. Honest tier-based approximations (structural
        // patterns economists document), NOT per-country real data — a country's real curated
        // companies then bump the relevant sectors above these baselines (see Seed below).
        // Index matches All[] order; each row sums to 100.
        static readonly float[] Low   = { 5f, 25f, 12f, 1.5f, 3f, 35f, 8f, 7f, 0.5f, 3f };
        static readonly float[] LowMid = { 4f, 12f, 18f, 1.5f, 4f, 45f, 5f, 7f, 0.5f, 3f };
        static readonly float[] UpMid = { 2f, 5f, 18f, 3f, 7f, 55f, 1f, 6f, 0.5f, 2.5f };
        static readonly float[] High  = { 1f, 1.5f, 15f, 5f, 9f, 62f, 0.5f, 4f, 0.5f, 1.5f };

        static float[] Template(double gdpPerCapita) =>
            gdpPerCapita < 5_000.0 ? Low :
            gdpPerCapita < 15_000.0 ? LowMid :
            gdpPerCapita < 30_000.0 ? UpMid : High;

        // Build a country's sector breakdown: tier template, then raise any sector whose real
        // curated companies already produce more than the template would imply, then renormalize
        // to 100%. companies may be empty (most countries) — then it's purely the tier template.
        public static List<SectorState> Seed(double gdp, double gdpPerCapita, IReadOnlyList<Company> companies)
        {
            var template = Template(gdpPerCapita);
            var output = new double[All.Length];
            for (int i = 0; i < All.Length; i++)
                output[i] = gdp * template[i] / 100.0;

            if (companies != null)
            {
                var companyBySector = new double[All.Length];
                foreach (var co in companies)
                    companyBySector[(int)co.Sector] += co.OutputBillions;
                for (int i = 0; i < All.Length; i++)
                    if (companyBySector[i] > output[i]) output[i] = companyBySector[i];
            }

            double total = 0;
            for (int i = 0; i < output.Length; i++) total += output[i];
            if (total <= 0) total = 1;

            var list = new List<SectorState>(All.Length);
            for (int i = 0; i < All.Length; i++)
                list.Add(new SectorState { Sector = All[i], Share = (float)(output[i] / total * 100.0), Output = output[i] });
            return list;
        }

        // Daily drift + output refresh, called from Economy.Tick. Nudges shares toward the
        // higher-drift sectors (structural transformation), renormalizes to 100, and refreshes
        // each sector's Output against the current GDP. Returns a small growth adjustment (in
        // %/yr, already tiny) reflecting how productively the economy is composed — a country
        // weighted into high-margin, high-drift sectors gets a hair more trend growth.
        public static float TickAll(List<SectorState> sectors, double gdp)
        {
            if (sectors == null || sectors.Count == 0) return 0f;

            float sum = 0f;
            foreach (var s in sectors)
            {
                // Drift is per-YEAR; apply a day's worth. Clamp so a share never goes negative or
                // runs away above 100.
                s.Share = Clamp(s.Share + SectorInfo.ShareDrift(s.Sector) / 365f, 0.05f, 100f);
                sum += s.Share;
            }
            if (sum <= 0f) sum = 1f;

            float productivity = 0f;
            foreach (var s in sectors)
            {
                s.Share = s.Share / sum * 100f; // renormalize to 100
                s.Output = gdp * s.Share / 100.0;
                // Weighted average profit margin across the economy, centered on ~0.09 so a
                // service/tech/finance-heavy economy scores positive and an agriculture-heavy one
                // negative.
                productivity += s.Share / 100f * (float)(SectorInfo.ProfitMargin(s.Sector) - 0.09);
            }
            // Scale to at most ~±0.1%/yr — a real but bounded nudge, never enough to override the
            // tuned macro model.
            return productivity * 1.2f;
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
