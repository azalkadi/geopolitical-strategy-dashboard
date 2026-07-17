using System;
using System.Collections.Generic;
using UnityEngine;

namespace Meridian.Sim
{
    // A player-initiated road/rail link between two cities in their own country. Construction
    // takes real sim-days (distance-scaled) and costs treasury up front — once Completed, the
    // segment is permanent and MapRenderer.RebuildPlayerInfrastructure paints it on the map.
    // Public fields throughout so the save system can serialize this wholesale (see Sim/SaveLoad.cs).
    public class BuiltRoute
    {
        public int FromCity;
        public int ToCity;
        public string FromName = "";
        public string ToName = "";
        public bool IsRailway;
        public int OwnerCountryIndex;
        public long StartDay;
        public long CompletionDay;
        public bool Completed;
        public double Cost;
    }

    public class InfrastructureSystem
    {
        public List<BuiltRoute> Routes = new();

        public const double CostPerKm = 0.03;             // $B per km, road
        public const double RailwayCostMultiplier = 2.5;  // railways cost more per km than roads
        public const double MinCost = 0.5;                // $B floor so short hops aren't free
        public const double DaysPerKm = 0.12;
        public const long MinBuildDays = 20;

        // Great-circle distance between two real lon/lat points (degrees), in km — city
        // positions are stored Mercator-projected (see GeoMath), so callers convert back via
        // GeoMath.MercatorToLonLat before calling this.
        public static double DistanceKm(Vector2 lonlatA, Vector2 lonlatB)
        {
            const double R = 6371.0;
            double lat1 = lonlatA.y * Math.PI / 180.0, lat2 = lonlatB.y * Math.PI / 180.0;
            double dLat = (lonlatB.y - lonlatA.y) * Math.PI / 180.0;
            double dLon = (lonlatB.x - lonlatA.x) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        public static double EstimateCost(double distanceKm, bool rail) =>
            Math.Max(MinCost, distanceKm * CostPerKm * (rail ? RailwayCostMultiplier : 1.0));

        public static long EstimateDays(double distanceKm) =>
            Math.Max(MinBuildDays, (long)(distanceKm * DaysPerKm));

        // Caller (GameUIRoot) has already resolved the two cities and shown the player the
        // exact cost/duration — this just books it and charges the treasury immediately
        // (same unconditional-spend pattern as DiplomacySystem.SendAid; going into debt over
        // it is the player's call, same as every other spending lever).
        public string Begin(int fromCity, int toCity, string fromName, string toName, bool rail, int ownerIdx, EconomyState payer, long day, double distanceKm)
        {
            double cost = EstimateCost(distanceKm, rail);
            long buildDays = EstimateDays(distanceKm);
            payer.Treasury -= cost;
            Routes.Add(new BuiltRoute
            {
                FromCity = fromCity,
                ToCity = toCity,
                FromName = fromName,
                ToName = toName,
                IsRailway = rail,
                OwnerCountryIndex = ownerIdx,
                StartDay = day,
                CompletionDay = day + buildDays,
                Cost = cost,
            });
            return $"{(rail ? "Railway" : "Road")} construction begun: {fromName} — {toName} " +
                   $"({distanceKm:0}km, ${cost:0.0}B, ready in {buildDays} days).";
        }

        // Flips Completed on any route whose day has arrived; returns only the ones that JUST
        // completed this call so the caller (MapInteraction) toasts + repaints the map once per
        // completion, not every tick.
        public List<BuiltRoute> TickAll(long day)
        {
            List<BuiltRoute> justCompleted = null;
            foreach (var r in Routes)
            {
                if (r.Completed || day < r.CompletionDay) continue;
                r.Completed = true;
                (justCompleted ??= new List<BuiltRoute>()).Add(r);
            }
            return justCompleted ?? EmptyCompleted;
        }
        static readonly List<BuiltRoute> EmptyCompleted = new();
    }
}
