using System.Collections.Generic;
using UnityEngine;
using Meridian.Geo;

namespace Meridian.Sim
{
    // Supranational unions as a real game system — the "Supranational Unions" vision pillar
    // (docs/obsidian-vault/Vision/Supranational Unions.md). The blocs curated in WorldAlignments
    // (NATO, EU, GCC, ASEAN, CSTO, Five Eyes, ...) already set relation floors between members;
    // this turns membership into real, per-FUNCTION passive effects, because a trade union and a
    // military alliance do very different things:
    //   - Economic (EU, GCC, ASEAN, Mercosur, Nordic, Benelux, CARICOM): a single market lifts
    //     members' exports and growth, scaled by how big the bloc is in this game.
    //   - Military (NATO, CSTO, AES): collective security — a standing + readiness bonus, plus
    //     mutual-defence anger (attacking a member turns its whole alliance against the aggressor;
    //     see WarSystem.Declare).
    //   - Intelligence (Five Eyes): a shared-intelligence standing bonus.
    //   - Political (Visegrad, Turkic States, Baltic Assembly): alignment only — the relation
    //     floor, no economic/military bonus.
    //
    // The registry (who is in what) is always rebuilt from WorldAlignments on load; the passive
    // effects are baked into serialized EconomyState/NationalState fields at initial seed only,
    // so they must NOT be re-applied on load or they'd double.
    public class UnionSystem
    {
        List<WorldAlignments.Bloc>[] byCountry;
        Dictionary<string, int> isoIndex;

        public static UnionSystem Build(IReadOnlyList<Country> countries)
        {
            int n = countries.Count;
            var sys = new UnionSystem
            {
                byCountry = new List<WorldAlignments.Bloc>[n],
                isoIndex = new Dictionary<string, int>(),
            };
            for (int i = 0; i < n; i++) sys.byCountry[i] = new List<WorldAlignments.Bloc>();
            for (int i = 0; i < n; i++)
                if (!string.IsNullOrEmpty(countries[i].IsoA3) && !sys.isoIndex.ContainsKey(countries[i].IsoA3))
                    sys.isoIndex[countries[i].IsoA3] = i;

            foreach (var bloc in WorldAlignments.Blocs)
                foreach (var iso in bloc.Members)
                    if (sys.isoIndex.TryGetValue(iso, out int ci))
                        sys.byCountry[ci].Add(bloc);
            return sys;
        }

        static readonly WorldAlignments.Bloc[] None = new WorldAlignments.Bloc[0];
        public IReadOnlyList<WorldAlignments.Bloc> MembershipsOf(int i) =>
            (byCountry != null && i >= 0 && i < byCountry.Length) ? byCountry[i] : None;

        // Count of a bloc's members actually present in this game's country list (some ISO codes
        // may be absent). Bigger present blocs give bigger economic effects.
        int PresentCount(WorldAlignments.Bloc bloc)
        {
            int c = 0;
            foreach (var iso in bloc.Members) if (isoIndex.ContainsKey(iso)) c++;
            return c;
        }

        // Bakes the passive per-function effects into the (serialized) economy/national fields.
        // Call ONCE at initial world seed — never on load (the values are already in the save).
        public void ApplyPassiveEffects(EconomySystem econ, NationalSystem nat)
        {
            var present = new Dictionary<string, int>();
            foreach (var bloc in WorldAlignments.Blocs) present[bloc.Name] = PresentCount(bloc);

            for (int i = 0; i < byCountry.Length && i < econ.States.Count && i < nat.States.Count; i++)
            {
                var e = econ.States[i];
                var na = nat.States[i];
                float standing = 0f, readiness = 0f;
                foreach (var bloc in byCountry[i])
                {
                    int size = present.TryGetValue(bloc.Name, out int s) ? s : bloc.Members.Length;
                    switch (bloc.Type)
                    {
                        case WorldAlignments.UnionType.Economic:
                            // Single-market export/growth dividend, scaled by bloc size (a member
                            // of the 27-strong EU gains far more than one of a 3-member bloc).
                            e.TradeAgreementExportBonus += 0.003f * Mathf.Min(size, 20);
                            break;
                        case WorldAlignments.UnionType.Military:
                            standing += 4f; readiness += 5f;
                            break;
                        case WorldAlignments.UnionType.Intelligence:
                            standing += 4f;
                            break;
                        case WorldAlignments.UnionType.Political:
                            break; // alignment only
                    }
                }
                na.AllianceStandingBonus = Mathf.Min(standing, 12f);
                na.AllianceReadinessBonus = Mathf.Min(readiness, 12f);
            }
        }

        // Fellow MILITARY-alliance members of a country (indices) — the countries that treat an
        // attack on it as their concern (see WarSystem mutual-defence). Excludes the country
        // itself. Cheap; called only when a war is declared.
        public List<int> MilitaryAlliesOf(int country)
        {
            var allies = new List<int>();
            if (byCountry == null || country < 0 || country >= byCountry.Length) return allies;
            foreach (var bloc in byCountry[country])
            {
                if (bloc.Type != WorldAlignments.UnionType.Military) continue;
                foreach (var iso in bloc.Members)
                    if (isoIndex.TryGetValue(iso, out int ci) && ci != country && !allies.Contains(ci))
                        allies.Add(ci);
            }
            return allies;
        }
    }
}
