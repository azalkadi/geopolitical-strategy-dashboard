using UnityEngine;

namespace Meridian.UI
{
    // Shared color palette for the UI Toolkit interface. Dark dashboard look: navy panels, a
    // gold accent (matches the map's capital-city markers), green/red for positive/negative
    // deltas. Plain Color constants — UI Toolkit's StyleColor takes a Color directly, no cached
    // textures needed (unlike the old IMGUI Theme, which had to fake solid backgrounds via 1x1
    // textures since legacy GUIStyle has no native color property).
    public static class GameTheme
    {
        public static readonly Color BgTop = new Color(0.05f, 0.07f, 0.10f, 0.97f);
        public static readonly Color BgBar = new Color(0.05f, 0.07f, 0.10f, 0.97f);
        public static readonly Color BgPanel = new Color(0.07f, 0.09f, 0.13f, 0.95f);
        public static readonly Color BgDropdown = new Color(0.09f, 0.11f, 0.16f, 0.98f);
        public static readonly Color BgButton = new Color(0.12f, 0.15f, 0.20f, 1f);
        public static readonly Color BgButtonHover = new Color(0.17f, 0.21f, 0.27f, 1f);
        public static readonly Color BgButtonActive = new Color(0.22f, 0.18f, 0.08f, 1f);
        public static readonly Color BgSliderTrack = new Color(0.02f, 0.03f, 0.05f, 1f);

        public static readonly Color Accent = new Color(1f, 0.78f, 0.25f, 1f);
        public static readonly Color Positive = new Color(0.40f, 0.85f, 0.50f, 1f);
        public static readonly Color Negative = new Color(0.95f, 0.42f, 0.42f, 1f);
        public static readonly Color TextPrimary = Color.white;
        public static readonly Color TextDim = new Color(1f, 1f, 1f, 0.55f);
        public static readonly Color Border = new Color(1f, 1f, 1f, 0.10f);

        public static Color Delta(double value) => value >= 0 ? Positive : Negative;
    }
}
