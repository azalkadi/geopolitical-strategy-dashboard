using System.Collections.Generic;
using UnityEngine;
using Meridian.Geo;

namespace Meridian.Sim
{
    // Supranational unions — the "Supranational Unions" vision pillar, now also the substrate for
    // Consequence Engine §4 (accession). Membership is MUTABLE and SERIALIZED: states can join
    // and leave during a game, which is the whole point of an invitation pipeline. It is seeded
    // from the curated WorldAlignments blocs at world start and then belongs to the save, NOT
    // re-derived from static data on load (doing that would silently undo every accession).
    //
    // Per-function effects (a trade union and a military alliance do very different things):
    //   - Economic   → single-market export/growth dividend, scaled by bloc size
    //   - Military   → standing + readiness, plus mutual-defence anger (WarSystem.Declare)
    //   - Intelligence → standing
    //   - Political  → alignment only (the relation floor), no bonus
    //
    // IDEMPOTENCE RULE — the trap this class must never fall into again: effects are recomputed
    // from scratch every time membership changes, so they must be SET, never accumulated. The
    // economic dividend therefore lives in its own EconomyState.UnionExportBonus field rather
    // than being added into TradeAgreementExportBonus (which player trade deals also mutate).
    // Adding into a shared field would double the bonus on every single join.

    public class MutableBloc
    {
        public string Name = "";
        public WorldAlignments.UnionType Type;
        public float Floor;
        public List<string> MemberIsos = new();

        // Members admitted with AccessionTerms.GuaranteesPreserved — they kept their outside
        // security relationships, so this bloc's MUTUAL DEFENCE does not extend to them (see
        // MilitaryAlliesOf). The concession that bought their yes is the concession that hollows
        // the alliance out. §4.
        public List<string> GuaranteedMembers = new();
    }

    public class UnionSystem
    {
        // Serialized state — the live membership of every bloc in THIS game.
        public List<MutableBloc> Blocs = new();

        // Derived indices, rebuilt from Blocs after load (never serialized).
        [Newtonsoft.Json.JsonIgnore] List<MutableBloc>[] byCountry;
        [Newtonsoft.Json.JsonIgnore] Dictionary<string, int> isoIndex;

        // Fresh world: copy the curated blocs into mutable per-game state.
        public static UnionSystem Seed(IReadOnlyList<Country> countries)
        {
            var sys = new UnionSystem();
            foreach (var b in WorldAlignments.Blocs)
                sys.Blocs.Add(new MutableBloc
                {
                    Name = b.Name,
                    Type = b.Type,
                    Floor = b.Floor,
                    MemberIsos = new List<string>(b.Members),
                });
            sys.RebuildIndex(countries);
            return sys;
        }

        // Rebuilds the country→blocs lookup from the (serialized) membership lists. Call after
        // load and after any membership change.
        public void RebuildIndex(IReadOnlyList<Country> countries)
        {
            int n = countries.Count;
            byCountry = new List<MutableBloc>[n];
            for (int i = 0; i < n; i++) byCountry[i] = new List<MutableBloc>();

            isoIndex = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
                if (!string.IsNullOrEmpty(countries[i].IsoA3) && !isoIndex.ContainsKey(countries[i].IsoA3))
                    isoIndex[countries[i].IsoA3] = i;

            foreach (var bloc in Blocs)
                foreach (var iso in bloc.MemberIsos)
                    if (isoIndex.TryGetValue(iso, out int ci))
                        byCountry[ci].Add(bloc);
        }

        static readonly MutableBloc[] None = new MutableBloc[0];
        public IReadOnlyList<MutableBloc> MembershipsOf(int i) =>
            (byCountry != null && i >= 0 && i < byCountry.Length) ? (IReadOnlyList<MutableBloc>)byCountry[i] : None;

        public MutableBloc FindBloc(string name)
        {
            foreach (var b in Blocs) if (b.Name == name) return b;
            return null;
        }

        public bool IsMember(string blocName, string iso)
        {
            var b = FindBloc(blocName);
            return b != null && b.MemberIsos.Contains(iso);
        }

        // --- accession / departure (§4) -------------------------------------------------
        // Both recompute effects from scratch afterwards, which is safe precisely because
        // ApplyPassiveEffects SETS rather than accumulates.

        public bool AddMember(string blocName, string iso, IReadOnlyList<Country> countries,
                              EconomySystem econ, NationalSystem nat)
        {
            var b = FindBloc(blocName);
            if (b == null || string.IsNullOrEmpty(iso) || b.MemberIsos.Contains(iso)) return false;
            b.MemberIsos.Add(iso);
            RebuildIndex(countries);
            ApplyPassiveEffects(econ, nat);
            return true;
        }

        public bool RemoveMember(string blocName, string iso, IReadOnlyList<Country> countries,
                                 EconomySystem econ, NationalSystem nat)
        {
            var b = FindBloc(blocName);
            if (b == null || !b.MemberIsos.Remove(iso)) return false;
            RebuildIndex(countries);
            ApplyPassiveEffects(econ, nat);
            return true;
        }

        string IsoOf(int country)
        {
            if (isoIndex == null) return null;
            foreach (var kv in isoIndex) if (kv.Value == country) return kv.Key;
            return null;
        }

        int PresentCount(MutableBloc bloc)
        {
            int c = 0;
            foreach (var iso in bloc.MemberIsos) if (isoIndex.ContainsKey(iso)) c++;
            return c;
        }

        // Recomputes every state's union-derived effects FROM SCRATCH. Safe to call any number of
        // times — every value is assigned, never added to. Non-members are explicitly zeroed so a
        // state that LEAVES a bloc actually loses the benefit.
        public void ApplyPassiveEffects(EconomySystem econ, NationalSystem nat)
        {
            if (byCountry == null) return;

            var present = new Dictionary<string, int>();
            foreach (var bloc in Blocs) present[bloc.Name] = PresentCount(bloc);

            for (int i = 0; i < byCountry.Length && i < econ.States.Count && i < nat.States.Count; i++)
            {
                float export = 0f, standing = 0f, readiness = 0f;
                foreach (var bloc in byCountry[i])
                {
                    int size = present.TryGetValue(bloc.Name, out int s) ? s : bloc.MemberIsos.Count;
                    switch (bloc.Type)
                    {
                        case WorldAlignments.UnionType.Economic:
                            export += 0.003f * Mathf.Min(size, 20);
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
                econ.States[i].UnionExportBonus = export;                 // SET, never +=
                nat.States[i].AllianceStandingBonus = Mathf.Min(standing, 12f);
                nat.States[i].AllianceReadinessBonus = Mathf.Min(readiness, 12f);
            }
        }

        // Fellow MILITARY-alliance members — the states that treat an attack on this one as their
        // concern (WarSystem.Declare mutual defence).
        public List<int> MilitaryAlliesOf(int country)
        {
            var allies = new List<int>();
            if (byCountry == null || country < 0 || country >= byCountry.Length) return allies;
            foreach (var bloc in byCountry[country])
            {
                if (bloc.Type != WorldAlignments.UnionType.Military) continue;
                // A member that kept its outside guarantees is not covered by, and does not
                // answer, this bloc's mutual defence.
                if (bloc.GuaranteedMembers.Count > 0 && IsoOf(country) != null
                    && bloc.GuaranteedMembers.Contains(IsoOf(country))) continue;
                foreach (var iso in bloc.MemberIsos)
                {
                    if (bloc.GuaranteedMembers.Contains(iso)) continue;
                    if (isoIndex.TryGetValue(iso, out int ci) && ci != country && !allies.Contains(ci))
                        allies.Add(ci);
                }
            }
            return allies;
        }
    }
}
