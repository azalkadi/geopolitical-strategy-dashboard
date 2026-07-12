using System.Collections.Generic;
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

        // Independent "map feature" selection — cities/roads/border crossings/water crossings
        // clicked directly, mutually exclusive with each other and with the country ministry
        // panel (picking one of these clears `selected` and vice versa, so only one info surface
        // shows at a time). -1 = none.
        public int SelectedCity { get; private set; } = -1;
        public int SelectedRoad { get; private set; } = -1;
        public int SelectedBorderCrossing { get; private set; } = -1;
        public int SelectedWaterCrossing { get; private set; } = -1;

        bool HasFeatureSelection => SelectedCity >= 0 || SelectedRoad >= 0 || SelectedBorderCrossing >= 0 || SelectedWaterCrossing >= 0;

        void ClearFeatureSelection()
        {
            SelectedCity = SelectedRoad = SelectedBorderCrossing = SelectedWaterCrossing = -1;
        }

        public void CloseFeaturePanel() => ClearFeatureSelection();

        // For a clicked road, the two cities nearest its two endpoints — not sourced from any
        // dataset (roads don't carry "connects city A to city B" data), just the closest named
        // settlement to where the road starts and ends.
        public (City start, City end) NearestCitiesForRoad(int roadIndex)
        {
            var feat = map.World.Roads[roadIndex];
            if (feat.Lines.Count == 0) return (null, null);
            var first = feat.Lines[0];
            var last = feat.Lines[feat.Lines.Count - 1];
            Vector2 a = first[0];
            Vector2 b = last[last.Count - 1];
            return (NearestCity(a), NearestCity(b));
        }

        City NearestCity(Vector2 pos)
        {
            City best = null;
            float bestDistSq = float.MaxValue;
            foreach (var c in map.World.Cities)
            {
                float d2 = (c.Pos - pos).sqrMagnitude;
                if (d2 < bestDistSq) { bestDistSq = d2; best = c; }
            }
            return best;
        }

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
                MaybeRunDiplomacyDiag();

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

        // Dev-only: MERIDIAN_DIAG_DIPLOMACY=1 runs a scripted diplomacy self-test at day 30 —
        // exercises the exact same SendAid/SignAgreement/Denounce calls the panel buttons make
        // and logs before/after values, so the logic is verifiable from Player.log alone without
        // relying on pixel-precise UI clicks (which have proven flaky under automation on this
        // dev machine). Normal play is unaffected: the env var is never set outside a test launch.
        bool diplomacyDiagDone;
        void MaybeRunDiplomacyDiag()
        {
            if (diplomacyDiagDone || simDay < 30 || map.Diplomacy == null) return;
            if (System.Environment.GetEnvironmentVariable("MERIDIAN_DIAG_DIPLOMACY") == null) return;
            diplomacyDiagDone = true;

            int me = PlayerState.CountryIndex;
            if (me < 0) return;
            var myEcon = map.Economy.States[me];
            var myNat = map.National.States[me];

            // Aid target: the friendliest neighbor. Agreement target: same (aid should push it
            // over the 65 threshold if it wasn't already). Denounce target: the frostiest.
            var friendliest = map.Diplomacy.RankedFor(me, friendliest: true, topN: 1)[0];
            var frostiest = map.Diplomacy.RankedFor(me, friendliest: false, topN: 1)[0];

            double treasuryBefore = myEcon.Treasury;
            float relBefore = friendliest.relation;
            Debug.Log($"[dipdiag] aid target={map.World.Countries[friendliest.index].Name} relBefore={relBefore:0.0} treasuryBefore={treasuryBefore:0.00}");
            map.Diplomacy.SendAid(me, friendliest.index, myEcon, myNat, simDay);
            Debug.Log($"[dipdiag] after aid: rel={map.Diplomacy.GetRelation(me, friendliest.index):0.0} treasury={myEcon.Treasury:0.00} (expect rel +12, treasury -{System.Math.Max(0.2, myEcon.Gdp * 0.0005):0.00})");

            var theirEcon = map.Economy.States[friendliest.index];
            float exportBonusBefore = myEcon.TradeAgreementExportBonus;
            string agreementResult = map.Diplomacy.SignAgreement(me, friendliest.index, myEcon, theirEcon, simDay);
            Debug.Log($"[dipdiag] agreement: result='{agreementResult ?? "REFUSED (relations too low)"}' myExportBonus {exportBonusBefore:0.000}->{myEcon.TradeAgreementExportBonus:0.000} hasAgreement={map.Diplomacy.HasAgreement(me, friendliest.index)}");

            float approvalBefore = myNat.ApprovalRating;
            float frostyRelBefore = frostiest.relation;
            map.Diplomacy.Denounce(me, frostiest.index, myNat, simDay);
            Debug.Log($"[dipdiag] denounce target={map.World.Countries[frostiest.index].Name}: rel {frostyRelBefore:0.0}->{map.Diplomacy.GetRelation(me, frostiest.index):0.0} approval {approvalBefore:0.0}->{myNat.ApprovalRating:0.0}");

            Debug.Log($"[dipdiag] cooldown check: canActAgain={map.Diplomacy.CanAct(me, friendliest.index, simDay)} (expect False right after acting)");
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

        // Click priority: water crossings > border crossings > cities > roads > countries — most
        // specific/smallest target wins, country (always a hit if you're on land) is the
        // fallback. Each of the first four is only actually tested while its layer is visible
        // (reuses MapLayers' own zoom-gated GameObject.activeSelf rather than duplicating zoom
        // thresholds here), so you can't select something you can't currently see on screen.
        void HandleClickSelect()
        {
            if (Input.GetMouseButtonDown(0)) { pressPos = Input.mousePosition; pressed = !PointerOverUI(Input.mousePosition); }
            if (!(Input.GetMouseButtonUp(0) && pressed)) return;
            pressed = false;
            // Only a click (not a pan-drag) if the pointer barely moved.
            if ((Input.mousePosition - pressPos).sqrMagnitude > 25f) return;
            if (PointerOverUI(Input.mousePosition)) return;

            Vector3 mouseScreen = Input.mousePosition;

            if (map.WaterCrossingsRoot != null && map.WaterCrossingsRoot.activeSelf)
            {
                var pts = map.World.WaterCrossings.ConvertAll(wc => (wc.Line[0] + wc.Line[wc.Line.Count - 1]) * 0.5f);
                if (TryPickNearestPoint(pts, mouseScreen, 14f, out int wcIdx))
                {
                    ClearFeatureSelection();
                    SelectedWaterCrossing = wcIdx;
                    return;
                }
            }

            if (map.BorderCrossingsRoot != null && map.BorderCrossingsRoot.activeSelf)
            {
                var pts = map.World.BorderCrossings.ConvertAll(bc => bc.Pos);
                if (TryPickNearestPoint(pts, mouseScreen, 10f, out int bcIdx))
                {
                    ClearFeatureSelection();
                    SelectedBorderCrossing = bcIdx;
                    return;
                }
            }

            {
                // Only cities whose zoom tier is currently VISIBLE are clickable. The full list
                // holds all 7,342 settlements; without this filter, a 9px radius around every
                // invisible town swallowed nearly every click meant for a country at world zoom
                // (observed: two attempts to select China landed on Choibalsan, Mongolia, an
                // off-screen-tier town of 55k).
                var visiblePts = new List<Vector2>();
                var visibleIdx = new List<int>();
                for (int i = 0; i < map.World.Cities.Count; i++)
                {
                    var tierRoot = map.CityTierRoots != null ? map.CityTierRoots[(int)map.World.Cities[i].Tier] : null;
                    if (tierRoot == null || !tierRoot.activeSelf) continue;
                    visiblePts.Add(map.World.Cities[i].Pos);
                    visibleIdx.Add(i);
                }
                if (TryPickNearestPoint(visiblePts, mouseScreen, 9f, out int pickIdx))
                {
                    ClearFeatureSelection();
                    SelectedCity = visibleIdx[pickIdx];
                    return;
                }
            }

            if (map.RoadsRoot != null && map.RoadsRoot.activeSelf)
            {
                if (TryPickRoad(map.World.Roads, mouseScreen, 6f, out int roadIdx))
                {
                    ClearFeatureSelection();
                    SelectedRoad = roadIdx;
                    return;
                }
            }

            Vector3 w = cam.ScreenToWorldPoint(mouseScreen);
            Vector2 lonlat = new Vector2(w.x, w.y);
            for (int i = 0; i < map.World.Countries.Count; i++)
            {
                var c = map.World.Countries[i];
                if (!GeoMath.BboxContains(c.BboxMin, c.BboxMax, lonlat)) continue;
                bool inside = false;
                foreach (var ring in c.OuterRings)
                    if (GeoMath.PointInRing(lonlat, ring)) { inside = true; break; }
                if (inside)
                {
                    ClearFeatureSelection();
                    selected = i;
                    break;
                }
            }
        }

        bool TryPickNearestPoint(List<Vector2> positions, Vector3 mouseScreen, float pixelRadius, out int index)
        {
            index = -1;
            float bestDistSq = pixelRadius * pixelRadius;
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 sp = cam.WorldToScreenPoint(new Vector3(positions[i].x, positions[i].y, 0f));
                float dx = sp.x - mouseScreen.x, dy = sp.y - mouseScreen.y;
                float d2 = dx * dx + dy * dy;
                if (d2 < bestDistSq) { bestDistSq = d2; index = i; }
            }
            return index >= 0;
        }

        bool TryPickRoad(List<LineFeature> roads, Vector3 mouseScreen, float pixelThreshold, out int roadIndex)
        {
            roadIndex = -1;
            float bestDistSq = pixelThreshold * pixelThreshold;
            for (int r = 0; r < roads.Count; r++)
            {
                foreach (var line in roads[r].Lines)
                {
                    for (int i = 0; i + 1 < line.Count; i++)
                    {
                        Vector3 spA = cam.WorldToScreenPoint(new Vector3(line[i].x, line[i].y, 0f));
                        Vector3 spB = cam.WorldToScreenPoint(new Vector3(line[i + 1].x, line[i + 1].y, 0f));
                        float d2 = SqDistPointToSegment(mouseScreen, spA, spB);
                        if (d2 < bestDistSq) { bestDistSq = d2; roadIndex = r; }
                    }
                }
            }
            return roadIndex >= 0;
        }

        static float SqDistPointToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 ap = new Vector2(p.x - a.x, p.y - a.y);
            Vector2 ab = new Vector2(b.x - a.x, b.y - a.y);
            float ab2 = ab.sqrMagnitude;
            float t = ab2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(ap, ab) / ab2) : 0f;
            Vector2 closest = new Vector2(a.x + ab.x * t, a.y + ab.y * t);
            float dx = p.x - closest.x, dy = p.y - closest.y;
            return dx * dx + dy * dy;
        }

        // Rendering moved to Meridian.UI (TopBar / MinistryBar / CountryPanel / StartScreen /
        // GameOverScreen) — this component now only owns the sim clock, election mechanic, and
        // click-to-select hit-testing.
    }
}
