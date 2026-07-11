using UnityEngine;

namespace Meridian.UI
{
    // The 8 ministry categories from the original Bevy dashboard's ministry bar, ported as-is
    // (same category set, same topic lists). All 8 now have real simulated data in
    // GameUIRoot's side-panel handlers (Economy/Budget/Trade from EconomyState, the rest from
    // NationalState) — no fabricated numbers, matching the original codebase's stated principle.
    public enum NationCategory { Economy, Budget, Trade, Politics, Diplomacy, Military, Society, Technology }

    public static class NationCategoryExt
    {
        public static string Label(this NationCategory c) => c switch
        {
            NationCategory.Economy => "Economy",
            NationCategory.Budget => "Budget",
            NationCategory.Trade => "Trade",
            NationCategory.Politics => "Politics",
            NationCategory.Diplomacy => "Diplomacy",
            NationCategory.Military => "Military",
            NationCategory.Society => "Society",
            NationCategory.Technology => "Technology",
            _ => c.ToString(),
        };

        public static string[] Topics(this NationCategory c) => c switch
        {
            NationCategory.Economy => new[] { "GDP & Growth", "Unemployment", "Inflation", "Tax Rates", "Interest Rate", "Treasury" },
            NationCategory.Budget => new[] { "Revenue", "Expenditures", "Deficit/Surplus", "Public Debt" },
            NationCategory.Trade => new[] { "Imports", "Exports", "Tariffs", "Trade Balance", "Trade Agreements" },
            NationCategory.Politics => new[] { "Parties", "Coalitions", "Elections", "Approval Ratings", "Corruption", "Constitutional Reform" },
            NationCategory.Diplomacy => new[] { "Treaties", "Alliances", "Foreign Relations", "International Standing" },
            NationCategory.Military => new[] { "Readiness", "Force Composition", "Defense Spending", "Doctrine" },
            NationCategory.Society => new[] { "Demographics", "Public Opinion", "Culture", "Migration" },
            NationCategory.Technology => new[] { "Research", "Innovation Index", "Infrastructure", "Adoption" },
            _ => System.Array.Empty<string>(),
        };

        // Distinct accent color per category so the active ministry tab, its side-panel
        // border, and section headers all visually "belong" to that ministry — a standard
        // strategy-dashboard pattern for letting players distinguish domains at a glance
        // without reading labels every time. Muted, flag/institution-inspired hues (not neon
        // candy tones) so the palette reads as diplomatic rather than a toy.
        public static Color Accent(this NationCategory c) => c switch
        {
            NationCategory.Economy => new Color(0.80f, 0.66f, 0.34f),    // gold (treasury/seal)
            NationCategory.Budget => new Color(0.36f, 0.68f, 0.58f),     // muted emerald
            NationCategory.Trade => new Color(0.30f, 0.52f, 0.78f),      // UN blue
            NationCategory.Politics => new Color(0.56f, 0.42f, 0.68f),   // muted royal purple
            NationCategory.Diplomacy => new Color(0.34f, 0.70f, 0.76f),  // steel cyan
            NationCategory.Military => new Color(0.70f, 0.32f, 0.30f),   // muted crimson
            NationCategory.Society => new Color(0.52f, 0.62f, 0.34f),    // olive/laurel green
            NationCategory.Technology => new Color(0.50f, 0.54f, 0.66f), // slate blue-gray
            _ => Color.white,
        };
    }
}
