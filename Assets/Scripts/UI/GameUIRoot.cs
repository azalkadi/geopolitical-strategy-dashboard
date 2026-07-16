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
        TextField startScreenSearch;
        VisualElement continueBtn;
        bool? hasValidSave; // checked once per session, not per keystroke (the save is megabytes)
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
        bool builtForPanelOpen = false;
        int builtForWarStamp = 0;
        readonly List<SliderBinding> activeSliders = new();

        // --- event toasts (AI policy changes / threshold-crossing events, surfaced live) ---
        VisualElement toastLayer;
        readonly List<VisualElement> activeToasts = new();
        string[] lastWhySeen;

        // --- decision-event modal (sim pauses while visible) ---
        VisualElement eventModal;
        GameEvent builtForEvent;

        class SliderBinding
        {
            public VisualElement Thumb;
            public Label ValueLabel;
            public Func<float> Get;
            public float Lo, Hi;
            public bool Dragging;
        }

        // A stat row whose value keeps updating while the panel stays open (war scores,
        // exhaustion — anything that changes every sim day). Plain Stat()/StatColored() rows
        // are snapshots taken at panel-build time; these re-read on every refresh tick.
        class LiveStatBinding
        {
            public Label ValueLabel;
            public Func<string> Get;
            public Func<bool> Good; // null = neutral color
        }
        readonly List<LiveStatBinding> activeLiveStats = new();

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
            BuildEventModal();

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
            title.style.letterSpacing = 2f;
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

            var saveBtn = MakeButton("SAVE", 11, GameTheme.BgButton, GameTheme.BgButtonHover, GameTheme.TextDim, () =>
            {
                bool ok = interaction.SaveNow();
                ShowToast("System", ok ? "Game saved." : "Save failed — see Player.log.");
                hasValidSave = null; // start screen re-checks next time it's shown
            });
            saveBtn.style.width = 48; saveBtn.style.height = 22; saveBtn.style.marginRight = 4;
            bar.Add(saveBtn);

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
            topStatsRow.style.alignItems = Align.Center;
            topStatsRow.style.display = DisplayStyle.None;
            bar.Add(topStatsRow);

            // Single-line "LABEL value" pairs, not stacked two-line tiles — every element in this
            // bar (day counter, badges, these stats) is then one text line tall, so Align.Center
            // on the row puts them all on the same visual baseline instead of the stat tiles
            // drifting off-center against their taller stacked neighbors.
            string[] statNames = { "GROWTH", "UNEMP.", "INFLATION", "TREASURY" };
            topStatLabels = new Label[statNames.Length];
            for (int i = 0; i < statNames.Length; i++)
            {
                var tile = new VisualElement();
                tile.style.flexDirection = FlexDirection.Row;
                tile.style.alignItems = Align.Center;
                tile.style.marginLeft = 14;
                var head = MakeLabel(statNames[i] + ":", 10, GameTheme.TextDim);
                head.style.marginRight = 4;
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
            // No panel background — just the row of nameplate buttons floating directly over the
            // map/satellite view, each button carrying its own solid color. The bar itself is
            // Ignore-picking and paints nothing, so the map underneath (including the gaps
            // between buttons) stays fully visible and clickable.
            var bar = new VisualElement();
            bar.pickingMode = PickingMode.Ignore;
            bar.style.position = Position.Absolute;
            bar.style.left = 0; bar.style.right = 0; bar.style.bottom = 0; bar.style.height = 42;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.justifyContent = Justify.Center;
            bar.style.alignItems = Align.Center;
            root.Add(bar);
            ministryBarRoot = bar;

            ministryButtons = new VisualElement[Categories.Length];
            dropdownLayer = new VisualElement();
            // Ignore, not Position: this layer spans the whole screen so its popup dropdowns can
            // render anywhere, but as an empty container it must never itself be a hit target —
            // it sat on top of the top bar and ministry bar in z-order and was silently swallowing
            // every click meant for the buttons beneath it. Ignore lets those clicks pass through;
            // the popup `dd` VisualElement added as its child still keeps its own Position picking.
            dropdownLayer.pickingMode = PickingMode.Ignore;
            dropdownLayer.style.position = Position.Absolute;
            dropdownLayer.style.left = 0; dropdownLayer.style.right = 0; dropdownLayer.style.top = 0; dropdownLayer.style.bottom = 0;
            root.Add(dropdownLayer); // added after bar so dropdowns render on top

            for (int i = 0; i < Categories.Length; i++)
            {
                var cat = Categories[i];
                var catColor = cat.Accent();
                var btn = MakeButton(cat.Label(), 13, GameTheme.Muted(catColor), GameTheme.Muted(catColor, 0.4f), GameTheme.TextPrimary,
                    () => { UIState.ActiveCategory = cat; UIState.ActiveTopic = null; UIState.PanelOpen = true; });
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
            dd.style.borderTopLeftRadius = 6; dd.style.borderTopRightRadius = 6;
            dd.style.borderBottomLeftRadius = 6; dd.style.borderBottomRightRadius = 6;
            dd.style.paddingTop = 6; dd.style.paddingBottom = 6; dd.style.paddingLeft = 4; dd.style.paddingRight = 4;
            dd.RegisterCallback<PointerEnterEvent>(_ => dropdownPinned = true);
            dd.RegisterCallback<PointerLeaveEvent>(_ => HideDropdownSoon());

            foreach (var topic in topics)
            {
                var capturedTopic = topic;
                var row = MakeButton(topic, 12, GameTheme.BgDropdown, GameTheme.BgButtonHover, GameTheme.TextPrimary,
                    () => { UIState.ActiveCategory = cat; UIState.ActiveTopic = capturedTopic; UIState.PanelOpen = true; }, align: TextAnchor.MiddleLeft);
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
            shadow.style.borderTopLeftRadius = 6; shadow.style.borderTopRightRadius = 6;
            shadow.style.borderBottomLeftRadius = 6; shadow.style.borderBottomRightRadius = 6;
            root.Add(shadow);
            sidePanelShadow = shadow;

            sidePanel = new VisualElement();
            sidePanel.style.position = Position.Absolute;
            sidePanel.style.right = 10; sidePanel.style.top = 44; sidePanel.style.bottom = 48;
            sidePanel.style.width = 330;
            sidePanel.style.backgroundColor = new StyleColor(GameTheme.BgPanel);
            sidePanel.style.borderTopLeftRadius = 6; sidePanel.style.borderTopRightRadius = 6;
            sidePanel.style.borderBottomLeftRadius = 6; sidePanel.style.borderBottomRightRadius = 6;
            sidePanel.style.borderLeftWidth = 3;
            sidePanel.style.borderLeftColor = new StyleColor(GameTheme.Accent);
            sidePanel.style.borderTopWidth = 1; sidePanel.style.borderRightWidth = 1; sidePanel.style.borderBottomWidth = 1;
            sidePanel.style.borderTopColor = new StyleColor(GameTheme.Border);
            sidePanel.style.borderRightColor = new StyleColor(GameTheme.Border);
            sidePanel.style.borderBottomColor = new StyleColor(GameTheme.Border);
            sidePanel.style.paddingLeft = 14; sidePanel.style.paddingRight = 14;
            sidePanel.style.paddingTop = 12; sidePanel.style.paddingBottom = 12;
            sidePanel.style.display = DisplayStyle.None;
            sidePanel.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity") };
            sidePanel.style.transitionDuration = new List<TimeValue> { new TimeValue(140, TimeUnit.Millisecond) };
            root.Add(sidePanel);

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            panelTitle = MakeLabel("", 16, GameTheme.TextPrimary, bold: true);
            headerRow.Add(panelTitle);
            var titleSpacer = new VisualElement();
            titleSpacer.style.flexGrow = 1;
            headerRow.Add(titleSpacer);
            // Explicit close control — the panel only opens when a ministry is chosen, and this
            // is how you dismiss it again without having to hunt for empty map to click.
            var closeBtn = MakeButton("✕", 12, GameTheme.BgButton, GameTheme.BgButtonHover, GameTheme.TextDim, () => UIState.PanelOpen = false);
            closeBtn.style.width = 22; closeBtn.style.height = 22;
            headerRow.Add(closeBtn);
            sidePanel.Add(headerRow);

            panelSubtitle = MakeLabel("", 11, GameTheme.TextDim);
            panelSubtitle.style.marginBottom = 8;
            sidePanel.Add(panelSubtitle);

            panelBody = new VisualElement();
            sidePanel.Add(panelBody);
        }

        void RebuildSidePanel()
        {
            activeSliders.Clear();
            activeSparklines.Clear();
            activeLiveStats.Clear();
            panelBody.Clear();

            int sel = interaction.Selected;
            if (map.World == null || sel < 0 || sel >= map.World.Countries.Count || !UIState.PanelOpen)
            {
                sidePanel.style.display = DisplayStyle.None;
                sidePanelShadow.style.display = DisplayStyle.None;
                return;
            }
            sidePanel.style.display = DisplayStyle.Flex;
            sidePanelShadow.style.display = DisplayStyle.Flex;
            // NOTE: this used to pop-in via a `scale` transform on sidePanel itself. Runtime UI
            // Toolkit panel picking does not reliably re-project pointer coordinates through an
            // ancestor's transform, so a scaled *container* left every nested button/slider/field
            // hit-testing against a stale, unscaled rect — the whole panel silently stopped
            // accepting input the first time this ran. Opacity is transform-free and safe here.
            sidePanel.style.opacity = 0.4f;
            sidePanel.schedule.Execute(() => sidePanel.style.opacity = 1f).ExecuteLater(10);
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

            // History charts exist only for the nation the player actually governs — the sim
            // doesn't record daily series for all 258 countries.
            if (interaction.Selected == PlayerState.CountryIndex && PlayerHistory.Gdp.Count >= 2)
            {
                AddSparkline("GDP ($B)", PlayerHistory.Gdp, GameTheme.Accent);
                AddSparkline("Growth (%/yr)", PlayerHistory.Growth, GameTheme.Positive);
                AddSparkline("Inflation (%)", PlayerHistory.Inflation, GameTheme.Negative);
            }

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

            Divider();
            SectionHeader("SPENDING (% OF GDP)");
            AddSlider("Education", () => e.SpendEducation, 0f, 12f, v => e.SpendEducation = v);
            AddSlider("Healthcare", () => e.SpendHealthcare, 0f, 15f, v => e.SpendHealthcare = v);
            AddSlider("Infrastructure", () => e.SpendInfrastructure, 0f, 12f, v => e.SpendInfrastructure = v);
            Stat("Other government", $"{EconomyState.SpendBase:0.0}%");
            HelpText("Infrastructure and education feed growth, education also drives innovation, healthcare lifts public mood — all of it costs treasury every day. 'Other' (administration, pensions, debt service) is fixed.");

            if (interaction.Selected == PlayerState.CountryIndex && PlayerHistory.Treasury.Count >= 2)
            {
                Divider();
                AddSparkline("Treasury ($B)", PlayerHistory.Treasury, GameTheme.Accent);
            }
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

            if (interaction.Selected == PlayerState.CountryIndex && PlayerHistory.Approval.Count >= 2)
            {
                Divider();
                SectionHeader("YOUR RECORD");
                AddSparkline("Approval (%)", PlayerHistory.Approval, GameTheme.Accent);
                AddSparkline("Unemployment (%)", PlayerHistory.Unemployment, GameTheme.Negative);
                HelpText("The whole term at a glance — this chart is what the election is really about.");
            }
        }

        void DrawMilitary(NationalState n)
        {
            SectionHeader("MILITARY");
            if (n == null) { HelpText("No data."); return; }
            StatColored("Readiness index", $"{n.ReadinessIndex:0.0}", n.ReadinessIndex >= 50f);

            int me = PlayerState.CountryIndex;
            int sel = interaction.Selected;
            var e = map.Economy != null && sel >= 0 && sel < map.Economy.States.Count ? map.Economy.States[sel] : null;
            if (e != null) Stat("Military strength", $"{WarSystem.Strength(e, n):0.0}");

            if (sel == me)
            {
                Divider();
                SectionHeader("SPENDING");
                AddSlider("Defense (% GDP)", () => n.DefenseSpending, 0f, 10f, v => n.DefenseSpending = v);
                HelpText("Readiness drifts toward a target set by defense spending. Strength = economy × defense share × readiness.");
                DrawOwnWars(me);
            }
            else if (me >= 0 && map.Wars != null)
            {
                DrawForeignMilitary(me, sel);
            }
        }

        // Your own Military tab: every war you're in, its momentum, and the ways out.
        void DrawOwnWars(int me)
        {
            var wars = map.Wars?.WarsOf(me);
            if (wars == null || wars.Count == 0) return;

            Divider();
            SectionHeader("ACTIVE WARS");
            foreach (var w in wars)
            {
                var war = w; // captured by the buttons below
                bool iAmAttacker = war.Attacker == me;
                int enemy = iAmAttacker ? war.Defender : war.Attacker;
                float myScore = iAmAttacker ? war.Score : -war.Score;
                float myExhaustion = iAmAttacker ? war.ExhaustionAttacker : war.ExhaustionDefender;

                Stat("At war with", map.World.Countries[enemy].Name);
                LiveStat("War score",
                    () => { float s = iAmAttacker ? war.Score : -war.Score; return $"{s:+0.0;-0.0;0.0}"; },
                    () => (iAmAttacker ? war.Score : -war.Score) >= 0f);
                LiveStat("War exhaustion",
                    () => $"{(iAmAttacker ? war.ExhaustionAttacker : war.ExhaustionDefender):0.0}%",
                    () => (iAmAttacker ? war.ExhaustionAttacker : war.ExhaustionDefender) < 50f);

                if (map.Wars.PlayerCanDemandConcessions(war))
                {
                    var demandBtn = MakeButton("Demand Concessions (they capitulate)", 12, GameTheme.BgButtonActive, GameTheme.BgButtonHover, GameTheme.TextPrimary, () =>
                    {
                        string result = map.Wars.PlayerDemandConcessions(war, map.Economy, map.National, map.Diplomacy, map.CountryNames);
                        if (result != null) ShowToast(PlayerState.CountryName, result);
                        builtForCategory = (NationCategory)(-1);
                    }, align: TextAnchor.MiddleLeft);
                    demandBtn.style.height = 30; demandBtn.style.marginTop = 4;
                    panelBody.Add(demandBtn);
                }

                var peaceBtn = MakeButton("Offer White Peace", 12, GameTheme.BgButton, GameTheme.BgButtonHover, GameTheme.TextPrimary, () =>
                {
                    string result = map.Wars.PlayerOfferWhitePeace(war, map.Diplomacy, map.CountryNames, interaction.SimDay);
                    ShowToast(PlayerState.CountryName, result ?? "Your envoys were already rebuffed recently — wait before offering again.");
                    builtForCategory = (NationCategory)(-1);
                }, align: TextAnchor.MiddleLeft);
                peaceBtn.style.height = 30; peaceBtn.style.marginTop = 4;
                panelBody.Add(peaceBtn);
                Divider();
            }
            HelpText("War score above +40 lets you demand reparations. Exhaustion grinds down approval and mood the longer a war runs — wars must END.");
        }

        // A foreign country's Military tab: the war option, or the state of your war with them.
        void DrawForeignMilitary(int me, int sel)
        {
            Divider();
            var existing = map.Wars.WarBetween(me, sel);
            if (existing != null)
            {
                SectionHeader("AT WAR");
                bool iAmAttacker = existing.Attacker == me;
                LiveStat("War score",
                    () => { float s = iAmAttacker ? existing.Score : -existing.Score; return $"{s:+0.0;-0.0;0.0}"; },
                    () => (iAmAttacker ? existing.Score : -existing.Score) >= 0f);
                HelpText("Manage this war from your own country's Military ministry.");
                return;
            }

            SectionHeader("WAR");
            if (map.Wars.CanDeclare(me, sel, map.Diplomacy))
            {
                var declareBtn = MakeButton("⚔ Declare War", 13, GameTheme.Muted(GameTheme.Negative, 0.35f), GameTheme.Negative, GameTheme.TextPrimary, () =>
                {
                    map.Wars.Declare(me, sel, interaction.SimDay, map.Diplomacy, map.National);
                    ShowToast(PlayerState.CountryName, $"War declared on {map.World.Countries[sel].Name}. The world is watching.");
                    builtForCategory = (NationCategory)(-1);
                }, align: TextAnchor.MiddleLeft);
                declareBtn.style.height = 32; declareBtn.style.marginTop = 4;
                panelBody.Add(declareBtn);
                HelpText("Declaring war floors relations, costs international standing, and drags both economies daily until peace. Compare military strength first.");
            }
            else
            {
                float rel = map.Diplomacy.GetRelation(me, sel);
                HelpText(rel >= WarSystem.DeclareRelationCeiling
                    ? $"Relations are too warm to justify war (must be below {WarSystem.DeclareRelationCeiling:0}; currently {rel:0})."
                    : "War is not possible right now (already at war, or a trade agreement binds you).");
            }
        }

        void DrawDiplomacy(NationalState n)
        {
            SectionHeader("DIPLOMACY");
            if (n == null) { HelpText("No data."); return; }
            StatColored("International standing", $"{n.InternationalStanding:0.0}", n.InternationalStanding >= 50f);

            var dip = map.Diplomacy;
            int me = PlayerState.CountryIndex;
            int sel = interaction.Selected;
            if (dip == null || me < 0) { HelpText("Standing is a composite of economic size, trade openness, and approval."); return; }

            if (sel >= 0 && sel != me && sel < dip.Count)
                DrawBilateralDiplomacy(dip, me, sel);
            else
                DrawDiplomacyOverview(dip, me);
        }

        // Viewing a FOREIGN country's Diplomacy tab = your bilateral relationship with it,
        // and the levers you can pull. This is where diplomacy is actually played.
        void DrawBilateralDiplomacy(DiplomacySystem dip, int me, int sel)
        {
            Divider();
            SectionHeader($"RELATIONS WITH {map.World.Countries[sel].Name.ToUpperInvariant()}");

            float rel = dip.GetRelation(me, sel);
            string mood = rel >= 75f ? "Ally" : rel >= 60f ? "Friendly" : rel >= 40f ? "Neutral" : rel >= 25f ? "Strained" : "Hostile";
            StatColored("Relationship", $"{rel:0} · {mood}", rel >= 50f);
            if (dip.HasAgreement(me, sel)) Stat("Trade agreement", "IN FORCE");

            bool onCooldown = !dip.CanAct(me, sel, interaction.SimDay);
            if (onCooldown)
            {
                HelpText("Diplomatic channels need time to reset after your last move here (90 days between actions per country).");
                return;
            }

            var myEcon = map.Economy.States[me];
            var myNat = map.National.States[me];
            var theirEcon = map.Economy.States[sel];

            var aidBtn = MakeButton($"Send Foreign Aid  (${System.Math.Max(0.2, myEcon.Gdp * 0.0005):0.0}B)", 12,
                GameTheme.BgButton, GameTheme.BgButtonHover, GameTheme.TextPrimary, () =>
                {
                    ShowToast(PlayerState.CountryName, dip.SendAid(me, sel, myEcon, myNat, interaction.SimDay));
                    builtForCategory = (NationCategory)(-1);
                }, align: TextAnchor.MiddleLeft);
            aidBtn.style.height = 30; aidBtn.style.marginTop = 6;
            panelBody.Add(aidBtn);

            if (!dip.HasAgreement(me, sel))
            {
                bool eligible = rel >= DiplomacySystem.AgreementThreshold;
                var tradeBtn = MakeButton(eligible ? "Sign Trade Agreement" : $"Trade Agreement (needs {DiplomacySystem.AgreementThreshold:0}+ relations)", 12,
                    eligible ? GameTheme.BgButton : GameTheme.Muted(GameTheme.BgButton), GameTheme.BgButtonHover,
                    eligible ? GameTheme.TextPrimary : GameTheme.TextDim, () =>
                    {
                        string result = dip.SignAgreement(me, sel, myEcon, theirEcon, interaction.SimDay);
                        if (result != null) { ShowToast(PlayerState.CountryName, result); builtForCategory = (NationCategory)(-1); }
                    }, align: TextAnchor.MiddleLeft);
                tradeBtn.style.height = 30; tradeBtn.style.marginTop = 4;
                panelBody.Add(tradeBtn);
            }

            var denounceBtn = MakeButton("Denounce Publicly", 12, GameTheme.Muted(GameTheme.Negative, 0.4f), GameTheme.Negative, GameTheme.TextPrimary, () =>
                {
                    ShowToast(PlayerState.CountryName, dip.Denounce(me, sel, myNat, interaction.SimDay));
                    builtForCategory = (NationCategory)(-1);
                }, align: TextAnchor.MiddleLeft);
            denounceBtn.style.height = 30; denounceBtn.style.marginTop = 4;
            panelBody.Add(denounceBtn);

            HelpText("Aid buys warmth. Agreements need 65+ relations and lift both economies' exports permanently. Denouncing plays well at home and badly abroad.");
        }

        // Viewing your OWN Diplomacy tab = the world's view of you: warmest and frostiest
        // relationships at a glance. Click a country on the map to open its bilateral view.
        void DrawDiplomacyOverview(DiplomacySystem dip, int me)
        {
            Divider();
            SectionHeader("CLOSEST PARTNERS");
            foreach (var (idx, rel) in dip.RankedFor(me, friendliest: true, topN: 5))
                Stat(map.World.Countries[idx].Name, dip.HasAgreement(me, idx) ? $"{rel:0} ◆" : $"{rel:0}");

            Divider();
            SectionHeader("MOST STRAINED");
            foreach (var (idx, rel) in dip.RankedFor(me, friendliest: false, topN: 5))
                StatColored(map.World.Countries[idx].Name, $"{rel:0}", false);

            HelpText("◆ = trade agreement in force. Select another country on the map, then open Diplomacy, to send aid, sign agreements, or denounce.");
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

        // A small line chart over a HistorySeries, drawn with UI Toolkit's vector API
        // (generateVisualContent/painter2D) — no textures, no chart library. Repaints on the
        // same 100ms Refresh cadence as the rest of the panel (MarkDirtyRepaint from Refresh).
        class Sparkline : VisualElement
        {
            public HistorySeries Series;
            public Color LineColor = Color.white;

            public Sparkline()
            {
                generateVisualContent += OnGenerate;
                pickingMode = PickingMode.Ignore;
            }

            void OnGenerate(MeshGenerationContext mgc)
            {
                if (Series == null || Series.Count < 2) return;
                float w = resolvedStyle.width, h = resolvedStyle.height;
                if (w <= 4f || h <= 4f) return;

                var (mn, mx) = Series.Range();
                float span = mx - mn;
                if (span < 1e-6f) span = 1e-6f;
                // Small vertical margin so a flat-ish line doesn't hug the border.
                const float pad = 2f;

                var painter = mgc.painter2D;
                painter.strokeColor = LineColor;
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                int n = Series.Count;
                for (int i = 0; i < n; i++)
                {
                    float x = (float)i / (n - 1) * w;
                    float t = (Series[i] - mn) / span;
                    float y = pad + (1f - t) * (h - pad * 2f); // y-down: max value at top
                    if (i == 0) painter.MoveTo(new Vector2(x, y));
                    else painter.LineTo(new Vector2(x, y));
                }
                painter.Stroke();
            }
        }

        readonly List<Sparkline> activeSparklines = new();

        // Chart row: dim uppercase label + min/max annotations, chart below it.
        void AddSparkline(string label, HistorySeries series, Color color)
        {
            var head = Row();
            head.style.marginTop = 6;
            head.Add(MakeLabel(label.ToUpperInvariant(), 10, GameTheme.TextDim));
            var spacer = new VisualElement(); spacer.style.flexGrow = 1; head.Add(spacer);
            var (mn, mx) = series.Range();
            head.Add(MakeLabel(series.Count >= 2 ? $"{mn:0.#} – {mx:0.#}" : "gathering data…", 9, GameTheme.TextDim));
            panelBody.Add(head);

            var chart = new Sparkline { Series = series, LineColor = color };
            chart.style.height = 36;
            chart.style.marginTop = 2;
            chart.style.backgroundColor = new StyleColor(GameTheme.BgSliderTrack);
            chart.style.borderTopLeftRadius = 3; chart.style.borderTopRightRadius = 3;
            chart.style.borderBottomLeftRadius = 3; chart.style.borderBottomRightRadius = 3;
            panelBody.Add(chart);
            activeSparklines.Add(chart);
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

        // Live variant: value re-reads every refresh tick while the panel is open, so numbers
        // that move daily (war score, exhaustion) don't render as frozen snapshots.
        void LiveStat(string label, Func<string> get, Func<bool> good = null)
        {
            var row = Row();
            row.style.marginBottom = 5;
            row.Add(MakeLabel(label.ToUpperInvariant(), 10, GameTheme.TextDim));
            var spacer = new VisualElement(); spacer.style.flexGrow = 1; row.Add(spacer);
            bool g = good?.Invoke() ?? true;
            var val = MakeLabel(get(), 14, good == null ? GameTheme.TextPrimary : (g ? GameTheme.Positive : GameTheme.Negative), bold: true);
            row.Add(val);
            panelBody.Add(row);
            activeLiveStats.Add(new LiveStatBinding { ValueLabel = val, Get = get, Good = good });
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
            thumb.style.width = 18; thumb.style.height = 18;
            thumb.style.top = -6;
            thumb.style.backgroundColor = new StyleColor(GameTheme.Accent);
            thumb.style.borderTopLeftRadius = 9; thumb.style.borderTopRightRadius = 9;
            thumb.style.borderBottomLeftRadius = 9; thumb.style.borderBottomRightRadius = 9;
            thumb.style.borderBottomWidth = 3;
            thumb.style.borderBottomColor = new StyleColor(GameTheme.Shade(GameTheme.Accent));
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
            thumb.style.marginLeft = -9; // center the 18px thumb on the percentage point
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
            title.style.letterSpacing = 6f;
            startScreen.Add(title);

            var rule = new VisualElement();
            rule.style.width = 120; rule.style.height = 1;
            rule.style.marginTop = 8; rule.style.marginBottom = 8;
            rule.style.backgroundColor = new StyleColor(GameTheme.Accent);
            startScreen.Add(rule);

            var subtitle = MakeLabel("SELECT A MEMBER STATE TO GOVERN", 12, GameTheme.TextDim);
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitle.style.letterSpacing = 1.5f;
            subtitle.style.marginTop = 4; subtitle.style.marginBottom = 12;
            startScreen.Add(subtitle);

            // Search + Continue row above the country list.
            var controlsRow = new VisualElement();
            controlsRow.style.flexDirection = FlexDirection.Row;
            controlsRow.style.alignItems = Align.Center;
            controlsRow.style.width = 420;
            controlsRow.style.marginBottom = 8;

            var searchLabel = MakeLabel("SEARCH", 10, GameTheme.TextDim);
            searchLabel.style.letterSpacing = 1f;
            searchLabel.style.marginRight = 6;
            controlsRow.Add(searchLabel);

            startScreenSearch = new TextField();
            startScreenSearch.style.flexGrow = 1;
            startScreenSearch.style.backgroundColor = new StyleColor(GameTheme.BgSliderTrack);
            startScreenSearch.style.color = new StyleColor(GameTheme.TextPrimary);
            startScreenSearch.RegisterValueChangedCallback(_ => { if (startScreenPopulated) PopulateStartScreen(); });
            controlsRow.Add(startScreenSearch);

            continueBtn = MakeButton("CONTINUE ▶", 12, GameTheme.BgButtonActive, GameTheme.BgButtonHover, GameTheme.TextPrimary, ContinueSavedGame);
            continueBtn.style.width = 110; continueBtn.style.height = 26; continueBtn.style.marginLeft = 8;
            continueBtn.style.display = DisplayStyle.None;
            controlsRow.Add(continueBtn);
            startScreen.Add(controlsRow);

            var listBox = new VisualElement();
            listBox.style.width = 420;
            listBox.style.flexGrow = 1;
            listBox.style.backgroundColor = new StyleColor(GameTheme.BgPanel);
            listBox.style.borderTopLeftRadius = 8; listBox.style.borderTopRightRadius = 8;
            listBox.style.borderBottomLeftRadius = 8; listBox.style.borderBottomRightRadius = 8;
            listBox.style.borderTopWidth = 1; listBox.style.borderLeftWidth = 1; listBox.style.borderRightWidth = 1; listBox.style.borderBottomWidth = 1;
            listBox.style.borderTopColor = new StyleColor(GameTheme.Border);
            listBox.style.borderLeftColor = new StyleColor(GameTheme.Border);
            listBox.style.borderRightColor = new StyleColor(GameTheme.Border);
            listBox.style.borderBottomColor = new StyleColor(GameTheme.Border);
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

            // Continue button appears when a valid save exists (validated once — the file is
            // megabytes and this repopulates on every search keystroke).
            hasValidSave ??= SaveLoad.TryRead(map.World.Countries.Count) != null;
            continueBtn.style.display = hasValidSave.Value ? DisplayStyle.Flex : DisplayStyle.None;

            string query = startScreenSearch?.value?.Trim().ToLowerInvariant() ?? "";

            var indices = new List<int>();
            for (int i = 0; i < map.World.Countries.Count; i++) indices.Add(i);
            indices.Sort((a, b) => string.Compare(map.World.Countries[a].Name, map.World.Countries[b].Name, StringComparison.Ordinal));

            foreach (int idx in indices)
            {
                var country = map.World.Countries[idx];
                if (query.Length > 0 && !country.Name.ToLowerInvariant().Contains(query)) continue;
                int capturedIdx = idx;
                var row = MakeButton(country.Name, 13, GameTheme.BgPanel, GameTheme.BgButtonHover, GameTheme.TextPrimary,
                    () => BeginGame(capturedIdx, country.Name), align: TextAnchor.MiddleLeft);
                row.style.height = 30;
                row.style.marginBottom = 2;
                startScreenList.Add(row);
            }
        }

        // Loads the save and drops straight into the running game — geography/meshes are
        // already built by MapRenderer.Start; only sim state swaps.
        void ContinueSavedGame()
        {
            var save = SaveLoad.TryRead(map.World.Countries.Count);
            if (save == null)
            {
                hasValidSave = false;
                continueBtn.style.display = DisplayStyle.None;
                ShowToast("System", "Save file is missing or invalid.");
                return;
            }

            map.ApplySave(save);
            interaction.RestoreClock(save.SimDay, save.DaysPerSecond);
            interaction.SelectCountry(save.PlayerCountryIndex);
            if (PlayerState.State != GameState.Playing) PlayerState.State = GameState.Playing;
            UIState.ActiveCategory = NationCategory.Economy;
            UIState.ActiveTopic = null;
            UIState.PanelOpen = false;
            builtForSelected = -2; // force a panel rebuild against the loaded state
            startScreen.style.display = DisplayStyle.None;
            ShowToast("System", $"Welcome back — day {save.SimDay}, governing {save.PlayerCountryName}.");
        }

        void BeginGame(int countryIndex, string countryName)
        {
            PlayerState.Begin(countryIndex, countryName, interaction.SimDay);
            interaction.SelectCountry(countryIndex);
            map.RefreshCountryColors(); // paint the world by relation-to-player from turn one
            UIState.ActiveCategory = NationCategory.Economy;
            // Closed by default — the side panel is opt-in via the ministry bar, not forced open
            // just because a nation (even your own) is selected.
            UIState.PanelOpen = false;
            startScreen.style.display = DisplayStyle.None;
        }

        // ============================== DECISION EVENT MODAL ==============================

        // Shown whenever EventSystem.Pending is non-null (Refresh() rebuilds it per event).
        // The sim clock is already frozen by MapInteraction while a decision is pending, so
        // this modal deliberately has no close/dismiss — governing means deciding.
        void BuildEventModal()
        {
            eventModal = new VisualElement();
            eventModal.pickingMode = PickingMode.Position; // swallow map clicks behind it
            eventModal.style.position = Position.Absolute;
            eventModal.style.left = 0; eventModal.style.right = 0; eventModal.style.top = 0; eventModal.style.bottom = 0;
            eventModal.style.backgroundColor = new StyleColor(new Color(0.02f, 0.03f, 0.05f, 0.72f));
            eventModal.style.alignItems = Align.Center;
            eventModal.style.justifyContent = Justify.Center;
            eventModal.style.display = DisplayStyle.None;
            root.Add(eventModal);
        }

        void PopulateEventModal(GameEvent ev)
        {
            eventModal.Clear();

            var box = new VisualElement();
            box.style.width = 520;
            box.style.backgroundColor = new StyleColor(GameTheme.BgPanel);
            box.style.borderTopLeftRadius = 8; box.style.borderTopRightRadius = 8;
            box.style.borderBottomLeftRadius = 8; box.style.borderBottomRightRadius = 8;
            box.style.borderLeftWidth = 3; box.style.borderLeftColor = new StyleColor(GameTheme.Accent);
            box.style.borderTopWidth = 1; box.style.borderRightWidth = 1; box.style.borderBottomWidth = 1;
            box.style.borderTopColor = new StyleColor(GameTheme.Border);
            box.style.borderRightColor = new StyleColor(GameTheme.Border);
            box.style.borderBottomColor = new StyleColor(GameTheme.Border);
            box.style.paddingTop = 20; box.style.paddingBottom = 20;
            box.style.paddingLeft = 24; box.style.paddingRight = 24;
            eventModal.Add(box);

            var kicker = MakeLabel("URGENT — DECISION REQUIRED", 10, GameTheme.Negative, bold: true);
            kicker.style.letterSpacing = 1.5f;
            kicker.style.marginBottom = 6;
            box.Add(kicker);

            var title = MakeLabel(ev.Title, 20, GameTheme.TextPrimary, bold: true);
            title.style.marginBottom = 8;
            box.Add(title);

            var desc = MakeLabel(ev.Description, 13, GameTheme.TextDim);
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.style.marginBottom = 16;
            box.Add(desc);

            for (int i = 0; i < ev.Choices.Length; i++)
            {
                int captured = i;
                var btn = MakeButton(ev.Choices[i].Label, 13, GameTheme.BgButton, GameTheme.BgButtonHover, GameTheme.TextPrimary, () =>
                {
                    var e = map.Economy.States[PlayerState.CountryIndex];
                    var n = map.National.States[PlayerState.CountryIndex];
                    string outcome = EventSystem.Choose(captured, e, n);
                    if (!string.IsNullOrEmpty(outcome)) ShowToast(PlayerState.CountryName, outcome);
                    builtForCategory = (NationCategory)(-1); // panel numbers may have changed
                }, align: TextAnchor.MiddleLeft);
                btn.style.height = 34;
                btn.style.marginBottom = 8;
                btn.style.paddingRight = 8;
                box.Add(btn);
            }

            var hint = MakeLabel("The world is paused until you decide.", 10, GameTheme.TextDim);
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.marginTop = 4;
            box.Add(hint);
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
            box.style.borderTopLeftRadius = 8; box.style.borderTopRightRadius = 8;
            box.style.borderBottomLeftRadius = 8; box.style.borderBottomRightRadius = 8;
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
                map.RefreshCountryColors(); // back to the neutral palette until a new country is picked
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
                // Only countries the player is actually looking at get economy toasts — with
                // 258 simulated economies, global threshold-crossing chatter crowded genuine
                // world headlines (war declarations, peaces) out of the 4-slot toast stack.
                bool relevant = i == PlayerState.CountryIndex || i == interaction.Selected;
                if (relevant && !string.IsNullOrEmpty(why) && why != lastWhySeen[i])
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

            // Dev-only: MERIDIAN_AUTOSTART=1 skips the start screen and begins governing the
            // named country (or the first alphabetically) immediately, at max speed. Exists so
            // headless/log-only testing can actually exercise the live sim loop — normal play
            // is completely unaffected since the env var is never set outside a debug launch.
            if (PlayerState.State == GameState.NotStarted && map.World != null && map.World.Countries.Count > 0)
            {
                // Dev-only: MERIDIAN_LOADSAVE=1 auto-continues the saved game at boot —
                // exercises the exact same path as the start screen's CONTINUE button, so the
                // cross-process load path is verifiable from Player.log alone.
                if (System.Environment.GetEnvironmentVariable("MERIDIAN_LOADSAVE") != null)
                {
                    hasValidSave ??= SaveLoad.TryRead(map.World.Countries.Count) != null;
                    if (hasValidSave.Value)
                    {
                        Debug.Log("[diag] MERIDIAN_LOADSAVE engaged — continuing saved game");
                        ContinueSavedGame();
                        return;
                    }
                    Debug.LogWarning("[diag] MERIDIAN_LOADSAVE set but no valid save found");
                }

                string autostart = System.Environment.GetEnvironmentVariable("MERIDIAN_AUTOSTART");
                if (autostart != null)
                {
                    int idx = 0;
                    if (autostart.Length > 1)
                    {
                        int found = map.World.Countries.FindIndex(c => c.Name.Equals(autostart, System.StringComparison.OrdinalIgnoreCase));
                        if (found >= 0) idx = found;
                    }
                    BeginGame(idx, map.World.Countries[idx].Name);
                    interaction.daysPerSecond = 10f;
                    Debug.Log($"[diag] MERIDIAN_AUTOSTART engaged: governing {map.World.Countries[idx].Name}");
                }
            }

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

            // World headlines (war declarations, peaces, AI agreements) from the sim.
            while (WorldFeed.TryDequeue(out string src, out string msg))
                ShowToast(src, msg);

            // Decision-event modal: visible exactly while a decision is pending, rebuilt once
            // per distinct event (not per refresh tick — the buttons hold closures).
            var pendingEvent = EventSystem.Pending;
            eventModal.style.display = pendingEvent != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (pendingEvent != null && !ReferenceEquals(pendingEvent, builtForEvent))
            {
                builtForEvent = pendingEvent;
                PopulateEventModal(pendingEvent);
            }
            if (pendingEvent == null) builtForEvent = null;

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
                SetButtonColor(kv.Value, active ? GameTheme.BgButtonActive : GameTheme.BgButton);
                (kv.Value.userData as Label).style.color = active ? GameTheme.Accent : GameTheme.TextDim;
            }

            foreach (var kv in mapModeButtons)
            {
                bool active = map.CurrentMode == kv.Key;
                SetButtonColor(kv.Value, active ? GameTheme.BgButtonActive : GameTheme.BgButton);
                (kv.Value.userData as Label).style.color = active ? GameTheme.Accent : GameTheme.TextDim;
            }

            for (int i = 0; i < ministryButtons.Length; i++)
            {
                bool active = UIState.ActiveCategory == Categories[i];
                var catColor = Categories[i].Accent();
                SetButtonColor(ministryButtons[i], active ? catColor : GameTheme.Muted(catColor));
                (ministryButtons[i].userData as Label).style.color = GameTheme.TextPrimary;
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

            // STRUCTURAL war changes (a war starting/ending, a peace/declare button becoming
            // valid or invalid) need a full panel rebuild — the LiveStat bindings only keep
            // NUMBERS fresh, they can't add or remove buttons. Cheap to compute, only nonzero
            // while the Military tab is showing.
            int warStamp = 0;
            if (UIState.ActiveCategory == NationCategory.Military && map.Wars != null && PlayerState.CountryIndex >= 0)
            {
                foreach (var w in map.Wars.WarsOf(PlayerState.CountryIndex))
                    warStamp = warStamp * 31 + (map.Wars.PlayerCanDemandConcessions(w) ? 2 : 1);
                if (sel >= 0 && sel != PlayerState.CountryIndex)
                {
                    warStamp = warStamp * 31 + (map.Wars.WarBetween(PlayerState.CountryIndex, sel) != null ? 2 : 1);
                    warStamp = warStamp * 31 + (map.Diplomacy != null && map.Wars.CanDeclare(PlayerState.CountryIndex, sel, map.Diplomacy) ? 2 : 1);
                }
            }

            if (sel != builtForSelected || UIState.ActiveCategory != builtForCategory || UIState.ActiveTopic != builtForTopic || UIState.PanelOpen != builtForPanelOpen || warStamp != builtForWarStamp)
            {
                builtForSelected = sel;
                builtForCategory = UIState.ActiveCategory;
                builtForTopic = UIState.ActiveTopic;
                builtForPanelOpen = UIState.PanelOpen;
                builtForWarStamp = warStamp;
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
                // Charts pick up the day's new samples on the same cadence as everything else.
                foreach (var chart in activeSparklines) chart.MarkDirtyRepaint();

                foreach (var ls in activeLiveStats)
                {
                    ls.ValueLabel.text = ls.Get();
                    if (ls.Good != null)
                        ls.ValueLabel.style.color = ls.Good() ? GameTheme.Positive : GameTheme.Negative;
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

        // "Nameplate button": squared-off corners with a thin gold bottom rim (like the engraved
        // nameplates on a committee-room desk), and a restrained hover/press response — a slight
        // brightness lift and a hairline scale nudge, not a bouncy candy pop. This is the single
        // shared factory behind every clickable element in the game, so the whole interface picks
        // up the institutional feel at once. Returns the button itself (not a wrapper) — callers
        // already rely on recoloring and re-parenting (e.g. the active-tab underline) whatever
        // this returns directly.
        static VisualElement MakeButton(string text, int fontSize, Color bg, Color hoverBg, Color textColor, Action onClick, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var btn = new VisualElement();
            btn.style.backgroundColor = new StyleColor(bg);
            btn.style.borderTopLeftRadius = 3; btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3; btn.style.borderBottomRightRadius = 3;
            btn.style.borderBottomWidth = 2;
            btn.style.borderBottomColor = new StyleColor(GameTheme.Accent);
            btn.style.justifyContent = align == TextAnchor.MiddleLeft ? Justify.FlexStart : Justify.Center;
            btn.style.alignItems = Align.Center;
            btn.style.flexDirection = FlexDirection.Row;
            if (align == TextAnchor.MiddleLeft) btn.style.paddingLeft = 8;
            // Smooth color fade + a small, restrained scale nudge on hover/press — a lift, not a bounce.
            btn.style.transitionProperty = new List<StylePropertyName>
                { new StylePropertyName("background-color"), new StylePropertyName("border-bottom-color"), new StylePropertyName("scale") };
            btn.style.transitionDuration = new List<TimeValue>
                { new TimeValue(120, TimeUnit.Millisecond), new TimeValue(120, TimeUnit.Millisecond), new TimeValue(90, TimeUnit.Millisecond) };

            var label = MakeLabel(text, fontSize, textColor);
            label.style.unityTextAlign = align;
            label.style.letterSpacing = 0.4f;
            label.pickingMode = PickingMode.Ignore;
            btn.Add(label);
            btn.userData = label; // Refresh() reads this back to recolor text on active/hover state

            btn.RegisterCallback<PointerEnterEvent>(_ =>
            {
                SetButtonColor(btn, hoverBg);
                btn.style.scale = new StyleScale(new Scale(new Vector3(1.015f, 1.015f, 1f)));
            });
            btn.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                SetButtonColor(btn, bg);
                btn.style.scale = new StyleScale(new Scale(Vector3.one));
            });
            btn.RegisterCallback<PointerDownEvent>(_ =>
                btn.style.scale = new StyleScale(new Scale(new Vector3(0.98f, 0.98f, 1f))));
            btn.RegisterCallback<PointerUpEvent>(_ =>
                btn.style.scale = new StyleScale(new Scale(new Vector3(1.015f, 1.015f, 1f))));
            btn.RegisterCallback<ClickEvent>(_ => onClick());
            return btn;
        }

        // Sets the fill only — the thin gold bottom rim is a constant nameplate-trim accent
        // (set once in MakeButton), not a shade of the fill, so it stays a steady gold line
        // across every state (default/hover/active/muted) instead of shifting with the button color.
        static void SetButtonColor(VisualElement btn, Color c)
        {
            btn.style.backgroundColor = new StyleColor(c);
        }
    }
}
