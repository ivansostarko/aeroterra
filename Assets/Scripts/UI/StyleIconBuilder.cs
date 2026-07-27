using System.Collections.Generic;
using UnityEngine;
using AeroTerra.Core;

namespace AeroTerra.UI
{
    /// <summary>
    /// Card art for each map style (Liberty/Terrain/Satellite/OSM/Dark) shown on the
    /// Settings ▸ Map style cards. Loads the real icon from
    /// Assets/Resources/Images/ui/Maps/{style}_map_icon.png when present, falling back
    /// to a procedural placeholder otherwise (same override spirit as MapIconBuilder).
    /// </summary>
    public static class StyleIconBuilder
    {
        private static readonly Dictionary<MapStyle, Sprite> _cache = new Dictionary<MapStyle, Sprite>();

        private static string IconFileName(MapStyle style) => style switch
        {
            MapStyle.OsmStandard => "osm_map_icon",
            _ => style.ToString().ToLowerInvariant() + "_map_icon",
        };

        public static Sprite GetIcon(MapStyle style)
        {
            if (_cache.TryGetValue(style, out var cached) && cached != null) return cached;
            var overridden = Resources.Load<Sprite>("Images/ui/Maps/" + IconFileName(style));
            var sprite = overridden != null ? overridden : Build(style);
            _cache[style] = sprite;
            return sprite;
        }

        private static Sprite Build(MapStyle style)
        {
            const int w = 200, h = 130;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];

            switch (style)
            {
                case MapStyle.Satellite: PaintSatellite(px, w, h); break;
                case MapStyle.Terrain: PaintTerrain(px, w, h); break;
                case MapStyle.Dark: PaintDark(px, w, h); break;
                case MapStyle.OsmStandard: PaintOsm(px, w, h); break;
                default: PaintLiberty(px, w, h); break;
            }

            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void Fill(Color[] px, Color c) { for (int i = 0; i < px.Length; i++) px[i] = c; }

        private static void PaintLiberty(Color[] px, int w, int h)
        {
            Fill(px, new Color(0.94f, 0.92f, 0.86f));
            FillRect(px, w, h, 0.55f, 0f, 1f, 0.4f, new Color(0.55f, 0.75f, 0.85f));
            FillRect(px, w, h, 0f, 0.55f, 0.4f, 0.95f, new Color(0.65f, 0.80f, 0.55f));
            Road(px, w, h, 0.5f, new Color(0.95f, 0.95f, 0.92f), true);
            Road(px, w, h, 0.45f, new Color(0.95f, 0.95f, 0.92f), false);
        }

        private static void PaintOsm(Color[] px, int w, int h)
        {
            Fill(px, new Color(0.96f, 0.93f, 0.82f));
            FillRect(px, w, h, 0f, 0f, 1f, 0.3f, new Color(0.68f, 0.82f, 0.95f));
            Road(px, w, h, 0.55f, new Color(0.96f, 0.75f, 0.4f), true);
            Road(px, w, h, 0.3f, new Color(0.9f, 0.9f, 0.9f), false);
        }

        private static void PaintDark(Color[] px, int w, int h)
        {
            Fill(px, new Color(0.07f, 0.09f, 0.13f));
            Road(px, w, h, 0.5f, new Color(0.25f, 0.28f, 0.35f), true);
            Road(px, w, h, 0.25f, new Color(0.2f, 0.23f, 0.3f), false);
            Road(px, w, h, 0.75f, new Color(0.2f, 0.23f, 0.3f), false);
        }

        private static void PaintTerrain(Color[] px, int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                Color row = Color.Lerp(new Color(0.55f, 0.42f, 0.28f), new Color(0.55f, 0.70f, 0.4f), y / (float)h);
                for (int x = 0; x < w; x++) px[y * w + x] = row;
            }
            var c = new Vector2(w * 0.5f, h * 0.45f);
            for (int ring = 1; ring <= 4; ring++)
            {
                float r = ring * (h * 0.11f);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (Mathf.Abs(Vector2.Distance(new Vector2(x, y), c) - r) < 1.1f)
                            px[y * w + x] = new Color(0.35f, 0.28f, 0.16f);
            }
        }

        private static void PaintSatellite(Color[] px, int w, int h)
        {
            var rnd = new System.Random(7);
            Fill(px, new Color(0.20f, 0.30f, 0.16f));
            for (int i = 0; i < 10; i++)
            {
                int x0 = rnd.Next(0, w), y0 = rnd.Next(0, h), size = rnd.Next(14, 34);
                Color c = i % 3 == 0 ? new Color(0.30f, 0.42f, 0.55f) : new Color(0.24f, 0.34f, 0.20f);
                var center = new Vector2(x0, y0);
                for (int y = Mathf.Max(0, y0 - size); y < Mathf.Min(h, y0 + size); y++)
                    for (int x = Mathf.Max(0, x0 - size); x < Mathf.Min(w, x0 + size); x++)
                        if (Vector2.Distance(new Vector2(x, y), center) < size)
                            px[y * w + x] = c;
            }
        }

        private static void FillRect(Color[] px, int w, int h, float x0f, float y0f, float x1f, float y1f, Color c)
        {
            int x0 = Mathf.RoundToInt(x0f * w), x1 = Mathf.RoundToInt(x1f * w);
            int y0 = Mathf.RoundToInt(y0f * h), y1 = Mathf.RoundToInt(y1f * h);
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++)
                    px[y * w + x] = c;
        }

        private static void Road(Color[] px, int w, int h, float posFrac, Color c, bool horizontal)
        {
            int thickness = Mathf.Max(2, (horizontal ? h : w) / 22);
            if (horizontal)
            {
                int y0 = Mathf.RoundToInt(posFrac * h) - thickness / 2;
                for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y0 + thickness); y++)
                    for (int x = 0; x < w; x++) px[y * w + x] = c;
            }
            else
            {
                int x0 = Mathf.RoundToInt(posFrac * w) - thickness / 2;
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x0 + thickness); x++)
                    for (int y = 0; y < h; y++) px[y * w + x] = c;
            }
        }
    }
}
