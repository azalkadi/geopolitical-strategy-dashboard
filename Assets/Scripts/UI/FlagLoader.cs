using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Meridian.UI
{
    // Loads small flag PNGs bundled in StreamingAssets/flags/{code}.png — sourced from
    // flagcdn.com (public, free, no API key) at build time, one file per ISO 3166-1 alpha-2
    // code actually used by the game's country dataset (see docs/obsidian-vault/Data Sources).
    // Read raw from disk the same way BuildSatelliteQuad reads the basemap, then cached in
    // memory — 237 files at ~1-4KB each, so caching every flag opened this session costs
    // nothing worth guarding against.
    public static class FlagLoader
    {
        static readonly Dictionary<string, Texture2D> cache = new();

        // A couple of real, commonly-governed entities Natural Earth's admin_0 dataset gives
        // ISO_A2 = "-99" (no officially assigned code), but flagcdn still serves under a widely
        // used conventional code. Everything else with no valid 2-letter ISO_A2 (disputed
        // buffer zones, ice fields, military bases, uninhabited reef claims) just has no flag —
        // callers skip the Image element rather than showing a broken/placeholder icon.
        static readonly Dictionary<string, string> overrides = new()
        {
            { "Taiwan", "tw" },
            { "Kosovo", "xk" },
        };

        public static Texture2D Get(string countryName, string isoA2)
        {
            string code = ResolveCode(countryName, isoA2);
            if (code == null) return null;
            if (cache.TryGetValue(code, out var cached)) return cached; // may be a cached miss (null)

            string path = Path.Combine(Application.streamingAssetsPath, "flags", code + ".png");
            if (!File.Exists(path))
            {
                cache[code] = null;
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            tex.filterMode = FilterMode.Bilinear;
            cache[code] = tex;
            return tex;
        }

        static string ResolveCode(string countryName, string isoA2)
        {
            if (!string.IsNullOrEmpty(isoA2) && isoA2.Length == 2) return isoA2.ToLowerInvariant();
            return overrides.TryGetValue(countryName, out var code) ? code : null;
        }
    }
}
