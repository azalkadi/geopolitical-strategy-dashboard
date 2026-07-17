using System.Collections.Generic;

namespace Meridian.Sim
{
    // First slice of the "Government, Legislature and Real Taxes" vision pillar (see
    // docs/obsidian-vault/Vision/Government, Legislature and Real Taxes.md) — real government
    // type and real headline tax rates for a curated set of major/well-known countries, in the
    // same spirit as Geo/GeoJsonLoader's Curated Datasets: hand-researched, honest about not
    // being exhaustive (258 countries' real fiscal/political detail is not a single-session
    // research task), rather than a formula pretending to be real data.
    //
    // Precision note on TaxIncome: every real country's income tax is progressive (multiple
    // brackets), but this sim models income tax as ONE flat lever (EconomyState.TaxIncome).
    // Where a country has no personal income tax at all (most of the GCC), that's an exact,
    // confident 0%. Everywhere else, the seeded figure is a representative single number, not a
    // specific bracket — a genuine simplification of the real system, not a claim of bracket-
    // level accuracy. VAT/GST and headline corporate tax rates are simple published single
    // numbers for almost every country, so those are seeded with much higher confidence.
    public enum GovernmentType
    {
        Unspecified,
        AbsoluteMonarchy,
        ConstitutionalMonarchy,
        PresidentialRepublic,
        ParliamentaryRepublic,
        OneServiceState, // single-party / authoritarian one-party state
    }

    public class CountryProfile
    {
        public GovernmentType Government;
        public float? TaxIncome;
        public float? TaxCorporate;
        public float? TaxVat;
        public float? TaxTariff;
    }

    public static class CountryProfiles
    {
        // Keyed by ISO A3 (Country.IsoA3 — stable even for the handful of entries whose
        // ISO_A2 is "-99", see FlagLoader's override table for that same class of gap).
        public static readonly Dictionary<string, CountryProfile> ByIsoA3 = new()
        {
            // --- GCC: no personal income tax anywhere in the bloc; VAT/corporate vary a lot ---
            ["SAU"] = new CountryProfile { Government = GovernmentType.AbsoluteMonarchy, TaxIncome = 0f, TaxCorporate = 20f, TaxVat = 15f, TaxTariff = 5f },
            ["ARE"] = new CountryProfile { Government = GovernmentType.AbsoluteMonarchy, TaxIncome = 0f, TaxCorporate = 9f, TaxVat = 5f, TaxTariff = 5f },
            ["QAT"] = new CountryProfile { Government = GovernmentType.AbsoluteMonarchy, TaxIncome = 0f, TaxCorporate = 10f, TaxVat = 0f, TaxTariff = 5f },
            ["KWT"] = new CountryProfile { Government = GovernmentType.ConstitutionalMonarchy, TaxIncome = 0f, TaxCorporate = 15f, TaxVat = 0f, TaxTariff = 5f },
            ["BHR"] = new CountryProfile { Government = GovernmentType.ConstitutionalMonarchy, TaxIncome = 0f, TaxCorporate = 0f, TaxVat = 10f, TaxTariff = 5f },
            ["OMN"] = new CountryProfile { Government = GovernmentType.AbsoluteMonarchy, TaxIncome = 0f, TaxCorporate = 15f, TaxVat = 5f, TaxTariff = 5f },

            // --- G7 ---
            ["USA"] = new CountryProfile { Government = GovernmentType.PresidentialRepublic, TaxIncome = 22f, TaxCorporate = 21f, TaxVat = 0f, TaxTariff = 3f },
            ["GBR"] = new CountryProfile { Government = GovernmentType.ConstitutionalMonarchy, TaxIncome = 20f, TaxCorporate = 25f, TaxVat = 20f, TaxTariff = 2f },
            ["DEU"] = new CountryProfile { Government = GovernmentType.ParliamentaryRepublic, TaxIncome = 30f, TaxCorporate = 30f, TaxVat = 19f, TaxTariff = 2f },
            ["FRA"] = new CountryProfile { Government = GovernmentType.PresidentialRepublic, TaxIncome = 30f, TaxCorporate = 25f, TaxVat = 20f, TaxTariff = 2f },
            ["ITA"] = new CountryProfile { Government = GovernmentType.ParliamentaryRepublic, TaxIncome = 27f, TaxCorporate = 24f, TaxVat = 22f, TaxTariff = 2f },
            ["CAN"] = new CountryProfile { Government = GovernmentType.ParliamentaryRepublic, TaxIncome = 26f, TaxCorporate = 15f, TaxVat = 5f, TaxTariff = 2f },
            ["JPN"] = new CountryProfile { Government = GovernmentType.ParliamentaryRepublic, TaxIncome = 23f, TaxCorporate = 30f, TaxVat = 10f, TaxTariff = 2f },

            // --- Other major G20 ---
            ["CHN"] = new CountryProfile { Government = GovernmentType.OneServiceState, TaxIncome = 25f, TaxCorporate = 25f, TaxVat = 13f, TaxTariff = 7f },
            ["IND"] = new CountryProfile { Government = GovernmentType.ParliamentaryRepublic, TaxIncome = 20f, TaxCorporate = 25f, TaxVat = 18f, TaxTariff = 10f },
            ["BRA"] = new CountryProfile { Government = GovernmentType.PresidentialRepublic, TaxIncome = 27f, TaxCorporate = 34f, TaxVat = 17f, TaxTariff = 8f },
            ["RUS"] = new CountryProfile { Government = GovernmentType.PresidentialRepublic, TaxIncome = 13f, TaxCorporate = 20f, TaxVat = 20f, TaxTariff = 5f },
            ["AUS"] = new CountryProfile { Government = GovernmentType.ParliamentaryRepublic, TaxIncome = 30f, TaxCorporate = 30f, TaxVat = 10f, TaxTariff = 2f },
            ["KOR"] = new CountryProfile { Government = GovernmentType.PresidentialRepublic, TaxIncome = 24f, TaxCorporate = 24f, TaxVat = 10f, TaxTariff = 3f },
            ["MEX"] = new CountryProfile { Government = GovernmentType.PresidentialRepublic, TaxIncome = 25f, TaxCorporate = 30f, TaxVat = 16f, TaxTariff = 5f },
            ["IDN"] = new CountryProfile { Government = GovernmentType.PresidentialRepublic, TaxIncome = 20f, TaxCorporate = 22f, TaxVat = 11f, TaxTariff = 6f },
            ["TUR"] = new CountryProfile { Government = GovernmentType.PresidentialRepublic, TaxIncome = 27f, TaxCorporate = 25f, TaxVat = 20f, TaxTariff = 5f },
            ["ZAF"] = new CountryProfile { Government = GovernmentType.ParliamentaryRepublic, TaxIncome = 30f, TaxCorporate = 27f, TaxVat = 15f, TaxTariff = 5f },
            ["ARG"] = new CountryProfile { Government = GovernmentType.PresidentialRepublic, TaxIncome = 25f, TaxCorporate = 35f, TaxVat = 21f, TaxTariff = 8f },

            // --- Geopolitically load-bearing for the "real 2026 conflicts" pillar ---
            ["ISR"] = new CountryProfile { Government = GovernmentType.ParliamentaryRepublic, TaxIncome = 30f, TaxCorporate = 23f, TaxVat = 17f, TaxTariff = 3f },
            ["IRN"] = new CountryProfile { Government = GovernmentType.OneServiceState, TaxIncome = 20f, TaxCorporate = 25f, TaxVat = 9f, TaxTariff = 15f },
            ["UKR"] = new CountryProfile { Government = GovernmentType.ParliamentaryRepublic, TaxIncome = 18f, TaxCorporate = 18f, TaxVat = 20f, TaxTariff = 5f },
            ["SYR"] = new CountryProfile { Government = GovernmentType.PresidentialRepublic, TaxIncome = 15f, TaxCorporate = 28f, TaxVat = 0f, TaxTariff = 15f },
            ["EGY"] = new CountryProfile { Government = GovernmentType.PresidentialRepublic, TaxIncome = 22f, TaxCorporate = 22f, TaxVat = 14f, TaxTariff = 10f },
            ["JOR"] = new CountryProfile { Government = GovernmentType.ConstitutionalMonarchy, TaxIncome = 20f, TaxCorporate = 20f, TaxVat = 16f, TaxTariff = 8f },
            ["PAK"] = new CountryProfile { Government = GovernmentType.ParliamentaryRepublic, TaxIncome = 25f, TaxCorporate = 29f, TaxVat = 18f, TaxTariff = 10f },
            ["NGA"] = new CountryProfile { Government = GovernmentType.PresidentialRepublic, TaxIncome = 19f, TaxCorporate = 30f, TaxVat = 7.5f, TaxTariff = 12f },

            // --- Rounding out familiar/major regional powers ---
            ["ESP"] = new CountryProfile { Government = GovernmentType.ConstitutionalMonarchy, TaxIncome = 30f, TaxCorporate = 25f, TaxVat = 21f, TaxTariff = 2f },
            ["NLD"] = new CountryProfile { Government = GovernmentType.ConstitutionalMonarchy, TaxIncome = 30f, TaxCorporate = 25.8f, TaxVat = 21f, TaxTariff = 2f },
            ["SWE"] = new CountryProfile { Government = GovernmentType.ConstitutionalMonarchy, TaxIncome = 32f, TaxCorporate = 20.6f, TaxVat = 25f, TaxTariff = 2f },
            ["CHE"] = new CountryProfile { Government = GovernmentType.ParliamentaryRepublic, TaxIncome = 20f, TaxCorporate = 14.9f, TaxVat = 8.1f, TaxTariff = 1f },
            ["SGP"] = new CountryProfile { Government = GovernmentType.ParliamentaryRepublic, TaxIncome = 15f, TaxCorporate = 17f, TaxVat = 9f, TaxTariff = 0f },
            ["NOR"] = new CountryProfile { Government = GovernmentType.ConstitutionalMonarchy, TaxIncome = 27f, TaxCorporate = 22f, TaxVat = 25f, TaxTariff = 1f },
            ["THA"] = new CountryProfile { Government = GovernmentType.ConstitutionalMonarchy, TaxIncome = 20f, TaxCorporate = 20f, TaxVat = 7f, TaxTariff = 8f },
            ["VNM"] = new CountryProfile { Government = GovernmentType.OneServiceState, TaxIncome = 20f, TaxCorporate = 20f, TaxVat = 10f, TaxTariff = 8f },
            ["PRK"] = new CountryProfile { Government = GovernmentType.OneServiceState, TaxIncome = 0f, TaxCorporate = 25f, TaxVat = 0f, TaxTariff = 20f },
        };

        public static CountryProfile Get(string isoA3) =>
            !string.IsNullOrEmpty(isoA3) && ByIsoA3.TryGetValue(isoA3, out var p) ? p : null;
    }
}
