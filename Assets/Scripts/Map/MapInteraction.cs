using UnityEngine;
using Meridian.Geo;
using Meridian.Sim;

namespace Meridian.Map
{
    // Ties the map to the simulation: advances every country's economy over time, lets you
    // click a country to select it (reusing the ported GeoMath.PointInRing hit-testing), runs
    // the approval-rating-based election/term mechanic for the player's own nation, and gates
    // the sim clock so nothing ticks until the player has actually picked a country to govern.
    public class MapInteraction : MonoBehaviour
    {
        [Tooltip("Simulated days advanced per real second.")]
        public float daysPerSecond = 5f;

        MapRenderer map;
        Camera cam;
        int selected = -1;
        double dayAccum;
        long simDay;

        public int Selected => selected;
        public long SimDay => simDay;

        // Click-vs-drag discrimination (left button is also the camera pan).
        Vector3 pressPos;
        bool pressed;

        void Start()
        {
            map = FindObjectOfType<MapRenderer>();
            cam = Camera.main;
        }

        // Forces the selection (used to auto-open the player's own country panel on game start).
        public void SelectCountry(int idx) => selected = idx;

        void Update()
        {
            if (map == null || map.World == null || map.Economy == null) return;

            // The world is frozen at the start screen / game-over screen — nothing ticks until
            // the player has actually picked a nation to govern.
            if (PlayerState.State == GameState.Playing)
                TickEconomy();
            HandleClickSelect();
        }

        void TickEconomy()
        {
            dayAccum += daysPerSecond * Time.deltaTime;
            int whole = (int)dayAccum;
            if (whole <= 0) return;
            dayAccum -= whole;
            for (int i = 0; i < whole; i++)
            {
                simDay++;
                map.Economy.TickAll();
                map.National?.TickAll(map.Economy);
                CheckElection(simDay);
            }
        }

        // Approval-rating-based term/election mechanic — the same ApprovalRating already shown
        // on the Politics tab decides whether the player keeps governing. Only fires once the
        // current term has elapsed; above 50% is a safe re-election, below 35% is a loss,
        // and the band between is a weighted coin flip so it isn't a hard cliff-edge.
        void CheckElection(long day)
        {
            if (PlayerState.State != GameState.Playing) return;
            if (PlayerState.CountryIndex < 0 || map.National == null) return;
            if (PlayerState.CountryIndex >= map.National.States.Count) return;
            if (day - PlayerState.TermStartDay < PlayerState.TermLengthDays) return;

            float approval = map.National.States[PlayerState.CountryIndex].ApprovalRating;

            bool reelected;
            if (approval >= 50f) reelected = true;
            else if (approval < 35f) reelected = false;
            else reelected = Random.value < Mathf.InverseLerp(35f, 50f, approval);

            if (reelected)
            {
                PlayerState.TermsServed++;
                PlayerState.TermStartDay = day;
                PlayerState.WonLastElection = true;
                PlayerState.LastResultMessage = $"Re-elected with {approval:0.0}% approval — term {PlayerState.TermsServed + 1} begins.";
            }
            else
            {
                PlayerState.WonLastElection = false;
                PlayerState.LastResultMessage = $"Voted out of office after {PlayerState.TermsServed} term(s) served — final approval {approval:0.0}%.";
                PlayerState.State = GameState.GameOver;
            }
        }

        void HandleClickSelect()
        {
            if (Input.GetMouseButtonDown(0)) { pressPos = Input.mousePosition; pressed = true; }
            if (Input.GetMouseButtonUp(0) && pressed)
            {
                pressed = false;
                // Only a click (not a pan-drag) if the pointer barely moved.
                if ((Input.mousePosition - pressPos).sqrMagnitude > 25f) return;

                Vector3 w = cam.ScreenToWorldPoint(Input.mousePosition);
                Vector2 lonlat = new Vector2(w.x, w.y);
                for (int i = 0; i < map.World.Countries.Count; i++)
                {
                    var c = map.World.Countries[i];
                    if (!GeoMath.BboxContains(c.BboxMin, c.BboxMax, lonlat)) continue;
                    bool inside = false;
                    foreach (var ring in c.OuterRings)
                        if (GeoMath.PointInRing(lonlat, ring)) { inside = true; break; }
                    if (inside) { selected = i; break; }
                }
            }
        }

        // Rendering moved to Meridian.UI (TopBar / MinistryBar / CountryPanel / StartScreen /
        // GameOverScreen) — this component now only owns the sim clock, election mechanic, and
        // click-to-select hit-testing.
    }
}
