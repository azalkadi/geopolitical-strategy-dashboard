using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Meridian.Map;
using Meridian.Sim;

namespace Meridian.UI
{
    // Real game UI built on UI Toolkit (UIDocument/VisualElement) — replaces the old OnGUI
    // implementation. OnGUI/IMGUI is Unity's own debug-only immediate-mode tool, never meant to
    // ship in a game (no real rounded corners, no proper layout, no hover states); UI Toolkit is
    // the production UI framework, built here entirely from C# (no UI Builder / USS assets, to
    // avoid a repeat of the shader-stripping build gotcha with an externally referenced asset —
    // every visual is set via inline C# style properties instead).
    //
    // Every custom control (buttons, sliders, dropdowns) is a plain VisualElement with manual
    // pointer-event handling rather than the built-in Button/Slider controls, because those rely
    // on a PanelSettings.themeStyleSheet for their default chrome — which we don't have without
    // the Editor's UI Builder — so building raw guarantees the visuals are fully under our
    // control regardless of theme availability.
    [RequireComponent(typeof(UIDocument))]
    public class GameUIRoot : MonoBehaviour
    {
        static readonly NationCategory[] Categories = (NationCategory[])Enum.GetValues(typeof(NationCategory));

        MapRenderer map;
        MapInteraction interaction;
        UIDocument doc;
        VisualElement root;

        // --- top bar ---
        VisualElement topBarRoot;
        Label dayLabel;
        readonly Dictionary<float, VisualElement> speedButtons = new();
        readonly Dictionary<MapMode, VisualElement> mapModeButtons = new();
        Label playerBadge;
        Label topCountryLabel;
        VisualElement topStatsRow;
        Label[] topStatLabels;

        // --- ministry bar ---
        VisualElement ministryBarRoot;
        VisualElement[] ministryButtons;
        VisualElement dropdownLayer;

        // --- start / game-over screens ---
        VisualElement startScreen;
        VisualElement startScreenList;
        bool startScreenPopulated;
        VisualElement gameOverScreen;
        Label gameOverMessage;
        Label gameOverStats;

        // --- side panel ---
        VisualElement sidePanel;
        VisualElement sidePanelShadow;
        Label panelTitle;
        Label panelSubtitle;
        VisualElement panelBody;
        int builtForSelected = -2;
        NationCategory builtForCategory = (NationCategory)(-1);
        string builtForTopic = "__unbuilt__";
        readonly List<SliderBinding> activeSliders = new();

        // --- event toasts (AI policy changes / threshold-crossing events, surfaced live) ---
        VisualElement toastLayer;
        readonly List<VisualElement> activeToasts = new();
        string[] lastWhySeen;

        class SliderBinding
        {
            public VisualElement Thumb;
            public Label ValueLabel;
            public Func<float> Get;
            public float Lo, Hi;
            public bool Dragging;
        }

        void Awake()
        {
            map = FindObjectOfType<MapRenderer>();
            interaction = FindObjectOfType<MapInteraction>();

            // A PanelSettings created purely at runtime via CreateInstance never gets the ICU
            // text-shaping data UI Toolkit's Advanced Text Generation needs — that data is only
            // embedded at *asset import* time. EnsurePanelSettingsAsset (build-time Editor step)
            // creates a real asset under Resources/ so it's bundled into every build and has
            // real ICU data; load that instead of building a throwaway instance here.
            var settings = Resources.Load<PanelSettings>("GamePanelSettings");
            if (settings == null)
            {
                Debug.LogWarning("[ui] GamePanelSettings.asset missing from Resources — falling back to a runtime PanelSettings (text will likely fail to render)");
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.textSettings = ScriptableObject.CreateInstance<PanelTextSettings>();
            }
            doc = GetComponent<UIDocument>();
            doc.panelSettings = settings;

            root = doc.rootVisualElement;
            root.style.position = Position.Absolute;
            root.style.left = 0; root.style.right = 0; root.style.top = 0; root.style.bottom = 0;
            root.pickingMode = PickingMode.Ignore; // let clicks fall through except on real controls

            BuildTopBar();
            BuildMinistryBar();
            BuildSidePanel();
            BuildToastLayer();
            BuildStartScreen();
            BuildGameOverScreen();

            root.schedule.Execute(Refresh).Every(100);
        }

        // ============================== TOP BAR ==============================

        void BuildTopBar()
        {
            var bar = new VisualElement();
            bar.pickingMode = PickingMode.Position;
            bar.style.position = Position.Absolute;
            bar.style.left = 0; bar.style.right = 0; bar.style.top = 0; bar.style.height = 36;
            bar.style.backgroundColor = new StyleColor(GameTheme.BgTop);
            bar.style.borderBottomWidth = 1;
            bar.style.borderBottomColor = new StyleColor(GameTheme.Border);
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.paddingLeft = 10; bar.style.paddingRight = 10;
            root.Add(bar);
            topBarRoot = bar;

            var title = MakeLabel("MERIDIAN", 14, GameTheme.Accent, bold: true);
            title.style.marginRight = 12;
            bar.Add(title);

            dayLabel = MakeLabel("Day 0", 13, GameTheme.TextPrimary, bold: true);
            dayLabel.style.marginRight = 12;
            bar.Add(dayLabel);

            foreach (var (label, speed) in new (string, float)[] { ("II", 0f), ("1x", 1f), ("3x", 3f), ("10x", 10f) })
            {
                var btn = MakeButton(label, 11, GameTheme.BgButton, GameTheme.BgButtonHover, GameTheme.TextDim, () => interaction.daysPerSecond = speed);
                btn.style.width = 28; btn.style.height = 22; btn.style.marginRight = 4;
                bar.Add(btn);
                speedButtons[speed] = btn;
            }

            var modeSep = new VisualElement();
            modeSep.style.width = 1; modeSep.style.height = 18;
            modeSep.style.marginLeft = 6; modeSep.style.marginRight = 6;
            modeSep.style.backgroundColor = new StyleColor(GameTheme.Border);
            bar.Add(modeSep);

            foreach (var (label, mode) in new (string, MapMode)[] { ("Political", MapMode.Political), ("Satellite", MapMode.Satellite) })
            {
                var btn = MakeButton(label, 11, GameTheme.BgButton, GameTheme.BgButtonHover, GameTheme.TextDim, () => map.SetMode(mode));
                btn.style.width = 62; btn.style.height = 22; btn.style.marginRight = 4;
                bar.Add(btn);
                mapModeButtons[mode] = btn;
            }

            // Persistent "your nation" badge — stays visible even while you're inspecting a
            // rival country's panel, since the election countdown is the thing you actually
            // need to keep an eye on regardless of what else you're looking at.
            playerBadge = MakeLabel("", 12, GameTheme.Accent, bold: true);
            playerBadge.style.marginRight = 16;
            playerBadge.style.display = DisplayStyle.None;
            bar.Add(playerBadge);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            bar.Add(spacer);

            topCountryLabel = MakeLabel("Click a country to inspect it", 12, GameTheme.TextDim);
            bar.Add(topCountryLabel);

            topStatsRow = new VisualElement();
            topStatsRow.style.flexDirection = FlexDirection.Row;
            topStatsRow.style.display = DisplayStyle.None;
            bar.Add(topStatsRow);

            string[] statNames = { "GROWTH", "UNEMP.", "INFLATION", "TREASURY" };
            topStatLabels = new Label[statNames.Length];
            for (int i = 0; i < statNames.Length; i++)
            {
                var tile = new VisualElement();
                tile.style.width = 84;
                tile.style.marginLeft = 10;
                var head = MakeLabel(statNames[i], 9, GameTheme.TextDim);
                var val = MakeLabel("—", 13, GameTheme.TextPrimary, bold: true);
                tile.Add(head);
                tile.Add(val);
                topStatsRow.Add(tile);
                topStatLabels[i] = val;
            }
        }

        // ============================== MINISTRY BAR ==============================

        void BuildMinistryBar()
        {
            var bar = new VisualElement();
            bar.pickingMode = PickingMode.Position;
            bar.style.position = Position.Absolute;
            bar.style.left = 0; bar.style.right = 0; bar.style.bottom = 0; bar.style.height = 42;
            bar.style.backgroundColor = new StyleColor(GameTheme.BgBar);
            bar.style.borderTopWidth = 1;
            bar.style.borderTopColor = new StyleColor(GameTheme.Border);
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.justifyContent = Justify.Center;
            bar.style.alignItems = Align.Center;
            root.Add(bar);
            ministryBarRoot = bar;

            ministryButtons = new VisualElement[Categories.Length];
            dropdownLayer = new VisualElement();
            dropdownLayer.pickingMode = PickingMode.Position;
            dropdownLayer.style.position = Position.Absolute;
            dropdownLayer.style.left = 0; dropdownLayer.style.right = 0; dropdownLayer.style.top = 0; dropdownLayer.style.bottom = 0;
            root.Add(dropdownLayer); // added after bar so dropdowns render on top

            for (int i = 0; i < Categories.Length; i++)
            {
                var cat = Categories[i];
                var btn = MakeButton(cat.Label(), 13, GameTheme.BgButton, GameTheme.BgButtonHover, GameTheme.TextDim,
                    () => { UIState.ActiveCategory = cat; UIState.ActiveTopic = null; });
                btn.style.width = 108;
                btn.style.height = 32;
                btn.style.marginLeft = 3; btn.style.marginRight = 3;

                // A small colored dot in front of the label — lets you recognize a ministry by
                // color alone once you've learned the palette, without reading text every time.
                var dot = new VisualElement();
                dot.pickingMode = PickingMode.Ignore;
                dot.style.width = 7; dot.style.height = 7;
                dot.style.borderTopLeftRadius = 4; dot.style.borderTopRightRadius = 4;
                dot.style.borderBottomLeftRadius = 4; dot.style.borderBottomRightRadius = 4;
                dot.style.backgroundColor = new StyleColor(cat.Accent());
                dot.style.marginRight = 6;
                btn.Insert(0, dot);

                var underline = new VisualElement();
                underline.style.position = Position.Absolute;
                underline.style.left = 0; underline.style.right = 0; underline.style.bottom = 0; underline.style.height = 2;
                underline.style.backgroundColor = new StyleColor(cat.Accent()); // this category's own color, not the global gold
                underline.style.display = DisplayStyle.None;
                underline.name = "underline";
                btn.Add(underline);

                int capturedIndex = i;
                btn.RegisterCallback<PointerEnterEvent>(_ => ShowDropdown(capturedIndex));
                btn.RegisterCallback<PointerLeaveEvent>(_ => HideDropdownSoon());

                bar.Add(btn);
                ministryButtons[i] = btn;
            }
        }

        bool dropdownPinned;

        void ShowDropdown(int index)
        {
            dropdownLayer.Clear();
            dropdownPinned = true;
            var cat = Categories[index];
            var topics = cat.Topics();
            if (topics.Length == 0) return;

            var btnRect = ministryButtons[index].worldBound;

            var dd = new VisualElement();
            dd.style.position = Position.Absolute;
            dd.style.width = 210;
            float left = Mathf.Clamp(btnRect.x + btnRect.width * 0.5f - 105f, 4f, Screen.width - 214f);
            dd.style.left = left;
            dd.style.bottom = 46; // just above the ministry bar
            dd.style.backgroundColor = new StyleColor(GameTheme.BgDropdown);
            dd.style.borderTopLeftRadius = 8; dd.style.borderTopRightRadius = 8;
            dd.style.borderBottomLeftRadius = 8; dd.style.borderBottomRightRadius = 8;
            dd.style.paddingTop = 6; dd.style.paddingBottom = 6; dd.style.paddingLeft = 4; dd.style.paddingRight = 4;
            dd.RegisterCallback<PointerEnterEvent>(_ => dropdownPinned = true);
            dd.RegisterCallback<PointerLeaveEvent>(_ => HideDropdownSoon());

            foreach (var topic in topics)
            {
                var capturedTopic = topic;
                var row = MakeButton(topic, 12, GameTheme.BgDropdown, GameTheme.BgButtonHover, GameTheme.TextPrimary,
                    () => { UIState.ActiveCategory = cat; UIState.ActiveTopic = capturedTopic; }, align: TextAnchor.MiddleLeft);
                row.style.height = 24;
                row.style.marginBottom = 2;
                row.style.paddingLeft = 8;
                dd.Add(row);
            }

            dropdownLayer.Add(dd);
        }

        void HideDropdownSoon()
        {
            dropdownPinned = false;
            root.schedule.Execute(() =>
            {
                if (!dropdownPinned) dropdownLayer.Clear();
            }).ExecuteLater(80);
        }

        // ============================== SIDE PANEL ==============================

        void BuildSidePanel()
        {
            // Soft drop shadow: a darker, slightly offset duplicate rect added first so it
            // renders behind the panel (UI Toolkit draws children in document order).
            var shadow = new VisualElement();
            shadow.style.position = Position.Absolute;
            shadow.style.right = 6; shadow.style.top = 48; shadow.style.bottom = 44;
            shadow.style.width = 330;
            shadow.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.35f));
            shadow.style.borderTopLeftRadius = 10; shadow.style.borderTopRightRadius = 10;
            shadow.style.borderBottomLeftRadius = 10; shadow.style.borderBottomRightRadius = 10;
            root.Add(shadow);
            sidePanelShadow = shadow;

            sidePanel = new VisualElement();
            sidePanel.style.position = Position.Absolute;
            sidePanel.style.right = 10; sidePanel.style.top = 44; sidePanel.style.bottom = 48;
            sidePanel.style.width = 330;
            sidePanel.style.backgroundColor = new StyleColor(GameTheme.BgPanel);
            sidePanel.style.borderTopLeftRadius = 10; sidePanel.style.borderTopRightRadius = 10;
            sidePanel.style.borderBottomLeftRadius = 10; sidePanel.style.borderBottomRightRadius = 10;
            sidePanel.style.borderLeftWidth = 3;
            sidePanel.style.borderLeftColor = new StyleColor(GameTheme.Accent);
            sidePanel.style.paddingLeft = 14; sidePanel.style.paddingRight = 14;
            sidePanel.style.paddingTop = 12; sidePanel.style.paddingBottom = 12;
            sidePanel.style.display = DisplayStyle.None;
            root.Add(sidePanel);

            panelTitle = MakeLabel("", 16, GameTheme.TextPrimary, bold: true);
            panelSubtitle = MakeLabel("", 11, GameTheme.TextDim);
            panelSubtitle.style.marginBottom = 8;
            sidePanel.Add(panelTitle);
            sidePanel.Add(panelSubtitle);

            panelBody = new VisualElement();
            sidePanel.Add(panelBody);
        }

        void RebuildSidePanel()
        {
            activeSliders.Clear();
            panelBody.Clear();

            int sel = interaction.Selected;
            if (map.World == null || sel < 0 || sel >= map.World.Countries.Count)
            {
                sidePanel.style.display = DisplayStyle.None;
                sidePanelShadow.style.display = DisplayStyle.None;
                return;
            }
            sidePanel.style.display = DisplayStyle.Flex;
            sidePanelShadow.style.display = DisplayStyle.Flex;
            // Panel's accent edge matches the active ministry's color — reinforces which
            // domain you're looking at even before reading a single label.
            sidePanel.style.borderLeftColor = new StyleColor(UIState.ActiveCategory.Accent());

            var c = map.World.Countries[sel];
            panelTitle.text = $"{c.Name}  ({c.IsoA3})";
            panelSubtitle.text = $"{c.Continent} · {c.Subregion} · Pop {c.PopEst:n0}";

            var e = map.Economy != null && sel < map.Economy.States.Count ? map.Economy.States[sel] : null;
            var n = map.National != null && sel < map.National.States.Count ? map.National.States[sel] : null;
            if (e == null) return;

            switch (UIState.ActiveCategory)
            {
                case NationCategory.Economy: DrawEconomy(e); break;
                case NationCategory.Budget: DrawBudget(e); break;
                case NationCategory.Trade: DrawTrade(e); break;
                case NationCategory.Politics: DrawPolitics(n); break;
                case NationCategory.Military: DrawMilitary(n); break;
                case NationCategory.Diplomacy: DrawDiplomacy(n); break;
                case NationCategory.Society: DrawSociety(n); break;
                case NationCategory.Technology: DrawTechnology(n); break;
            }
        }

        void DrawEconomy(EconomyState e)
        {
            // "Tax Rates" (clicked from the Economy hover dropdown) jumps straight to a
            // focused tax-only view instead of the whole overview — matches how every other
            // topic should eventually behave; Tax is the one built out first since it's the
            // one with the most content (core levers + arbitrary player-created taxes).
            if (UIState.ActiveTopic == "Tax Rates")
            {
                Breadcrumb("Economy", "Tax Rates");
                DrawTaxSection(e);
                return;
            }

            SectionHeader("ECONOMY");
            string real = e.HasRealBaseline ? "" : "  (placeholder baseline)";
            Stat("GDP", $"${e.Gdp:n1}B{real}");
            StatColored("Growth", $"{e.GrowthRate:0.0}%/yr", e.GrowthRate >= 0);
            Stat("Unemployment", $"{e.Unemployment:0.0}%");
            Stat("Inflation", $"{e.Inflation:0.0}%");
            StatColored("Treasury", $"${e.Treasury:n1}B", e.Treasury >= 0);
            Stat("Effective tax rate", $"{e.EffectiveTaxRate():0.0}%");
            Why(e.LastWhy);

            Divider();
            DrawTaxSection(e);
        }

        void DrawTaxSection(EconomyState e)
        {
            SectionHeader("CORE TAXES & RATES");
            AddSlider("Income tax", () => e.TaxIncome, 0f, 60f, v => e.TaxIncome = v);
            AddSlider("Corporate tax", () => e.TaxCorporate, 0f, 60f, v => e.TaxCorporate = v);
            AddSlider("VAT", () => e.TaxVat, 0f, 40f, v => e.TaxVat = v);
            AddSlider("Tariffs", () => e.TaxTariff, 0f, 40f, v => e.TaxTariff = v);
            AddSlider("Interest rate", () => e.InterestRate, 0f, 20f, v => e.InterestRate = v);

            Divider();
            SectionHeader($"CUSTOM TAXES ({e.CustomTaxes.Count})");
            HelpText("Create any tax you want — a plastic bag tax, a sugar tax, a luxury tax, a carbon tax, anything. Drag to set its rate, or remove it entirely.");
            foreach (var tax in e.CustomTaxes)
            {
                var t = tax; // local copy for the closures below
                AddSlider(t.Name, () => t.Rate, 0f, 50f, v => t.Rate = v, onRemove: () => e.CustomTaxes.Remove(t));
            }
            AddNewTaxRow(e);
        }

        // Small "Economy › Tax Rates" trail with a clickable way back to the category overview
        // — topics are a drill-down from the category, not a totally separate place.
        void Breadcrumb(string category, string topic)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 8;

            var back = MakeButton($"← {category}", 11, GameTheme.BgPanel, GameTheme.BgButtonHover, GameTheme.TextDim,
                () => UIState.ActiveTopic = null, align: TextAnchor.MiddleLeft);
            back.style.height = 20; back.style.paddingLeft = 4; back.style.paddingRight = 8;
            row.Add(back);

            var sep = MakeLabel("›", 12, GameTheme.TextDim);
            sep.style.marginLeft = 4; sep.style.marginRight = 4;
            row.Add(sep);

            row.Add(MakeLabel(topic, 12, UIState.ActiveCategory.Accent(), bold: true));
            panelBody.Add(row);
        }

        void AddNewTaxRow(EconomyState e)
        {
            var hint = MakeLabel("New tax name:", 10, GameTheme.TextDim);
            hint.style.marginTop = 10;
            panelBody.Add(hint);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 3;

            var nameField = new TextField();
            nameField.style.flexGrow = 1;
            nameField.style.marginRight = 6;
            nameField.style.backgroundColor = new StyleColor(GameTheme.BgSliderTrack);
            nameField.style.color = new StyleColor(GameTheme.TextPrimary);
            row.Add(nameField);

            var addBtn = MakeButton("+ ADD", 11, GameTheme.BgButtonActive, GameTheme.BgButtonHover, GameTheme.Accent, () =>
            {
                string name = nameField.value?.Trim();
                if (string.IsNullOrEmpty(name)) return;
                e.CustomTaxes.Add(new CustomTax(name, 10f));
                builtForCategory = (NationCategory)(-1); // force the panel to rebuild next tick
            });
            addBtn.style.width = 80; addBtn.style.height = 26;
            row.Add(addBtn);

            panelBody.Add(row);
        }

        void DrawBudget(EconomyState e)
        {
            SectionHeader("BUDGET");
            Stat("Annual revenue", $"${e.AnnualRevenue:n1}B");
            Stat("Annual expenditure", $"${e.AnnualExpenditure:n1}B");
            StatColored("Deficit/Surplus", $"{(e.AnnualDeficit > 0 ? "-" : "+")}${System.Math.Abs(e.AnnualDeficit):n1}B/yr", e.AnnualDeficit <= 0);
            Divider();
            Stat("Public debt", $"${e.PublicDebt:n1}B");
            StatColored("Debt-to-GDP", $"{e.DebtToGdp:0.0}%", e.DebtToGdp <= 60.0);
            Why(e.LastWhy);
            HelpText("Revenue and expenditure follow from the Economy tax/treasury simulation.");
        }

        void DrawTrade(EconomyState e)
        {
            SectionHeader("TRADE");
            Stat("Exports", $"${e.Exports:n1}B");
            Stat("Imports", $"${e.Imports:n1}B");
            StatColored("Trade balance", $"{(e.TradeBalance >= 0 ? "+" : "")}{e.TradeBalance:n1}B", e.TradeBalance >= 0);
            Divider();
            Stat("Tariff rate", $"{e.TaxTariff:0.0}%");
            HelpText("Higher tariffs shrink imports and improve the trade balance — adjust tariffs under Economy.");
        }

        void DrawPolitics(NationalState n)
        {
            SectionHeader("POLITICS");
            if (n == null) { HelpText("No data."); return; }
            StatColored("Approval rating", $"{n.ApprovalRating:0.0}%", n.ApprovalRating >= 50f);
            HelpText("Approval drifts with growth, unemployment, and inflation — govern well and it rises.");
        }

        void DrawMilitary(NationalState n)
        {
            SectionHeader("MILITARY");
            if (n == null) { HelpText("No data."); return; }
            StatColored("Readiness index", $"{n.ReadinessIndex:0.0}", n.ReadinessIndex >= 50f);
            Divider();
            SectionHeader("SPENDING");
            AddSlider("Defense (% GDP)", () => n.DefenseSpending, 0f, 10f, v => n.DefenseSpending = v);
            HelpText("Readiness drifts toward a target set by defense spending.");
        }

        void DrawDiplomacy(NationalState n)
        {
            SectionHeader("DIPLOMACY");
            if (n == null) { HelpText("No data."); return; }
            StatColored("International standing", $"{n.InternationalStanding:0.0}", n.InternationalStanding >= 50f);
            HelpText("Standing is a composite of economic size, trade openness, and approval.");
        }

        void DrawSociety(NationalState n)
        {
            SectionHeader("SOCIETY");
            if (n == null) { HelpText("No data."); return; }
            StatColored("Public mood", $"{n.PublicMood:0.0}", n.PublicMood >= 50f);
            HelpText("Mood tracks daily-life conditions — unemployment and inflation, distinct from government approval.");
        }

        void DrawTechnology(NationalState n)
        {
            SectionHeader("TECHNOLOGY");
            if (n == null) { HelpText("No data."); return; }
            StatColored("Innovation index", $"{n.InnovationIndex:0.0}", n.InnovationIndex >= 50f);
            Divider();
            SectionHeader("SPENDING");
            AddSlider("Research (% GDP)", () => n.ResearchSpending, 0f, 8f, v => n.ResearchSpending = v);
            HelpText("Innovation drifts toward a target from economic scale and research spending.");
        }

        // --- panel content helpers ---

        void SectionHeader(string text)
        {
            var l = MakeLabel(text, 12, UIState.ActiveCategory.Accent(), bold: true);
            l.style.letterSpacing = 1f;
            l.style.marginBottom = 4;
            panelBody.Add(l);
        }

        // Deliberately mismatched sizes for label vs. value — small/dim/uppercase for the label,
        // larger/bright/bold for the value — so the number a player actually needs is always the
        // most prominent thing in the row, per standard HUD/dashboard hierarchy practice.
        void Stat(string label, string value)
        {
            var row = Row();
            row.style.marginBottom = 5;
            row.Add(MakeLabel(label.ToUpperInvariant(), 10, GameTheme.TextDim));
            var spacer = new VisualElement(); spacer.style.flexGrow = 1; row.Add(spacer);
            row.Add(MakeLabel(value, 14, GameTheme.TextPrimary, bold: true));
            panelBody.Add(row);
        }

        void StatColored(string label, string value, bool good)
        {
            var row = Row();
            row.style.marginBottom = 5;
            row.Add(MakeLabel(label.ToUpperInvariant(), 10, GameTheme.TextDim));
            var spacer = new VisualElement(); spacer.style.flexGrow = 1; row.Add(spacer);
            row.Add(MakeLabel(value, 14, good ? GameTheme.Positive : GameTheme.Negative, bold: true));
            panelBody.Add(row);
        }

        void Why(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var l = MakeLabel($"↳ {text}", 11, GameTheme.Accent);
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginTop = 4;
            panelBody.Add(l);
        }

        void HelpText(string text)
        {
            var l = MakeLabel(text, 10, GameTheme.TextDim);
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginTop = 4;
            panelBody.Add(l);
        }

        void Divider()
        {
            var d = new VisualElement();
            d.style.height = 1;
            d.style.marginTop = 8; d.style.marginBottom = 8;
            d.style.backgroundColor = new StyleColor(GameTheme.Border);
            panelBody.Add(d);
        }

        static VisualElement Row()
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            r.style.marginBottom = 2;
            return r;
        }

        void AddSlider(string label, Func<float> get, float lo, float hi, Action<float> set, Action onRemove = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4;

            var lbl = MakeLabel(label, 11, GameTheme.TextDim);
            lbl.style.width = onRemove != null ? 82 : 100;
            lbl.style.overflow = Overflow.Hidden;
            row.Add(lbl);

            var track = new VisualElement();
            track.style.flexGrow = 1;
            track.style.height = 6;
            track.style.marginLeft = 4; track.style.marginRight = 8;
            track.style.backgroundColor = new StyleColor(GameTheme.BgSliderTrack);
            track.style.borderTopLeftRadius = 3; track.style.borderTopRightRadius = 3;
            track.style.borderBottomLeftRadius = 3; track.style.borderBottomRightRadius = 3;

            var thumb = new VisualElement();
            thumb.style.position = Position.Absolute;
            thumb.style.width = 12; thumb.style.height = 12;
            thumb.style.top = -3;
            thumb.style.backgroundColor = new StyleColor(GameTheme.Accent);
            thumb.style.borderTopLeftRadius = 6; thumb.style.borderTopRightRadius = 6;
            thumb.style.borderBottomLeftRadius = 6; thumb.style.borderBottomRightRadius = 6;
            track.Add(thumb);

            var valueLabel = MakeLabel($"{get():0.0}", 11, GameTheme.Accent, bold: true);
            valueLabel.style.width = 42;

            var binding = new SliderBinding { Thumb = thumb, ValueLabel = valueLabel, Get = get, Lo = lo, Hi = hi };
            activeSliders.Add(binding);

            void ApplyFromPointer(Vector2 localPos)
            {
                float w = track.resolvedStyle.width;
                if (w <= 0f) return;
                float t = Mathf.Clamp01(localPos.x / w);
                float v = Mathf.Lerp(lo, hi, t);
                set(v);
                PositionThumb(thumb, t);
                valueLabel.text = $"{v:0.0}";
            }

            track.RegisterCallback<PointerDownEvent>(evt =>
            {
                binding.Dragging = true;
                track.CapturePointer(evt.pointerId);
                ApplyFromPointer(evt.localPosition);
                evt.StopPropagation();
            });
            track.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (binding.Dragging) ApplyFromPointer(evt.localPosition);
            });
            track.RegisterCallback<PointerUpEvent>(evt =>
            {
                binding.Dragging = false;
                track.ReleasePointer(evt.pointerId);
            });

            row.Add(track);
            row.Add(valueLabel);

            if (onRemove != null)
            {
                var removeBtn = MakeButton("✕", 10, GameTheme.BgButton, new Color(0.45f, 0.16f, 0.16f), GameTheme.Negative, () =>
                {
                    onRemove();
                    // No selection/category change happens on removal, so force the panel to
                    // rebuild on the next refresh tick by invalidating the "already built" state.
                    builtForCategory = (NationCategory)(-1);
                });
                removeBtn.style.width = 20; removeBtn.style.height = 18; removeBtn.style.marginLeft = 6;
                row.Add(removeBtn);
            }

            panelBody.Add(row);

            // Position the thumb once the track has a resolved width (first layout pass).
            track.RegisterCallback<GeometryChangedEvent>(_ => PositionThumb(thumb, Mathf.InverseLerp(lo, hi, get())));
        }

        static void PositionThumb(VisualElement thumb, float t)
        {
            thumb.style.left = new Length(Mathf.Clamp01(t) * 100f, LengthUnit.Percent);
            thumb.style.marginLeft = -6; // center the 12px thumb on the percentage point
        }

        // ============================== REFRESH ==============================

        // ============================== TOASTS ==============================

        void BuildToastLayer()
        {
            // Top-LEFT, not top-right — the side panel already owns the right edge (right:10,
            // top:44); anchoring toasts there would silently overlap it.
            toastLayer = new VisualElement();
            toastLayer.pickingMode = PickingMode.Ignore;
            toastLayer.style.position = Position.Absolute;
            toastLayer.style.left = 10;
            toastLayer.style.top = 44;
            toastLayer.style.width = 300;
            root.Add(toastLayer);
        }

        // ============================== START SCREEN ==============================

        void BuildStartScreen()
        {
            startScreen = new VisualElement();
            startScreen.pickingMode = PickingMode.Position;
            startScreen.style.position = Position.Absolute;
            startScreen.style.left = 0; startScreen.style.right = 0; startScreen.style.top = 0; startScreen.style.bottom = 0;
            startScreen.style.backgroundColor = new StyleColor(new Color(0.03f, 0.04f, 0.06f, 0.94f));
            startScreen.style.alignItems = Align.Center;
            startScreen.style.paddingTop = 50; startScreen.style.paddingBottom = 40;
            root.Add(startScreen);

            var title = MakeLabel("MERIDIAN", 40, GameTheme.Accent, bold: true);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            startScreen.Add(title);

            var subtitle = MakeLabel("Choose a nation to govern", 15, GameTheme.TextDim);
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitle.style.marginTop = 4; subtitle.style.marginBottom = 18;
            startScreen.Add(subtitle);

            var listBox = new VisualElement();
            listBox.style.width = 420;
            listBox.style.flexGrow = 1;
            listBox.style.backgroundColor = new StyleColor(GameTheme.BgPanel);
            listBox.style.borderTopLeftRadius = 10; listBox.style.borderTopRightRadius = 10;
            listBox.style.borderBottomLeftRadius = 10; listBox.style.borderBottomRightRadius = 10;
            listBox.style.paddingTop = 8; listBox.style.paddingBottom = 8;
            listBox.style.paddingLeft = 8; listBox.style.paddingRight = 8;
            startScreen.Add(listBox);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            listBox.Add(scroll);
            startScreenList = scroll.contentContainer;
        }

        // Country list depends on GeoJSON having finished loading (a few seconds after boot),
        // so this populates lazily from Refresh() the first time map.World is non-null rather
        // than assuming it's ready in Awake().
        void PopulateStartScreen()
        {
            startScreenPopulated = true;
            startScreenList.Clear();

            var indices = new List<int>();
            for (int i = 0; i < map.World.Countries.Count; i++) indices.Add(i);
            indices.Sort((a, b) => string.Compare(map.World.Countries[a].Name, map.World.Countries[b].Name, StringComparison.Ordinal));

            foreach (int idx in indices)
            {
                var country = map.World.Countries[idx];
                int capturedIdx = idx;
                var row = MakeButton(country.Name, 13, GameTheme.BgPanel, GameTheme.BgButtonHover, GameTheme.TextPrimary,
                    () => BeginGame(capturedIdx, country.Name), align: TextAnchor.MiddleLeft);
                row.style.height = 30;
                row.style.marginBottom = 2;
                startScreenList.Add(row);
            }
        }

        void BeginGame(int countryIndex, string countryName)
        {
            PlayerState.Begin(countryIndex, countryName, interaction.SimDay);
            interaction.SelectCountry(countryIndex);
            UIState.ActiveCategory = NationCategory.Economy;
            startScreen.style.display = DisplayStyle.None;
        }

        // ============================== GAME OVER SCREEN ==============================

        void BuildGameOverScreen()
        {
            gameOverScreen = new VisualElement();
            gameOverScreen.pickingMode = PickingMode.Position;
            gameOverScreen.style.position = Position.Absolute;
            gameOverScreen.style.left = 0; gameOverScreen.style.right = 0; gameOverScreen.style.top = 0; gameOverScreen.style.bottom = 0;
            gameOverScreen.style.backgroundColor = new StyleColor(new Color(0.05f, 0.02f, 0.02f, 0.94f));
            gameOverScreen.style.alignItems = Align.Center;
            gameOverScreen.style.justifyContent = Justify.Center;
            gameOverScreen.style.display = DisplayStyle.None;
            root.Add(gameOverScreen);

            var box = new VisualElement();
            box.style.width = 440;
            box.style.backgroundColor = new StyleColor(GameTheme.BgPanel);
            box.style.borderTopLeftRadius = 12; box.style.borderTopRightRadius = 12;
            box.style.borderBottomLeftRadius = 12; box.style.borderBottomRightRadius = 12;
            box.style.borderLeftWidth = 3; box.style.borderLeftColor = new StyleColor(GameTheme.Negative);
            box.style.paddingTop = 24; box.style.paddingBottom = 24; box.style.paddingLeft = 28; box.style.paddingRight = 28;
            gameOverScreen.Add(box);

            var title = MakeLabel("GAME OVER", 26, GameTheme.Negative, bold: true);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = 10;
            box.Add(title);

            gameOverMessage = MakeLabel("", 13, GameTheme.TextPrimary);
            gameOverMessage.style.whiteSpace = WhiteSpace.Normal;
            gameOverMessage.style.unityTextAlign = TextAnchor.MiddleCenter;
            gameOverMessage.style.marginBottom = 8;
            box.Add(gameOverMessage);

            gameOverStats = MakeLabel("", 11, GameTheme.TextDim);
            gameOverStats.style.whiteSpace = WhiteSpace.Normal;
            gameOverStats.style.unityTextAlign = TextAnchor.MiddleCenter;
            gameOverStats.style.marginBottom = 18;
            box.Add(gameOverStats);

            var again = MakeButton("PLAY AGAIN", 14, GameTheme.BgButtonActive, GameTheme.BgButtonHover, GameTheme.Accent, () =>
            {
                PlayerState.Reset();
                gameOverScreen.style.display = DisplayStyle.None;
                startScreen.style.display = DisplayStyle.Flex;
            });
            again.style.height = 34;
            box.Add(again);
        }

        // Diffs every country's LastWhy against what we last saw and pops a toast for anything
        // new — turns AI policy changes and threshold-crossing events (already computed by the
        // sim, previously only visible if you happened to have that exact country's panel open)
        // into a live event feed, the same way a real strategy game surfaces what's happening.
        void CheckForNewEvents()
        {
            if (map?.Economy == null || map.World == null) return;
            int n = map.Economy.States.Count;
            if (lastWhySeen == null || lastWhySeen.Length != n) lastWhySeen = new string[n];

            for (int i = 0; i < n; i++)
            {
                string why = map.Economy.States[i].LastWhy;
                if (!string.IsNullOrEmpty(why) && why != lastWhySeen[i])
                    ShowToast(map.World.Countries[i].Name, why);
                lastWhySeen[i] = why;
            }
        }

        void ShowToast(string country, string message)
        {
            if (activeToasts.Count >= 4)
            {
                var oldest = activeToasts[0];
                activeToasts.RemoveAt(0);
                toastLayer.Remove(oldest);
            }

            var box = new VisualElement();
            box.style.backgroundColor = new StyleColor(GameTheme.BgDropdown);
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = new StyleColor(GameTheme.Accent);
            box.style.borderTopLeftRadius = 6; box.style.borderTopRightRadius = 6;
            box.style.borderBottomLeftRadius = 6; box.style.borderBottomRightRadius = 6;
            box.style.paddingLeft = 10; box.style.paddingRight = 10; box.style.paddingTop = 6; box.style.paddingBottom = 6;
            box.style.marginBottom = 6;
            box.style.opacity = 0f;
            box.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity") };
            box.style.transitionDuration = new List<TimeValue> { new TimeValue(300, TimeUnit.Millisecond) };

            var nameLabel = MakeLabel(country, 11, GameTheme.Accent, bold: true);
            var msgLabel = MakeLabel(message, 11, GameTheme.TextPrimary);
            msgLabel.style.whiteSpace = WhiteSpace.Normal;
            box.Add(nameLabel);
            box.Add(msgLabel);

            toastLayer.Add(box);
            activeToasts.Add(box);

            box.schedule.Execute(() => box.style.opacity = 1f).ExecuteLater(20);
            box.schedule.Execute(() =>
            {
                box.style.opacity = 0f;
                box.schedule.Execute(() =>
                {
                    activeToasts.Remove(box);
                    toastLayer.Remove(box);
                }).ExecuteLater(320);
            }).ExecuteLater(4500);
        }

        void Refresh()
        {
            if (map == null || interaction == null) return;

            // Start screen: populate the country list lazily once the world has finished
            // loading (a few seconds after boot), and keep it visible until a nation is chosen.
            if (!startScreenPopulated && map.World != null)
                PopulateStartScreen();
            startScreen.style.display = PlayerState.State == GameState.NotStarted ? DisplayStyle.Flex : DisplayStyle.None;

            // Game-over screen: sync its text every tick while shown (cheap, and avoids a
            // separate "did we just transition" tracking flag).
            bool gameOver = PlayerState.State == GameState.GameOver;
            gameOverScreen.style.display = gameOver ? DisplayStyle.Flex : DisplayStyle.None;
            if (gameOver)
            {
                gameOverMessage.text = PlayerState.LastResultMessage;
                gameOverStats.text = $"You governed {PlayerState.CountryName} for {PlayerState.TermsServed} full term(s) before losing office.";
            }

            // The normal HUD only makes sense once a game is actually in progress.
            bool playing = PlayerState.State == GameState.Playing;
            var hudDisplay = playing ? DisplayStyle.Flex : DisplayStyle.None;
            topBarRoot.style.display = hudDisplay;
            ministryBarRoot.style.display = hudDisplay;
            toastLayer.style.display = hudDisplay;
            if (!playing)
            {
                dropdownLayer.Clear();
                if (sidePanel != null) sidePanel.style.display = DisplayStyle.None;
                if (sidePanelShadow != null) sidePanelShadow.style.display = DisplayStyle.None;
                return;
            }

            CheckForNewEvents();

            dayLabel.text = $"Day {interaction.SimDay}";

            if (PlayerState.CountryIndex >= 0)
            {
                long daysLeft = PlayerState.TermStartDay + PlayerState.TermLengthDays - interaction.SimDay;
                float approval = map.National != null && PlayerState.CountryIndex < map.National.States.Count
                    ? map.National.States[PlayerState.CountryIndex].ApprovalRating : 50f;
                playerBadge.style.display = DisplayStyle.Flex;
                playerBadge.text = $"YOU: {PlayerState.CountryName}  ·  Approval {approval:0}%  ·  Election in {Math.Max(0, daysLeft)}d";
                playerBadge.style.color = approval >= 50f ? GameTheme.Positive : (approval < 35f ? GameTheme.Negative : GameTheme.Accent);
            }
            foreach (var kv in speedButtons)
            {
                bool active = Mathf.Approximately(interaction.daysPerSecond, kv.Key);
                kv.Value.style.backgroundColor = new StyleColor(active ? GameTheme.BgButtonActive : GameTheme.BgButton);
                (kv.Value.userData as Label).style.color = active ? GameTheme.Accent : GameTheme.TextDim;
            }

            foreach (var kv in mapModeButtons)
            {
                bool active = map.CurrentMode == kv.Key;
                kv.Value.style.backgroundColor = new StyleColor(active ? GameTheme.BgButtonActive : GameTheme.BgButton);
                (kv.Value.userData as Label).style.color = active ? GameTheme.Accent : GameTheme.TextDim;
            }

            for (int i = 0; i < ministryButtons.Length; i++)
            {
                bool active = UIState.ActiveCategory == Categories[i];
                ministryButtons[i].style.backgroundColor = new StyleColor(active ? GameTheme.BgButtonActive : GameTheme.BgButton);
                (ministryButtons[i].userData as Label).style.color = active ? Categories[i].Accent() : GameTheme.TextDim;
                ministryButtons[i].Q("underline").style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            }

            int sel = interaction.Selected;
            if (map.World != null && sel >= 0 && sel < map.World.Countries.Count)
            {
                var c = map.World.Countries[sel];
                var e = map.Economy != null && sel < map.Economy.States.Count ? map.Economy.States[sel] : null;
                topCountryLabel.text = c.Name;
                if (e != null)
                {
                    topStatsRow.style.display = DisplayStyle.Flex;
                    SetStat(0, $"{e.GrowthRate:0.0}%/yr", e.GrowthRate >= 0);
                    SetStat(1, $"{e.Unemployment:0.0}%", e.Unemployment <= 8f);
                    SetStat(2, $"{e.Inflation:0.0}%", e.Inflation <= 5f);
                    SetStat(3, $"${e.Treasury:0.0}B", e.Treasury >= 0);
                }
            }
            else
            {
                topCountryLabel.text = "Click a country to inspect it";
                topStatsRow.style.display = DisplayStyle.None;
            }

            if (sel != builtForSelected || UIState.ActiveCategory != builtForCategory || UIState.ActiveTopic != builtForTopic)
            {
                builtForSelected = sel;
                builtForCategory = UIState.ActiveCategory;
                builtForTopic = UIState.ActiveTopic;
                RebuildSidePanel();
            }
            else
            {
                foreach (var s in activeSliders)
                {
                    if (s.Dragging) continue;
                    float v = s.Get();
                    PositionThumb(s.Thumb, Mathf.InverseLerp(s.Lo, s.Hi, v));
                    s.ValueLabel.text = $"{v:0.0}";
                }
            }
        }

        void SetStat(int i, string text, bool good)
        {
            topStatLabels[i].text = text;
            topStatLabels[i].style.color = good ? GameTheme.Positive : GameTheme.Negative;
        }

        // ============================== ELEMENT FACTORIES ==============================

        static Font builtinFont;

        // UI Toolkit's text shaper needs a resolved font — without a PanelSettings.themeStyleSheet
        // (which requires the Editor's UI Builder to author), every Label's font stays null and
        // text shaping throws a NullReferenceException at runtime. Every label gets Unity's
        // built-in font explicitly so this never depends on a theme asset existing.
        static bool fontLogged;

        static Font BuiltinFont()
        {
            if (builtinFont == null)
            {
                builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                              ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (!fontLogged)
                {
                    fontLogged = true;
                    Debug.Log($"[ui] builtin font resolved: {(builtinFont != null ? builtinFont.name : "NULL")}");
                }
            }
            return builtinFont;
        }

        static Label MakeLabel(string text, int fontSize, Color color, bool bold = false)
        {
            var l = new Label(text);
            l.style.fontSize = fontSize;
            l.style.color = color;
            var font = BuiltinFont();
            if (font != null) l.style.unityFontDefinition = FontDefinition.FromFont(font);
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.unityTextAlign = TextAnchor.MiddleLeft;
            return l;
        }

        static VisualElement MakeButton(string text, int fontSize, Color bg, Color hoverBg, Color textColor, Action onClick, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var btn = new VisualElement();
            btn.style.backgroundColor = new StyleColor(bg);
            btn.style.borderTopLeftRadius = 6; btn.style.borderTopRightRadius = 6;
            btn.style.borderBottomLeftRadius = 6; btn.style.borderBottomRightRadius = 6;
            btn.style.justifyContent = align == TextAnchor.MiddleLeft ? Justify.FlexStart : Justify.Center;
            btn.style.alignItems = Align.Center;
            btn.style.flexDirection = FlexDirection.Row;
            if (align == TextAnchor.MiddleLeft) btn.style.paddingLeft = 6;
            // Smooth color fade on hover/active instead of an instant snap — cheap way to make
            // the interface feel responsive rather than static.
            btn.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("background-color") };
            btn.style.transitionDuration = new List<TimeValue> { new TimeValue(120, TimeUnit.Millisecond) };

            var label = MakeLabel(text, fontSize, textColor);
            label.style.unityTextAlign = align;
            label.pickingMode = PickingMode.Ignore;
            btn.Add(label);
            btn.userData = label; // Refresh() reads this back to recolor text on active/hover state

            btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.backgroundColor = new StyleColor(hoverBg));
            btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.backgroundColor = new StyleColor(bg));
            btn.RegisterCallback<ClickEvent>(_ => onClick());
            return btn;
        }
    }
}
