using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// Full procedural keyboard layout for Settings ▸ Controls' Keyboard-scheme
    /// diagram — every row of a standard keyboard, built from UIBuilder primitives
    /// (no imported keyboard image, per this project's text-only-assets rule). Keys
    /// actually bound to a game action (read live from InputManager, including any
    /// player rebind) light up in the accent color; hovering one shows its action and
    /// a short description in the strip below. Unbound keys stay dim/idle — the
    /// keyboard is complete, only the *marking* is data-driven.
    /// </summary>
    public static class KeyboardDiagram
    {
        private const string DefaultHint = "Hover a highlighted key to see what it does.";

        private struct KeyDef
        {
            public string Label, Token;
            public float Width;
            public KeyDef(string label, string token, float width = 1f) { Label = label; Token = token; Width = width; }
        }

        public static void Draw(RectTransform area)
        {
            TextMeshProUGUI hint = Label(area, DefaultHint, 13, new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.135f),
                                          TextDim, TMPro.TextAlignmentOptions.Center);

            KeyDef[][] rows =
            {
                new[]
                {
                    new KeyDef("Esc", "escape", 1.3f),
                    new KeyDef("F1", "f1"), new KeyDef("F2", "f2"), new KeyDef("F3", "f3"), new KeyDef("F4", "f4"),
                    new KeyDef("F5", "f5"), new KeyDef("F6", "f6"), new KeyDef("F7", "f7"), new KeyDef("F8", "f8"),
                    new KeyDef("F9", "f9"), new KeyDef("F10", "f10"), new KeyDef("F11", "f11"), new KeyDef("F12", "f12"),
                },
                new[]
                {
                    new KeyDef("1", "1"), new KeyDef("2", "2"), new KeyDef("3", "3"), new KeyDef("4", "4"),
                    new KeyDef("5", "5"), new KeyDef("6", "6"), new KeyDef("7", "7"), new KeyDef("8", "8"),
                    new KeyDef("9", "9"), new KeyDef("0", "0"), new KeyDef("-", "minus"), new KeyDef("=", "equals"),
                    new KeyDef("⌫", "backspace", 1.8f),
                },
                new[]
                {
                    new KeyDef("Tab", "tab", 1.5f),
                    new KeyDef("Q", "q"), new KeyDef("W", "w"), new KeyDef("E", "e"), new KeyDef("R", "r"),
                    new KeyDef("T", "t"), new KeyDef("Y", "y"), new KeyDef("U", "u"), new KeyDef("I", "i"),
                    new KeyDef("O", "o"), new KeyDef("P", "p"), new KeyDef("[", "leftBracket"), new KeyDef("]", "rightBracket"),
                    new KeyDef("\\", "backslash", 1.3f),
                },
                new[]
                {
                    new KeyDef("Caps", "capsLock", 1.8f),
                    new KeyDef("A", "a"), new KeyDef("S", "s"), new KeyDef("D", "d"), new KeyDef("F", "f"),
                    new KeyDef("G", "g"), new KeyDef("H", "h"), new KeyDef("J", "j"), new KeyDef("K", "k"),
                    new KeyDef("L", "l"), new KeyDef(";", "semicolon"), new KeyDef("'", "quote"),
                    new KeyDef("⏎", "enter", 2.0f),
                },
                new[]
                {
                    new KeyDef("Shift", "leftShift", 2.3f),
                    new KeyDef("Z", "z"), new KeyDef("X", "x"), new KeyDef("C", "c"), new KeyDef("V", "v"),
                    new KeyDef("B", "b"), new KeyDef("N", "n"), new KeyDef("M", "m"), new KeyDef(",", "comma"),
                    new KeyDef(".", "period"), new KeyDef("/", "slash"),
                    new KeyDef("Shift", "rightShift", 2.3f),
                },
                new[]
                {
                    new KeyDef("Ctrl", "leftCtrl", 1.5f), new KeyDef("Alt", "leftAlt", 1.3f),
                    new KeyDef("Space", "space", 6.2f),
                    new KeyDef("Alt", "rightAlt", 1.3f), new KeyDef("Ctrl", "rightCtrl", 1.5f),
                },
                new[]
                {
                    new KeyDef("", "", 9f), // spacer so the arrow cluster sits at the right, like a real board
                    new KeyDef("◀", "leftArrow"), new KeyDef("▲", "upArrow"), new KeyDef("▼", "downArrow"), new KeyDef("▶", "rightArrow"),
                },
            };

            const float rowsTop = 1f, rowsBottom = 0.16f;
            float rowH = (rowsTop - rowsBottom) / rows.Length;
            for (int r = 0; r < rows.Length; r++)
            {
                float y1 = rowsTop - r * rowH, y0 = y1 - rowH;
                DrawRow(area, rows[r], y0, y1, hint);
            }
        }

        private static void DrawRow(RectTransform area, KeyDef[] row, float y0, float y1, TextMeshProUGUI hint)
        {
            float totalUnits = 0f;
            foreach (var k in row) totalUnits += k.Width;
            float unitW = 0.96f / totalUnits;
            float x = 0.02f;
            foreach (var k in row)
            {
                float w = k.Width * unitW;
                if (!string.IsNullOrEmpty(k.Token))
                    DrawKey(area, k.Label, k.Token, x, x + w, y0, y1, hint);
                x += w;
            }
        }

        private static void DrawKey(RectTransform area, string label, string token,
                                     float x0, float x1, float y0, float y1, TextMeshProUGUI hint)
        {
            var (actionLabel, desc) = FindBinding(token);
            bool bound = actionLabel != null;
            var bg = bound ? new Color(Accent.r, Accent.g, Accent.b, 0.9f) : new Color(0.15f, 0.18f, 0.23f, 1f);
            var rt = Panel_(area, "Key_" + label, bg, new Vector2(x0, y0), new Vector2(x1, y1),
                             new Vector2(2, 2), new Vector2(-2, -2));
            Label(rt, label, 12, Vector2.zero, Vector2.one, bound ? Bg : TextDim,
                  TMPro.TextAlignmentOptions.Center, bound ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal);

            if (!bound) return;
            var trigger = rt.gameObject.AddComponent<EventTrigger>();
            AddHover(trigger, EventTriggerType.PointerEnter,
                     () => { if (hint != null) hint.text = $"<b>{label}</b>  —  {actionLabel}: {desc}"; });
            AddHover(trigger, EventTriggerType.PointerExit,
                     () => { if (hint != null) hint.text = DefaultHint; });
        }

        private static void AddHover(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        /// <summary>Finds which action (if any) is currently bound to this physical
        /// keyboard key — reads InputAction.bindings' effectivePath (override-aware, so
        /// a player rebind is reflected here too) rather than duplicating a static
        /// key-to-action table that could drift out of sync with InputManager.</summary>
        private static (string action, string desc) FindBinding(string token)
        {
            var im = AeroTerra.Input.InputManager.Instance;
            if (im == null || string.IsNullOrEmpty(token)) return (null, null);
            string want = "<keyboard>/" + token.ToLowerInvariant();
            foreach (var action in im.AllActions())
            {
                if (action == null) continue;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var b = action.bindings[i];
                    if (b.isComposite) continue;
                    if (string.IsNullOrEmpty(b.effectivePath)) continue;
                    if (!string.Equals(b.effectivePath, want, System.StringComparison.OrdinalIgnoreCase)) continue;
                    return (ActionLabel(action.name, b.isPartOfComposite, b.name), DescriptionFor(action.name));
                }
            }
            return (null, null);
        }

        private static string ActionLabel(string actionName, bool isPartOfComposite, string partName)
        {
            string baseName = FriendlyName(actionName);
            if (!isPartOfComposite) return baseName;
            string dir = (actionName, partName?.ToLowerInvariant()) switch
            {
                ("Throttle", "positive") => "Up",
                ("Throttle", "negative") => "Down",
                ("Pitch", "positive") => "Forward",
                ("Pitch", "negative") => "Back",
                ("Roll", "positive") => "Right",
                ("Roll", "negative") => "Left",
                _ => partName,
            };
            return $"{baseName} ({dir})";
        }

        private static string FriendlyName(string actionName) => actionName switch
        {
            "PayloadDrop" => "Payload Drop",
            "SmokeScreen" => "Smoke Screen",
            "PhotoMode" => "Photo Mode",
            "DroneFlip" => "Drone Flip",
            "Parachute" => "Parachute",
            _ => actionName,
        };

        private static string DescriptionFor(string actionName) => actionName switch
        {
            "Throttle" => "Climb/descend (multirotor & VTOL) or engine power trim (fixed-wing).",
            "Pitch" => "Tilt forward/back to fly forward or brake back.",
            "Roll" => "Bank left/right to strafe or turn.",
            "Camera" => "Cycle chase default → chase details → front → bottom → thermal.",
            "Reset" => "Teleport back to this flight's spawn point.",
            "PayloadDrop" => "Release the next payload store.",
            "Boost" => "Snap to full throttle for a very-fast burst.",
            "Brake" => "Cut all motor thrust and drop (fixed-wing: airbrake).",
            "SmokeScreen" => "Toggle the trailing smoke screen, if equipped in the Workshop.",
            "Screenshot" => "Save a screenshot to Media.",
            "Replay" => "Play back the last ~90 seconds of flight.",
            "PhotoMode" => "Detach into a free-fly photo camera.",
            "DroneFlip" => "Perform a scripted barrel-roll flip trick.",
            "Parachute" => "Deploy the parachute (above 100 m, if equipped in the Workshop) for a slow, controlled descent.",
            "Pause" => "Pause the flight.",
            _ => "",
        };
    }
}
