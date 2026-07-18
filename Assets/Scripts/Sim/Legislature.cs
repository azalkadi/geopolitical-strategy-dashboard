using System.Collections.Generic;

namespace Meridian.Sim
{
    // The bill pipeline — the core mechanic of the "Government, Legislature and Real Taxes"
    // vision pillar (docs/obsidian-vault/Vision/). The player proposes a policy change (today:
    // one of the four core tax levers); what happens next depends on the country's real
    // political structure:
    //   - Has real parties data (CountryProfiles.Parties) -> it goes to the legislature. Each
    //     party takes a stance from its economic ideology (left backs tax raises, right backs
    //     cuts, with per-party-per-bill variation so votes aren't robotic), stances are
    //     weighted by seat share, and the bill resolves days later with a pass/fail vote.
    //   - No parties (monarchies, one-party states, uncurated countries) -> it's decreed:
    //     enacted automatically after a short delay, no vote, flavor by government type.
    // Both paths surface headlines through WorldFeed — proposal, the fight, and the outcome —
    // which is where "parties fighting over things" becomes visible to the player.
    //
    // All public fields / parameterless types so SaveLoad's dumb-complete Newtonsoft dump
    // round-trips the whole system (see Sim/SaveLoad.cs).

    public enum BillKind { IncomeTax, CorporateTax, Vat, Tariff, FreedomSpeech, FreedomReligion, FreedomInternet }

    public enum BillStatus { Pending, Passed, Rejected }

    // One party's recorded stance on one bill — captured at proposal time (deterministically,
    // from ideology + per-bill variation) and stored, so the UI can show the fight while the
    // bill is pending and the resolution just tallies what was already public.
    public class BillStance
    {
        public string Party = "";
        public float SeatShare;
        public bool Supports;
    }

    public class Bill
    {
        public int Id;
        public int CountryIndex;
        public BillKind Kind;
        public float OldValue;
        public float NewValue;
        public long ProposedDay;
        public long DecisionDay;
        public BillStatus Status = BillStatus.Pending;
        public bool IsDecree;           // no-parties path: auto-enacts, no vote
        public List<BillStance> Stances = new();
        public float YesShare;          // filled at resolution (vote path)

        public string KindLabel => Kind switch
        {
            BillKind.IncomeTax => "Income tax",
            BillKind.CorporateTax => "Corporate tax",
            BillKind.Vat => "VAT",
            BillKind.Tariff => "Tariffs",
            BillKind.FreedomSpeech => "Freedom of speech",
            BillKind.FreedomReligion => "Freedom of religion",
            _ => "Internet freedom",
        };

        public bool IsFreedom => Kind == BillKind.FreedomSpeech || Kind == BillKind.FreedomReligion || Kind == BillKind.FreedomInternet;
        public bool IsTightening => IsFreedom && NewValue < OldValue;
    }

    public class LegislatureSystem
    {
        public List<Bill> Bills = new();
        public int NextId = 1;

        public const long VoteDays = 14;   // a real legislature takes time to fight it out
        public const long DecreeDays = 5;  // decrees are fast, not instant

        public Bill PendingFor(int countryIndex, BillKind kind)
        {
            foreach (var b in Bills)
                if (b.CountryIndex == countryIndex && b.Kind == kind && b.Status == BillStatus.Pending)
                    return b;
            return null;
        }

        public List<Bill> BillsOf(int countryIndex)
        {
            var list = new List<Bill>();
            foreach (var b in Bills)
                if (b.CountryIndex == countryIndex) list.Add(b);
            return list;
        }

        // Proposes a change and returns the headline to push. Caller has already checked
        // PendingFor (one open bill per lever at a time) and clamped newValue to the lever's
        // legal range. countryName/govType are for flavor only — the structural decision
        // (vote vs. decree) is "does this country have real parties data".
        public string Propose(int countryIndex, string countryName, GovernmentType gov,
                              List<PartyProfile> parties, BillKind kind, float oldValue, float newValue, long day)
        {
            var bill = new Bill
            {
                Id = NextId++,
                CountryIndex = countryIndex,
                Kind = kind,
                OldValue = oldValue,
                NewValue = newValue,
                ProposedDay = day,
            };

            if (parties == null || parties.Count == 0)
            {
                bill.IsDecree = true;
                bill.DecisionDay = day + DecreeDays;
                Bills.Add(bill);
                string how = gov switch
                {
                    GovernmentType.AbsoluteMonarchy or GovernmentType.ConstitutionalMonarchy => "Royal decree drafted",
                    GovernmentType.OneServiceState => "Party leadership directive issued",
                    _ => "Executive order drafted",
                };
                return $"{how}: {bill.KindLabel} {oldValue:0.0}% → {newValue:0.0}% (takes effect in {DecreeDays} days).";
            }

            bill.DecisionDay = day + VoteDays;
            string backers = null, opponents = null;
            float backShare = 0f, opposeShare = 0f;
            foreach (var p in parties)
            {
                bool supports = PartySupports(p, bill);
                bill.Stances.Add(new BillStance { Party = p.Name, SeatShare = p.SeatShare, Supports = supports });
                if (supports && p.SeatShare > backShare) { backShare = p.SeatShare; backers = p.Name; }
                if (!supports && p.SeatShare > opposeShare) { opposeShare = p.SeatShare; opponents = p.Name; }
            }
            Bills.Add(bill);

            string fight =
                backers != null && opponents != null ? $"{backers} back it; {opponents} vow to fight it." :
                backers != null ? $"{backers} back it; no organized opposition." :
                "No party is willing to sponsor it.";
            return $"{countryName}'s legislature takes up a bill: {bill.KindLabel} {oldValue:0.0}% → {newValue:0.0}%. {fight}";
        }

        // Ideology model: economic-left parties back tax raises (funding the state), economic-
        // right parties back cuts. Freedom bills reuse the same single EconLean axis as a coarse
        // proxy for a social lib-conservative axis this sim hasn't curated yet (a known
        // simplification, documented in the Vision page) — left backs expanding freedoms,
        // right backs tightening. Either way there's a deterministic per-party-per-bill wrinkle
        // so the same party isn't perfectly predictable on every bill (centrist parties in
        // particular swing on the specifics). lean in [-1 left .. +1 right].
        static bool PartySupports(PartyProfile p, Bill bill)
        {
            float wrinkle = (Hash($"{p.Name}|{bill.Kind}|{bill.ProposedDay}") % 1000u) / 1000f * 0.5f - 0.25f;
            if (bill.IsFreedom)
            {
                float direction = bill.NewValue > bill.OldValue ? 1f : -1f; // +1 == expanding freedom
                return -p.EconLean * direction + wrinkle > 0f;
            }
            float taxDirection = bill.NewValue < bill.OldValue ? 1f : -1f; // +1 == a cut
            return p.EconLean * taxDirection + wrinkle > 0f;
        }

        // Resolves any bill whose day has come. Returns headlines (empty list most days).
        // `nat` is optional (callers that only ever propose tax bills can pass null); freedom
        // bills are a no-op without it.
        public List<string> TickAll(long day, EconomySystem econ, NationalSystem nat, GeoWorldNames names)
        {
            List<string> headlines = null;
            foreach (var b in Bills)
            {
                if (b.Status != BillStatus.Pending || day < b.DecisionDay) continue;

                if (b.IsDecree)
                {
                    b.Status = BillStatus.Passed;
                    Apply(b, econ, nat);
                    (headlines ??= new List<string>()).Add(
                        $"{names.Name(b.CountryIndex)}: {b.KindLabel} is now {b.NewValue:0.0}{Unit(b)} by decree.");
                    continue;
                }

                float yes = 0f;
                foreach (var s in b.Stances)
                    if (s.Supports) yes += s.SeatShare;
                b.YesShare = yes;

                if (yes > 0.5f)
                {
                    b.Status = BillStatus.Passed;
                    Apply(b, econ, nat);
                    (headlines ??= new List<string>()).Add(
                        $"{names.Name(b.CountryIndex)}: bill passes {yes * 100f:0}–{(1f - yes) * 100f:0} — {b.KindLabel} is now {b.NewValue:0.0}{Unit(b)}.");
                }
                else
                {
                    b.Status = BillStatus.Rejected;
                    (headlines ??= new List<string>()).Add(
                        $"{names.Name(b.CountryIndex)}: bill defeated {yes * 100f:0}–{(1f - yes) * 100f:0} — {b.KindLabel} stays {b.OldValue:0.0}{Unit(b)}.");
                }
            }
            return headlines ?? Empty;
        }
        static readonly List<string> Empty = new();
        static string Unit(Bill b) => b.IsFreedom ? "" : "%";

        static void Apply(Bill b, EconomySystem econ, NationalSystem nat)
        {
            if (b.IsFreedom)
            {
                if (nat == null || b.CountryIndex < 0 || b.CountryIndex >= nat.States.Count) return;
                var n = nat.States[b.CountryIndex];
                switch (b.Kind)
                {
                    case BillKind.FreedomSpeech: n.FreedomSpeech = b.NewValue; break;
                    case BillKind.FreedomReligion: n.FreedomReligion = b.NewValue; break;
                    case BillKind.FreedomInternet: n.FreedomInternet = b.NewValue; break;
                }
                // Real international reaction: tightening freedoms costs standing, expanding
                // them earns a little back — see docs/obsidian-vault/Vision/Government,
                // Legislature and Real Taxes.md's "freedoms as real levers with real
                // consequences" requirement. Asymmetric on purpose (losing standing is easy,
                // earning it back is slow), same spirit as real reputational politics.
                float delta = b.NewValue - b.OldValue;
                n.InternationalStanding = Clampf(n.InternationalStanding + (delta < 0 ? delta * 0.3f : delta * 0.08f), 0f, 100f);
                return;
            }

            if (b.CountryIndex < 0 || b.CountryIndex >= econ.States.Count) return;
            var e = econ.States[b.CountryIndex];
            switch (b.Kind)
            {
                case BillKind.IncomeTax: e.TaxIncome = b.NewValue; break;
                case BillKind.CorporateTax: e.TaxCorporate = b.NewValue; break;
                case BillKind.Vat: e.TaxVat = b.NewValue; break;
                case BillKind.Tariff: e.TaxTariff = b.NewValue; break;
            }
        }

        static float Clampf(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);

        static uint Hash(string s)
        {
            unchecked
            {
                uint h = 0x811c9dc5;
                foreach (char c in s) { h ^= (byte)c; h *= 0x01000193; }
                return h;
            }
        }
    }
}
