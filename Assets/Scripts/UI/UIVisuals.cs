using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Meridian.UI
{
    // Procedural texture generation for panel/button chrome — the same "no external art assets"
    // constraint that produced the satellite/flag Texture2D pipelines elsewhere in the project.
    // Flat, single-color VisualElements (the entire UI before this) read as an HTML page; a cheap
    // 2-stop vertical gradient behind every button and panel is enough to fake the "beveled
    // nameplate / carved slab" depth a real strategy-game HUD has, with zero new assets and near-
    // zero runtime cost (every texture is 1x2 or 1x1, generated once and cached by color).
    public static class UIVisuals
    {
        static readonly Dictionary<long, Texture2D> gradientCache = new();
        static Texture2D vignetteCache;

        static long Key(Color a, Color b) =>
            ((long)(a.r * 255) << 48) | ((long)(a.g * 255) << 40) | ((long)(a.b * 255) << 32) |
            ((long)(b.r * 255) << 24) | ((long)(b.g * 255) << 16) | ((long)(b.b * 255) << 8) | (long)(b.a * 255);

        // Row index (height-1) renders at the TOP of a displayed VisualElement/Image (same
        // top-down convention confirmed by BuildSatelliteQuad/the minimap's V mapping elsewhere
        // in this project) — so `top` goes in row 1, `bottom` in row 0.
        public static Texture2D VerticalGradient(Color top, Color bottom)
        {
            long key = Key(top, bottom);
            if (gradientCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = new Texture2D(1, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixel(0, 0, bottom);
            tex.SetPixel(0, 1, top);
            tex.Apply();
            gradientCache[key] = tex;
            return tex;
        }

        public static void ApplyVerticalGradient(VisualElement el, Color top, Color bottom)
        {
            el.style.backgroundImage = new StyleBackground(VerticalGradient(top, bottom));
            el.style.backgroundSize = new StyleBackgroundSize(
                new BackgroundSize(new Length(100, LengthUnit.Percent), new Length(100, LengthUnit.Percent)));
            el.style.backgroundRepeat = new StyleBackgroundRepeat(new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat));
        }

        // Panel chrome: a subtle top-lit gradient off the panel's own base color (reads as a
        // slightly convex carved slab instead of a flat rectangle) plus a faint 1px inner
        // highlight along the top edge and a darker 1px seam along the bottom — the same visual
        // grammar real strategy-game panels (EU4/HOI4/Civ) use for "this is a solid object", not
        // Photoshop-drop-shadow realism, just enough to break up flatness.
        public static void ApplyPanelChrome(VisualElement panel, Color baseColor)
        {
            ApplyVerticalGradient(panel, GameTheme.Tint(baseColor, 0.07f), GameTheme.Shade(baseColor, 0.14f));

            var topHighlight = new VisualElement();
            topHighlight.pickingMode = PickingMode.Ignore;
            topHighlight.style.position = Position.Absolute;
            topHighlight.style.left = 0; topHighlight.style.right = 0; topHighlight.style.top = 0; topHighlight.style.height = 1;
            topHighlight.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.10f));
            panel.Insert(0, topHighlight);
        }

        // Soft radial vignette (transparent center, dark edges) — used behind the start screen
        // and any other full-bleed backdrop to fake cinematic depth-of-field without a shader.
        public static Texture2D Vignette(int size = 256)
        {
            if (vignetteCache != null) return vignetteCache;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = center.magnitude;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    float a = Mathf.SmoothStep(0f, 0.85f, Mathf.Clamp01((d - 0.35f) / 0.65f));
                    pixels[y * size + x] = new Color(0f, 0f, 0f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            vignetteCache = tex;
            return tex;
        }
    }
}
