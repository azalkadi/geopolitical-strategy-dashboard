using UnityEngine;
using UnityEngine.UIElements;
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
            // the player has actually picked a nation to govern. It also freezes while a
            // decision event awaits the player's choice: the world waits for the head of state.
            if (PlayerState.State == GameState.Playing && EventSystem.Pending == null)
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
                map.Diplomacy?.TickAll();
                CheckElection(simDay);
                LogEconomyDiagnostic();

                // Decision events fire for the player's own country only. Once one fires, the
                // Update() gate freezes the clock until the player chooses, so stop mid-batch —
                // no more days may pass this frame.
                if (PlayerState.CountryIndex >= 0 && map.National != null && PlayerState.CountryIndex < map.National.States.Count)
                {
                    EventSystem.MaybeFire(simDay, map.Economy.States[PlayerState.CountryIndex], map.National.States[PlayerState.CountryIndex]);
                    if (EventSystem.Pending != null)
                    {
                        // A crisis drops the game out of fast-forward: after deciding, the world
                        // resumes at 1x so the player reads the consequences instead of instantly
                        // slamming into the next event. Cranking speed back up is one click.
                        if (daysPerSecond > 1f) daysPerSecond = 1f;
                        break;
                    }
                }
            }
        }

        // Dev-only: periodic snapshot of the player's own economy so extended play can be
        // observed via Player.log even without driving the UI (used to sanity-check that the
        // sim actually feels dynamic over months/years of play, not just at a single instant).
        long nextDiagDay;
        void LogEconomyDiagnostic()
        {
            if (PlayerState.State != GameState.Playing || PlayerState.CountryIndex < 0) return;
            if (simDay < nextDiagDay) return;
            nextDiagDay = simDay + 30;
            var e = map.Economy.States[PlayerState.CountryIndex];
            Debug.Log($"[econdiag] day {simDay}: GDP=${e.Gdp:n1}B growth={e.GrowthRate:0.00}% unemp={e.Unemployment:0.00}% inflation={e.Inflation:0.00}% treasury=${e.Treasury:n1}B taxes=[income={e.TaxIncome:0.0} corp={e.TaxCorporate:0.0} vat={e.TaxVat:0.0} tariff={e.TaxTariff:0.0} custom={e.CustomTaxes.Count}] effTax={e.EffectiveTaxRate():0.0}%");
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

        UIDocument uiDoc;

        // Legacy Input polling doesn't know UI Toolkit exists — without this check, every
        // click on a HUD button/panel/modal ALSO fell through to the map and click-selected
        // whatever country happened to be under the cursor (observed: choosing an event-modal
        // option silently changed the selected country). Ask the UI panel what's under the
        // pointer; any pickable element there means the click belongs to the UI, not the map.
        bool PointerOverUI(Vector2 mouseScreenPos)
        {
            if (uiDoc == null) uiDoc = FindObjectOfType<UIDocument>();
            var panel = uiDoc != null ? uiDoc.rootVisualElement?.panel : null;
            if (panel == null) return false;
            // Screen space is bottom-left origin; panel space is top-left.
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(mouseScreenPos.x, Screen.height - mouseScreenPos.y));
            return panel.Pick(panelPos) != null;
        }

        void HandleClickSelect()
        {
            if (Input.GetMouseButtonDown(0)) { pressPos = Input.mousePosition; pressed = !PointerOverUI(Input.mousePosition); }
            if (Input.GetMouseButtonUp(0) && pressed)
            {
                pressed = false;
                // Only a click (not a pan-drag) if the pointer barely moved.
                if ((Input.mousePosition - pressPos).sqrMagnitude > 25f) return;
                if (PointerOverUI(Input.mousePosition)) return;

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
