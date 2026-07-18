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

    public enum BillKind { IncomeTax, CorporateTax, Vat, Tariff, FreedomSpeech, FreedomReligion, FreedomInternet, RegimeChange, CompanyOwnership }

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
        public GovernmentType? NewGovernment; // only set for RegimeChange bills
        public int CompanyIndex = -1;         // only set for CompanyOwnership bills
        public string CompanyName = "";       // denormalized for display — the index stays authoritative for Apply

        public string KindLabel => Kind switch
        {
            BillKind.IncomeTax => "Income tax",
            BillKind.CorporateTax => "Corporate tax",
            BillKind.Vat => "VAT",
            BillKind.Tariff => "Tariffs",
            BillKind.FreedomSpeech => "Freedom of speech",
            BillKind.FreedomReligion => "Freedom of religion",
            BillKind.FreedomInternet => "Internet freedom",
            BillKind.CompanyOwnership => $"{CompanyName} ownership",
            _ => "Regime change",
        };

        public bool IsFreedom => Kind == BillKind.FreedomSpeech || Kind == BillKind.FreedomReligion || Kind == BillKind.FreedomInternet;
        public bool IsTightening => IsFreedom && NewValue < OldValue;
        public bool IsRegimeChange => Kind == BillKind.RegimeChange;
        public bool IsCompanyOwnership => Kind == BillKind.CompanyOwnership;
        // CompanyOwnership encodes the Ownership enum as a float (0=Public,1=Mixed,2=Private)
        // in Old/NewValue so it can reuse the same vote/decree pipeline every other numeric
        // bill uses instead of a fourth special-cased path.
        public Ownership OldOwnership => (Ownership)(int)OldValue;
        public Ownership NewOwnership => (Ownership)(int)NewValue;
    }

    public class LegislatureSystem
    {
        public List<Bill> Bills = new();
        public int NextId = 1;

        public const long VoteDays = 14;   // a real legislature takes time to fight it out
        public const long DecreeDays = 5;  // decrees are fast, not instant
        public const long RegimeChangeDays = 45; // a constitutional transition, not a policy tweak

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
        // (vote vs. decree) is "does this country have real parties data". companyIndex/
        // companyName are only meaningful for BillKind.CompanyOwnership (see Bill.CompanyIndex).
        public string Propose(int countryIndex, string countryName, GovernmentType gov,
                              List<PartyProfile> parties, BillKind kind, float oldValue, float newValue, long day,
                              int companyIndex = -1, string companyName = "")
        {
            var bill = new Bill
            {
                Id = NextId++,
                CountryIndex = countryIndex,
                Kind = kind,
                OldValue = oldValue,
                NewValue = newValue,
                ProposedDay = day,
                CompanyIndex = companyIndex,
                CompanyName = companyName,
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
                return $"{how}: {bill.KindLabel} {Fmt(bill, oldValue)} → {Fmt(bill, newValue)} (takes effect in {DecreeDays} days).";
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
            return $"{countryName}'s legislature takes up a bill: {bill.KindLabel} {Fmt(bill, oldValue)} → {Fmt(bill, newValue)}. {fight}";
        }

        // Shared value formatter for headlines — ownership bills show the real word
        // (Public/Mixed/Private), everything else shows a percentage (freedoms are 0-100
        // indices, formatted without the "%" — see Unit()).
        static string Fmt(Bill b, float value) =>
            b.IsCompanyOwnership ? OwnershipLabel((Ownership)(int)value) : $"{value:0.0}{Unit(b)}";

        static string OwnershipLabel(Ownership o) => o switch { Ownership.Public => "Public", Ownership.Mixed => "Mixed", _ => "Private" };

        // Regime change: converting a country's own government type — categorically different
        // from ordinary policy, so it deliberately bypasses the party-vote branch entirely (a
        // multi-party legislature doesn't get to vote itself out of existence; this is the
        // player, as head of government, driving a constitutional transition unilaterally) and
        // takes RegimeChangeDays (45) instead of the ordinary decree/vote timers, reflecting the
        // magnitude. See docs/obsidian-vault/Vision/Government, Legislature and Real Taxes.md's
        // "regime change, and the world reacting realistically" requirement — the standing
        // consequence (applied in Apply) reacts to the STRUCTURAL fact of gaining/losing real
        // pluralism, not a scripted judgment that any one government type is doomed to succeed
        // or fail; whether the player's country is actually stable afterward is still driven by
        // the normal ApprovalRating/PublicMood/economy numbers, exactly like every other country.
        public string ProposeRegimeChange(int countryIndex, string countryName, GovernmentType oldGov, GovernmentType newGov, long day)
        {
            var bill = new Bill
            {
                Id = NextId++,
                CountryIndex = countryIndex,
                Kind = BillKind.RegimeChange,
                ProposedDay = day,
                IsDecree = true,
                DecisionDay = day + RegimeChangeDays,
                NewGovernment = newGov,
            };
            Bills.Add(bill);
            return $"{countryName} begins a constitutional transition from {GovLabel(oldGov)} to {GovLabel(newGov)} " +
                   $"(takes {RegimeChangeDays} days).";
        }

        public static string GovLabel(GovernmentType g) => g switch
        {
            GovernmentType.AbsoluteMonarchy => "absolute monarchy",
            GovernmentType.ConstitutionalMonarchy => "constitutional monarchy",
            GovernmentType.PresidentialRepublic => "a presidential republic",
            GovernmentType.ParliamentaryRepublic => "a parliamentary republic",
            GovernmentType.OneServiceState => "a one-party state",
            _ => "an unclassified system",
        };

        // Pluralism axis for the standing consequence — real multi-party competition and a
        // real independent legislature, regardless of whether the head of state is elected or
        // hereditary (a constitutional monarchy counts; an absolute monarchy or one-party state
        // doesn't).
        static bool IsPluralistic(GovernmentType g) =>
            g == GovernmentType.ConstitutionalMonarchy || g == GovernmentType.PresidentialRepublic || g == GovernmentType.ParliamentaryRepublic;

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
            if (bill.IsCompanyOwnership)
            {
                // Real, uncontroversial partisan pattern: economic-right parties back
                // privatizing (moving toward Private), economic-left back nationalizing
                // (moving toward Public) — the same sign convention as tax cuts, since
                // privatization IS a shrink-the-state move in the same sense a tax cut is.
                float direction = bill.NewValue > bill.OldValue ? 1f : -1f; // +1 == toward Private
                return p.EconLean * direction + wrinkle > 0f;
            }
            float taxDirection = bill.NewValue < bill.OldValue ? 1f : -1f; // +1 == a cut
            return p.EconLean * taxDirection + wrinkle > 0f;
        }

        // Convenience wrapper for CompanyOwnership bills — encodes the enum as the float scale
        // Propose already understands (0=Public,1=Mixed,2=Private) so ownership changes get the
        // exact same vote-or-decree pipeline as every other bill for free.
        public string ProposeOwnershipChange(int countryIndex, string countryName, GovernmentType gov,
                                             List<PartyProfile> parties, int companyIndex, string companyName,
                                             Ownership oldOwnership, Ownership newOwnership, long day) =>
            Propose(countryIndex, countryName, gov, parties, BillKind.CompanyOwnership,
                    (float)(int)oldOwnership, (float)(int)newOwnership, day, companyIndex, companyName);

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
                    if (b.IsRegimeChange)
                        (headlines ??= new List<string>()).Add(
                            $"{names.Name(b.CountryIndex)} completes its transition to {GovLabel(b.NewGovernment ?? GovernmentType.Unspecified)}.");
                    else
                        (headlines ??= new List<string>()).Add(
                            $"{names.Name(b.CountryIndex)}: {b.KindLabel} is now {Fmt(b, b.NewValue)} by decree.");
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
                        $"{names.Name(b.CountryIndex)}: bill passes {yes * 100f:0}–{(1f - yes) * 100f:0} — {b.KindLabel} is now {Fmt(b, b.NewValue)}.");
                }
                else
                {
                    b.Status = BillStatus.Rejected;
                    (headlines ??= new List<string>()).Add(
                        $"{names.Name(b.CountryIndex)}: bill defeated {yes * 100f:0}–{(1f - yes) * 100f:0} — {b.KindLabel} stays {Fmt(b, b.OldValue)}.");
                }
            }
            return headlines ?? Empty;
        }
        static readonly List<string> Empty = new();
        static string Unit(Bill b) => b.IsFreedom ? "" : "%";

        static void Apply(Bill b, EconomySystem econ, NationalSystem nat)
        {
            if (b.IsRegimeChange)
            {
                if (nat == null || b.CountryIndex < 0 || b.CountryIndex >= nat.States.Count || b.NewGovernment == null) return;
                var n = nat.States[b.CountryIndex];
                bool wasPluralistic = IsPluralistic(n.Government);
                bool nowPluralistic = IsPluralistic(b.NewGovernment.Value);
                n.Government = b.NewGovernment.Value;
                // Reacts to the structural fact of the change, not a judgment about which
                // government type is "better" — losing real pluralism costs standing hard
                // (real diplomatic backsliding reaction), gaining it earns real credit, and even
                // a same-category transition carries a small transitional-uncertainty cost.
                float standingDelta =
                    wasPluralistic && !nowPluralistic ? -25f :
                    !wasPluralistic && nowPluralistic ? 12f : -3f;
                n.InternationalStanding = Clampf(n.InternationalStanding + standingDelta, 0f, 100f);
                return;
            }

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

            if (b.IsCompanyOwnership)
            {
                if (b.CompanyIndex < 0 || b.CompanyIndex >= e.Companies.Count) return;
                var company = e.Companies[b.CompanyIndex];
                // A real, one-time buyout-or-sale cash flow, sized by the company's approximate
                // real output (see Sim/Companies.cs) — nationalizing costs the state a buyout,
                // privatizing raises a real one-time windfall. Doesn't yet add an ongoing
                // dividend/tax stream tied to the new ownership (that's real future work, see
                // docs/obsidian-vault/Architecture/Legislature and Bills.md's open items) — kept
                // out of this slice specifically to avoid touching EconomyState.Tick's core GDP
                // formula until sector output composing GDP is properly designed.
                float oldStake = StateStake(b.OldOwnership);
                float newStake = StateStake(b.NewOwnership);
                e.Treasury += (oldStake - newStake) * company.OutputBillions * 0.4;
                company.Ownership = b.NewOwnership;
                return;
            }

            switch (b.Kind)
            {
                case BillKind.IncomeTax: e.TaxIncome = b.NewValue; break;
                case BillKind.CorporateTax: e.TaxCorporate = b.NewValue; break;
                case BillKind.Vat: e.TaxVat = b.NewValue; break;
                case BillKind.Tariff: e.TaxTariff = b.NewValue; break;
            }
        }

        // 1 = fully state-owned, 0 = fully private — how much of the company's value the state
        // holds a claim to, for sizing the buyout-vs-sale transaction above.
        static float StateStake(Ownership o) => o switch { Ownership.Public => 1f, Ownership.Mixed => 0.5f, _ => 0f };

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
