using System.Collections.Generic;
using Meridian.Geo;

namespace Meridian.Sim
{
    // Real early-2026 geopolitical alignment, overlaid on DiplomacySystem's geography-derived
    // baseline at world seed time. Same "hand-researched, real, not a formula" principle as
    // CountryProfiles and the Curated Datasets: alliance blocs whose members are systematically
    // warm, bilateral hostilities/friendships that geography alone can't know about, and the
    // interstate wars actually burning when the game's clock starts (Jan 1 2026). Kosovo=KOS,
    // Taiwan=TWN, Palestine=PSE, N.Cyprus=CYN per the Natural Earth codes the map itself uses.
    //
    // Data curated as of January 2026 and adversarially fact-checked (multi-agent verification
    // pass) before being committed — see docs/obsidian-vault/Data Sources.
    public static class WorldAlignments
    {
        public class Bloc
        {
            public string Name;
            public float Floor;      // minimum bilateral relation between any two members
            public string[] Members; // ISO A3
            public Bloc(string name, float floor, params string[] members)
            { Name = name; Floor = floor; Members = members; }
        }

        public class PairSeed
        {
            public string A, B;
            public float Relation;   // relation AND baseline both set to this
            public bool AtWar;       // seed an actual active War at game start
            public long WarStartDaysAgo;   // how long the war has been running at day 0
            public float WarScore;         // from the A-side's perspective (A = attacker)
            public float WarExhaustionA, WarExhaustionB;
            public string Why;
            public PairSeed(string a, string b, float relation, string why)
            { A = a; B = b; Relation = relation; Why = why; }
        }

        // Filled from the curated dataset — see the static constructor region below.
        public static readonly List<Bloc> Blocs = new();
        public static readonly List<PairSeed> Pairs = new();

        static WorldAlignments()
        {
            void B(string name, float floor, params string[] members) => Blocs.Add(new Bloc(name, floor, members));
            void P(string a, string b, float rel, string why) => Pairs.Add(new PairSeed(a, b, rel, why));
            void W(string a, string b, float rel, long daysAgo, float score, float exhA, float exhB, string why) =>
                Pairs.Add(new PairSeed(a, b, rel, why) { AtWar = true, WarStartDaysAgo = daysAgo, WarScore = score, WarExhaustionA = exhA, WarExhaustionB = exhB });

            // ---- Alliance / integration blocs (floors; most-specific pair overrides below) ----
            B("NATO", 70, "USA", "CAN", "GBR", "FRA", "DEU", "ITA", "ESP", "PRT", "NLD", "BEL", "LUX", "DNK", "NOR", "ISL",
                "POL", "CZE", "SVK", "HUN", "ROU", "BGR", "GRC", "TUR", "ALB", "HRV", "SVN", "MNE", "MKD",
                "EST", "LVA", "LTU", "FIN", "SWE"); // 32 members: Finland 2023, Sweden 2024
            B("European Union", 72, "AUT", "BEL", "BGR", "HRV", "CYP", "CZE", "DNK", "EST", "FIN", "FRA", "DEU", "GRC",
                "HUN", "IRL", "ITA", "LVA", "LTU", "LUX", "MLT", "NLD", "POL", "PRT", "ROU", "SVK", "SVN", "ESP", "SWE");
            B("Gulf Cooperation Council", 70, "SAU", "ARE", "KWT", "QAT", "BHR", "OMN"); // post-AlUla reconciliation
            B("CSTO", 65, "RUS", "BLR", "KAZ", "KGZ", "TJK"); // Armenia excluded: participation frozen since 2024
            B("ASEAN", 55, "BRN", "KHM", "IDN", "LAO", "MYS", "MMR", "PHL", "SGP", "THA", "VNM", "TLS"); // TLS admitted Oct 2025
            B("Five Eyes", 85, "USA", "GBR", "CAN", "AUS", "NZL");
            B("Nordic Council", 85, "DNK", "FIN", "ISL", "NOR", "SWE");
            B("Baltic Assembly", 85, "EST", "LVA", "LTU");
            B("Benelux", 85, "BEL", "NLD", "LUX");
            B("Visegrad Group", 58, "POL", "CZE", "SVK", "HUN"); // cohesion eroded over Ukraine
            B("Mercosur", 62, "ARG", "BRA", "PRY", "URY", "BOL"); // Bolivia full member since July 2024
            B("Organization of Turkic States", 60, "TUR", "AZE", "KAZ", "KGZ", "UZB");
            B("Alliance of Sahel States", 75, "MLI", "BFA", "NER"); // post-coup confederation, left ECOWAS Jan 2025
            B("CARICOM", 65, "ATG", "BHS", "BRB", "BLZ", "DMA", "GRD", "GUY", "HTI", "JAM", "KNA", "LCA", "VCT", "SUR", "TTO");

            // ---- Active interstate wars at game start (Jan 1 2026) ----
            // Russia-Ukraine: full-scale invasion since Feb 24 2022 = 1,407 days before the game
            // epoch. Modest attacker score edge (holds territory, no decisive breakthrough),
            // both sides deeply worn — seeded below the >70 mutual-exhaustion threshold so the
            // war continues but a negotiated end within the first game year is plausible.
            W("RUS", "UKR", 0, 1407, 12f, 58f, 64f, "Full-scale Russian invasion of Ukraine ongoing since Feb 2022");

            // ---- Severe structural hostility (relations single digits to mid-teens) ----
            P("ISR", "PSE", 3, "Gaza war since Oct 2023; fragile Oct 2025 ceasefire, West Bank violence continues");
            P("ISR", "IRN", 4, "June 2025 12-day war with direct missile exchanges; uneasy ceasefire, nuclear standoff");
            P("USA", "IRN", 6, "US struck Iranian nuclear sites June 2025; sanctions, no diplomatic relations");
            P("ISR", "LBN", 8, "Nov 2024 Hezbollah ceasefire, but strikes continue and border points remain occupied");
            P("ISR", "SYR", 12, "Israel occupies buffer zone and strikes Syria post-Assad; security talks unresolved");
            P("ISR", "YEM", 15, "Houthi attacks halted after Oct 2025 Gaza ceasefire — hostile but quiet");
            P("TUR", "ISR", 15, "Relations collapsed over Gaza; trade suspended, rivalry in Syria");
            P("PRK", "KOR", 6, "Kim declared the South a 'hostile state', cut all inter-Korean links");
            P("PRK", "USA", 10, "Nuclear/ICBM standoff, sanctions, no diplomatic relations");
            P("PRK", "JPN", 12, "Missile overflights, abduction issue, no diplomatic relations");
            P("CHN", "TWN", 8, "Beijing claims Taiwan; large-scale drills and blockade rehearsals");
            P("IND", "PAK", 6, "May 2025 Operation Sindoor clashes; Indus Waters Treaty suspended; Kashmir");
            P("AFG", "PAK", 10, "Oct 2025 border clashes and strikes over TTP sanctuaries; talks collapsed");
            P("THA", "KHM", 12, "July 2025 border war over temple areas; ceasefire violated repeatedly");
            P("RUS", "USA", 15, "Post-2022 sanctions and proxy confrontation over Ukraine");
            P("RUS", "GBR", 10, "UK leads Ukraine support; sabotage and naval shadowing");
            P("RUS", "POL", 8, "Drone incursions into Polish airspace 2025, hybrid sabotage");
            P("RUS", "EST", 10, "Airspace violations, GPS jamming, Baltic cable sabotage");
            P("RUS", "LTU", 10, "Kaliningrad transit disputes, hybrid attacks");
            P("RUS", "FIN", 13, "Border fully closed since 2023; NATO accession hostility");
            P("RUS", "MDA", 12, "Transnistria occupation, election interference, energy coercion");
            P("UKR", "BLR", 8, "Belarus staged the 2022 invasion and hosts Russian nuclear weapons");
            P("SRB", "KOS", 10, "Serbia rejects Kosovo independence; north Kosovo standoffs");
            P("CYP", "CYN", 12, "Island divided since 1974; new pro-federation TRNC leadership (Oct 2025) but recognition disputes persist");
            P("MAR", "DZA", 8, "Relations severed 2021 over Western Sahara; borders closed, arms race");
            P("MLI", "DZA", 12, "Algeria downed a Malian drone April 2025; mutual airspace bans");
            P("ETH", "ERI", 10, "Post-Tigray rupture; Red Sea port ambitions raise war fears");
            P("COD", "RWA", 10, "Rwanda-backed M23 seized Goma 2025; peace framework fragile");
            P("SDN", "ARE", 8, "Sudan severed ties 2025, accusing the UAE of arming the RSF");
            P("SOM", "SOL", 12, "Somalia claims Somaliland, which seeks recognition");
            P("USA", "VEN", 5, "US strikes on alleged trafficking boats, carrier deployment, regime-change pressure");
            P("VEN", "GUY", 10, "Venezuela claims Essequibo; annexation referendum");
            P("USA", "CUB", 13, "Six-decade embargo, terrorism listing restored, tightened sanctions");

            // ---- Tense (real friction, functional relations: 15-35) ----
            P("IRN", "SYR", 15, "Post-Assad Syria expelled Iranian forces; Damascus blames Tehran for backing the old regime");
            P("USA", "CHN", 25, "Strategic rivalry: tariffs, export controls, Taiwan — but managed dialogue");
            P("CHN", "IND", 32, "Disputed Himalayan border; partial thaw since 2024 Modi-Xi meetings");
            P("CHN", "JPN", 20, "Nov 2025 crisis over Taiwan remarks; seafood bans, Senkaku incursions");
            P("CHN", "PHL", 20, "Water-cannon and ramming incidents at Second Thomas and Scarborough Shoals");
            P("CHN", "VNM", 32, "Overlapping South China Sea claims despite strong trade ties");
            P("GRC", "TUR", 30, "Aegean airspace/continental shelf disputes and Cyprus, amid periodic detente");
            P("CYP", "TUR", 20, "Turkey occupies northern Cyprus and contests Cypriot EEZ gas exploration");
            P("EGY", "ETH", 25, "GERD dam completed 2025 without a Nile water-sharing agreement");
            P("ETH", "SOM", 30, "Somaliland port MoU angered Mogadishu; Ankara Declaration partial fix");
            P("SSD", "SDN", 28, "Oil pipeline transit disputes, Abyei status, civil war spillover");
            P("RWA", "BDI", 20, "Burundi closed the border in 2024 accusing Rwanda of backing rebels");
            P("DZA", "FRA", 25, "2024-25 diplomatic crisis over Western Sahara stance; expulsions");
            P("ARM", "AZE", 30, "Aug 2025 Washington peace declaration after 2023 Karabakh exodus; corridor still sensitive");
            P("AZE", "RUS", 22, "AZAL plane downing Dec 2024 and 2025 arrests soured ties");
            P("GEO", "RUS", 22, "Russia occupies Abkhazia/South Ossetia; pragmatic but no diplomatic ties");
            P("BLR", "POL", 20, "Engineered migrant crisis at the border, Wagner presence");
            P("BLR", "LTU", 20, "Border closures over smuggling balloons and migrant instrumentalization");
            P("HUN", "UKR", 30, "Orban blocks EU accession/aid; minority and pipeline disputes");
            P("IND", "BGD", 30, "Post-Hasina interim government friction: extradition, trade curbs");
            P("MMR", "BGD", 25, "Rohingya refugee crisis and Arakan Army takeover of the shared border");
            P("JPN", "RUS", 22, "Kuril Islands dispute; peace-treaty talks frozen, sanctions since 2022");
            P("COL", "USA", 28, "Petro-Trump clashes: drug decertification, tariff threats");
            P("ARG", "GBR", 35, "Falklands/Malvinas sovereignty claim persists despite functional ties");
            P("USA", "DNK", 30, "Trump pressure to acquire Greenland; spying reports, tariff threats");
            P("RUS", "DEU", 18, "Drone incursions over German airports 2025; Germany a top Ukraine backer");
            P("USA", "ZAF", 28, "Aid cut, ambassador expelled, 30% tariffs, G20 boycott");
            P("USA", "CAN", 32, "Trade war (35% tariffs), annexation rhetoric, talks terminated Oct 2025");

            // ---- Special allies (80-92, beyond any bloc floor) ----
            P("USA", "ISR", 90, "Decades-long strategic alliance, $3.8B annual military aid");
            P("USA", "JPN", 88, "1960 Mutual Security Treaty, ~54,000 US troops based in Japan");
            P("USA", "KOR", 85, "1953 Mutual Defense Treaty, ~28,500 US troops");
            P("USA", "PHL", 82, "1951 Mutual Defense Treaty, expanded EDCA base access");
            P("USA", "AUS", 88, "ANZUS treaty, AUKUS submarine pact, Five Eyes");
            P("USA", "GBR", 90, "Special relationship: AUKUS, Five Eyes, nuclear cooperation");
            P("CHN", "PAK", 88, "'All-weather' strategic partnership, CPEC corridor, primary arms supplier");
            P("CHN", "PRK", 80, "1961 mutual aid treaty, North Korea's economic lifeline");
            P("CHN", "RUS", 85, "'No limits' partnership since 2022; alignment against the West");
            P("RUS", "BLR", 90, "Union State integration, Russian tactical nukes stationed in Belarus");
            P("RUS", "PRK", 86, "June 2024 mutual defense treaty; DPRK troops supporting Russia in Ukraine");
            P("IND", "RUS", 80, "'Special and privileged strategic partnership'; S-400s, discounted oil");
            P("IND", "BTN", 85, "2007 friendship treaty; India guides Bhutan's defense");
            P("TUR", "AZE", 90, "'One nation, two states'; Shusha Declaration mutual defense");
            P("TUR", "CYN", 92, "Turkey is the sole recognizer of Northern Cyprus, ~35,000 troops garrisoned");
            P("TUR", "QAT", 82, "Turkish military base in Doha since the 2017 blockade");
            P("SAU", "PAK", 84, "Sept 2025 Strategic Mutual Defence Agreement");
            P("KOS", "ALB", 88, "Shared Albanian nationhood; open border, coordinated foreign policy");
            P("GRC", "CYP", 90, "Hellenic kin-states with joint defense doctrine");
            P("FRA", "GRC", 80, "2021 bilateral defense pact with mutual assistance clause");
            P("CUB", "VEN", 85, "ALBA axis: Venezuelan oil for Cuban doctors and intelligence");
            P("VEN", "NIC", 80, "ALBA anti-US axis; mutual regime backing");
            P("CUB", "NIC", 80, "ALBA partners; ideological and security alignment");
            P("CHN", "KHM", 82, "'Ironclad' friendship; Ream naval base, dominant investment");
            P("MLI", "BFA", 86, "AES confederation mutual defense pact");
            P("MLI", "NER", 85, "AES confederation mutual defense pact");
            P("BFA", "NER", 85, "AES confederation mutual defense pact");

            // ---- Friendly partnerships (65-78, beyond geography/bloc) ----
            P("USA", "TWN", 78, "Taiwan Relations Act arms sales; strategic ambiguity, no formal treaty");
            P("USA", "IND", 65, "Quad partners, defense tech ties, strained by 2025 tariff dispute");
            P("JPN", "IND", 72, "Special Strategic and Global Partnership; Quad, infrastructure investment");
            P("AUS", "JPN", 75, "Reciprocal Access Agreement; closest quasi-alliance outside the US");
            P("JPN", "PHL", 72, "2024 Reciprocal Access Agreement, coast guard cooperation");
            P("VNM", "USA", 68, "2023 Comprehensive Strategic Partnership, Vietnam's highest tier");
            P("ARE", "ISR", 68, "2020 Abraham Accords; ties survived the Gaza war");
            P("MAR", "ISR", 65, "Abraham Accords normalization tied to Western Sahara recognition");
            P("GRC", "EGY", 72, "EEZ delimitation deal, East Med energy alignment");
            P("AZE", "ISR", 72, "Azerbaijani oil for Israeli arms; quiet strategic axis near Iran");
            P("TUR", "PAK", 75, "Deep defense-industrial cooperation (drones, frigates)");
            P("IRN", "RUS", 75, "Jan 2025 Comprehensive Strategic Partnership Treaty; drone cooperation");
            P("IRN", "CHN", 70, "25-year cooperation agreement; main buyer of sanctioned Iranian oil");
            P("IRN", "VEN", 68, "Sanctions-evasion partnership: fuel shipments, refinery repair");
            P("RUS", "VEN", 72, "2025 strategic partnership treaty; arms, oil ventures");
            P("RUS", "SRB", 72, "Orthodox-Slavic ties; Serbia refuses sanctions, depends on Russian gas");
            P("CHN", "SRB", 74, "'Ironclad friendship'; 2024 free trade agreement, Belt and Road");
            P("ARM", "FRA", 70, "French arms deliveries and political backing after Armenia's pivot from Moscow");
            P("RUS", "MLI", 72, "Africa Corps forces underpin the junta after French withdrawal");
            P("RUS", "BFA", 70, "Africa Corps deployment and junta security patronage");
            P("RUS", "NER", 70, "Africa Corps deployment and junta security patronage");
            P("TUR", "SYR", 72, "Ankara is chief patron of the post-Assad government installed Dec 2024");
            P("SAU", "USA", 75, "2025 reset: arms/investment deals, major non-NATO ally designation");
            P("USA", "QAT", 72, "Al Udeid air base; Oct 2025 written US security guarantee");
            P("UKR", "GBR", 75, "Jan 2025 '100-Year Partnership' treaty; staunchest military backer");
            P("SOL", "TWN", 70, "Mutual representative offices; two unrecognized partners");
            P("KOS", "USA", 78, "US-led 1999 intervention and Camp Bondsteel make Washington the guarantor");
            P("PRT", "BRA", 76, "Lusophone special relationship: reciprocal citizen rights");
            // Taiwan's remaining formal diplomatic partners as of Jan 2026.
            P("TWN", "GTM", 65, "One of Taiwan's remaining formal diplomatic allies");
            P("TWN", "PRY", 65, "One of Taiwan's remaining formal diplomatic allies");
            P("TWN", "BLZ", 65, "One of Taiwan's remaining formal diplomatic allies");
            P("TWN", "PLW", 65, "One of Taiwan's remaining formal diplomatic allies");
            P("TWN", "MHL", 65, "One of Taiwan's remaining formal diplomatic allies");
            P("TWN", "TUV", 65, "One of Taiwan's remaining formal diplomatic allies");
            P("TWN", "SWZ", 65, "One of Taiwan's remaining formal diplomatic allies");
        }

        // Blocs first (broad floors), then explicit pairs (most specific wins — a curated pair
        // overrides any bloc floor, e.g. GRC-TUR are both NATO but genuinely tense).
        // Returns how many pairs were touched, for the boot log.
        public static int Apply(IReadOnlyList<Country> countries, DiplomacySystem dip)
        {
            var index = BuildIndex(countries);
            int touched = 0;

            foreach (var bloc in Blocs)
            {
                for (int i = 0; i < bloc.Members.Length; i++)
                {
                    if (!index.TryGetValue(bloc.Members[i], out int a)) continue;
                    for (int j = i + 1; j < bloc.Members.Length; j++)
                    {
                        if (!index.TryGetValue(bloc.Members[j], out int b)) continue;
                        if (dip.GetRelation(a, b) < bloc.Floor)
                        {
                            SetBoth(dip, a, b, bloc.Floor);
                            touched++;
                        }
                    }
                }
            }

            foreach (var p in Pairs)
            {
                if (!index.TryGetValue(p.A, out int a) || !index.TryGetValue(p.B, out int b)) continue;
                SetBoth(dip, a, b, p.Relation);
                touched++;
            }
            return touched;
        }

        // Seeds the wars actually in progress at game start. Bypasses Declare()'s eligibility
        // gates on purpose — this is recorded history, not a new declaration, so no fresh
        // rally-round-the-flag approval bumps or standing hits either (those happened years
        // ago in the real world; the seeded exhaustion below is their long-run residue).
        public static int SeedWars(IReadOnlyList<Country> countries, WarSystem wars)
        {
            var index = BuildIndex(countries);
            int seeded = 0;
            foreach (var p in Pairs)
            {
                if (!p.AtWar) continue;
                if (!index.TryGetValue(p.A, out int a) || !index.TryGetValue(p.B, out int b)) continue;
                if (wars.WarBetween(a, b) != null) continue;
                wars.Active.Add(new War
                {
                    Attacker = a,
                    Defender = b,
                    StartDay = -p.WarStartDaysAgo,
                    Score = p.WarScore,
                    ExhaustionAttacker = p.WarExhaustionA,
                    ExhaustionDefender = p.WarExhaustionB,
                });
                seeded++;
            }
            return seeded;
        }

        static Dictionary<string, int> BuildIndex(IReadOnlyList<Country> countries)
        {
            var index = new Dictionary<string, int>();
            for (int i = 0; i < countries.Count; i++)
                if (!string.IsNullOrEmpty(countries[i].IsoA3) && !index.ContainsKey(countries[i].IsoA3))
                    index[countries[i].IsoA3] = i;
            return index;
        }

        // Baseline too, not just the live value — DiplomacySystem.TickAll drifts relations
        // back toward baseline daily, so setting only the live value would see the whole
        // curated world decay back to the geography hash within a few game years.
        static void SetBoth(DiplomacySystem dip, int a, int b, float value)
        {
            float current = dip.GetRelation(a, b);
            dip.ChangeRelation(a, b, value - current);
            int lo = a < b ? a : b, hi = a < b ? b : a;
            dip.Baselines[lo * (2 * dip.Count - lo - 1) / 2 + (hi - lo - 1)] = value;
        }
    }
}
