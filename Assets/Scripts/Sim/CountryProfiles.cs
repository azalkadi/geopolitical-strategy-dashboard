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

    // A real political party: economic lean in [-1 left .. +1 right] drives how it votes on
    // bills (see LegislatureSystem.PartySupports); SeatShare is its approximate share of the
    // lower house as of the mid-2020s. Seat shares are deliberately approximate — election
    // results shift and coalitions blur lines; what matters for the mechanic is the honest
    // relative balance of power, not decimal-exact seat counts. Countries with no Parties list
    // (monarchies, one-party states, uncurated countries) route bills through the decree path.
    public class PartyProfile
    {
        public string Name = "";
        public float EconLean;
        public float SeatShare;

        public PartyProfile() { }
        public PartyProfile(string name, float econLean, float seatShare)
        { Name = name; EconLean = econLean; SeatShare = seatShare; }
    }

    public class CountryProfile
    {
        public GovernmentType Government;
        public float? TaxIncome;
        public float? TaxCorporate;
        public float? TaxVat;
        public float? TaxTariff;
        public List<PartyProfile> Parties;
        public List<CompanySeed> Companies;
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

        // Real named parties for the curated multi-party countries — approximate lower-house
        // seat shares as of the mid-2020s (see PartyProfile's precision note). Multi-party
        // countries NOT yet given a list here (and every uncurated country) fall through to the
        // decree/executive path in LegislatureSystem — honest about what's been researched, the
        // same principle as the tax data above. Small parties are folded into an "Others" bloc
        // (lean ~0) so seat shares still sum to ~1 and votes stay honest.
        static CountryProfiles()
        {
            void P(string iso, params PartyProfile[] parties) =>
                ByIsoA3[iso].Parties = new List<PartyProfile>(parties);

            P("USA",
                new PartyProfile("Republicans", 0.7f, 0.51f),
                new PartyProfile("Democrats", -0.5f, 0.49f));
            P("GBR",
                new PartyProfile("Labour", -0.4f, 0.63f),
                new PartyProfile("Conservatives", 0.6f, 0.19f),
                new PartyProfile("Liberal Democrats", -0.1f, 0.11f),
                new PartyProfile("Others", 0f, 0.07f));
            P("DEU",
                new PartyProfile("CDU/CSU", 0.5f, 0.33f),
                new PartyProfile("AfD", 0.8f, 0.24f),
                new PartyProfile("SPD", -0.4f, 0.19f),
                new PartyProfile("Greens", -0.5f, 0.14f),
                new PartyProfile("Die Linke", -0.8f, 0.10f));
            P("FRA",
                new PartyProfile("New Popular Front", -0.7f, 0.31f),
                new PartyProfile("Ensemble", 0.1f, 0.28f),
                new PartyProfile("National Rally", 0.6f, 0.25f),
                new PartyProfile("Les Républicains", 0.6f, 0.08f),
                new PartyProfile("Others", 0f, 0.08f));
            P("ITA",
                new PartyProfile("Fratelli d'Italia", 0.7f, 0.30f),
                new PartyProfile("Partito Democratico", -0.4f, 0.17f),
                new PartyProfile("Movimento 5 Stelle", -0.3f, 0.13f),
                new PartyProfile("Forza Italia", 0.5f, 0.11f),
                new PartyProfile("Lega", 0.7f, 0.10f),
                new PartyProfile("Others", 0f, 0.19f));
            P("CAN",
                new PartyProfile("Liberals", -0.3f, 0.49f),
                new PartyProfile("Conservatives", 0.6f, 0.42f),
                new PartyProfile("Bloc Québécois", -0.2f, 0.06f),
                new PartyProfile("NDP", -0.7f, 0.03f));
            P("JPN",
                new PartyProfile("LDP", 0.5f, 0.40f),
                new PartyProfile("CDP", -0.3f, 0.32f),
                new PartyProfile("Ishin", 0.4f, 0.08f),
                new PartyProfile("DPP", 0.0f, 0.06f),
                new PartyProfile("Komeito", 0.1f, 0.05f),
                new PartyProfile("Others", 0f, 0.09f));
            P("IND",
                new PartyProfile("BJP", 0.4f, 0.44f),
                new PartyProfile("INC", -0.3f, 0.18f),
                new PartyProfile("Others", 0f, 0.38f));
            P("AUS",
                new PartyProfile("Labor", -0.3f, 0.62f),
                new PartyProfile("Liberal–National Coalition", 0.6f, 0.29f),
                new PartyProfile("Others", -0.2f, 0.09f));
            P("KOR",
                new PartyProfile("Democratic Party", -0.3f, 0.57f),
                new PartyProfile("People Power Party", 0.5f, 0.36f),
                new PartyProfile("Others", 0f, 0.07f));
            P("BRA",
                new PartyProfile("PL", 0.7f, 0.19f),
                new PartyProfile("PT", -0.6f, 0.13f),
                new PartyProfile("Centrão / Others", 0.1f, 0.68f));
            P("MEX",
                new PartyProfile("Morena", -0.6f, 0.55f),
                new PartyProfile("PAN", 0.5f, 0.14f),
                new PartyProfile("PRI", 0.2f, 0.07f),
                new PartyProfile("Others", 0f, 0.24f));
            P("TUR",
                new PartyProfile("AKP", 0.4f, 0.45f),
                new PartyProfile("CHP", -0.3f, 0.28f),
                new PartyProfile("Others", 0f, 0.27f));
            P("ISR",
                new PartyProfile("Likud", 0.5f, 0.27f),
                new PartyProfile("Religious & right bloc", 0.7f, 0.28f),
                new PartyProfile("Yesh Atid", -0.1f, 0.20f),
                new PartyProfile("Center-left & Arab bloc", -0.4f, 0.25f));
            P("ESP",
                new PartyProfile("PP", 0.5f, 0.39f),
                new PartyProfile("PSOE", -0.4f, 0.35f),
                new PartyProfile("Vox", 0.8f, 0.09f),
                new PartyProfile("Sumar", -0.7f, 0.09f),
                new PartyProfile("Others", 0f, 0.08f));
            P("NLD",
                new PartyProfile("PVV", 0.6f, 0.25f),
                new PartyProfile("GroenLinks–PvdA", -0.5f, 0.17f),
                new PartyProfile("VVD", 0.5f, 0.16f),
                new PartyProfile("NSC", 0.2f, 0.13f),
                new PartyProfile("Others", 0f, 0.29f));
            P("SWE",
                new PartyProfile("Social Democrats", -0.4f, 0.30f),
                new PartyProfile("Sweden Democrats", 0.6f, 0.21f),
                new PartyProfile("Moderates", 0.5f, 0.19f),
                new PartyProfile("Others", 0f, 0.30f));
            P("ZAF",
                new PartyProfile("ANC", -0.4f, 0.40f),
                new PartyProfile("DA", 0.3f, 0.22f),
                new PartyProfile("MK", -0.5f, 0.15f),
                new PartyProfile("EFF", -0.8f, 0.10f),
                new PartyProfile("Others", 0f, 0.13f));
            P("ARG",
                new PartyProfile("LLA & allies", 0.9f, 0.33f),
                new PartyProfile("Unión por la Patria", -0.5f, 0.40f),
                new PartyProfile("Others", 0f, 0.27f));
            P("RUS",
                new PartyProfile("United Russia", 0.3f, 0.72f),
                new PartyProfile("CPRF", -0.6f, 0.13f),
                new PartyProfile("Others", 0f, 0.15f));
            P("UKR",
                new PartyProfile("Servant of the People", 0.0f, 0.56f),
                new PartyProfile("Others", 0f, 0.44f));

            // Real, well-known companies per curated country — revenue figures are approximate/
            // rounded public-knowledge figures (annual revenue, billions USD) for gameplay
            // sizing only, not audited financials. Not exhaustive — same honesty pattern as
            // everything else in this file.
            void C(string iso, params CompanySeed[] companies) =>
                ByIsoA3[iso].Companies = new List<CompanySeed>(companies);

            C("SAU",
                new CompanySeed("Saudi Aramco", Sector.Energy, Ownership.Public, 500),
                new CompanySeed("SABIC", Sector.Manufacturing, Ownership.Public, 35),
                new CompanySeed("Al Rajhi Bank", Sector.Finance, Ownership.Private, 8));
            C("USA",
                new CompanySeed("Apple", Sector.Technology, Ownership.Private, 400),
                new CompanySeed("ExxonMobil", Sector.Energy, Ownership.Private, 340),
                new CompanySeed("JPMorgan Chase", Sector.Finance, Ownership.Private, 160));
            C("CHN",
                new CompanySeed("Sinopec", Sector.Energy, Ownership.Public, 400),
                new CompanySeed("ICBC", Sector.Finance, Ownership.Public, 200),
                new CompanySeed("Huawei", Sector.Technology, Ownership.Private, 95));
            C("DEU",
                new CompanySeed("Volkswagen", Sector.Manufacturing, Ownership.Private, 320),
                new CompanySeed("Siemens", Sector.Manufacturing, Ownership.Private, 80),
                new CompanySeed("Deutsche Bank", Sector.Finance, Ownership.Private, 30));
            C("JPN",
                new CompanySeed("Toyota", Sector.Manufacturing, Ownership.Private, 280),
                new CompanySeed("SoftBank", Sector.Technology, Ownership.Private, 55));
            C("GBR",
                new CompanySeed("BP", Sector.Energy, Ownership.Private, 210),
                new CompanySeed("HSBC", Sector.Finance, Ownership.Private, 65));
            C("FRA",
                new CompanySeed("TotalEnergies", Sector.Energy, Ownership.Private, 200),
                new CompanySeed("LVMH", Sector.Services, Ownership.Private, 90));
            C("RUS",
                new CompanySeed("Gazprom", Sector.Energy, Ownership.Public, 100),
                new CompanySeed("Rosneft", Sector.Energy, Ownership.Public, 130));
            C("IND",
                new CompanySeed("Reliance Industries", Sector.Energy, Ownership.Private, 100),
                new CompanySeed("Tata Motors", Sector.Manufacturing, Ownership.Private, 45));
            C("BRA",
                // Real, genuinely Mixed ownership: the Brazilian state holds a majority stake
                // in a publicly-traded company — one of the few real Mixed examples, kept as
                // such rather than rounded to Public or Private.
                new CompanySeed("Petrobras", Sector.Energy, Ownership.Mixed, 100));
            C("ARE",
                new CompanySeed("ADNOC", Sector.Energy, Ownership.Public, 100));
            C("CAN",
                new CompanySeed("Royal Bank of Canada", Sector.Finance, Ownership.Private, 50));
            C("KOR",
                new CompanySeed("Samsung Electronics", Sector.Technology, Ownership.Private, 230));
        }
    }
}
