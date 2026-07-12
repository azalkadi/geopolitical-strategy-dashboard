using UnityEngine;
using UnityEngine.UIElements;
using Meridian.Map;
using Meridian.Geo;

namespace Meridian.UI
{
    // Small floating info panel for whichever map feature MapInteraction's click-to-select
    // picked (city / road / border crossing / water crossing) — kept in its own file/component
    // rather than folded into GameUIRoot's 1200+ lines, since it's a self-contained concern with
    // its own simple rebuild-on-change logic. Shares the same UIDocument/root as GameUIRoot
    // (one root visual tree for the whole scene) but owns its own subtree.
    //
    // Deliberately left-anchored (the country ministry panel is right-anchored, the ministry bar
    // is bottom-center) so all three can be open/visible without overlapping.
    public class FeaturePanel : MonoBehaviour
    {
        MapRenderer map;
        MapInteraction interaction;
        VisualElement root;

        VisualElement panel;
        Label title;
        Label subtitle;
        VisualElement body;

        // Rebuild only when the selection actually changes, same pattern GameUIRoot uses for its
        // own side panel.
        int builtCity = -2, builtRoad = -2, builtBorder = -2, builtWater = -2;

        void Start()
        {
            map = FindObjectOfType<MapRenderer>();
            interaction = FindObjectOfType<MapInteraction>();
            var doc = FindObjectOfType<UIDocument>();
            if (doc == null) { Debug.LogWarning("[ui] FeaturePanel found no UIDocument; skipping"); enabled = false; return; }
            root = doc.rootVisualElement;

            Build();
            root.schedule.Execute(Refresh).Every(100);
        }

        void Build()
        {
            panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.left = 10; panel.style.bottom = 54;
            panel.style.width = 300;
            panel.style.backgroundColor = new StyleColor(GameTheme.BgPanel);
            panel.style.borderTopLeftRadius = 6; panel.style.borderTopRightRadius = 6;
            panel.style.borderBottomLeftRadius = 6; panel.style.borderBottomRightRadius = 6;
            panel.style.borderLeftWidth = 3; panel.style.borderLeftColor = new StyleColor(GameTheme.Accent);
            panel.style.borderTopWidth = 1; panel.style.borderRightWidth = 1; panel.style.borderBottomWidth = 1;
            panel.style.borderTopColor = new StyleColor(GameTheme.Border);
            panel.style.borderRightColor = new StyleColor(GameTheme.Border);
            panel.style.borderBottomColor = new StyleColor(GameTheme.Border);
            panel.style.paddingLeft = 12; panel.style.paddingRight = 12;
            panel.style.paddingTop = 10; panel.style.paddingBottom = 10;
            panel.style.display = DisplayStyle.None;
            root.Add(panel);

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            title = new Label { text = "" };
            title.style.fontSize = 14; title.style.color = new StyleColor(GameTheme.TextPrimary);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;
            headerRow.Add(title);
            var closeBtn = new Label { text = "✕" };
            closeBtn.style.fontSize = 12; closeBtn.style.color = new StyleColor(GameTheme.TextDim);
            closeBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            closeBtn.style.width = 20; closeBtn.style.height = 20;
            closeBtn.pickingMode = PickingMode.Position;
            closeBtn.RegisterCallback<ClickEvent>(_ => interaction.CloseFeaturePanel());
            headerRow.Add(closeBtn);
            panel.Add(headerRow);

            subtitle = new Label { text = "" };
            subtitle.style.fontSize = 11; subtitle.style.color = new StyleColor(GameTheme.TextDim);
            subtitle.style.marginBottom = 6;
            panel.Add(subtitle);

            body = new VisualElement();
            panel.Add(body);
        }

        void Refresh()
        {
            if (map == null || map.World == null || interaction == null) return;

            int city = interaction.SelectedCity, road = interaction.SelectedRoad;
            int border = interaction.SelectedBorderCrossing, water = interaction.SelectedWaterCrossing;

            if (city < 0 && road < 0 && border < 0 && water < 0)
            {
                panel.style.display = DisplayStyle.None;
                builtCity = builtRoad = builtBorder = builtWater = -2;
                return;
            }
            if (city == builtCity && road == builtRoad && border == builtBorder && water == builtWater) return;
            builtCity = city; builtRoad = road; builtBorder = border; builtWater = water;

            panel.style.display = DisplayStyle.Flex;
            body.Clear();

            if (city >= 0 && city < map.World.Cities.Count) BuildCity(map.World.Cities[city]);
            else if (road >= 0 && road < map.World.Roads.Count) BuildRoad(road);
            else if (border >= 0 && border < map.World.BorderCrossings.Count) BuildBorderCrossing(map.World.BorderCrossings[border]);
            else if (water >= 0 && water < map.World.WaterCrossings.Count) BuildWaterCrossing(map.World.WaterCrossings[water]);
        }

        void BuildCity(City c)
        {
            title.text = c.Name;
            subtitle.text = $"{c.Country} · {c.Tier.Label()}";
            Stat("Population (est.)", c.PopMax > 0 ? c.PopMax.ToString("n0") : "unknown");
            if (c.IsCapital) Stat("Status", "National capital");
        }

        void BuildRoad(int roadIndex)
        {
            var road = map.World.Roads[roadIndex];
            title.text = string.IsNullOrEmpty(road.Name) ? "Road" : road.Name;
            var (start, end) = interaction.NearestCitiesForRoad(roadIndex);
            subtitle.text = "Road";
            if (start != null && end != null && start != end)
                Stat("Connects (nearest cities)", $"{start.Name} ↔ {end.Name}");
            else if (start != null)
                Stat("Nearest city", start.Name);
            Why("Endpoints matched to the nearest known settlement — roads don't carry \"connects city A to city B\" data directly.");
        }

        void BuildBorderCrossing(BorderCrossing bc)
        {
            title.text = "Border Crossing";
            subtitle.text = bc.RoadName;
            Stat("Countries", $"{bc.CountryA} ↔ {bc.CountryB}");
        }

        void BuildWaterCrossing(WaterCrossing wc)
        {
            title.text = wc.Name;
            subtitle.text = "Intercountry causeway / bridge";
            Stat("Countries", $"{wc.CountryA} ↔ {wc.CountryB}");
        }

        void Stat(string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 4;
            var l = new Label { text = label };
            l.style.fontSize = 11; l.style.color = new StyleColor(GameTheme.TextDim);
            var v = new Label { text = value };
            v.style.fontSize = 11; v.style.color = new StyleColor(GameTheme.TextPrimary);
            v.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(l); row.Add(v);
            body.Add(row);
        }

        void Why(string text)
        {
            var l = new Label { text = "↳ " + text };
            l.style.fontSize = 10; l.style.color = new StyleColor(GameTheme.TextDim);
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginTop = 4;
            body.Add(l);
        }
    }
}
