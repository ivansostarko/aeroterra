using UnityEngine;
using AeroTerra.Core;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// "How to play" graphic per control scheme, drawn with UI primitives — a
    /// stylized gamepad and a tilting phone for gyroscope. Keyboard has its own,
    /// much more detailed diagram — see KeyboardDiagram.
    /// </summary>
    public static class ControlsDiagram
    {
        public static void Draw(RectTransform area, ControlScheme scheme)
        {
            switch (scheme)
            {
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
