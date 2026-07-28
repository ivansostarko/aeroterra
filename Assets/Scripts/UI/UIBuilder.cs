using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using AeroTerra.Core;

namespace AeroTerra.UI
{
    /// <summary>
    /// Small immediate-style helper for constructing the whole UI in code.
    /// Keeps the project fully text-based (no scene/prefab binaries required)
    /// while producing a consistent, professional dark theme.
    /// </summary>
    public static class UIBuilder
    {
        public static readonly Color Bg       = new Color(0.055f, 0.07f, 0.10f, 0.97f);
        public static readonly Color Panel    = new Color(0.10f, 0.13f, 0.18f, 0.95f);
        public static readonly Color PanelAlt = new Color(0.13f, 0.17f, 0.23f, 1f);
        public static readonly Color Accent   = new Color(0.15f, 0.65f, 0.95f, 1f);
        public static readonly Color AccentWarn = new Color(0.95f, 0.45f, 0.15f, 1f);
        public static readonly Color TextMain = new Color(0.92f, 0.95f, 0.98f, 1f);
        public static readonly Color TextDim  = new Color(0.6f, 0.66f, 0.74f, 1f);

        private static TMP_FontAsset _customFont;
        private static bool _customFontChecked;

        /// <summary>Project-wide font hook: every TextMeshProUGUI in the game is built
        /// through Label() below, so dropping a TMP Font Asset at
        /// Assets/Resources/Fonts/AeroTerraFont.asset is the only step needed to change
        /// the game's typeface everywhere — no per-screen code changes. Falls back to
        /// TMP's default (LiberationSans SDF) when absent, which is the case out of the
        /// box: no custom font asset ships with this project today. Generating a TMP
        /// Font Asset from a .ttf/.otf requires the Editor's Font Asset Creator (Window ▸
        /// TextMeshPro ▸ Font Asset Creator) — not something that can be produced headlessly.</summary>
        private static TMP_FontAsset CustomFont()
        {
            if (!_customFontChecked)
            {
                _customFontChecked = true;
                _customFont = Resources.Load<TMP_FontAsset>("Fonts/AeroTerraFont");
            }
            return _customFont;
        }

        public static Canvas RootCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            return canvas;
        }

        public static RectTransform Panel_(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            return rt;
        }

        /// <summary>
        /// Masked viewport + scrollable content pair wired to a vertical-only
        /// ScrollRect (drag-to-scroll and scrollbar sync) PLUS a direct mouse-wheel
        /// driver — the shared primitive for any left-rail/sidebar list that can
        /// outgrow its window (drone rosters etc.). Callers add fixed-pixel-height
        /// rows as children of the returned content transform, top-anchored
        /// (anchorMin/Max.y = 1) with pixel offsets — e.g. via Panel_(content, name,
        /// color, new Vector2(x0,1), new Vector2(x1,1), new Vector2(0,-(topY+h)),
        /// new Vector2(0,-topY)) — then set content.sizeDelta = new Vector2(0,
        /// totalContentHeightPx) once every row has been added so the ScrollRect
        /// knows how far it can scroll.
        ///
        /// Wheel scrolling is deliberately NOT left to ScrollRect's own IScrollHandler
        /// path: this project's EventSystem is built with a bare
        /// InputSystemUIInputModule and no assigned InputActionAsset (see
        /// UIBuilder.RootCanvas), so whether a "Scroll Wheel" UI action ends up bound
        /// at all depends on that module's default-actions fallback — not something
        /// to depend on for a core interaction. WheelScroll instead polls
        /// Mouse.current directly (bypassing the UI event pipeline entirely) and a
        /// DragOnlyScrollRect subclass no-ops ScrollRect's own OnScroll so the two
        /// paths can't both fire and double-speed the same wheel notch. Drag-to-scroll
        /// and the scrollbar handle are unaffected — only OnScroll is neutered.
        /// </summary>
        public static (RectTransform viewport, RectTransform content, ScrollRect scrollRect) ScrollList(
            Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var viewport = Panel_(parent, name + "Viewport", Color.clear, anchorMin, anchorMax);
            viewport.gameObject.AddComponent<RectMask2D>();

            var contentGo = new GameObject(name + "Content", typeof(RectTransform));
            var content = (RectTransform)contentGo.transform;
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            var scrollRect = viewport.gameObject.AddComponent<DragOnlyScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var wheel = viewport.gameObject.AddComponent<WheelScroll>();
            wheel.Viewport = viewport;
            wheel.Content = content;
            return (viewport, content, scrollRect);
        }

        /// <summary>ScrollRect with wheel handling disabled — see ScrollList's remarks.
        /// Drag (OnBeginDrag/OnDrag/OnEndDrag) and scrollbar sync are untouched.</summary>
        private class DragOnlyScrollRect : ScrollRect
        {
            public override void OnScroll(PointerEventData eventData) { }
        }

        /// <summary>Reads Mouse.current.scroll directly (new Input System, no
        /// dependency on the EventSystem's UI action bindings) and moves Content by a
        /// fixed step per wheel notch whenever the pointer is over Viewport — see
        /// ScrollList's remarks for why. Sign/magnitude deliberately ignore the raw
        /// device delta value (platform/driver-dependent) in favor of a flat step per
        /// notch, so behavior is identical everywhere this ships.</summary>
        private class WheelScroll : MonoBehaviour
        {
            public RectTransform Viewport;
            public RectTransform Content;
            public float PixelsPerNotch = 50f;

            private void Update()
            {
                var mouse = Mouse.current;
                if (mouse == null || Viewport == null || Content == null) return;
                float dy = mouse.scroll.ReadValue().y;
                if (Mathf.Approximately(dy, 0f)) return;
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                        Viewport, mouse.position.ReadValue(), null)) return;

                float maxScroll = Mathf.Max(0f, Content.rect.height - Viewport.rect.height);
                if (maxScroll <= 0f) return;

                // Content is top-pivoted: anchoredPosition.y <= 0 once scrolled down.
                // Wheel "up" (dy > 0) should scroll toward the top (offsetDown shrinks).
                // Mathf.Sign, not the raw dy, per this class's own remarks above: the new
                // Input System's scroll delta magnitude is platform-dependent (observed
                // ~120 per notch on Windows vs. ~1 elsewhere) — multiplying that raw value
                // straight into PixelsPerNotch turned one wheel notch into an instant snap
                // to the top/bottom of the list instead of a small step, which is what made
                // this scroll feel broken.
                float offsetDown = Mathf.Clamp(-Content.anchoredPosition.y - Mathf.Sign(dy) * PixelsPerNotch, 0f, maxScroll);
                Content.anchoredPosition = new Vector2(Content.anchoredPosition.x, -offsetDown);
            }
        }

        /// <summary>Thin vertical scroll-position indicator for a ScrollList — assign
        /// the return value to scrollRect.verticalScrollbar (+ verticalScrollbarVisibility
        /// = Permanent) and Unity keeps its handle size/position in sync automatically.</summary>
        public static Scrollbar VScrollbar_(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = Panel_(parent, "Scrollbar", new Color(1f, 1f, 1f, 0.06f), anchorMin, anchorMax);
            var handle = Panel_(rt, "Handle", new Color(Accent.r, Accent.g, Accent.b, 0.7f), Vector2.zero, Vector2.one);

            var scrollbar = rt.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            return scrollbar;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size,
            Vector2 anchorMin, Vector2 anchorMax, Color? color = null,
            TextAlignmentOptions align = TextAlignmentOptions.Left, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject("Label", typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color ?? TextMain;
            t.alignment = align; t.fontStyle = style; t.enableWordWrapping = true;
            var font = CustomFont();
            if (font != null) t.font = font;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(8, 4); rt.offsetMax = new Vector2(-8, -4);
            return t;
        }

        public static Button Button_(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax,
            System.Action onClick, Color? bg = null, float fontSize = 26)
        {
            var rt = Panel_(parent, "Btn_" + text, bg ?? PanelAlt, anchorMin, anchorMax,
                            new Vector2(4, 4), new Vector2(-4, -4));
            var btn = rt.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.pressedColor = Accent;
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());
            btn.onClick.AddListener(() => AudioManager.Instance?.PlayButtonClick());

            var accentRt = Panel_(rt, "Accent", new Color(Accent.r, Accent.g, Accent.b, 0f),
                                   new Vector2(0, 0), new Vector2(0, 1));
            accentRt.pivot = new Vector2(0, 0.5f);
            accentRt.sizeDelta = new Vector2(4, 0);
            var accentImg = accentRt.GetComponent<Image>();

            var trigger = rt.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () =>
            {
                accentImg.color = Accent;
                AudioManager.Instance?.PlayButtonHover();
            });
            AddTrigger(trigger, EventTriggerType.PointerExit,
                       () => accentImg.color = new Color(Accent.r, Accent.g, Accent.b, 0f));

            Label(rt, text, fontSize, Vector2.zero, Vector2.one, TextMain, TextAlignmentOptions.Center);
            return btn;
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private static Texture2D _backIconWhite;
        private static bool _backIconWhiteChecked;

        /// <summary>
        /// Icon-only back/close button — every screen's back navigation goes through
        /// this one helper so the icon only needs to be swapped/restyled in one place.
        /// Uses the white icon everywhere since every screen in the game is dark-themed
        /// (Bg/Panel/PanelAlt are all near-black) — back_icon_dark.svg has no current
        /// use but is kept in Resources for a future light-background screen. Falls back
        /// to the old "&lt; BACK" text button if the icon hasn't been imported yet (e.g.
        /// still an unconverted .svg — Unity has no built-in SVG importer), so navigation
        /// never silently breaks.
        /// </summary>
        public static void BackButton_(Transform parent, Vector2 anchorMin, Vector2 anchorMax, System.Action onClick)
        {
            if (!_backIconWhiteChecked)
            {
                _backIconWhiteChecked = true;
                _backIconWhite = Resources.Load<Texture2D>("Images/ui/back_icon_white");
            }
            if (_backIconWhite == null)
            {
                Button_(parent, "< BACK", anchorMin, anchorMax, onClick);
                return;
            }

            var go = new GameObject("BackButton", typeof(RawImage));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<RawImage>();
            img.texture = _backIconWhite;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());
            btn.onClick.AddListener(() => AudioManager.Instance?.PlayButtonClick());

            var trigger = go.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => AudioManager.Instance?.PlayButtonHover());
        }

        /// <summary>
        /// Lightweight code-built dropdown: a closed button showing the current value
        /// that, on click, reveals a popup list of `options` rows (in the caller-given
        /// popupMin/popupMax rect, normalized within the same `parent`); clicking a row
        /// selects it and closes the popup, and a full-`parent` invisible catcher closes
        /// it on any click elsewhere. This is hand-rolled rather than Unity's
        /// Dropdown/TMP_Dropdown component (those need a pre-built template hierarchy)
        /// to match this project's fully code-driven UI, same as every other UIBuilder
        /// primitive.
        /// </summary>
        public static void Dropdown_<T>(Transform parent, Vector2 buttonMin, Vector2 buttonMax,
            Vector2 popupMin, Vector2 popupMax, T[] options, T current,
            System.Action<T> onPick, System.Func<T, string> labelFn = null)
        {
            string LabelOf(T v) => labelFn != null ? labelFn(v) : v.ToString();

            var btnRt = Panel_(parent, "Dropdown", PanelAlt, buttonMin, buttonMax);
            var valueLabel = Label(btnRt, LabelOf(current), 15, new Vector2(0.06f, 0f), new Vector2(0.85f, 1f),
                                   TextMain, TextAlignmentOptions.MidlineLeft);
            Label(btnRt, "▾", 15, new Vector2(0.85f, 0f), new Vector2(0.97f, 1f),
                  TextDim, TextAlignmentOptions.MidlineRight);

            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnRt.GetComponent<Image>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            btn.colors = btnColors;

            RectTransform popupRoot = null;
            GameObject catcher = null;

            void Close()
            {
                if (popupRoot != null) { Object.Destroy(popupRoot.gameObject); popupRoot = null; }
                if (catcher != null) { Object.Destroy(catcher); catcher = null; }
            }

            btn.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayButtonClick();
                if (popupRoot != null) { Close(); return; }

                // Invisible full-parent click-catcher UNDER the popup (created first, so
                // the popup — created after — renders and raycasts on top of it) closes
                // the list on any click outside a row.
                var catcherRt = Panel_(parent, "DropdownCatcher", Color.clear, Vector2.zero, Vector2.one);
                catcher = catcherRt.gameObject;
                var catchBtn = catcher.AddComponent<Button>();
                catchBtn.transition = Selectable.Transition.None;
                catchBtn.onClick.AddListener(Close);

                popupRoot = Panel_(parent, "DropdownPopup", PanelAlt, popupMin, popupMax);

                for (int i = 0; i < options.Length; i++)
                {
                    var opt = options[i];
                    bool selected = Equals(opt, current);
                    float y1 = 1f - (float)i / options.Length, y0 = 1f - (float)(i + 1) / options.Length;
                    var row = Panel_(popupRoot, "Opt_" + i, selected ? Accent : Color.clear,
                                      new Vector2(0f, y0), new Vector2(1f, y1));
                    Label(row, LabelOf(opt), 14, new Vector2(0.08f, 0f), new Vector2(0.96f, 1f),
                          selected ? TextMain : TextDim, TextAlignmentOptions.MidlineLeft);

                    var rowBtn = row.gameObject.AddComponent<Button>();
                    rowBtn.targetGraphic = row.GetComponent<Image>();
                    var rowColors = rowBtn.colors;
                    rowColors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
                    rowBtn.colors = rowColors;
                    rowBtn.onClick.AddListener(() =>
                    {
                        AudioManager.Instance?.PlayButtonClick();
                        current = opt;
                        valueLabel.text = LabelOf(opt);
                        Close();
                        onPick?.Invoke(opt);
                    });
                }
            });
        }

        public static Slider Slider_(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            float value, System.Action<float> onChanged)
        {
            var rt = Panel_(parent, "Slider", Color.clear, anchorMin, anchorMax);
            var bg = Panel_(rt, "BG", new Color(0, 0, 0, 0.5f), new Vector2(0, 0.4f), new Vector2(1, 0.6f));
            var fillArea = Panel_(rt, "FillArea", Color.clear, new Vector2(0, 0.4f), new Vector2(1, 0.6f));
            var fill = Panel_(fillArea, "Fill", Accent, Vector2.zero, Vector2.one);
            var handleArea = Panel_(rt, "HandleArea", Color.clear, Vector2.zero, Vector2.one);
            var handle = Panel_(handleArea, "Handle", TextMain, new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.85f));
            handle.sizeDelta = new Vector2(22, 0);

            var slider = rt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill; slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = value;
            slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));
            return slider;
        }

        public static Toggle Toggle_(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax,
            bool value, System.Action<bool> onChanged, float fontSize = 24)
        {
            var rt = Panel_(parent, "Toggle_" + label, Color.clear, anchorMin, anchorMax);
            var box = Panel_(rt, "Box", PanelAlt, new Vector2(0, 0.2f), new Vector2(0, 0.8f));
            box.sizeDelta = new Vector2(36, 0); box.anchoredPosition = new Vector2(24, 0);
            var check = Panel_(box, "Check", Accent, new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f));
            Label(rt, label, fontSize, new Vector2(0.12f, 0), Vector2.one, TextMain);

            var toggle = rt.gameObject.AddComponent<Toggle>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.isOn = value;
            toggle.onValueChanged.AddListener(v => onChanged?.Invoke(v));
            return toggle;
        }

        /// <summary>Pill-style on/off switch — a colored track with a sliding thumb that
        /// fills/moves on state change, far more visually distinct at a glance than
        /// Toggle_'s small checkbox. Same Toggle-component-driven shape/API as Toggle_
        /// (label left, control right, whole row clickable) so call sites read the same;
        /// use this wherever a toggle deserves more visual weight (e.g. Workshop loadout
        /// switches) and Toggle_ for dense settings lists.</summary>
        public static Toggle SwitchToggle_(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax,
            bool value, System.Action<bool> onChanged, float fontSize = 14)
        {
            var rt = Panel_(parent, "Switch_" + label, Color.clear, anchorMin, anchorMax);
            Label(rt, label, fontSize, new Vector2(0f, 0f), new Vector2(0.78f, 1f), TextMain,
                  TextAlignmentOptions.MidlineLeft);

            var track = Panel_(rt, "Track", value ? Accent : PanelAlt,
                                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-54, -13), new Vector2(-2, 13));
            var trackImg = track.GetComponent<Image>();
            var thumb = Panel_(track, "Thumb", new Color(0.95f, 0.97f, 1f, 1f),
                                new Vector2(value ? 0.55f : 0.05f, 0.14f), new Vector2(value ? 0.95f : 0.45f, 0.86f));

            var toggle = rt.gameObject.AddComponent<Toggle>();
            toggle.transition = Selectable.Transition.None; // visuals are hand-driven below, not Unity's default tint
            toggle.targetGraphic = trackImg;
            toggle.isOn = value;
            toggle.onValueChanged.AddListener(v =>
            {
                trackImg.color = v ? Accent : PanelAlt;
                thumb.anchorMin = new Vector2(v ? 0.55f : 0.05f, 0.14f);
                thumb.anchorMax = new Vector2(v ? 0.95f : 0.45f, 0.86f);
                onChanged?.Invoke(v);
            });
            return toggle;
        }

        /// <summary>Row of mutually exclusive option buttons; highlights selection.</summary>
        public static void OptionRow<T>(Transform parent, T[] options, T current,
            Vector2 anchorMin, Vector2 anchorMax, System.Action<T> onPick,
            System.Func<T, string> labelFn = null)
        {
            int n = options.Length;
            var buttons = new Button[n];
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                float x0 = anchorMin.x + (anchorMax.x - anchorMin.x) * i / n;
                float x1 = anchorMin.x + (anchorMax.x - anchorMin.x) * (i + 1) / n;
                bool selected = Equals(options[i], current);
                string lbl = labelFn != null ? labelFn(options[i]) : options[i].ToString();
                buttons[idx] = Button_(parent, lbl, new Vector2(x0, anchorMin.y), new Vector2(x1, anchorMax.y),
                    () =>
                    {
                        onPick(options[idx]);
                        for (int b = 0; b < n; b++)
                            buttons[b].GetComponent<Image>().color = b == idx ? Accent : PanelAlt;
                    },
                    selected ? Accent : PanelAlt, 20);
            }
        }

        /// <summary>Row of up to `max` small rating pips — the first `rating` filled with
        /// Accent, the rest dim. Used for drone spec categories (Speed/Payload/Range/...);
        /// deliberately plain rectangles rather than star glyphs, since TMP's default font
        /// asset isn't guaranteed to include a ★ glyph and a missing-glyph "tofu" box would
        /// look far worse than a clean segmented meter.</summary>
        public static void StarRow(Transform parent, int rating, int max, Vector2 anchorMin, Vector2 anchorMax)
        {
            var row = Panel_(parent, "Rating", Color.clear, anchorMin, anchorMax);
            const float gap = 0.06f;
            float cellW = (1f - gap * (max - 1)) / max;
            for (int i = 0; i < max; i++)
            {
                float x0 = i * (cellW + gap);
                bool filled = i < rating;
                Panel_(row, "Pip" + i, filled ? Accent : new Color(1, 1, 1, 0.12f),
                       new Vector2(x0, 0.1f), new Vector2(x0 + cellW, 0.9f));
            }
        }

        public static TMP_InputField Input_(Transform parent, string placeholder,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = Panel_(parent, "Input", PanelAlt, anchorMin, anchorMax);
            var field = rt.gameObject.AddComponent<TMP_InputField>();
            var textArea = Panel_(rt, "TextArea", Color.clear, Vector2.zero, Vector2.one,
                                  new Vector2(10, 6), new Vector2(-10, -6));
            var ph = Label(textArea, placeholder, 22, Vector2.zero, Vector2.one, TextDim);
            var txt = Label(textArea, "", 22, Vector2.zero, Vector2.one, TextMain);
            field.textViewport = textArea;
            field.placeholder = ph; field.textComponent = txt;
            return field;
        }
    }
}
