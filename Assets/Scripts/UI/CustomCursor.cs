using UnityEngine;

namespace AeroTerra.UI
{
    /// <summary>
    /// Procedural reticle-style cursor used across the menu screens (Home, Free Flight,
    /// Workshop, Settings) and the in-flight pause menu. Reset() restores the OS default
    /// arrow for active flight, where there's nothing to point at with a mouse cursor.
    /// </summary>
    public static class CustomCursor
    {
        private static Texture2D _texture;

        public static void Apply()
        {
            if (_texture == null) _texture = Build();
            Cursor.SetCursor(_texture, new Vector2(_texture.width / 2f, _texture.height / 2f), CursorMode.Auto);
        }

        public static void Reset() => Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        private static Texture2D Build()
        {
            const int size = 24;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

            var px = new Color[size * size];
            var center = new Vector2(size / 2f, size / 2f);
            Color ring = UIBuilder.Accent;
            Color dot = UIBuilder.TextMain;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    Color c = Color.clear;
                    if (d <= 2f) c = dot;                                  // solid center dot
                    else if (d >= 8f && d < 10f) c = ring;                 // outer ring
                    else if (d >= 10f && d < 11f)                          // 1px soft anti-aliased edge
                        c = new Color(ring.r, ring.g, ring.b, Mathf.Clamp01(11f - d));
                    px[y * size + x] = c;
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
