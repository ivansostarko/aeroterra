using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using AeroTerra.Core;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// Settings window: vertical tab sidebar — GAME · FLYING CONDITIONS · VIDEO · AUDIO ·
    /// MAP · CONTROLS · KEY BINDINGS. Every change is live-applied and persisted
    /// immediately; SAVE at the top re-applies everything at once as an explicit,
    /// visible confirmation. Usable both from the main menu and the in-flight pause menu.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        private const int TabGame = 0, TabConditions = 1, TabVideo = 2, TabAudio = 3, TabMap = 4, TabControls = 5, TabKeyBindings = 6;
        private static readonly string[] TabNames =
            { "GAME", "FLYING CONDITIONS", "VIDEO", "AUDIO", "MAP", "CONTROLS", "KEY BINDINGS" };

        private RectTransform _root, _content;
        private System.Action _onBack;
        private Canvas _ownCanvas;
        private int _tab;
        private InputActionRebindingExtensions.RebindingOperation _rebindOp;
        private RectTransform _resolutionPopup;
        private TMPro.TextMeshProUGUI _savedLabel;
        private float _savedFlashTimer;

        private Canvas Canvas
        {
            get
            {
                var menu = GetComponent<MainMenuUI>();
                if (menu != null) return menu.Canvas;
                if (_ownCanvas == null) _ownCanvas = RootCanvas("SettingsCanvas");
                return _ownCanvas;
            }
        }

        public bool IsOpen => _root != null;

        public void Open(System.Action onBack)
        {
            _onBack = onBack;
            Build();
        }

        private void Update()
        {
            if (_root == null) return;
            var im = AeroTerra.Input.InputManager.Instance;
            if (im != null && im.PauseAction.WasPressedThisFrame()) GoBack();

            if (_savedFlashTimer > 0f)
            {
                _savedFlashTimer -= Time.unscaledDeltaTime;
                if (_savedFlashTimer <= 0f && _savedLabel != null) _savedLabel.gameObject.SetActive(false);
            }
        }

        private void GoBack()
        {
            GameManager.Instance.SaveSettings();
            Clear();
            _onBack?.Invoke();
        }

        // ---------------- Layout: vertical sidebar + content ----------------
        private void Build()
        {
            Clear();
            _root = Panel_(Canvas.transform, "Settings", Bg, Vector2.zero, Vector2.one);

            _root.gameObject.AddComponent<BackgroundSlider>().Init(_root,
                new[] { "Images/Backgrounds/main-menu/slider_1" });
            Panel_(_root, "Scrim", new Color(0f, 0f, 0f, 0.55f), Vector2.zero, Vector2.one);

            BackButton_(_root, new Vector2(0.02f, 0.90f), new Vector2(0.075f, 0.965f), GoBack);
            Label(_root, "SETTINGS", 40, new Vector2(0.09f, 0.90f), new Vector2(0.55f, 0.98f),
                  TextMain, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);

            Button_(_root, "SAVE", new Vector2(0.82f, 0.905f), new Vector2(0.97f, 0.975f), () =>
            {
                GameManager.Instance.SaveSettings();
                ApplyEverything();
                _savedFlashTimer = 1.6f;
                if (_savedLabel != null) _savedLabel.gameObject.SetActive(true);
            }, Accent, 20);
            _savedLabel = Label(_root, "✓ SAVED", 16, new Vector2(0.82f, 0.865f), new Vector2(0.97f, 0.90f),
                                Accent, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            _savedLabel.gameObject.SetActive(_savedFlashTimer > 0f);

            // Vertical tab sidebar
            const float sideX0 = 0.03f, sideX1 = 0.22f, sideTop = 0.84f, sideBottom = 0.12f;
            float rowH = (sideTop - sideBottom) / TabNames.Length;
            for (int i = 0; i < TabNames.Length; i++)
            {
                int idx = i;
                float y1 = sideTop - i * rowH;
                float y0 = y1 - rowH + 0.012f;
                Button_(_root, TabNames[i], new Vector2(sideX0, y0), new Vector2(sideX1, y1),
                        () => { _tab = idx; Build(); }, _tab == idx ? Accent : PanelAlt, 18);
            }

            _content = Panel_(_root, "Content", Panel, new Vector2(0.25f, 0.10f), new Vector2(0.97f, 0.86f));

            if (_tab == TabConditions)
            {
                // Wind/Temperature/Humidity sliders on top of Sky/Weather outgrew one
                // screen's worth of space — scrollable, same ScrollList primitive
                // Workshop's Loadout tab uses. _content is reassigned to the taller
                // scroll content for the rest of this Build() call so BuildConditions()
                // needs no parameter — it already just writes through the _content field.
                var (viewport, scrollContent, _) = ScrollList(_content, "ConditionsScroll", Vector2.zero, Vector2.one);
                scrollContent.sizeDelta = new Vector2(0f, viewport.rect.height * 1.5f);
                _content = scrollContent;
                BuildConditions();
                return;
            }

            switch (_tab)
            {
                case TabGame: BuildGame(); break;
                case TabVideo: BuildVideo(); break;
                case TabAudio: BuildAudio(); break;
                case TabMap: BuildMap(); break;
                case TabControls: BuildControls(); break;
                case TabKeyBindings: BuildKeyBindings(); break;
            }
        }

        /// <summary>Re-applies every settings category at once — what the SAVE button guarantees.</summary>
        private void ApplyEverything()
        {
            var s = GameManager.Instance.Settings;
            VideoSettingsApplier.ApplyAll(s);
            AudioManager.Instance?.ApplyMasterVolume(s.MasterVolume);
            AudioManager.Instance?.ApplyMusicVolume(s.MusicVolume);
            AudioManager.Instance?.ApplySfxVolume(s.SfxVolume);
            AudioManager.Instance?.ApplyVoiceVolume(s.VoiceVolume);
            Map.MapManager.Instance?.ApplyMapSettings();
            Map.SkySystem.Instance?.Apply(s.Sky);
            Map.WeatherSystem.Instance?.Apply(s.Weather);
            Map.WeatherSystem.Instance?.ApplyEffectsQuality(s.Effects);
            FlightHUD.Instance?.SetVisible(s.ShowHud);
            FlightHUD.Instance?.ApplyHudElementSettings();
        }

        // ---------------- GAME ----------------
        private void BuildGame()
        {
            var s = GameManager.Instance.Settings;

            Toggle_(_content, "Show HUD", new Vector2(0.04f, 0.90f), new Vector2(0.5f, 0.97f),
                    s.ShowHud, v =>
                    {
                        s.ShowHud = v;
                        FlightHUD.Instance?.SetVisible(v);
                        GameManager.Instance.SaveSettings();
                    }, 22);
            Label(_content, "Master switch — turns the entire in-flight HUD on or off.", 13,
                  new Vector2(0.09f, 0.855f), new Vector2(0.9f, 0.885f), TextDim);

            Panel_(_content, "Divider", new Color(Accent.r, Accent.g, Accent.b, 0.3f),
                   new Vector2(0.04f, 0.825f), new Vector2(0.96f, 0.828f));

            Label(_content, "HUD ELEMENTS", 18, new Vector2(0.04f, 0.775f), new Vector2(0.6f, 0.815f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            Label(_content, "Show or hide individual instruments in flight — every element is on by default.", 13,
                  new Vector2(0.04f, 0.74f), new Vector2(0.96f, 0.775f), TextDim);

            var group = Panel_(_content, "HudElementsGroup", PanelAlt, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.725f));

            var elements = new (string label, bool value, System.Action<bool> set)[]
            {
                ("Speed",                    s.HudShowSpeed,       v => s.HudShowSpeed = v),
                ("Altitude",                 s.HudShowAltitude,    v => s.HudShowAltitude = v),
                ("GPS Coordinates",          s.HudShowGps,         v => s.HudShowGps = v),
                ("Battery / Fuel",           s.HudShowBattery,     v => s.HudShowBattery = v),
                ("Narrator (voice & text)",  s.HudShowNarrator,    v => s.HudShowNarrator = v),
                ("Payload Indicator",        s.HudShowPayload,     v => s.HudShowPayload = v),
                ("Compass",                  s.HudShowCompass,     v => s.HudShowCompass = v),
                ("Mini Map",                 s.HudShowMinimap,     v => s.HudShowMinimap = v),
                ("Wind",                     s.HudShowWind,        v => s.HudShowWind = v),
                ("Throttle",                 s.HudShowThrottle,    v => s.HudShowThrottle = v),
                ("Temperature",              s.HudShowTemperature, v => s.HudShowTemperature = v),
            };

            const int cols = 2;
            int rows = Mathf.CeilToInt(elements.Length / (float)cols);
            float colW = 1f / cols;
            float rowH = 1f / rows;
            for (int i = 0; i < elements.Length; i++)
            {
                int col = i % cols, row = i / cols;
                float x0 = col * colW + 0.03f, x1 = (col + 1) * colW - 0.03f;
                float y1 = 1f - row * rowH - 0.02f, y0 = 1f - (row + 1) * rowH + 0.02f;
                var (label, value, set) = elements[i];
                Toggle_(group, label, new Vector2(x0, y0), new Vector2(x1, y1), value, v =>
                {
                    set(v);
                    GameManager.Instance.SaveSettings();
                    FlightHUD.Instance?.ApplyHudElementSettings();
                }, 17);
            }
        }

        // ---------------- FLYING CONDITIONS ----------------
        // Sky / Weather / Wind used to live in GAME — split out into their own tab since
        // they'd outgrown sharing space with Show HUD, then grew further (Temperature,
        // Humidity, and Wind's redesign from an on/off override into a plain slider).
        // Same SettingsData fields, same appliers; FreeFlightMenuUI's own Flying
        // Conditions screen (shown right before launching a flight) reads/writes these
        // same fields independently, so this tab doesn't touch that screen — see its
        // BuildConditionsScreen (it keeps its own read-only Wind readout, no
        // Temperature/Humidity controls there — this task only asked for Settings).
        private void BuildConditions()
        {
            var s = GameManager.Instance.Settings;

            Label(_content, "SKY", 22, new Vector2(0.04f, 0.90f), new Vector2(0.3f, 0.96f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            OptionRow(_content, new[] { SkyPreset.Day, SkyPreset.Dawn, SkyPreset.Dusk, SkyPreset.Night },
                s.Sky, new Vector2(0.04f, 0.83f), new Vector2(0.96f, 0.89f),
                sky => { s.Sky = sky; GameManager.Instance.SaveSettings(); Map.SkySystem.Instance?.Apply(sky); });

            Label(_content, "WEATHER", 22, new Vector2(0.04f, 0.73f), new Vector2(0.4f, 0.79f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            // Picking a weather type resets Wind/Temperature/Humidity below to that
            // preset's typical values (a full Build() refreshes every slider's position
            // + label to match) — each stays freely adjustable afterward, this is a
            // sensible starting point per weather, not a lock.
            OptionRow(_content,
                new[] { WeatherPreset.Clear, WeatherPreset.Cloudy, WeatherPreset.Rain,
                        WeatherPreset.Storm, WeatherPreset.Fog, WeatherPreset.Snow },
                s.Weather, new Vector2(0.04f, 0.66f), new Vector2(0.96f, 0.72f),
                w =>
                {
                    s.Weather = w;
                    s.WindSpeedMs = Map.WeatherSystem.BaseWindSpeedMs(w);
                    s.TemperatureC = Map.WeatherSystem.BaseTemperatureC(w);
                    s.HumidityPercent = Map.WeatherSystem.BaseHumidityPercent(w);
                    GameManager.Instance.SaveSettings();
                    Map.WeatherSystem.Instance?.Apply(w);
                    Build();
                });

            Label(_content, "WIND", 22, new Vector2(0.04f, 0.58f), new Vector2(0.3f, 0.64f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            var windValueLabel = Label(_content, "", 16, new Vector2(0.3f, 0.58f), new Vector2(0.96f, 0.64f),
                                       TextMain, TMPro.TextAlignmentOptions.Right);
            var windTrack = Panel_(_content, "WindTrack", PanelAlt, new Vector2(0.04f, 0.555f), new Vector2(0.96f, 0.575f));
            var windFill = Panel_(windTrack, "WindFill", Accent, Vector2.zero, new Vector2(0f, 1f));

            // WeatherSystem.Update() polls WindSpeedMs live every frame (same pull
            // pattern DroneFlightController already uses for InvertPitch), so this
            // callback only needs to persist the setting — no separate apply call.
            SliderRow("WIND SPEED", Mathf.InverseLerp(0f, WindMaxMs, s.WindSpeedMs), 0.50f,
                new Vector2(0.04f, 0.96f),
                v =>
                {
                    s.WindSpeedMs = Mathf.Lerp(0f, WindMaxMs, v);
                    GameManager.Instance.SaveSettings();
                    SetWindRow(windValueLabel, windFill, s);
                },
                v => $"{Mathf.Lerp(0f, WindMaxMs, v):0.0} m/s");
            SetWindRow(windValueLabel, windFill, s);

            // Temperature feeds BatterySystem.PerformanceFactor in flight — too cold or
            // too hot and battery-powered airframes lose thrust ceiling (fuel-powered
            // ones are unaffected). Purely descriptive for now on the Humidity side.
            SliderRow("TEMPERATURE", Mathf.InverseLerp(MinTemperatureC, MaxTemperatureC, s.TemperatureC), 0.36f,
                new Vector2(0.04f, 0.96f),
                v =>
                {
                    s.TemperatureC = Mathf.Lerp(MinTemperatureC, MaxTemperatureC, v);
                    GameManager.Instance.SaveSettings();
                },
                v => $"{Mathf.Lerp(MinTemperatureC, MaxTemperatureC, v):0}°C");

            SliderRow("HUMIDITY", s.HumidityPercent / 100f, 0.22f, new Vector2(0.04f, 0.96f),
                v =>
                {
                    s.HumidityPercent = v * 100f;
                    GameManager.Instance.SaveSettings();
                },
                v => $"{v * 100f:0}%");
        }

        /// <summary>Wind readout — a plain live figure now (Wind is a single free slider,
        /// no on/off override concept anymore). Shared visual pattern with
        /// FreeFlightMenuUI's Flying Conditions screen (its own private copy).</summary>
        private const float MaxWindMeterMs = 10.5f;
        private const float WindMaxMs = 15f;
        private const float MinTemperatureC = -20f, MaxTemperatureC = 50f;

        private static void SetWindRow(TMPro.TextMeshProUGUI valueLabel, RectTransform fill, SettingsData s)
        {
            if (valueLabel == null || fill == null) return;
            valueLabel.text = $"{s.WindSpeedMs:0.0} m/s";
            fill.anchorMax = new Vector2(Mathf.Clamp01(s.WindSpeedMs / Mathf.Max(MaxWindMeterMs, WindMaxMs)), 1f);
        }

        // ---------------- VIDEO ----------------
        private void BuildVideo()
        {
            var s = GameManager.Instance.Settings;

            Label(_content, "WINDOW MODE", 18, new Vector2(0.04f, 0.855f), new Vector2(0.46f, 0.90f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            OptionRow(_content, new[] { WindowMode.Windowed, WindowMode.Fullscreen }, s.WindowMode,
                new Vector2(0.04f, 0.80f), new Vector2(0.46f, 0.855f),
                m => { s.WindowMode = m; SaveAndApplyVideo(s); },
                m => m == WindowMode.Fullscreen ? "FULLSCREEN" : "WINDOWED");

            Label(_content, "RESOLUTION", 18, new Vector2(0.52f, 0.855f), new Vector2(0.96f, 0.90f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            BuildResolutionDropdown(new Vector2(0.52f, 0.80f), new Vector2(0.96f, 0.855f));

            Label(_content, "FRAME RATE LIMIT", 18, new Vector2(0.04f, 0.735f), new Vector2(0.5f, 0.78f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            OptionRow(_content, new[] { 30, 60, 90, 120, 144, -1 }, s.FrameRateLimit,
                new Vector2(0.04f, 0.68f), new Vector2(0.96f, 0.735f),
                fps => { s.FrameRateLimit = fps; SaveAndApplyVideo(s); },
                fps => fps <= 0 ? "UNCAPPED" : $"{fps}");

            Label(_content, "GRAPHICS QUALITY", 18, new Vector2(0.04f, 0.615f), new Vector2(0.46f, 0.66f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            OptionRow(_content, new[] { GraphicsQuality.Low, GraphicsQuality.Medium, GraphicsQuality.High, GraphicsQuality.Ultra },
                s.Quality, new Vector2(0.04f, 0.56f), new Vector2(0.46f, 0.615f),
                q => { s.Quality = q; SaveAndApplyVideo(s); }, q => q.ToString().ToUpperInvariant());

            Label(_content, "ANTI-ALIASING", 18, new Vector2(0.52f, 0.615f), new Vector2(0.96f, 0.66f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            OptionRow(_content, new[] { AntiAliasingMode.Off, AntiAliasingMode.FXAA, AntiAliasingMode.SMAA, AntiAliasingMode.TAA },
                s.AntiAliasing, new Vector2(0.52f, 0.56f), new Vector2(0.96f, 0.615f),
                a => { s.AntiAliasing = a; SaveAndApplyVideo(s); }, a => a.ToString().ToUpperInvariant());

            SliderRow("RENDER SCALE", Mathf.InverseLerp(0.5f, 2f, s.RenderScale), 0.475f, new Vector2(0.04f, 0.96f),
                v => { s.RenderScale = Mathf.Lerp(0.5f, 2f, v); SaveAndApplyVideo(s); },
                v => $"{Mathf.Lerp(0.5f, 2f, v):0.00}x");

            SliderRow("VIEW DISTANCE", s.ViewDistance01, 0.35f, new Vector2(0.04f, 0.96f),
                v => { s.ViewDistance01 = v; GameManager.Instance.SaveSettings(); Map.MapManager.Instance?.ApplyViewDistance(v); });

            Label(_content, "SHADOWS", 18, new Vector2(0.04f, 0.225f), new Vector2(0.46f, 0.27f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            OptionRow(_content, new[] { ShadowDetail.Off, ShadowDetail.Low, ShadowDetail.Medium, ShadowDetail.High },
                s.Shadows, new Vector2(0.04f, 0.17f), new Vector2(0.46f, 0.225f),
                sh => { s.Shadows = sh; SaveAndApplyVideo(s); }, sh => sh.ToString().ToUpperInvariant());

            Label(_content, "TEXTURES", 18, new Vector2(0.52f, 0.225f), new Vector2(0.96f, 0.27f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            OptionRow(_content, new[] { TextureQuality.Low, TextureQuality.Medium, TextureQuality.High, TextureQuality.Full },
                s.Textures, new Vector2(0.52f, 0.17f), new Vector2(0.96f, 0.225f),
                t => { s.Textures = t; SaveAndApplyVideo(s); }, t => t.ToString().ToUpperInvariant());

            Label(_content, "EFFECTS QUALITY", 18, new Vector2(0.04f, 0.105f), new Vector2(0.5f, 0.15f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            OptionRow(_content, new[] { EffectsQuality.Low, EffectsQuality.Medium, EffectsQuality.High },
                s.Effects, new Vector2(0.04f, 0.05f), new Vector2(0.46f, 0.105f),
                e => { s.Effects = e; GameManager.Instance.SaveSettings(); Map.WeatherSystem.Instance?.ApplyEffectsQuality(e); },
                e => e.ToString().ToUpperInvariant());
        }

        private void SaveAndApplyVideo(SettingsData s)
        {
            GameManager.Instance.SaveSettings();
            VideoSettingsApplier.ApplyAll(s);
        }

        /// <summary>Lightweight dropdown: a closed button that expands a popup list (drawn as the
        /// last sibling of _root so it renders above every other control) on click.</summary>
        private void BuildResolutionDropdown(Vector2 anchorMin, Vector2 anchorMax)
        {
            var s = GameManager.Instance.Settings;
            var resolutions = VideoSettingsApplier.AvailableResolutions();
            if (resolutions.Length == 0) resolutions = new[] { (s.ResolutionWidth, s.ResolutionHeight) };
            int current = System.Array.FindIndex(resolutions, r => r.width == s.ResolutionWidth && r.height == s.ResolutionHeight);
            if (current < 0) current = 0;

            var closed = Panel_(_content, "ResDropdown", PanelAlt, anchorMin, anchorMax);
            var label = Label(closed, $"{resolutions[current].width} × {resolutions[current].height}   ▾", 18,
                              Vector2.zero, Vector2.one, TextMain, TMPro.TextAlignmentOptions.Center);
            var btn = closed.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.onClick.AddListener(() => AudioManager.Instance?.PlayButtonClick());
            btn.onClick.AddListener(() => ToggleResolutionPopup(closed, resolutions, label, s));
            AddHoverSfx(closed);
        }

        private void ToggleResolutionPopup(RectTransform closed, (int width, int height)[] resolutions,
                                            TMPro.TextMeshProUGUI label, SettingsData s)
        {
            if (_resolutionPopup != null) { Destroy(_resolutionPopup.gameObject); _resolutionPopup = null; return; }

            var corners = new Vector3[4];
            closed.GetWorldCorners(corners); // [0]=bottom-left [3]=bottom-right, world space

            _resolutionPopup = Panel_(_root, "ResPopup", Panel, Vector2.zero, Vector2.zero);
            _resolutionPopup.pivot = new Vector2(0f, 1f);
            _resolutionPopup.position = corners[0];
            float width = corners[3].x - corners[0].x;
            int shown = Mathf.Min(resolutions.Length, 10);
            const float rowHeightPx = 30f;
            _resolutionPopup.sizeDelta = new Vector2(width, rowHeightPx * shown);

            for (int i = 0; i < shown; i++)
            {
                int idx = i;
                float y0 = 1f - (i + 1) / (float)shown, y1 = 1f - i / (float)shown;
                var row = Panel_(_resolutionPopup, "Item", PanelAlt, new Vector2(0, y0), new Vector2(1, y1));
                var rowBtn = row.gameObject.AddComponent<UnityEngine.UI.Button>();
                Label(row, $"{resolutions[idx].width} × {resolutions[idx].height}", 16,
                      Vector2.zero, Vector2.one, TextMain, TMPro.TextAlignmentOptions.Center);
                AddHoverSfx(row);
                rowBtn.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlayButtonClick();
                    s.ResolutionWidth = resolutions[idx].width;
                    s.ResolutionHeight = resolutions[idx].height;
                    label.text = $"{resolutions[idx].width} × {resolutions[idx].height}   ▾";
                    SaveAndApplyVideo(s);
                    Destroy(_resolutionPopup.gameObject);
                    _resolutionPopup = null;
                });
            }
        }

        /// <summary>Shared label+value+slider row used by both AUDIO and VIDEO tabs.</summary>
        private void SliderRow(string label, float value01, float yTop, Vector2 xRange,
                               System.Action<float> onChange, System.Func<float, string> format = null)
        {
            format ??= v => $"{v * 100f:0}%";
            float mid = xRange.x + (xRange.y - xRange.x) * 0.6f;
            Label(_content, label, 18, new Vector2(xRange.x, yTop), new Vector2(mid, yTop + 0.05f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            var valueLabel = Label(_content, format(value01), 16, new Vector2(mid, yTop), new Vector2(xRange.y, yTop + 0.05f),
                                   TextMain, TMPro.TextAlignmentOptions.Right);
            Slider_(_content, new Vector2(xRange.x, yTop - 0.05f), new Vector2(xRange.y, yTop - 0.005f), value01, v =>
            {
                valueLabel.text = format(v);
                onChange(v);
            });
        }

        // ---------------- AUDIO ----------------
        private void BuildAudio()
        {
            var s = GameManager.Instance.Settings;
            var am = AudioManager.Instance;

            Label(_content, "VOLUME", 20, new Vector2(0.04f, 0.91f), new Vector2(0.48f, 0.97f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            SliderRow("MASTER", s.MasterVolume, 0.82f, new Vector2(0.04f, 0.48f),
                v => { s.MasterVolume = v; am?.ApplyMasterVolume(v); GameManager.Instance.SaveSettings(); });
            SliderRow("MUSIC", s.MusicVolume, 0.66f, new Vector2(0.04f, 0.48f),
                v => { s.MusicVolume = v; am?.ApplyMusicVolume(v); GameManager.Instance.SaveSettings(); });
            SliderRow("SFX", s.SfxVolume, 0.50f, new Vector2(0.04f, 0.48f),
                v => { s.SfxVolume = v; am?.ApplySfxVolume(v); GameManager.Instance.SaveSettings(); });
            SliderRow("VOICE", s.VoiceVolume, 0.34f, new Vector2(0.04f, 0.48f),
                v => { s.VoiceVolume = v; am?.ApplyVoiceVolume(v); GameManager.Instance.SaveSettings(); });

            Panel_(_content, "Divider", new Color(Accent.r, Accent.g, Accent.b, 0.3f),
                   new Vector2(0.505f, 0.05f), new Vector2(0.51f, 0.95f));

            Label(_content, "LANGUAGE", 20, new Vector2(0.55f, 0.91f), new Vector2(0.96f, 0.97f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);

            Label(_content, "Audio", 16, new Vector2(0.55f, 0.83f), new Vector2(0.96f, 0.89f), TextDim);
            OptionRow(_content, new[] { Language.English }, s.AudioLanguage,
                new Vector2(0.55f, 0.76f), new Vector2(0.80f, 0.82f),
                lang => { s.AudioLanguage = lang; GameManager.Instance.SaveSettings(); }, lang => "ENGLISH");

            Label(_content, "Subtitles", 16, new Vector2(0.55f, 0.65f), new Vector2(0.96f, 0.71f), TextDim);
            OptionRow(_content, new[] { Language.English }, s.SubtitleLanguage,
                new Vector2(0.55f, 0.58f), new Vector2(0.80f, 0.64f),
                lang => { s.SubtitleLanguage = lang; GameManager.Instance.SaveSettings(); }, lang => "ENGLISH");

            Toggle_(_content, "Subtitles enabled", new Vector2(0.55f, 0.44f), new Vector2(0.96f, 0.51f),
                    s.SubtitlesEnabled, v => { s.SubtitlesEnabled = v; GameManager.Instance.SaveSettings(); });
        }

        // ---------------- MAP ----------------
        private void BuildMap()
        {
            var s = GameManager.Instance.Settings;

            Label(_content, "MAP STYLE", 20, new Vector2(0.04f, 0.86f), new Vector2(0.5f, 0.94f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            BuildStyleCards(s);

            Toggle_(_content, "3D Buildings", new Vector2(0.04f, 0.38f), new Vector2(0.48f, 0.45f),
                    s.Enable3DBuildings, v =>
                    {
                        s.Enable3DBuildings = v;
                        GameManager.Instance.SaveSettings();
                        Map.MapManager.Instance?.ApplyMapSettings();
                    });
            Toggle_(_content, "3D Terrain", new Vector2(0.04f, 0.28f), new Vector2(0.48f, 0.35f),
                    s.Enable3DTerrain, v =>
                    {
                        s.Enable3DTerrain = v;
                        GameManager.Instance.SaveSettings();
                        Map.MapManager.Instance?.ApplyMapSettings();
                    });
            Toggle_(_content, "Show place labels", new Vector2(0.04f, 0.18f), new Vector2(0.48f, 0.25f),
                    s.ShowMapPlaceLabels, v =>
                    {
                        s.ShowMapPlaceLabels = v;
                        GameManager.Instance.SaveSettings();
                        Map.MapManager.Instance?.ApplyMapSettings();
                    });

            Toggle_(_content, "Photorealistic 3D Tiles", new Vector2(0.52f, 0.38f), new Vector2(0.96f, 0.45f),
                    s.Enable3DTiles, v =>
                    {
                        s.Enable3DTiles = v;
                        GameManager.Instance.SaveSettings();
                        Map.MapManager.Instance?.ApplyMapSettings();
                    });
            Label(_content, "Google, streamed via Cesium ion — replaces 3D Buildings/Terrain and\nthe map style's imagery above with Google's own photorealistic mesh.",
                  11, new Vector2(0.52f, 0.28f), new Vector2(0.96f, 0.37f), TextDim);
        }

        private void BuildStyleCards(SettingsData s)
        {
            var styles = new[] { MapStyle.Satellite, MapStyle.Liberty, MapStyle.Terrain, MapStyle.OsmStandard, MapStyle.Dark };
            string[] names = { "SATELLITE", "LIBERTY", "TERRAIN", "OSM", "DARK" };
            int n = styles.Length;
            const float x0Area = 0.04f, x1Area = 0.96f, gap = 0.01f;
            float cellW = (x1Area - x0Area) / n;

            for (int i = 0; i < n; i++)
            {
                var style = styles[i];
                bool selected = s.Style == style;
                float x0 = x0Area + i * cellW + gap, x1 = x0Area + (i + 1) * cellW - gap;
                var card = Panel_(_content, "Style_" + style, selected ? Accent : Panel, new Vector2(x0, 0.50f), new Vector2(x1, 0.80f));

                var iconGo = new GameObject("Icon", typeof(UnityEngine.UI.Image));
                iconGo.transform.SetParent(card, false);
                iconGo.GetComponent<UnityEngine.UI.Image>().sprite = StyleIconBuilder.GetIcon(style);
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.06f, 0.30f);
                iconRt.anchorMax = new Vector2(0.94f, 0.92f);
                iconRt.offsetMin = Vector2.zero; iconRt.offsetMax = Vector2.zero;

                Label(card, names[i], 14, new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.26f),
                      TextMain, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

                var btn = card.gameObject.AddComponent<UnityEngine.UI.Button>();
                AddHoverSfx(card);
                btn.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlayButtonClick();
                    s.Style = style;
                    GameManager.Instance.SaveSettings();
                    Map.MapManager.Instance?.ApplyMapSettings();
                    Build();
                });
            }
        }

        // ---------------- CONTROLS ----------------
        private void BuildControls()
        {
            var s = GameManager.Instance.Settings;
            Label(_content, "CONTROL SCHEME", 22, new Vector2(0.04f, 0.90f), new Vector2(0.6f, 0.97f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            OptionRow(_content,
                new[] { ControlScheme.Keyboard, ControlScheme.Gamepad, ControlScheme.Gyroscope },
                s.Scheme, new Vector2(0.04f, 0.82f), new Vector2(0.96f, 0.89f),
                scheme =>
                {
                    s.Scheme = scheme;
                    AeroTerra.Input.InputManager.Instance?.ApplyScheme(scheme);
                    GameManager.Instance.SaveSettings();
                    Build();
                },
                scheme => scheme switch
                {
                    ControlScheme.Keyboard => "KEYBOARD",
                    ControlScheme.Gamepad => "GAMEPAD",
                    _ => "GYROSCOPE"
                });

            // Bigger diagram now that the key bindings list lives on its own tab.
            var diagramArea = Panel_(_content, "Diagram", PanelAlt, new Vector2(0.04f, 0.30f), new Vector2(0.96f, 0.78f));
            ControlsDiagram.Draw(diagramArea, s.Scheme);

            Toggle_(_content, "Invert pitch", new Vector2(0.04f, 0.20f), new Vector2(0.46f, 0.27f),
                    s.InvertPitch, v => { s.InvertPitch = v; GameManager.Instance.SaveSettings(); });

            if (s.Scheme == ControlScheme.Gyroscope)
            {
                Label(_content, $"Gyro sensitivity: {s.GyroSensitivity:0.0}x", 18,
                      new Vector2(0.52f, 0.235f), new Vector2(0.96f, 0.27f), TextDim);
                Slider_(_content, new Vector2(0.52f, 0.20f), new Vector2(0.96f, 0.23f),
                        Mathf.InverseLerp(0.2f, 3f, s.GyroSensitivity),
                        v => { s.GyroSensitivity = Mathf.Lerp(0.2f, 3f, v); GameManager.Instance.SaveSettings(); });
            }

            Button_(_content, "MANAGE KEY BINDINGS →", new Vector2(0.04f, 0.06f), new Vector2(0.46f, 0.15f),
                    () => { _tab = TabKeyBindings; Build(); }, Accent, 18);
        }

        // ---------------- KEY BINDINGS ----------------
        private void BuildKeyBindings()
        {
            Label(_content, "KEY BINDINGS", 22, new Vector2(0.04f, 0.88f), new Vector2(0.6f, 0.97f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            Label(_content, "Click a binding to rebind it. Pause (Esc) is fixed and cannot be changed.", 16,
                  new Vector2(0.04f, 0.81f), new Vector2(0.96f, 0.87f), TextDim);

            var im = AeroTerra.Input.InputManager.Instance;
            if (im == null) return;

            var actions = im.AllActions().Where(a => a != im.PauseAction).ToArray();
            const int cols = 2;
            float colW = 0.92f / cols;
            // 10 rebindable actions → 5 rows; keep the last row clear of the
            // RESET ALL BINDINGS button anchored at the bottom.
            const float rowH = 0.125f;
            for (int i = 0; i < actions.Length; i++)
            {
                int col = i % cols, row = i / cols;
                float x0 = 0.04f + col * colW, x1 = x0 + colW - 0.03f;
                float yTop = 0.72f - row * rowH;
                var action = actions[i];
                int bindingIndex = FirstKeyboardBinding(action);
                string display = action.GetBindingDisplayString(bindingIndex);

                Label(_content, action.name.ToUpperInvariant(), 18, new Vector2(x0, yTop), new Vector2(x1, yTop + 0.055f),
                      TextMain, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
                var a = action; var bi = bindingIndex;
                Button_(_content, display, new Vector2(x0, yTop - 0.065f), new Vector2(x1, yTop),
                        () => StartRebind(a, bi), PanelAlt, 18);
            }

            Button_(_content, "RESET ALL BINDINGS", new Vector2(0.04f, 0.06f), new Vector2(0.4f, 0.14f), () =>
            {
                foreach (var a in im.AllActions()) a.RemoveAllBindingOverrides();
                GameManager.Instance.Settings.BindingOverrides.Clear();
                GameManager.Instance.SaveSettings();
                Build();
            }, AccentWarn, 18);
        }

        private static int FirstKeyboardBinding(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (!b.isComposite && (b.isPartOfComposite || b.path.StartsWith("<Keyboard>")))
                    return i;
            }
            return 0;
        }

        private void StartRebind(InputAction action, int bindingIndex)
        {
            _rebindOp?.Dispose();
            action.Disable();
            _rebindOp = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op =>
                {
                    op.Dispose();
                    action.Enable();
                    AeroTerra.Input.InputManager.Instance.StoreOverride(action, bindingIndex);
                    Build();
                })
                .OnCancel(op => { op.Dispose(); action.Enable(); Build(); })
                .Start();
        }

        private void Clear() { if (_root != null) Destroy(_root.gameObject); }

        /// <summary>Hover SFX for the hand-rolled buttons in this file (dropdown, style cards)
        /// that don't go through UIBuilder.Button_ — click SFX is added at each call site.</summary>
        private static void AddHoverSfx(RectTransform rt)
        {
            var trigger = rt.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => AudioManager.Instance?.PlayButtonHover());
            trigger.triggers.Add(entry);
        }
    }
}
