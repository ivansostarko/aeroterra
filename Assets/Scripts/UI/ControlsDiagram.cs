using UnityEngine;
using AeroTerra.Core;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// "How to play" graphic per control scheme, drawn with UI primitives —
    /// keycaps, a stylized gamepad, and a tilting phone for gyroscope.
    /// </summary>
    public static class ControlsDiagram
    {
        public static void Draw(RectTransform area, ControlScheme scheme)
        {
            switch (scheme)
            {
                case ControlScheme.Keyboard:
                    Title(area, "KEYBOARD");
                    Key(area, "W", 0.28f, 0.68f); Key(area, "S", 0.28f, 0.50f);
                    Key(area, "A", 0.16f, 0.50f); Key(area, "D", 0.40f, 0.50f);
                    Note(area, "W/S throttle · A/D yaw", 0.05f, 0.36f);
                    Key(area, "▲", 0.72f, 0.68f); Key(area, "▼", 0.72f, 0.50f);
                    Key(area, "◄", 0.60f, 0.50f); Key(area, "►", 0.84f, 0.50f);
                    Note(area, "Arrows: fly fwd/back & left/right", 0.55f, 0.36f);
                    Note(area, "SHIFT boost · SPACE brake/hover", 0.05f, 0.20f);
                    Note(area, "C camera · R reset · ESC pause", 0.05f, 0.08f);
                    break;

                case ControlScheme.KeyboardMouse:
                    Title(area, "KEYBOARD + MOUSE");
                    Key(area, "W", 0.22f, 0.66f); Key(area, "S", 0.22f, 0.48f);
                    Key(area, "A", 0.10f, 0.48f); Key(area, "D", 0.34f, 0.48f);
                    Note(area, "W/S throttle · A/D yaw", 0.04f, 0.34f);
                    Mouse(area, 0.68f, 0.55f);
                    Note(area, "Mouse: fine pitch & roll", 0.55f, 0.34f);
                    Note(area, "SHIFT boost · SPACE brake/hover", 0.04f, 0.20f);
                    Note(area, "C camera · R reset · ESC pause", 0.04f, 0.08f);
                    break;

                case ControlScheme.Gamepad:
                    Title(area, "GAMEPAD");
                    Stick(area, 0.25f, 0.55f, "L");
                    Note(area, "Left stick:\nthrottle / yaw", 0.08f, 0.28f);
                    Stick(area, 0.72f, 0.55f, "R");
                    Note(area, "Right stick:\npitch / roll", 0.6f, 0.28f);
                    Note(area, "RT boost · LT brake", 0.05f, 0.16f);
                    Note(area, "Y camera · B reset · START pause", 0.05f, 0.06f);
                    break;

                case ControlScheme.Gyroscope:
                    Title(area, "GYROSCOPE (MOBILE)");
                    Phone(area, 0.5f, 0.55f);
                    Note(area, "Tilt device forward/back: pitch\nTilt left/right: roll", 0.1f, 0.24f);
                    Note(area, "On-screen slider: throttle · buttons: yaw / camera / pause", 0.05f, 0.05f);
                    break;
            }
        }

        private static void Title(RectTransform a, string t) =>
            Label(a, t, 22, new Vector2(0, 0.86f), new Vector2(1, 0.98f), Accent,
                  TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

        private static void Note(RectTransform a, string t, float x, float y) =>
            Label(a, t, 16, new Vector2(x, y), new Vector2(x + 0.42f, y + 0.14f), TextDim);

        private static void Key(RectTransform a, string k, float x, float y)
        {
            var rt = Panel_(a, "Key", PanelAlt, new Vector2(x, y), new Vector2(x + 0.11f, y + 0.15f));
            rt.gameObject.GetComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.25f, 0.33f);
            Label(rt, k, 22, Vector2.zero, Vector2.one, TextMain, TMPro.TextAlignmentOptions.Center,
                  TMPro.FontStyles.Bold);
        }

        private static void Mouse(RectTransform a, float x, float y)
        {
            var body = Panel_(a, "Mouse", PanelAlt, new Vector2(x, y - 0.12f), new Vector2(x + 0.14f, y + 0.2f));
            body.gameObject.GetComponent<UnityEngine.UI.Image>().color = new Color(0.22f, 0.27f, 0.35f);
            Panel_(body, "Wheel", Accent, new Vector2(0.44f, 0.75f), new Vector2(0.56f, 0.92f));
        }

        private static void Stick(RectTransform a, float x, float y, string label)
        {
            var ring = Panel_(a, "Stick", PanelAlt, new Vector2(x - 0.1f, y - 0.16f), new Vector2(x + 0.1f, y + 0.16f));
            ring.gameObject.GetComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.25f, 0.33f);
            var knob = Panel_(ring, "Knob", Accent, new Vector2(0.3f, 0.32f), new Vector2(0.7f, 0.68f));
            Label(knob, label, 20, Vector2.zero, Vector2.one, Bg, TMPro.TextAlignmentOptions.Center,
                  TMPro.FontStyles.Bold);
        }

        private static void Phone(RectTransform a, float x, float y)
        {
            var body = Panel_(a, "Phone", PanelAlt, new Vector2(x - 0.18f, y - 0.16f), new Vector2(x + 0.18f, y + 0.22f));
            body.localRotation = Quaternion.Euler(0, 0, -8f);
            body.gameObject.GetComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.25f, 0.33f);
            Panel_(body, "Screen", new Color(0.1f, 0.4f, 0.6f), new Vector2(0.08f, 0.1f), new Vector2(0.92f, 0.9f));
            Label(a, "⟲  ⟳", 30, new Vector2(x - 0.2f, y + 0.22f), new Vector2(x + 0.2f, y + 0.36f),
                  Accent, TMPro.TextAlignmentOptions.Center);
        }
    }
}
