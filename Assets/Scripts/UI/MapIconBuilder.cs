using System.Collections.Generic;
using UnityEngine;
using AeroTerra.Core;

namespace AeroTerra.UI
{
    /// <summary>
    /// Procedural placeholder card art for each Free Flight map: a flat, vector-style
    /// skyline silhouette against a gradient sky, deterministic per map (seeded by Id
    /// so it's stable across sessions). To use real artwork instead, drop a Sprite at
    /// Assets/Resources/MapIcons/{map.Id}.png (e.g. MapIcons/london.png) — it is picked
    /// up automatically and the procedural fallback is skipped for that map.
    /// </summary>
    public static class MapIconBuilder
    {
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static Sprite GetIcon(MapDefinition map)
        {
            if (_cache.TryGetValue(map.Id, out var cached) && cached != null) return cached;

            var overridden = Resources.Load<Sprite>("MapIcons/" + map.Id);
            var sprite = overridden != null ? overridden : Build(map);
            _cache[map.Id] = sprite;
            return sprite;
        }

        private static Sprite Build(MapDefinition map)
        {
            const int w = 320, h = 200;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

            var rnd = new System.Random(map.Id.GetHashCode());
            Color skyTop = new Color(0.05f, 0.09f, 0.19f);
            Color skyHorizon = new Color(0.42f, 0.55f, 0.72f);
            Color accent = Color.HSVToRGB((float)rnd.NextDouble(), 0.6f, 0.95f);
            Color silhouette = new Color(0.04f, 0.05f, 0.08f);

            float baseline = h * 0.80f;
            var pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                Color row = y < baseline ? Color.Lerp(skyTop, skyHorizon, Mathf.Clamp01(y / baseline)) : silhouette;
                for (int x = 0; x < w; x++) pixels[y * w + x] = row;
            }

            // Flat sun/moon accent disc in the upper-right sky.
            Vector2 sunPos = new Vector2(w * 0.76f, h * 0.24f);
            float sunR = h * 0.11f;
            for (int y = 0; y < (int)baseline; y++)
                for (int x = 0; x < w; x++)
                    if (Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), sunPos) <= sunR)
                        pixels[y * w + x] = accent;

            // Flat skyline silhouette bars with one taller "landmark" spike near the center.
            const int cols = 16;
            float colW = w / (float)cols;
            int landmarkCol = cols / 2 + rnd.Next(-2, 3);
            for (int c = 0; c < cols; c++)
            {
                float heightFrac = 0.16f + (float)rnd.NextDouble() * 0.30f;
                if (c == landmarkCol) heightFrac = 0.50f + (float)rnd.NextDouble() * 0.24f;
                int barH = Mathf.RoundToInt(h * heightFrac);
                int x0 = Mathf.RoundToInt(c * colW + colW * 0.10f);
                int x1 = Mathf.RoundToInt((c + 1) * colW - colW * 0.10f);
                int yTop = Mathf.Max(0, (int)baseline - barH);
                for (int y = yTop; y < (int)baseline; y++)
                    for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++)
                        pixels[y * w + x] = silhouette;
            }

            // Accent horizon line where the skyline meets the ground band.
            int baseRow = Mathf.Clamp((int)baseline, 0, h - 1);
            for (int x = 0; x < w; x++) pixels[baseRow * w + x] = accent;

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
