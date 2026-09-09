using System.Collections.Generic;

namespace Meridian.Sim
{
    // THE LEGITIMACY LEDGER — see docs/obsidian-vault/Vision/Consequence Engine.md §3.
    //
    // The single most important system in the Consequence Engine design, and the one that most
    // directly contradicts how this game worked before it: legitimacy is NOT one number. Every
    // observer class holds its own opinion of whether the player's authority is legitimate, and
    // the same action routinely moves different observers in OPPOSITE directions.
    //
    // THE RULE THAT MAKES THIS WORK, AND THE ONE THING NOT TO "HELPFULLY" ADD LATER:
    // there is deliberately NO Average(), NO Overall, NO composite score anywhere in this file.
    // A country adored by foreign populations and rejected by every foreign government is a
    // specific, unstable, extremely interesting position — averaging it away destroys the entire
    // point of the design. If you find yourself wanting one number for a UI bar, show six bars.
    //
    // Memory here is PERMANENT and never pruned (Pillar 3: costs arrive late). Every recorded
    // action keeps its day, its per-observer deltas and its reason forever, so the game can answer
    // "why is this happening to me" forty years later with the actual causal chain (see Trace).
    //
    // Engine-agnostic pure C#, public fields throughout so SaveLoad's Newtonsoft dump round-trips
    // it — same rule as every other Sim/ class.

    public enum Observer
    {
        OwnPopulation,        // pride, prosperity, defiance of stronger powers, promises kept
        ForeignPopulations,   // the observer players most often forget they have
        ForeignGovernments,   // predictability, sovereignty, whether agreements are kept
        ReligiousAuthority,   // defensibility within the tradition the player claims
        BlocMembers,          // whether the player obeys the rules their own bloc wrote
        OwnMilitary,          // pay, purpose, and whether it is used for things it believes in
    }

    public static class ObserverExt
    {
        public const int Count = 6;

        public static string Label(this Observer o) => o switch
        {
            Observer.OwnPopulation => "Own population",
            Observer.ForeignPopulations => "Foreign populations",
            Observer.ForeignGovernments => "Foreign governments",
            Observer.ReligiousAuthority => "Religious authority",
            Observer.BlocMembers => "Bloc members",
            _ => "Own military",
        };
    }

    // One recorded action and what it did to each observer. Never deleted, never decayed.
    public class LegitimacyEvent
    {
        public long Day;
        public string Action = "";              // "Denounced Iran"
        public string Why = "";                 // the reason, for the causal trace
        public float[] Deltas = new float[ObserverExt.Count];

        public string YearLabel => $"{2026 + (Day / 365)}";

        // "-8.0 foreign governments, +3.0 own population" — only the observers that actually moved.
        public string DeltaSummary()
        {
            var parts = new List<string>();
            for (int i = 0; i < Deltas.Length; i++)
                if (Deltas[i] > 0.05f || Deltas[i] < -0.05f)
                    parts.Add($"{Deltas[i]:+0.0;-0.0} {((Observer)i).Label().ToLowerInvariant()}");
            return parts.Count == 0 ? "no measurable effect" : string.Join(", ", parts);
        }
    }

    // One state's ledger: six independent scores plus permanent memory.
    public class LegitimacyLedger
    {
        public float[] Scores = new float[ObserverExt.Count];
        public List<LegitimacyEvent> Memory = new();   // PERMANENT. Never pruned. Pillar 3.

        public float Get(Observer o) => Scores[(int)o];

        // NOTE: intentionally no Average()/Overall property. See the file header.
    }

    public class LegitimacySystem
    {
        public List<LegitimacyLedger> Ledgers = new();

        // Everyone starts middling-legitimate with every observer; divergence is earned.
        public static LegitimacySystem Seed(int countryCount)
        {
            var sys = new LegitimacySystem();
            for (int i = 0; i < countryCount; i++)
            {
                var led = new LegitimacyLedger();
                for (int o = 0; o < ObserverExt.Count; o++) led.Scores[o] = 50f;
                sys.Ledgers.Add(led);
            }
            return sys;
        }

        public LegitimacyLedger Of(int country) =>
            country >= 0 && country < Ledgers.Count ? Ledgers[country] : null;

        // Records an action against a state: applies the per-observer deltas AND stores the event
        // permanently. deltas is indexed by (int)Observer; pass 0 for observers that don't move.
        // Returns the stored event so callers can log/report it.
        public LegitimacyEvent Record(int country, long day, string action, string why, float[] deltas)
        {
            var led = Of(country);
            if (led == null) return null;

            var ev = new LegitimacyEvent { Day = day, Action = action, Why = why };
            for (int o = 0; o < ObserverExt.Count && o < deltas.Length; o++)
            {
                ev.Deltas[o] = deltas[o];
                led.Scores[o] = Clampf(led.Scores[o] + deltas[o], 0f, 100f);
            }
            led.Memory.Add(ev);
            return ev;
        }

        // A continuous pressure that is NOT an event. Ongoing phenomena (a crowd standing in the
        // street for months) move an observer every single day, and recording one permanent memory
        // entry per day would drown the causal trace that Record() exists to keep readable. The
        // event is recorded once when the thing starts; this is the drip that follows it.
        public void Drift(int country, Observer o, float delta)
        {
            var led = Of(country);
            if (led == null) return;
            led.Scores[(int)o] = Clampf(led.Scores[(int)o] + delta, 0f, 100f);
        }

        // Convenience builder so call sites read like the design doc rather than like array maths.
        public static float[] Deltas(
            float ownPopulation = 0f, float foreignPopulations = 0f, float foreignGovernments = 0f,
            float religiousAuthority = 0f, float blocMembers = 0f, float ownMilitary = 0f)
        {
            var d = new float[ObserverExt.Count];
            d[(int)Observer.OwnPopulation] = ownPopulation;
            d[(int)Observer.ForeignPopulations] = foreignPopulations;
            d[(int)Observer.ForeignGovernments] = foreignGovernments;
            d[(int)Observer.ReligiousAuthority] = religiousAuthority;
            d[(int)Observer.BlocMembers] = blocMembers;
            d[(int)Observer.OwnMilitary] = ownMilitary;
            return d;
        }

        // THE CAUSAL TRACE (§12): "why is this happening?" — the chain of decisions that actually
        // moved this observer, worst first, with years attached. This is what turns a slow game
        // into a legible one, and it is only possible because memory is never pruned.
        public List<string> Trace(int country, Observer observer, int maxEntries = 6)
        {
            var outp = new List<string>();
            var led = Of(country);
            if (led == null) return outp;

            var moved = new List<LegitimacyEvent>();
            foreach (var ev in led.Memory)
            {
                float d = ev.Deltas[(int)observer];
                if (d > 0.05f || d < -0.05f) moved.Add(ev);
            }
            // Biggest absolute movers first — the decisions that actually explain where you are.
            moved.Sort((a, b) =>
                System.Math.Abs(b.Deltas[(int)observer]).CompareTo(System.Math.Abs(a.Deltas[(int)observer])));

            int n = moved.Count < maxEntries ? moved.Count : maxEntries;
            for (int i = 0; i < n; i++)
            {
                var ev = moved[i];
                outp.Add($"{ev.YearLabel}: {ev.Action} ({ev.Deltas[(int)observer]:+0.0;-0.0}) — {ev.Why}");
            }
            return outp;
        }

        // The headline the design actually wants surfaced: how far apart this state's observers
        // are. A wide spread is the interesting position, not a problem to be smoothed.
        public float Spread(int country)
        {
            var led = Of(country);
            if (led == null) return 0f;
            float lo = 101f, hi = -1f;
            foreach (var s in led.Scores) { if (s < lo) lo = s; if (s > hi) hi = s; }
            return hi - lo;
        }

        static float Clampf(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
