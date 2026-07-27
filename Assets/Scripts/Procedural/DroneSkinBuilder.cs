using System.Collections.Generic;
using UnityEngine;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// Procedural drone "skins" — runtime-generated pattern textures (camo, stripes,
    /// split-fade, digital) parametrized by a drone's own fixed DefaultBodyColor/
    /// DefaultAccentColor, applied as the body material's base texture. Replaces the
    /// old flat body/accent livery color pickers with a real per-drone customization
    /// axis. No imported image files — same fully-procedural convention as every other
    /// visual system in this project (see DroneMeshBuilder, MapIconBuilder).
    /// </summary>
    public static class DroneSkinBuilder
    {
        public static readonly string[] SkinIds = { "stock", "camo", "stripes", "splitfade", "digital" };

        public static string SkinLabel(string id) => id switch
        {
            "camo" => "CAMOUFLAGE",
            "stripes" => "RACING STRIPES",
            "splitfade" => "SPLIT-FADE",
            "digital" => "DIGITAL",
            _ => "STOCK",
        };

        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        /// <summary>Texture for a given skin id + color pair, cached per (id, body,
        /// accent) combination — the same drone/skin choice is requested repeatedly
        /// (Workshop icon cards, the live 3D preview, and every Free Flight spawn).</summary>
        public static Texture2D GetTexture(string skinId, Color body, Color accent)
        {
            string key = $"{skinId}|{ColorUtility.ToHtmlStringRGB(body)}|{ColorUtility.ToHtmlStringRGB(accent)}";
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[size * size];

            switch (skinId)
            {
                case "camo": PaintCamo(pixels, size, body, accent); break;
                case "stripes": PaintStripes(pixels, size, body, accent); break;
                case "splitfade": PaintSplitFade(pixels, size, body, accent); break;
                case "digital": PaintDigital(pixels, size, body, accent); break;
                default: PaintStock(pixels, size, body); break;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            _cache[key] = tex;
            return tex;
        }

        private static void PaintStock(Color[] px, int size, Color body)
        {
            for (int i = 0; i < px.Length; i++) px[i] = body;
        }

        /// <summary>Organic blotches in a darker/lighter mix of body+accent, deterministic
        /// per color pair so the same drone always renders the same camo pattern.</summary>
        private static void PaintCamo(Color[] px, int size, Color body, Color accent)
        {
            var rnd = new System.Random(body.GetHashCode() ^ accent.GetHashCode());
            Color dark = Color.Lerp(body, Color.black, 0.35f);
            Color mix = Color.Lerp(body, accent, 0.5f);
            for (int i = 0; i < px.Length; i++) px[i] = body;

            var blotchColors = new[] { dark, mix, accent };
            for (int b = 0; b < 14; b++)
            {
                float cx = (float)rnd.NextDouble() * size, cy = (float)rnd.NextDouble() * size;
                float r = size * (0.10f + (float)rnd.NextDouble() * 0.16f);
                Color c = blotchColors[rnd.Next(blotchColors.Length)];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - cx, dy = y - cy;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float edge = r * (0.75f + 0.25f * Mathf.PerlinNoise(x * 0.15f, y * 0.15f));
                        if (d < edge) px[y * size + x] = c;
                    }
                }
            }
        }

        /// <summary>Diagonal racing stripes in the accent color over the body color.</summary>
        private static void PaintStripes(Color[] px, int size, Color body, Color accent)
        {
            const float stripeWidth = 0.14f, period = 0.22f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + y) / (float)size; // diagonal coordinate
                    float t = Mathf.Repeat(u, period);
                    px[y * size + x] = t < stripeWidth ? accent : body;
                }
            }
        }

        /// <summary>Two-tone diagonal split: body on one half, accent on the other,
        /// blended across a soft seam.</summary>
        private static void PaintSplitFade(Color[] px, int size, Color body, Color accent)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x - y) / (float)size + 0.5f;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.42f) / 0.16f));
                    px[y * size + x] = Color.Lerp(body, accent, t);
                }
            }
        }

        /// <summary>Pixelated digital-camo blocks: a coarse grid where each cell is
        /// randomly (but deterministically) body or a body/accent blend.</summary>
        private static void PaintDigital(Color[] px, int size, Color body, Color accent)
        {
            const int cell = 10;
            var rnd = new System.Random(body.GetHashCode() * 31 ^ accent.GetHashCode());
            Color mix = Color.Lerp(body, accent, 0.6f);
            Color dark = Color.Lerp(body, Color.black, 0.3f);
            var options = new[] { body, mix, dark };

            int cols = Mathf.CeilToInt(size / (float)cell);
            var cellColors = new Color[cols * cols];
            for (int i = 0; i < cellColors.Length; i++) cellColors[i] = options[rnd.Next(options.Length)];

            for (int y = 0; y < size; y++)
            {
                int cy = y / cell;
                for (int x = 0; x < size; x++)
                {
                    int cx = x / cell;
                    px[y * size + x] = cellColors[cy * cols + cx];
                }
            }
        }
    }
}
