using System.Collections.Generic;

namespace Meridian.Sim
{
    // The rest of the world acts without the player: hostile neighbors occasionally go to war,
    // friendly states sign trade agreements. Deliberately RARE — a handful of headline events
    // per decade, not a nightly news ticker — so each one lands as an actual event, surfaced
    // through the same toast feed as everything else.
    public class WorldAI
    {
        readonly System.Func<int, string> continentOf;
        uint rng = 0x51c3d7e9;

        long nextWarConsiderDay = 300;       // world stays quiet for the opening months
        long nextAgreementConsiderDay = 150;
        public const int MaxConcurrentAiWars = 3;

        public WorldAI(System.Func<int, string> continentOf) { this.continentOf = continentOf; }

        // Called once per simulated day. Returns headline messages for the toast feed.
        public List<string> Tick(long day, EconomySystem econ, NationalSystem nat, DiplomacySystem dip, WarSystem wars, GeoWorldNames names)
        {
            var headlines = new List<string>();
            int n = econ.States.Count;
            if (n < 2) return headlines;

            if (day >= nextWarConsiderDay)
            {
                // Reschedule regardless of outcome — this is a consideration cadence, not a queue.
                nextWarConsiderDay = day + 240 + (long)(Next() % 240);

                int aiWars = 0;
                foreach (var w in wars.Active)
                    if (w.Attacker != PlayerState.CountryIndex && w.Defender != PlayerState.CountryIndex) aiWars++;

                if (aiWars < MaxConcurrentAiWars && TryFindHostilePair(n, dip, wars, out int agg, out int def))
                {
                    wars.Declare(agg, def, day, dip, nat);
                    headlines.Add($"WAR: {names.Name(agg)} has declared war on {names.Name(def)}.");
                }
            }

            if (day >= nextAgreementConsiderDay)
            {
                nextAgreementConsiderDay = day + 120 + (long)(Next() % 180);

                if (TryFindFriendlyPair(n, dip, out int a, out int b))
                {
                    string result = dip.SignAgreement(a, b, econ.States[a], econ.States[b], day);
                    if (result != null)
                        headlines.Add($"{names.Name(a)} and {names.Name(b)} have signed a trade agreement.");
                }
            }

            return headlines;
        }

        // Samples random pairs looking for a plausible aggressor/target: very hostile, same
        // continent (wars are overwhelmingly regional), neither already fighting, and the
        // aggressor isn't a microstate picking on a giant (strength ratio floor).
        bool TryFindHostilePair(int n, DiplomacySystem dip, WarSystem wars, out int aggressor, out int defender)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                int a = (int)(Next() % (uint)n);
                int b = (int)(Next() % (uint)n);
                if (a == b || a == PlayerState.CountryIndex || b == PlayerState.CountryIndex) continue;
                if (dip.GetRelation(a, b) >= 15f) continue;
                string ca = continentOf(a), cb = continentOf(b);
                if (string.IsNullOrEmpty(ca) || ca != cb) continue;
                if (wars.AtWar(a) || wars.AtWar(b)) continue;
                if (!wars.CanDeclare(a, b, dip)) continue;
                aggressor = a; defender = b;
                return true;
            }
            aggressor = defender = -1;
            return false;
        }

        bool TryFindFriendlyPair(int n, DiplomacySystem dip, out int a, out int b)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                int x = (int)(Next() % (uint)n);
                int y = (int)(Next() % (uint)n);
                if (x == y || x == PlayerState.CountryIndex || y == PlayerState.CountryIndex) continue;
                if (dip.GetRelation(x, y) < 75f || dip.HasAgreement(x, y)) continue;
                a = x; b = y;
                return true;
            }
            a = b = -1;
            return false;
        }

        uint Next()
        {
            uint x = rng;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            rng = x;
            return x;
        }
    }
}
