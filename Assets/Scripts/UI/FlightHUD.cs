using UnityEngine;
using AeroTerra.Core;
using AeroTerra.Drone;
using AeroTerra.Map;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// In-flight HUD: a top status strip (drone name, active camera mode, payload
    /// status), a camera-mode-aware center reticle, a lightweight attitude cue, the
    /// bottom telemetry bar (speed/altitude/throttle/heading/battery/GPS/vertical
    /// speed), a payload-drop key hint, a low-battery warning banner, a wind-direction
    /// dial, and a north-up radar minimap showing bearing/distance back to the map's
    /// spawn origin.
    /// </summary>
    public class FlightHUD : MonoBehaviour
    {
        public static FlightHUD Instance { get; private set; }

        private DroneFlightController _flight;
        private RectTransform _root;
        private TMPro.TextMeshProUGUI _speed, _alt, _throttle, _battery, _heading, _vspeed, _lat, _lon, _warning;
        private TMPro.TextMeshProUGUI _camModeLabel, _payloadLabel, _dropHint, _fpsLabel;
        private RectTransform _batteryFill;
        private RectTransform _powerIconHolder;
        private RectTransform _payloadPipsRow;
        private RectTransform _reticleCross, _reticleV;
        private RectTransform[] _reticleCorners;
        private RectTransform _horizonLine;

        private int _hardpoints;
        private bool _militaryPayload;
        private bool _kamikaze;
        private UnityEngine.UI.Image[] _payloadPips;
        private PayloadDropper _dropper;

        private RectTransform _compassRing;
        private RectTransform[] _compassLabels;

        private RectTransform _windDial, _windNeedle;
        private TMPro.TextMeshProUGUI _windSpeedLabel;

        private RectTransform _tempPanel;
        private TMPro.TextMeshProUGUI _tempLabel;

        private RectTransform _minimapFrame;
        private RectTransform _minimapHome, _minimapNose;
        private TMPro.TextMeshProUGUI _minimapDistLabel;
        private const float MinimapRangeM = 400f;
        private const float MinimapRadiusPx = 58f;

        // Landmark bearing markers (MapDefinition.Landmarks) — offsets computed once at
        // Init (landmarks/map don't change mid-flight), positions re-derived every frame
        // the same off-scale-clamp way the HOME marker already is.
        private RectTransform[] _minimapLandmarks;
        private Vector2[] _minimapLandmarkOffsetsM;

        private RectTransform _photoPanel;
        private TMPro.TextMeshProUGUI _photoReadout;

        private float _fpsSmoothed = 60f;
        private float _lowPowerBeepTimer;

        public void Init(Canvas canvas, DroneFlightController flight)
        {
            Instance = this;
            _flight = flight;

            _root = Panel_(canvas.transform, "HUDRoot", Color.clear, Vector2.zero, Vector2.one);

            BuildTopStrip();
            BuildReticle();
            BuildAttitudeCue();
            BuildBottomBar();
            BuildCompass();
            BuildWindIndicator();
            BuildTemperatureIndicator();
            BuildMinimap();
            BuildFpsCounter();

            _warning = Label(_root, "", 34, new Vector2(0.2f, 0.83f), new Vector2(0.8f, 0.89f),
                             AccentWarn, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

            BuildPhotoModeOverlay();

            SetCameraMode(CamMode.ChaseDefault);
            SetVisible(GameManager.Instance.Settings.ShowHud);
            ApplyHudElementSettings();
        }

        private void BuildTopStrip()
        {
            var top = Panel_(_root, "TopBar", new Color(0, 0, 0, 0.35f), new Vector2(0, 0.90f), new Vector2(1, 1f));
            Panel_(top, "BottomBorder", Accent, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, -2), new Vector2(0, 0));

            Label(top, _flight.Spec.DisplayName, 22, new Vector2(0.02f, 0), new Vector2(0.35f, 1), TextMain);
            _camModeLabel = Label(top, "CHASE DEFAULT", 22, new Vector2(0.35f, 0), new Vector2(0.65f, 1),
                                  Accent, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            _payloadLabel = Label(top, "", 20, new Vector2(0.65f, 0), new Vector2(0.98f, 1),
                                  TextDim, TMPro.TextAlignmentOptions.Right);
        }

        private void BuildReticle()
        {
            var reticle = Panel_(_root, "Reticle", Color.clear, Vector2.zero, Vector2.one);
            _reticleCross = Panel_(reticle, "CrossH", TextMain, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                    new Vector2(-16, -1), new Vector2(16, 1));
            _reticleV = Panel_(reticle, "CrossV", TextMain, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                new Vector2(-1, -16), new Vector2(1, 16));

            _reticleCorners = new RectTransform[4];
            Vector2[] corners = { new Vector2(-26, 26), new Vector2(26, 26), new Vector2(-26, -26), new Vector2(26, -26) };
            for (int i = 0; i < 4; i++)
            {
                Vector2 c = corners[i];
                _reticleCorners[i] = Panel_(reticle, "Corner" + i, AccentWarn, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                             c - new Vector2(3, 3), c + new Vector2(3, 3));
            }
        }

        private void BuildAttitudeCue()
        {
            var cueArea = Panel_(_root, "AttitudeCue", Color.clear, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f),
                                  new Vector2(-90, -30), new Vector2(90, 30));
            _horizonLine = Panel_(cueArea, "HorizonLine", TextDim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                   new Vector2(-90, -1), new Vector2(90, 1));
            Panel_(cueArea, "BoreRef", Accent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(-4, -4), new Vector2(4, 4));
        }

        private void BuildBottomBar()
        {
            var bar = Panel_(_root, "HUDBar", new Color(0, 0, 0, 0.45f), new Vector2(0, 0), new Vector2(1, 0.12f));
            Panel_(bar, "TopBorder", Accent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(0, 2));

            // Row 1 (top half): speed, altitude, throttle, heading, battery.
            _speed = Label(bar, "0 km/h", 24, new Vector2(0.02f, 0.5f), new Vector2(0.14f, 1));
            _alt = Label(bar, "ALT 0 m", 24, new Vector2(0.15f, 0.5f), new Vector2(0.30f, 1));
            _throttle = Label(bar, "THR 0%", 24, new Vector2(0.31f, 0.5f), new Vector2(0.44f, 1));
            _heading = Label(bar, "HDG 000°", 24, new Vector2(0.45f, 0.5f), new Vector2(0.58f, 1));
            _battery = Label(bar, "BAT 100%", 24, new Vector2(0.60f, 0.5f), new Vector2(0.78f, 1));
            BuildPowerIcon(bar, new Vector2(0.785f, 0.58f), new Vector2(0.98f, 0.92f),
                           _flight.Spec.PowerSystem == PowerSystemType.Fuel);

            foreach (float x in new[] { 0.145f, 0.305f, 0.445f, 0.585f, 0.785f })
                Panel_(bar, "Div", new Color(1, 1, 1, 0.12f), new Vector2(x, 0.15f), new Vector2(x, 0.85f),
                       new Vector2(-1, 0), new Vector2(1, 0));

            // Row 2 (bottom half): GPS coordinates, vertical speed, drop-payload hint.
            _lat = Label(bar, "LAT 0.00000°", 20, new Vector2(0.02f, 0), new Vector2(0.20f, 0.48f), TextDim);
            _lon = Label(bar, "LON 0.00000°", 20, new Vector2(0.21f, 0), new Vector2(0.39f, 0.48f), TextDim);
            _vspeed = Label(bar, "V/S 0.0 m/s", 20, new Vector2(0.40f, 0), new Vector2(0.58f, 0.48f), TextDim);
            // Kamikaze airframes have nothing to release — the whole drone is the
            // munition and detonates on impact, so the hint says so instead of [I].
            _kamikaze = _flight.Spec.IsKamikazeClass;
            _dropHint = Label(bar, _kamikaze ? "IMPACT DETONATION" : "[I] DROP", 20,
                              new Vector2(0.60f, 0), new Vector2(0.78f, 0.48f), TextDim,
                              TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);

            BuildPayloadIcons(bar);
        }

        /// <summary>Row of hardpoint icons (bottom bar, right of the drop hint) — one
        /// pip per DroneSpecification.PayloadHardpoints, shaped by PayloadKind so the
        /// icon itself communicates ordnance TYPE, not just "military vs. civilian."
        /// Drops are sequential per store (see PayloadDropper), so pips go dark one at
        /// a time as stores are expended, then all relight after the rearm cooldown.
        /// Skipped entirely for drones with no payload capability (e.g. the racing quad).</summary>
        private void BuildPayloadIcons(Transform bar)
        {
            _hardpoints = _flight.Spec.PayloadHardpoints;
            _militaryPayload = _flight.Spec.IsMilitaryClass;
            if (_hardpoints <= 0 || _flight.Spec.MaxPayloadKg <= 0f) return;

            var kind = _flight.EffectivePayloadKind;
            _payloadPipsRow = Panel_(bar, "PayloadPipsRow", Color.clear, new Vector2(0.80f, 0f), new Vector2(0.98f, 0.48f));
            float slot = 1f / _hardpoints;
            _payloadPips = new UnityEngine.UI.Image[_hardpoints];
            for (int i = 0; i < _hardpoints; i++)
            {
                float cx0 = i * slot, cx1 = cx0 + slot;
                _payloadPips[i] = BuildPayloadPip(_payloadPipsRow, new Vector2(cx0, 0f), new Vector2(cx1, 1f), kind);
            }
        }

        /// <summary>One ordnance-type silhouette per PayloadKind. Only the returned
        /// (main body) Image is live-tinted fill/empty in Update() — accent details
        /// (fins/tail/strap) stay a fixed dim shade, same contract the crate's "Strap"
        /// overlay already established, so Update()'s per-pip color loop doesn't need
        /// to change.</summary>
        private UnityEngine.UI.Image BuildPayloadPip(Transform parent, Vector2 anchorMin, Vector2 anchorMax, PayloadKind kind)
        {
            var holder = Panel_(parent, "PayloadPip", Color.clear, anchorMin, anchorMax);
            var accentShade = new Color(1f, 1f, 1f, 0.3f);
            RectTransform shape;

            switch (kind)
            {
                case PayloadKind.Warhead:
                    // Squat diamond (rotated square) — abstract "ordnance" silhouette.
                    shape = Panel_(holder, "Warhead", TextDim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                   new Vector2(-7, -7), new Vector2(7, 7));
                    shape.localRotation = Quaternion.Euler(0, 0, 45f);
                    break;

                case PayloadKind.GuidedAmmunition:
                    // Slim elongated diamond (missile body) + a small fixed-shade fin.
                    shape = Panel_(holder, "Missile", TextDim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                   new Vector2(-5, -10), new Vector2(5, 10));
                    shape.localRotation = Quaternion.Euler(0, 0, 45f);
                    Panel_(holder, "Fin", accentShade, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                           new Vector2(-10, -2), new Vector2(-3, 2));
                    break;

                case PayloadKind.DropAmmunition:
                    // Bomb body (diamond) + a fixed-shade cross-tail fin behind it —
                    // the classic dumb-bomb silhouette.
                    shape = Panel_(holder, "Bomb", TextDim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                   new Vector2(-6, -8), new Vector2(6, 5));
                    Panel_(holder, "TailV", accentShade, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                           new Vector2(-1, -13), new Vector2(1, -6));
                    Panel_(holder, "TailH", accentShade, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                           new Vector2(-5, -11), new Vector2(5, -9));
                    break;

                default: // Cargo
                    // Boxy crate with a strap line across the middle.
                    shape = Panel_(holder, "Crate", TextDim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                   new Vector2(-9, -7), new Vector2(9, 7));
                    Panel_(shape, "Strap", new Color(0, 0, 0, 0.35f), new Vector2(0, 0.42f), new Vector2(1, 0.58f));
                    break;
            }
            return shape.GetComponent<UnityEngine.UI.Image>();
        }

        /// <summary>Battery (or fuel-canister, for PowerSystemType.Fuel airframes) gauge
        /// — a real silhouette (body outline + terminal/spout nub) with the charge fill
        /// inset inside it, replacing the old bare rectangle bar. _batteryFill keeps the
        /// exact same role Update() already drives it by (anchorMax.x = powerPct) — only
        /// its container changed shape.</summary>
        private void BuildPowerIcon(Transform parent, Vector2 anchorMin, Vector2 anchorMax, bool fuel)
        {
            var holder = Panel_(parent, "PowerIcon", Color.clear, anchorMin, anchorMax);
            _powerIconHolder = holder;
            var bodyShade = new Color(1f, 1f, 1f, 0.18f);
            RectTransform fillArea;

            if (fuel)
            {
                // Jerry-can silhouette: body + a narrower spout nub on top.
                Panel_(holder, "Body", bodyShade, new Vector2(0.08f, 0f), new Vector2(0.92f, 0.78f));
                Panel_(holder, "Spout", bodyShade, new Vector2(0.36f, 0.78f), new Vector2(0.64f, 1f));
                fillArea = Panel_(holder, "FillArea", Color.clear, new Vector2(0.16f, 0.10f), new Vector2(0.84f, 0.68f));
            }
            else
            {
                // Battery silhouette: body + a small positive-terminal nub on the right.
                Panel_(holder, "Body", bodyShade, new Vector2(0f, 0.14f), new Vector2(0.88f, 0.86f));
                Panel_(holder, "Terminal", bodyShade, new Vector2(0.88f, 0.36f), new Vector2(1f, 0.64f));
                fillArea = Panel_(holder, "FillArea", Color.clear, new Vector2(0.07f, 0.22f), new Vector2(0.81f, 0.78f));
            }

            _batteryFill = Panel_(fillArea, "Fill", Accent, Vector2.zero, new Vector2(0f, 1f));
        }

        /// <summary>Rotating compass rose: an 8-point dial that spins beneath a fixed
        /// heading pointer, with each letter counter-rotated so it always stays upright
        /// on screen regardless of the dial's current rotation.</summary>
        private static readonly (string text, float deg)[] CompassPoints =
        {
            ("N", 0f), ("NE", 45f), ("E", 90f), ("SE", 135f),
            ("S", 180f), ("SW", 225f), ("W", 270f), ("NW", 315f),
        };

        private void BuildCompass()
        {
            var ring = Panel_(_root, "Compass", new Color(0, 0, 0, 0.30f),
                               new Vector2(0.14f, 0.90f), new Vector2(0.14f, 0.90f),
                               new Vector2(-68, -102), new Vector2(68, -14));

            const float radius = 34f;
            _compassLabels = new RectTransform[CompassPoints.Length];
            for (int i = 0; i < CompassPoints.Length; i++)
            {
                var (text, deg) = CompassPoints[i];
                bool cardinal = deg % 90f == 0f;
                var lbl = Label(ring, text, cardinal ? 15 : 11,
                                 new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                 cardinal ? Accent : TextDim, TMPro.TextAlignmentOptions.Center,
                                 cardinal ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal);
                var rt = lbl.rectTransform;
                rt.offsetMin = new Vector2(-13, -9); rt.offsetMax = new Vector2(13, 9);
                float rad = deg * Mathf.Deg2Rad;
                rt.anchoredPosition = new Vector2(Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius);
                _compassLabels[i] = rt;
            }

            // Fixed marker at the top of the dial — never rotates, always reads
            // "this is the direction the nose is pointing right now."
            Panel_(ring, "Pointer", Accent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(-2, radius + 8), new Vector2(2, radius + 16));

            _compassRing = ring;
        }

        /// <summary>Small dial directly below the compass: a needle pointing the
        /// direction WeatherSystem's current wind is blowing TOWARD (windsock
        /// convention), plus the steady-state speed in m/s — the same figure Free
        /// Flight's conditions screen shows (WeatherSystem.BaseWindSpeedMs, or the
        /// manual override), not the raw force vector's magnitude (CurrentWind is a
        /// force applied via AddForce, not a velocity, so its magnitude isn't m/s).</summary>
        private void BuildWindIndicator()
        {
            var dial = Panel_(_root, "WindDial", new Color(0, 0, 0, 0.30f),
                               new Vector2(0.14f, 0.90f), new Vector2(0.14f, 0.90f),
                               new Vector2(-50f, -186f), new Vector2(50f, -108f));
            _windDial = dial;

            Label(dial, "WIND", 10, new Vector2(0f, 0.74f), new Vector2(1f, 0.98f),
                  TextDim, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            _windSpeedLabel = Label(dial, "-- m/s", 13, new Vector2(0f, 0.02f), new Vector2(1f, 0.26f),
                                    TextMain, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

            // Half-and-half needle (accent head / dim tail) — same "plain shapes over
            // missing glyphs" convention as StarRow, reads as a windsock/weather-vane
            // pointer without needing an imported icon. Rotating the shared parent
            // (rather than head+tail separately) keeps both halves moving as one needle.
            _windNeedle = Panel_(dial, "NeedleArea", Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                 new Vector2(-16f, -16f), new Vector2(16f, 16f));
            Panel_(_windNeedle, "Tail", TextDim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(-2f, -16f), new Vector2(2f, 0f));
            Panel_(_windNeedle, "Head", Accent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(-2f, 0f), new Vector2(2f, 16f));
        }

        /// <summary>Small instrument directly below the wind dial, same frame style —
        /// shows the current ambient air temperature (Settings ▸ Flying Conditions),
        /// the same figure that drives BatterySystem.PerformanceFactor's thrust derate.</summary>
        private void BuildTemperatureIndicator()
        {
            var panel = Panel_(_root, "TempPanel", new Color(0, 0, 0, 0.30f),
                                new Vector2(0.14f, 0.90f), new Vector2(0.14f, 0.90f),
                                new Vector2(-50f, -270f), new Vector2(50f, -192f));
            _tempPanel = panel;

            Label(panel, "TEMP", 10, new Vector2(0f, 0.74f), new Vector2(1f, 0.98f),
                  TextDim, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            _tempLabel = Label(panel, "-- °C", 13, new Vector2(0f, 0.02f), new Vector2(1f, 0.26f),
                                TextMain, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
        }

        /// <summary>Square "radar" nav readout, top-right — a framed instrument rather
        /// than a literal top-down camera view (which would need a second camera
        /// actively streaming Cesium 3D tiles, unverified without an Editor). North-up,
        /// fixed at the drone (center); the only tracked point right now is the map's
        /// spawn origin — Unity world (0,0,0) IS that origin by construction, since
        /// MapManager.BuildWorld() sets the CesiumGeoreference there, so no lat/lon
        /// lookup is needed for the HOME marker's bearing/distance.</summary>
        private void BuildMinimap()
        {
            var frame = Panel_(_root, "MinimapFrame", new Color(Accent.r, Accent.g, Accent.b, 0.5f),
                                new Vector2(0.98f, 0.83f), new Vector2(0.98f, 0.83f),
                                new Vector2(-152f, -152f), new Vector2(0f, 0f));
            _minimapFrame = frame;

            Label(frame, "NAV", 11, new Vector2(0f, 0.90f), new Vector2(1f, 1f),
                  TextDim, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            _minimapDistLabel = Label(frame, "HOME --", 11, new Vector2(0f, 0f), new Vector2(1f, 0.10f),
                                      TextDim, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

            var box = Panel_(frame, "MinimapBg", new Color(0, 0, 0, 0.45f), new Vector2(0f, 0.10f), new Vector2(1f, 0.90f),
                              new Vector2(2f, 2f), new Vector2(-2f, -2f));

            // Decorative range rings (nested translucent squares, not true circles —
            // same reasoning as the frame itself) giving a rough sense of scale.
            Panel_(box, "RingOuter", new Color(1, 1, 1, 0.05f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(-MinimapRadiusPx, -MinimapRadiusPx), new Vector2(MinimapRadiusPx, MinimapRadiusPx));
            Panel_(box, "RingInner", new Color(1, 1, 1, 0.07f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(-MinimapRadiusPx * 0.5f, -MinimapRadiusPx * 0.5f), new Vector2(MinimapRadiusPx * 0.5f, MinimapRadiusPx * 0.5f));

            // HOME marker — a small accent diamond, clamped to the ring's edge once the
            // real distance exceeds MinimapRangeM (classic off-scale radar behavior).
            _minimapHome = Panel_(box, "Home", Accent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                  new Vector2(-4f, -4f), new Vector2(4f, 4f));
            _minimapHome.localRotation = Quaternion.Euler(0, 0, 45f);

            // Drone marker — fixed at center; a short nose line shows live heading
            // (north-up map, matching the LAT/LON readout convention — the map itself
            // never rotates, only this line does).
            Panel_(box, "Drone", TextMain, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(-3f, -3f), new Vector2(3f, 3f));
            _minimapNose = Panel_(box, "Nose", TextMain, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                  new Vector2(-1f, 0f), new Vector2(1f, 14f));

            BuildMinimapLandmarks(box);
        }

        /// <summary>One small diamond + name tag per MapDefinition.Landmark on the current
        /// map — same off-scale ring-edge clamp treatment as the HOME marker (most real
        /// landmarks sit well outside MinimapRangeM, so this mainly reads as "which
        /// direction to fly," same as HOME already does past 400 m). Offsets are computed
        /// once here via the flat-earth approximation (MapDefinition.FlatOffsetMeters) since
        /// neither the map nor its landmarks change mid-flight.</summary>
        private void BuildMinimapLandmarks(Transform box)
        {
            var map = GameManager.Instance != null ? GameManager.Instance.SelectedMap : null;
            var landmarks = map != null ? map.Landmarks : null;
            if (landmarks == null || landmarks.Length == 0)
            {
                _minimapLandmarks = System.Array.Empty<RectTransform>();
                _minimapLandmarkOffsetsM = System.Array.Empty<Vector2>();
                return;
            }

            _minimapLandmarks = new RectTransform[landmarks.Length];
            _minimapLandmarkOffsetsM = new Vector2[landmarks.Length];
            for (int i = 0; i < landmarks.Length; i++)
            {
                var lm = landmarks[i];
                _minimapLandmarkOffsetsM[i] = MapDefinition.FlatOffsetMeters(
                    lm.Latitude, lm.Longitude, map.Latitude, map.Longitude);

                var marker = Panel_(box, "Landmark_" + lm.Name, Accent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                    new Vector2(-3f, -3f), new Vector2(3f, 3f));
                marker.localRotation = Quaternion.Euler(0, 0, 45f);
                marker.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
                _minimapLandmarks[i] = marker;

                var label = Label(marker, lm.Name, 8, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                  Accent, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
                label.enableWordWrapping = false;
                label.raycastTarget = false;
                var labelRt = label.rectTransform;
                labelRt.localRotation = Quaternion.Euler(0, 0, -45f); // counter the diamond's rotation
                labelRt.pivot = new Vector2(0.5f, 0f);
                labelRt.sizeDelta = new Vector2(70f, 12f);
                labelRt.anchoredPosition = new Vector2(0f, 5f);
            }
        }

        private void BuildFpsCounter()
        {
            _fpsLabel = Label(_root, "FPS --", 16, new Vector2(0.85f, 0.855f), new Vector2(0.98f, 0.895f),
                              TextDim, TMPro.TextAlignmentOptions.Right, TMPro.FontStyles.Bold);
        }

        /// <summary>Bottom-center control-hint bar + live FOV/exposure readout, shown only
        /// while DroneCameraRig's detached Photo mode is active (see SetPhotoModeActive/
        /// UpdatePhotoModeReadout, driven every frame from DroneCameraRig.UpdatePhotoMode).</summary>
        private void BuildPhotoModeOverlay()
        {
            _photoPanel = Panel_(_root, "PhotoModeBar", new Color(0, 0, 0, 0.55f),
                                 new Vector2(0.24f, 0.02f), new Vector2(0.76f, 0.095f));
            Label(_photoPanel, "PHOTO MODE", 15, new Vector2(0.03f, 0.52f), new Vector2(0.30f, 0.94f),
                  Accent, TMPro.TextAlignmentOptions.MidlineLeft, TMPro.FontStyles.Bold);
            _photoReadout = Label(_photoPanel, "", 13, new Vector2(0.03f, 0.05f), new Vector2(0.30f, 0.50f),
                                  TextDim, TMPro.TextAlignmentOptions.MidlineLeft);
            Label(_photoPanel, "RMB LOOK · WASD MOVE · Q/E UP-DOWN · SHIFT FAST · [ ] FOV · − = EXPOSURE · F8 EXIT",
                  11, new Vector2(0.32f, 0f), new Vector2(0.98f, 1f), TextDim, TMPro.TextAlignmentOptions.MidlineLeft);
            _photoPanel.gameObject.SetActive(false);
        }

        /// <summary>Called by DroneCameraRig.TogglePhotoMode on every enter/exit.</summary>
        public void SetPhotoModeActive(bool active) => _photoPanel.gameObject.SetActive(active);

        /// <summary>Called every LateUpdate while Photo mode is active — see
        /// DroneCameraRig.UpdatePhotoMode.</summary>
        public void UpdatePhotoModeReadout(float fov, float exposureEv)
        {
            if (_photoReadout != null) _photoReadout.text = $"FOV {fov:0}°   EV {exposureEv:+0.0;-0.0;0.0}";
        }

        /// <summary>Called by DroneCameraRig on every camera-mode change (key C, or O for
        /// Photo — see TogglePhotoMode).</summary>
        public void SetCameraMode(CamMode mode)
        {
            _camModeLabel.text = mode switch
            {
                CamMode.ChaseDetails => "CHASE DETAILS",
                CamMode.Front => "FRONT",
                CamMode.Bottom => "BOTTOM — SURVEILLANCE",
                CamMode.Thermal => "THERMAL",
                CamMode.Photo => "PHOTO",
                _ => "CHASE DEFAULT",
            };

            bool showReticle = mode != CamMode.ChaseDefault && mode != CamMode.ChaseDetails && mode != CamMode.Photo;
            _reticleCross.gameObject.SetActive(showReticle);
            _reticleV.gameObject.SetActive(showReticle);
            foreach (var c in _reticleCorners) c.gameObject.SetActive(mode == CamMode.Bottom);

            Color reticleColor = mode switch
            {
                CamMode.Bottom => AccentWarn,
                CamMode.Thermal => new Color(0.55f, 0.95f, 0.65f),
                _ => TextMain,
            };
            _reticleCross.GetComponent<UnityEngine.UI.Image>().color = reticleColor;
            _reticleV.GetComponent<UnityEngine.UI.Image>().color = reticleColor;
        }

        /// <summary>Live-toggled from Settings ▸ Game while already in flight.</summary>
        public void SetVisible(bool visible) => _root.gameObject.SetActive(visible);

        /// <summary>Applies each per-element HUD visibility toggle from Settings ▸ Game.
        /// Called once from Init() and again live any time one is flipped from the
        /// pause menu mid-flight. Narrator (voice+text) isn't handled here — it's
        /// gated directly in NarratorController.Enqueue().</summary>
        public void ApplyHudElementSettings()
        {
            var s = GameManager.Instance.Settings;
            _speed.gameObject.SetActive(s.HudShowSpeed);
            _alt.gameObject.SetActive(s.HudShowAltitude);
            _throttle.gameObject.SetActive(s.HudShowThrottle);
            _lat.gameObject.SetActive(s.HudShowGps);
            _lon.gameObject.SetActive(s.HudShowGps);
            _battery.gameObject.SetActive(s.HudShowBattery);
            if (_powerIconHolder != null) _powerIconHolder.gameObject.SetActive(s.HudShowBattery);
            _payloadLabel.gameObject.SetActive(s.HudShowPayload);
            if (_payloadPipsRow != null) _payloadPipsRow.gameObject.SetActive(s.HudShowPayload);
            if (_compassRing != null) _compassRing.gameObject.SetActive(s.HudShowCompass);
            if (_windDial != null) _windDial.gameObject.SetActive(s.HudShowWind);
            if (_tempPanel != null) _tempPanel.gameObject.SetActive(s.HudShowTemperature);
            if (_minimapFrame != null) _minimapFrame.gameObject.SetActive(s.HudShowMinimap);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private static float NormalizeAngle(float deg) => deg > 180f ? deg - 360f : deg;

        private void Update()
        {
            if (_flight == null || !_root.gameObject.activeSelf) return;
            _speed.text = $"{_flight.CurrentSpeedKmh:0} km/h";
            _alt.text = $"ALT {_flight.transform.position.y:0} m";
            _throttle.text = $"THR {_flight.Throttle01 * 100f:0}%";
            _heading.text = $"HDG {(_flight.transform.eulerAngles.y):000}°";
            _vspeed.text = $"V/S {_flight.VerticalSpeedMs:+0.0;-0.0} m/s";

            var llh = MapManager.Instance != null
                ? MapManager.Instance.ToLongitudeLatitudeHeight(_flight.transform.position)
                : Vector3.zero;
            _lat.text = $"LAT {llh.y:0.00000}°";
            _lon.text = $"LON {llh.x:0.00000}°";

            // Whichever power source this airframe actually has (battery or fuel tank —
            // see PowerSystemType) drives the same readout, just relabeled.
            bool fuelPowered = _flight.Spec.PowerSystem == PowerSystemType.Fuel;
            IPowerSource power = fuelPowered ? (IPowerSource)_flight.Fuel : _flight.Battery;
            string powerLabel = fuelPowered ? "FUEL" : "BAT";
            float powerPct = power != null ? power.Percent : 0f;
            _battery.text = power != null
                ? $"{powerLabel} {powerPct * 100f:0}%  ({power.EstimatedMinutesLeft:0} min)"
                : $"{powerLabel} --";
            _batteryFill.anchorMax = new Vector2(powerPct, 1);
            _batteryFill.GetComponent<UnityEngine.UI.Image>().color =
                powerPct > 0.3f ? Accent : AccentWarn;

            float payloadKg = _flight.Payload != null ? _flight.Payload.CurrentPayloadKg : 0f;
            bool loaded = payloadKg > 0f;
            // Multi-hardpoint drops are sequential now — show live stores-remaining
            // from PayloadDropper where one exists (kamikaze airframes have none).
            if (_dropper == null && !_kamikaze) _dropper = _flight.GetComponent<PayloadDropper>();
            int remaining = _dropper != null
                ? Mathf.RoundToInt(_dropper.StoresRemaining * (float)_hardpoints / Mathf.Max(1, _dropper.StoreCount))
                : loaded ? _hardpoints : 0;
            _payloadLabel.text = _kamikaze
                ? (loaded ? $"WARHEAD ARMED {payloadKg:0.#} kg" : "WARHEAD EXPENDED")
                : loaded
                    ? $"{remaining}/{_hardpoints} {_flight.Spec.PayloadTypeName.ToUpperInvariant()} {payloadKg:0.#} kg"
                    : _hardpoints > 0 ? $"0/{_hardpoints} REARMING" : "PAYLOAD EMPTY";
            _dropHint.color = loaded ? (_kamikaze ? AccentWarn : Accent) : TextDim;

            if (_payloadPips != null)
            {
                Color fillColor = _militaryPayload ? AccentWarn : Accent;
                Color emptyColor = new Color(1, 1, 1, 0.15f);
                for (int i = 0; i < _payloadPips.Length; i++)
                    _payloadPips[i].color = (_kamikaze ? loaded : i < remaining) ? fillColor : emptyColor;
            }

            float heading = _flight.transform.eulerAngles.y;
            if (_compassRing != null)
            {
                _compassRing.localEulerAngles = new Vector3(0, 0, heading);
                var counter = Quaternion.Euler(0, 0, -heading);
                foreach (var lbl in _compassLabels) lbl.localRotation = counter;
            }

            if (_windNeedle != null)
            {
                Vector3 wind = WeatherSystem.Instance != null ? WeatherSystem.Instance.CurrentWind : Vector3.zero;
                // CurrentWind is a force (applied via AddForce in DroneFlightController),
                // not a velocity, so only its DIRECTION is used here — bearing the wind
                // blows TOWARD, windsock convention, same atan2(x,z) heading math used
                // everywhere else in this HUD.
                float windBearing = wind.sqrMagnitude > 0.0001f
                    ? Mathf.Atan2(wind.x, wind.z) * Mathf.Rad2Deg : 0f;
                _windNeedle.localEulerAngles = new Vector3(0, 0, -windBearing);

                _windSpeedLabel.text = $"{GameManager.Instance.Settings.WindSpeedMs:0.0} m/s";
            }

            if (_tempLabel != null)
                _tempLabel.text = $"{GameManager.Instance.Settings.TemperatureC:0} °C";

            if (_minimapHome != null)
            {
                // Unity world (0,0,0) IS the map's spawn origin (see BuildMinimap's
                // remarks) — home's position relative to the drone is just the drone's
                // own negated XZ position, no georeference lookup needed.
                Vector3 pos = _flight.transform.position;
                Vector2 homeOffsetM = new Vector2(-pos.x, -pos.z);
                float homeDistM = homeOffsetM.magnitude;

                float scale = MinimapRadiusPx / MinimapRangeM;
                Vector2 raw = homeOffsetM * scale;
                _minimapHome.anchoredPosition = raw.magnitude > MinimapRadiusPx
                    ? raw.normalized * MinimapRadiusPx : raw;
                _minimapNose.localEulerAngles = new Vector3(0, 0, -heading);
                _minimapDistLabel.text = homeDistM < 1000f
                    ? $"HOME {homeDistM:0} m" : $"HOME {homeDistM / 1000f:0.0} km";

                if (_minimapLandmarks != null)
                {
                    // Landmark offsets are stored relative to the map origin (world XZ);
                    // homeOffsetM is "origin minus drone," so adding it re-bases the same
                    // offset onto "landmark minus drone" — the vector the marker needs.
                    for (int i = 0; i < _minimapLandmarks.Length; i++)
                    {
                        Vector2 lmRaw = (_minimapLandmarkOffsetsM[i] + homeOffsetM) * scale;
                        _minimapLandmarks[i].anchoredPosition = lmRaw.magnitude > MinimapRadiusPx
                            ? lmRaw.normalized * MinimapRadiusPx : lmRaw;
                    }
                }
            }

            if (_fpsLabel != null)
            {
                _fpsSmoothed = Mathf.Lerp(_fpsSmoothed, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime),
                                          Time.unscaledDeltaTime * 6f);
                _fpsLabel.text = $"FPS {_fpsSmoothed:0}";
            }

            float roll = NormalizeAngle(_flight.transform.eulerAngles.z);
            float pitch = NormalizeAngle(_flight.transform.eulerAngles.x);
            _horizonLine.localEulerAngles = new Vector3(0, 0, -roll);
            _horizonLine.anchoredPosition = new Vector2(0, Mathf.Clamp(-pitch * 0.6f, -25f, 25f));

            bool powerEmpty = power != null && power.IsEmpty;
            // Battery-only: cold or hot air temperature derates thrust ceiling (see
            // BatterySystem.PerformanceFactor / DroneFlightController) — surfaced here
            // so a sudden performance drop reads as "why" rather than as a bug. Lower
            // priority than the depleted/low-power warnings above it.
            float tempC = GameManager.Instance.Settings.TemperatureC;
            bool batteryDerated = !fuelPowered && BatterySystem.PerformanceFactor(tempC) < 0.95f;
            bool lowPower = !powerEmpty && powerPct < 0.15f;
            _warning.text = powerEmpty ? $"{powerLabel} DEPLETED"
                          : lowPower ? $"LOW {powerLabel} — RETURN NOW"
                          : batteryDerated ? $"BATTERY {(tempC < 5f ? "COLD" : "OVERHEATING")} — REDUCED THRUST" : "";

            // Flashing readout + periodic audio cue while low/depleted — the plain
            // static banner above was easy to miss mid-flight; auto-cutoff (thrust
            // stopping once IsEmpty, see DroneFlightController.FixedUpdate) follows
            // shortly after LOW first shows, so this is the "before auto-cutoff" alert.
            bool warnFlashing = powerEmpty || lowPower;
            var warnColor = _warning.color;
            warnColor.a = warnFlashing ? Mathf.Lerp(0.35f, 1f, Mathf.PingPong(Time.unscaledTime * 2.2f, 1f)) : 1f;
            _warning.color = warnColor;

            if (lowPower)
            {
                _lowPowerBeepTimer -= Time.unscaledDeltaTime;
                if (_lowPowerBeepTimer <= 0f)
                {
                    _lowPowerBeepTimer = 4f;
                    AudioManager.Instance?.PlayLowPowerWarning();
                }
            }
            else
            {
                _lowPowerBeepTimer = 0f; // next time it drops low, beep immediately
            }
        }
    }
}
