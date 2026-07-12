using System;
using System.Collections.Generic;

namespace Meridian.Sim
{
    // Decision events: every so often the player's country faces a situation with 2-3 choices,
    // each with real, stated consequences applied to the same EconomyState/NationalState
    // numbers the rest of the simulation runs on — no separate "event stats". The sim clock
    // PAUSES while a decision is pending (MapInteraction checks EventSystem.Pending), the
    // classic grand-strategy pattern: the world waits for the head of state.
    //
    // Effects use fractions of GDP rather than flat billions so the same event is
    // proportionally meaningful whether you govern Luxembourg or China.

    public class GameEvent
    {
        public string Title;
        public string Description;
        public EventChoice[] Choices;
        // Eligibility test — lets crisis events fire only during actual crises. Null = always.
        public Func<EconomyState, NationalState, bool> Condition;
    }

    public class EventChoice
    {
        public string Label;
        public string Outcome; // shown as a toast after choosing
        public Action<EconomyState, NationalState> Apply;
    }

    public static class EventSystem
    {
        // The event awaiting a decision, or null. UI shows the modal while non-null.
        // Settable so PlayerState.Begin/Reset can clear a stale decision across games.
        public static GameEvent Pending;

        public static long NextEventDay = 90; // first event ~3 months in
        // 150-360 days between events ≈ 4-9 decisions per 4-year term. Tighter gaps play-tested
        // badly: at the 10x speed setting, 60-150 day gaps meant a new crisis every 6-15 real
        // seconds — faster than a human could reach the pause button between modals.
        const long MinGapDays = 150;
        const long MaxGapDays = 360;

        static uint rng = 0x2a5f1c33;
        static List<GameEvent> pool;

        public static void Reset()
        {
            Pending = null;
            NextEventDay = 90;
        }

        // Called once per simulated day by MapInteraction, for the player's country only.
        public static void MaybeFire(long day, EconomyState e, NationalState n)
        {
            if (Pending != null || day < NextEventDay) return;
            if (pool == null) pool = BuildPool();

            // Collect currently-eligible events, pick one deterministically-ish.
            var eligible = new List<GameEvent>();
            foreach (var ev in pool)
                if (ev.Condition == null || ev.Condition(e, n))
                    eligible.Add(ev);
            if (eligible.Count == 0) { NextEventDay = day + MinGapDays; return; }

            Pending = eligible[(int)(Next() % (uint)eligible.Count)];
            NextEventDay = day + MinGapDays + (long)(Next() % (uint)(MaxGapDays - MinGapDays));
        }

        // Applies the chosen option and clears the modal. Returns the outcome text for a toast.
        public static string Choose(int index, EconomyState e, NationalState n)
        {
            if (Pending == null || index < 0 || index >= Pending.Choices.Length) return null;
            var choice = Pending.Choices[index];
            choice.Apply?.Invoke(e, n);
            Pending = null;
            return choice.Outcome;
        }

        static uint Next()
        {
            uint x = rng;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            rng = x;
            return x;
        }

        // The event pool. Effects are deliberately in "one clear upside, one clear cost" shape
        // so choices are strategy, not guesswork. GDP-fraction helper keeps costs proportional.
        static List<GameEvent> BuildPool()
        {
            static double Pct(EconomyState e, double pctOfGdp) => e.Gdp * pctOfGdp / 100.0;

            return new List<GameEvent>
            {
                new GameEvent
                {
                    Title = "Corruption Scandal",
                    Description = "Leaked documents implicate two ministers in a procurement kickback scheme. The press is demanding your response.",
                    Choices = new[]
                    {
                        new EventChoice {
                            Label = "Fire them and launch a full public inquiry",
                            Outcome = "The inquiry costs money and momentum, but voters respect the housecleaning.",
                            Apply = (e, n) => { e.Treasury -= Pct(e, 0.15); n.ApprovalRating += 4f; }
                        },
                        new EventChoice {
                            Label = "Quietly reassign them and move on",
                            Outcome = "The story festers in the press for weeks. Your reputation takes the hit instead of your schedule.",
                            Apply = (e, n) => { n.ApprovalRating -= 6f; }
                        },
                    }
                },
                new GameEvent
                {
                    Title = "Catastrophic Flooding",
                    Description = "A once-in-a-generation storm has flooded three provinces. Homes, roads, and power infrastructure are underwater.",
                    Choices = new[]
                    {
                        new EventChoice {
                            Label = "Full federal emergency response and rebuild",
                            Outcome = "Costly, but the response is swift and the country pulls together.",
                            Apply = (e, n) => { e.Treasury -= Pct(e, 0.8); n.ApprovalRating += 3f; n.PublicMood += 2f; }
                        },
                        new EventChoice {
                            Label = "Minimal response — provinces handle their own recovery",
                            Outcome = "The treasury is spared. The affected regions do not forget it.",
                            Apply = (e, n) => { n.ApprovalRating -= 5f; n.PublicMood -= 5f; e.GrowthRate -= 0.4f; }
                        },
                    }
                },
                new GameEvent
                {
                    Title = "General Strike Threatened",
                    Description = "The largest labor federation threatens a general strike over stagnant public-sector wages.",
                    Choices = new[]
                    {
                        new EventChoice {
                            Label = "Negotiate a wage increase",
                            Outcome = "The strike is averted. Payroll costs rise, and so does inflation pressure.",
                            Apply = (e, n) => { e.Treasury -= Pct(e, 0.3); e.Inflation += 0.5f; n.PublicMood += 3f; }
                        },
                        new EventChoice {
                            Label = "Refuse and ride out the strike",
                            Outcome = "Two weeks of stoppages hit output, but the budget line holds.",
                            Apply = (e, n) => { e.GrowthRate -= 0.8f; n.PublicMood -= 4f; n.ApprovalRating -= 2f; }
                        },
                    }
                },
                new GameEvent
                {
                    Title = "Banking Sector Wobble",
                    Description = "Your third-largest bank is rumored to be insolvent. Depositors are getting nervous; a run could spread.",
                    Condition = (e, n) => e.GrowthRate < 2.5f,
                    Choices = new[]
                    {
                        new EventChoice {
                            Label = "Guarantee deposits and bail it out",
                            Outcome = "Expensive, but contagion is stopped cold.",
                            Apply = (e, n) => { e.Treasury -= Pct(e, 1.2); n.ApprovalRating -= 1f; }
                        },
                        new EventChoice {
                            Label = "Let it fail — moral hazard is real",
                            Outcome = "Markets convulse. Credit tightens across the whole economy.",
                            Apply = (e, n) => { e.GrowthRate -= 1.5f; e.Unemployment += 0.8f; n.ApprovalRating -= 3f; }
                        },
                    }
                },
                new GameEvent
                {
                    Title = "Tech Giant Considering Relocation",
                    Description = "A major technology firm is scouting locations for a new global R&D campus — 20,000 jobs. They want tax concessions.",
                    Choices = new[]
                    {
                        new EventChoice {
                            Label = "Offer the concessions package",
                            Outcome = "They sign. The revenue giveaway stings, but the innovation ecosystem gets a jolt.",
                            Apply = (e, n) => { e.Treasury -= Pct(e, 0.25); n.InnovationIndex += 6f; e.GrowthRate += 0.3f; }
                        },
                        new EventChoice {
                            Label = "Decline — no special treatment",
                            Outcome = "They build elsewhere. Principled, and quietly expensive.",
                            Apply = (e, n) => { n.InnovationIndex -= 2f; }
                        },
                    }
                },
                new GameEvent
                {
                    Title = "Influenza Outbreak",
                    Description = "A severe flu strain is overwhelming hospitals in the capital. Health officials want emergency funding before it spreads.",
                    Choices = new[]
                    {
                        new EventChoice {
                            Label = "Emergency health funding now",
                            Outcome = "Containment works. The bill is real but the outbreak stays regional.",
                            Apply = (e, n) => { e.Treasury -= Pct(e, 0.35); n.PublicMood += 2f; }
                        },
                        new EventChoice {
                            Label = "Existing budgets will have to cope",
                            Outcome = "The outbreak spreads. Absenteeism dents output and the public notices the empty wards.",
                            Apply = (e, n) => { e.GrowthRate -= 0.6f; n.PublicMood -= 5f; n.ApprovalRating -= 3f; }
                        },
                    }
                },
                new GameEvent
                {
                    Title = "Global Commodity Spike",
                    Description = "A supply shock has sent world energy prices soaring. Fuel and electricity costs are rippling through everything.",
                    Choices = new[]
                    {
                        new EventChoice {
                            Label = "Subsidize household energy bills",
                            Outcome = "The pain is cushioned — at the treasury's expense.",
                            Apply = (e, n) => { e.Treasury -= Pct(e, 0.5); e.Inflation += 0.4f; n.PublicMood += 2f; }
                        },
                        new EventChoice {
                            Label = "Let prices pass through",
                            Outcome = "Inflation bites hard, but the market adjusts and the budget survives.",
                            Apply = (e, n) => { e.Inflation += 1.2f; n.PublicMood -= 4f; }
                        },
                    }
                },
                new GameEvent
                {
                    Title = "Universities Demand Funding",
                    Description = "Every major university has signed an open letter: research funding has fallen behind rivals and the brain drain is measurable.",
                    Choices = new[]
                    {
                        new EventChoice {
                            Label = "Announce a national research initiative",
                            Outcome = "Labs stay open, talent stays home.",
                            Apply = (e, n) => { e.Treasury -= Pct(e, 0.3); n.InnovationIndex += 4f; }
                        },
                        new EventChoice {
                            Label = "Praise their work, promise nothing",
                            Outcome = "Warm words, cold labs. A few star researchers take offers abroad.",
                            Apply = (e, n) => { n.InnovationIndex -= 3f; }
                        },
                    }
                },
                new GameEvent
                {
                    Title = "Runaway Inflation Protest",
                    Description = "Prices are rising faster than wages and tens of thousands are marching in the capital demanding action.",
                    Condition = (e, n) => e.Inflation > 6f,
                    Choices = new[]
                    {
                        new EventChoice {
                            Label = "Emergency rate hike (+2% interest)",
                            Outcome = "The central bank slams the brakes. Growth will feel it, but the fever should break.",
                            Apply = (e, n) => { e.InterestRate = Math.Min(20f, e.InterestRate + 2f); n.ApprovalRating += 2f; }
                        },
                        new EventChoice {
                            Label = "Announce price controls on staples",
                            Outcome = "Popular today; shortages tomorrow. Economists weep.",
                            Apply = (e, n) => { n.ApprovalRating += 4f; n.PublicMood += 2f; e.GrowthRate -= 0.7f; }
                        },
                        new EventChoice {
                            Label = "Do nothing — this will pass",
                            Outcome = "The marches grow.",
                            Apply = (e, n) => { n.ApprovalRating -= 5f; n.PublicMood -= 3f; }
                        },
                    }
                },
                new GameEvent
                {
                    Title = "Military Requests Modernization",
                    Description = "The general staff presents a modernization plan: aging equipment is hurting readiness and morale.",
                    Choices = new[]
                    {
                        new EventChoice {
                            Label = "Fund the modernization program",
                            Outcome = "New equipment starts arriving within the year.",
                            Apply = (e, n) => { e.Treasury -= Pct(e, 0.4); n.ReadinessIndex += 8f; }
                        },
                        new EventChoice {
                            Label = "Defer it to the next budget cycle",
                            Outcome = "The generals salute and quietly update their resignation letters.",
                            Apply = (e, n) => { n.ReadinessIndex -= 4f; }
                        },
                    }
                },
                new GameEvent
                {
                    Title = "Boom-Time Windfall",
                    Description = "Strong growth has produced an unexpected revenue surplus this quarter. The cabinet is already arguing about it.",
                    Condition = (e, n) => e.GrowthRate > 3.5f,
                    Choices = new[]
                    {
                        new EventChoice {
                            Label = "Bank it — pay down debt",
                            Outcome = "Unfashionable, responsible.",
                            Apply = (e, n) => { e.Treasury += Pct(e, 0.4); }
                        },
                        new EventChoice {
                            Label = "One-time citizen dividend",
                            Outcome = "Everyone loves free money. Prices tick up accordingly.",
                            Apply = (e, n) => { n.ApprovalRating += 5f; n.PublicMood += 4f; e.Inflation += 0.4f; }
                        },
                    }
                },
            };
        }
    }
}
