using UnityEngine;

namespace Meridian.UI
{
    // Institutional "UN Security Council chamber" theme — deep UN-blue backgrounds matching the
    // map's ocean color, gold nameplate/podium accents instead of neon, muted flag-inspired
    // per-category colors instead of a rainbow of candy tones. Plain Color constants — UI
    // Toolkit's StyleColor takes a Color directly, no cached textures needed.
    public static class GameTheme
    {
        public static readonly Color BgTop = new Color(0.055f, 0.09f, 0.15f, 0.97f);
        public static readonly Color BgBar = new Color(0.055f, 0.09f, 0.15f, 0.97f);
        public static readonly Color BgPanel = new Color(0.075f, 0.115f, 0.175f, 0.96f);
        public static readonly Color BgDropdown = new Color(0.095f, 0.14f, 0.205f, 0.98f);
        // Section cards inside the panel — one step lighter than the panel so grouped content
        // reads as distinct plates rather than one long list.
        public static readonly Color BgCard = new Color(0.10f, 0.15f, 0.215f, 0.92f);

        // Default (non-category) buttons: official UN blue instead of flat gray or candy purple,
        // so neutral controls read as institutional/official rather than toy-like.
        public static readonly Color BgButton = new Color(0.16f, 0.30f, 0.46f, 1f);
        public static readonly Color BgButtonHover = new Color(0.22f, 0.40f, 0.58f, 1f);
        public static readonly Color BgButtonActive = new Color(0.72f, 0.58f, 0.28f, 1f);
        public static readonly Color BgSliderTrack = new Color(0.04f, 0.06f, 0.10f, 1f);

        // Gold — the podium-nameplate / laurel-emblem color that reads as ceremonial and official.
        public static readonly Color Accent = new Color(0.80f, 0.66f, 0.34f, 1f);
        public static readonly Color Positive = new Color(0.40f, 0.78f, 0.52f, 1f);
        public static readonly Color Negative = new Color(0.86f, 0.36f, 0.34f, 1f);
        public static readonly Color TextPrimary = new Color(0.93f, 0.93f, 0.90f, 1f);
        public static readonly Color TextDim = new Color(0.93f, 0.93f, 0.90f, 0.58f);
        public static readonly Color Border = new Color(0.80f, 0.66f, 0.34f, 0.22f);

        // A darker shade of any color, used as the "3D rim" along a button's bottom edge —
        // the classic candy-button trick that makes a flat rectangle read as pressable.
        public static Color Shade(Color c, float amount = 0.35f) =>
            new Color(c.r * (1f - amount), c.g * (1f - amount), c.b * (1f - amount), c.a);

        // A deep, muted-but-still-tinted version of a vivid color — used for a button's resting
        // state so the ministry bar reads as a row of distinctly colored (not uniform gray)
        // buttons even before you hover or select one.
        public static Color Muted(Color c, float mix = 0.62f) => Color.Lerp(c, BgPanel, mix);

        public static Color Delta(double value) => value >= 0 ? Positive : Negative;
    }
}
