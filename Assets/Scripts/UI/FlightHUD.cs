using UnityEngine;
using AeroTerra.Core;
using AeroTerra.Drone;
using AeroTerra.Map;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// In-flight HUD. Three distinct visual/layout skins are built off
    /// DroneSpecification.Category — the same field that already drives the
    /// Workshop showroom badge — so the HUD reads as "belonging" to the aircraft
    /// flying it instead of one generic instrument cluster for every drone:
    ///   - Military:  green phosphor jet-HUD look — pitch-ladder artificial
    ///                horizon, permanent bracket/cross boresight reticle, and
    ///                scrolling vertical speed/altitude tapes either side of center.
    ///   - Civilian:  DJI/Betaflight-style FPV goggle OSD — flat monospace-ish
    ///                readouts with no background chrome, plain numeric speed/alt,
    ///                a thin center crosshair, nothing else drawn over the view.
    ///   - CargoLogistics: amber industrial/logistics look — bordered readout
    ///                panels plus vertical bar-gauges for altitude and payload
    ///                load, since ceiling and cargo weight are what a logistics
    ///                pilot actually watches.
    /// All three share the same telemetry math (Update()) and the same secondary
    /// instruments (heading tape, wind/temp, NAV minimap, payload pips, low-power
    /// warning) — only their color, position, and a handful of style-only widgets
    /// (ladder/tapes/gauges) differ. Every element toggle in Settings ▸ Game still
    /// works uniformly across all three skins.
    /// </summary>
    public class FlightHUD : MonoBehaviour
    {
        public static FlightHUD Instance { get; private set; }

        // Style theme colors — chrome only; ordnance-type tinting (payload pips,
        // military/civilian payload fill) is a separate semantic and untouched.
        private static readonly Color MilPrimary = new Color(0.35f, 0.95f, 0.55f, 1f);
        private static readonly Color MilDim = new Color(0.35f, 0.95f, 0.55f, 0.55f);
        private static readonly Color MilWarn = new Color(0.95f, 0.25f, 0.22f, 1f);
        private static readonly Color CivPrimary = new Color(0.95f, 0.97f, 1f, 1f);
        private static readonly Color CivDim = new Color(0.95f, 0.97f, 1f, 0.6f);
        private static readonly Color CargoPrimary = new Color(1f, 0.72f, 0.2f, 1f);
        private static readonly Color CargoDim = new Color(1f, 0.72f, 0.2f, 0.6f);
        private static readonly Color CargoWarn = new Color(0.95f, 0.28f, 0.2f, 1f);

        private DroneFlightController _flight;
        private RectTransform _root;
        private DroneCategory _style;
        private Color _primary, _dim, _warnColor;

        private TMPro.TextMeshProUGUI _speed, _alt, _throttle, _battery, _vspeed, _lat, _lon, _warning;
        private TMPro.TextMeshProUGUI _camModeLabel, _payloadLabel, _dropHint, _fpsLabel;
        private RectTransform _batteryFill;
        private RectTransform _powerIconHolder;
        private RectTransform _payloadPipsRow;
        private RectTransform _reticleCross, _reticleV;
        private RectTransform[] _reticleCorners;
        private RectTransform _horizonLine; // Civilian / Cargo simple attitude cue

        private int _hardpoints;
        private bool _militaryPayload;
        private bool _kamikaze;
        private UnityEngine.UI.Image[] _payloadPips;
        private PayloadDropper _dropper;

        // Transient flight-event callout (e.g. "PARACHUTE DEPLOYED") — see
        // ShowFlightMessage. Separate from _warning, which is a persistent
        // power-state banner recomputed every frame, not a one-off event ping.
        private const float FlightMessageDurationSec = 2.6f;
        private const float FlightMessageFadeSec = 0.6f;
        private TMPro.TextMeshProUGUI _flightMessageLabel;
        private float _flightMessageTimer;

        // Heading tape (top ribbon) — replaces the old radial compass dial.
        private RectTransform _headingWidget, _headingRibbon;
        private TMPro.TextMeshProUGUI[] _headingTickLabels;
        private float[] _headingTickDegrees;
        private TMPro.TextMeshProUGUI _headingCenterLabel;

        // Wind / temperature — compact readouts tucked beside the power gauge.
        private RectTransform _windDial, _windNeedle;
        private TMPro.TextMeshProUGUI _windSpeedLabel;
        private RectTransform _tempPanel;
        private TMPro.TextMeshProUGUI _tempLabel;

        // Military-only: artificial-horizon pitch ladder.
        private RectTransform _ladder;

        // Military-only: scrolling vertical speed/altitude tapes.
        private RectTransform _speedTapeArea, _altTapeArea;
        private TMPro.TextMeshProUGUI[] _speedTickLabels, _altTickLabels;
        private TMPro.TextMeshProUGUI _speedCenterLabel, _altCenterLabel;

        // Cargo-only: vertical bar-gauges for altitude ceiling and payload load.
        private RectTransform _altGaugeArea, _altGaugeFill;
        private RectTransform _payloadGaugeArea, _payloadGaugeFill;

        private RectTransform _minimapFrame;
        private RectTransform _minimapOperator, _minimapNose;
        private TMPro.TextMeshProUGUI _minimapOperatorLabel;
        private Vector3 _minimapOperatorGroundPos; // GameManager.SpawnLocalPosition, cached once — same X/Z the operator prop actually stands at
        private bool _minimapOperatorPosCached;
        private const float MinimapRangeM = 400f;
        private const float MinimapRadiusPx = 64f;

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
            _style = flight.Spec.Category;
            switch (_style)
            {
                case DroneCategory.Military:
                    _primary = MilPrimary; _dim = MilDim; _warnColor = MilWarn;
                    break;
                case DroneCategory.CargoLogistics:
                    _primary = CargoPrimary; _dim = CargoDim; _warnColor = CargoWarn;
                    break;
                default: // Civilian
                    _primary = CivPrimary; _dim = CivDim; _warnColor = AccentWarn;
                    break;
            }

            _root = Panel_(canvas.transform, "HUDRoot", Color.clear, Vector2.zero, Vector2.one);

            BuildTopLeftStack();
            BuildTopRightStack();
            BuildHeadingTape();
            BuildReticle();

            if (_style == DroneCategory.Military)
            {
                BuildPitchLadder();
                BuildVerticalTape(true, out _speedTapeArea, out _speedTickLabels, out _speedCenterLabel);
                BuildVerticalTape(false, out _altTapeArea, out _altTickLabels, out _altCenterLabel);
            }
            else
            {
                BuildSimpleHorizon();
            }

            if (_style == DroneCategory.CargoLogistics)
            {
                _altGaugeArea = BuildVerticalGauge("AltGauge", 0.035f, "ALT", out _altGaugeFill);
                _payloadGaugeArea = BuildVerticalGauge("PayloadGauge", 0.945f, "LOAD", out _payloadGaugeFill);
            }

            BuildBottomBar();
            BuildWindTempCompact();
            BuildMinimap();
            BuildFpsCounter();

            _warning = Label(_root, "", 34, new Vector2(0.2f, 0.83f), new Vector2(0.8f, 0.89f),
                             _warnColor, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            BuildFlightMessage();

            BuildPhotoModeOverlay();

            SetCameraMode(CamMode.ChaseDefault);
            SetVisible(GameManager.Instance.Settings.ShowHud);
            ApplyHudElementSettings();
        }

        private void BuildTopLeftStack()
        {
            Label(_root, _flight.Spec.DisplayName, 20, new Vector2(0.02f, 0.905f), new Vector2(0.32f, 0.945f),
                  _primary, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            _lat = Label(_root, "LAT 0.00000°", 15, new Vector2(0.02f, 0.865f), new Vector2(0.32f, 0.90f), _dim);
            _lon = Label(_root, "LON 0.00000°", 15, new Vector2(0.02f, 0.83f), new Vector2(0.32f, 0.865f), _dim);
        }

        private void BuildTopRightStack()
        {
            _camModeLabel = Label(_root, "CHASE DEFAULT", 18, new Vector2(0.68f, 0.905f), new Vector2(0.98f, 0.945f),
                                  _primary, TMPro.TextAlignmentOptions.Right, TMPro.FontStyles.Bold);
            _payloadLabel = Label(_root, "", 15, new Vector2(0.68f, 0.865f), new Vector2(0.98f, 0.90f),
                                  _dim, TMPro.TextAlignmentOptions.Right);
        }

        /// <summary>Horizontal scrolling heading tape along the top edge — the FPV/jet-HUD
        /// replacement for the old radial compass dial. A fixed pool of tick labels (every
        /// 15°, cardinals bold) get repositioned every frame by their signed angular delta
        /// from the current heading (Mathf.DeltaAngle) times a px-per-degree scale derived
        /// from the ribbon's own live rect width, and hidden once they scroll outside the
        /// visible window — RectMask2D on the ribbon clips anything that slips past the
        /// edge before Update() gets a chance to deactivate it.</summary>
        private const float HeadingHalfWindowDeg = 75f;

        private void BuildHeadingTape()
        {
            _headingWidget = Panel_(_root, "HeadingWidget", Color.clear, Vector2.zero, Vector2.one);

            var ribbon = Panel_(_headingWidget, "HeadingRibbon", new Color(0, 0, 0, 0.30f),
                                new Vector2(0.30f, 0.955f), new Vector2(0.70f, 1f));
            ribbon.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            _headingRibbon = ribbon;
            Panel_(ribbon, "BottomBorder", _primary, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 2));

            _headingTickDegrees = new float[24];
            _headingTickLabels = new TMPro.TextMeshProUGUI[24];
            for (int i = 0; i < 24; i++)
            {
                float deg = i * 15f;
                _headingTickDegrees[i] = deg;
                bool cardinal = deg % 90f == 0f;
                string text = cardinal ? CardinalName(deg) : ((int)deg).ToString("000");
                var lbl = Label(ribbon, text, cardinal ? 16 : 12, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                cardinal ? _primary : _dim, TMPro.TextAlignmentOptions.Center,
                                cardinal ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal);
                lbl.enableWordWrapping = false;
                var rt = lbl.rectTransform;
                rt.offsetMin = new Vector2(-22, -10); rt.offsetMax = new Vector2(22, 10);
                _headingTickLabels[i] = lbl;
            }

            // Fixed downward pointer + boxed numeric readout just below the ribbon.
            Panel_(ribbon, "Pointer", _primary, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-3, -2), new Vector2(3, 4));
            var box = Panel_(_headingWidget, "HeadingBox", new Color(0, 0, 0, 0.55f), new Vector2(0.465f, 0.915f), new Vector2(0.535f, 0.953f));
            _headingCenterLabel = Label(box, "000", 16, Vector2.zero, Vector2.one, _primary,
                                       TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
        }

        private static string CardinalName(float deg) => deg switch
        {
            0f => "N", 90f => "E", 180f => "S", 270f => "W", _ => ((int)deg).ToString("000"),
        };

        private void BuildReticle()
        {
            var reticle = Panel_(_root, "Reticle", Color.clear, Vector2.zero, Vector2.one);
            _reticleCross = Panel_(reticle, "CrossH", _primary, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                    new Vector2(-16, -1), new Vector2(16, 1));
            _reticleV = Panel_(reticle, "CrossV", _primary, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
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

        /// <summary>Civilian/Cargo attitude cue — a single roll+pitch line, same
        /// treatment the whole HUD used before this redesign.</summary>
        private void BuildSimpleHorizon()
        {
            var cueArea = Panel_(_root, "AttitudeCue", Color.clear, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f),
                                  new Vector2(-90, -30), new Vector2(90, 30));
            _horizonLine = Panel_(cueArea, "HorizonLine", _dim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                   new Vector2(-90, -1), new Vector2(90, 1));
            Panel_(cueArea, "BoreRef", _primary, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(-4, -4), new Vector2(4, 4));
        }

        /// <summary>Military artificial-horizon pitch ladder — rungs every 10° built once
        /// as static geometry around a single pivot transform (_ladder); Update() rotates
        /// that one pivot by -roll and translates it by -pitch, exactly the same two-line
        /// math the old single-line horizon cue used, so every rung moves together as a
        /// rigid ladder. The permanent boresight cross (BuildReticle) stays fixed at
        /// center as the "where the nose actually points" reference the ladder moves past.</summary>
        private const float LadderPxPerDeg = 4f;

        private void BuildPitchLadder()
        {
            var area = Panel_(_root, "PitchLadderArea", Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                              new Vector2(-140, -140), new Vector2(140, 140));
            _ladder = Panel_(area, "LadderPivot", Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            foreach (int a in new[] { 10, 20, 30 })
            {
                AddLadderRung(a);
                AddLadderRung(-a);
            }
        }

        private void AddLadderRung(int angleDeg)
        {
            float y = angleDeg * LadderPxPerDeg;
            var rung = Panel_(_ladder, "Rung" + angleDeg, Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                              new Vector2(-90, y - 10), new Vector2(90, y + 10));
            Panel_(rung, "L", _primary, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-90, 9), new Vector2(-30, 11));
            Panel_(rung, "R", _primary, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30, 9), new Vector2(90, 11));
            var lbl = Label(rung, Mathf.Abs(angleDeg).ToString(), 12, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            _primary, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            lbl.enableWordWrapping = false;
            var rt = lbl.rectTransform;
            rt.offsetMin = new Vector2(-118, 0); rt.offsetMax = new Vector2(-98, 20);
        }

        /// <summary>Military scrolling vertical tape (speed left of center, altitude right)
        /// — a fixed pool of 5 tick labels recomputed every frame from the live value
        /// (nearest-step baseline ± 2 steps) rather than a true masked-scroll, so no extra
        /// content/viewport plumbing is needed: the same illusion, far less machinery.</summary>
        private void BuildVerticalTape(bool isSpeed, out RectTransform area,
            out TMPro.TextMeshProUGUI[] ticks, out TMPro.TextMeshProUGUI centerLabel)
        {
            float x0 = isSpeed ? 0.155f : 0.845f;
            var areaRt = Panel_(_root, isSpeed ? "SpeedTape" : "AltTape", Color.clear,
                                new Vector2(x0, 0.5f), new Vector2(x0, 0.5f), new Vector2(-40, -110), new Vector2(40, 110));
            areaRt.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            area = areaRt;

            var t = new TMPro.TextMeshProUGUI[5];
            for (int i = 0; i < 5; i++)
            {
                var lbl = Label(areaRt, "", 14, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), _dim,
                                TMPro.TextAlignmentOptions.Center);
                lbl.enableWordWrapping = false;
                var rt = lbl.rectTransform;
                rt.offsetMin = new Vector2(-38, -12); rt.offsetMax = new Vector2(38, 12);
                t[i] = lbl;
            }
            ticks = t;

            var box = Panel_(areaRt, "CenterBox", new Color(0, 0, 0, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(-38, -15), new Vector2(38, 15));
            centerLabel = Label(box, "0", 18, Vector2.zero, Vector2.one, _primary,
                                TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
        }

        /// <summary>Cargo vertical bar-gauge (altitude ceiling / payload load) — a simple
        /// bottom-anchored fill, same "anchorMax driven by fraction" convention the power
        /// gauge already uses, placed at the screen's side edges.</summary>
        private RectTransform BuildVerticalGauge(string name, float x0, string caption, out RectTransform fill)
        {
            Label(_root, caption, 12, new Vector2(x0 - 0.03f, 0.775f), new Vector2(x0 + 0.05f, 0.80f),
                  _dim, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

            var area = Panel_(_root, name, new Color(0, 0, 0, 0.35f), new Vector2(x0, 0.22f), new Vector2(x0 + 0.02f, 0.77f));
            Panel_(area, "Track", new Color(1, 1, 1, 0.08f), new Vector2(0.15f, 0.02f), new Vector2(0.85f, 0.98f));
            var fillArea = Panel_(area, "FillArea", Color.clear, new Vector2(0.15f, 0.02f), new Vector2(0.85f, 0.98f));
            fill = Panel_(fillArea, "Fill", _primary, new Vector2(0f, 0f), new Vector2(1f, 0f));
            return area;
        }

        private void BuildBottomBar()
        {
            var bar = Panel_(_root, "HUDBar", new Color(0, 0, 0, 0.45f), new Vector2(0, 0), new Vector2(1, 0.13f));
            Panel_(bar, "TopBorder", _primary, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(0, 2));

            bool showSpeedAlt = _style != DroneCategory.Military;
            float colX = 0.02f;
            if (showSpeedAlt)
            {
                _speed = Label(bar, "0 km/h", 22, new Vector2(0.02f, 0.5f), new Vector2(0.16f, 1), _primary);
                _alt = Label(bar, "ALT 0 m", 22, new Vector2(0.17f, 0.5f), new Vector2(0.31f, 1), _primary);
                colX = 0.33f;
            }
            _throttle = Label(bar, "THR 0%", 22, new Vector2(colX, 0.5f), new Vector2(colX + 0.14f, 1), _dim);
            float battX = colX + 0.16f;
            _battery = Label(bar, "BAT 100%", 22, new Vector2(battX, 0.5f), new Vector2(battX + 0.18f, 1), _primary);
            BuildPowerIcon(bar, new Vector2(battX + 0.185f, 0.58f), new Vector2(battX + 0.235f, 0.92f),
                          _flight.Spec.PowerSystem == PowerSystemType.Fuel);

            // Row 2 (bottom half): vertical speed, drop-payload hint, hardpoint pips.
            _vspeed = Label(bar, "V/S 0.0 m/s", 18, new Vector2(0.02f, 0), new Vector2(0.22f, 0.48f), _dim);
            // Kamikaze airframes have nothing to release — the whole drone is the
            // munition and detonates on impact, so the hint says so instead of [I].
            _kamikaze = _flight.Spec.IsKamikazeClass;
            _dropHint = Label(bar, _kamikaze ? "IMPACT DETONATION" : "[I] DROP", 18,
                              new Vector2(0.24f, 0), new Vector2(0.48f, 0.48f), _dim,
                              TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);

            BuildPayloadIcons(bar);
        }

        /// <summary>Row of hardpoint icons (bottom bar, right side) — one pip per
        /// DroneSpecification.PayloadHardpoints, shaped by PayloadKind so the icon itself
        /// communicates ordnance TYPE, not just "military vs. civilian." Drops are
        /// sequential per store (see PayloadDropper), so pips go dark one at a time as
        /// stores are expended, then all relight after the rearm cooldown. Skipped
        /// entirely for drones with no payload capability (e.g. the racing quad).</summary>
        private void BuildPayloadIcons(Transform bar)
        {
            _hardpoints = _flight.Spec.PayloadHardpoints;
            _militaryPayload = _flight.Spec.IsMilitaryClass;
            if (_hardpoints <= 0 || _flight.Spec.MaxPayloadKg <= 0f) return;

            var kind = _flight.EffectivePayloadKind;
            _payloadPipsRow = Panel_(bar, "PayloadPipsRow", Color.clear, new Vector2(0.55f, 0f), new Vector2(0.98f, 0.48f));
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
        /// inset inside it. _batteryFill keeps the exact same role Update() already drives
        /// it by (anchorMax.x = powerPct) — only its container/theme color changed.</summary>
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

            _batteryFill = Panel_(fillArea, "Fill", _primary, Vector2.zero, new Vector2(0f, 1f));
        }

        /// <summary>Compact wind/temperature readouts tucked directly beside the power
        /// gauge (Settings ▸ Game ▸ HUD elements can toggle either independently) — replaces
        /// the old stacked corner panels with two small right-aligned lines, matching the
        /// "redistribute secondary instruments to the edges, keep them out of the way"
        /// direction the rest of this redesign follows.</summary>
        private void BuildWindTempCompact()
        {
            _windDial = Panel_(_root, "WindCompact", Color.clear, new Vector2(0.80f, 0.155f), new Vector2(0.895f, 0.185f));
            _windNeedle = Panel_(_windDial, "NeedleArea", Color.clear, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                 new Vector2(-6f, -6f), new Vector2(6f, 6f));
            Panel_(_windNeedle, "Tail", _dim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-1f, -6f), new Vector2(1f, 0f));
            Panel_(_windNeedle, "Head", _primary, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-1f, 0f), new Vector2(1f, 6f));
            _windSpeedLabel = Label(_windDial, "-- m/s", 13, new Vector2(0.12f, 0f), new Vector2(1f, 1f), _dim,
                                    TMPro.TextAlignmentOptions.Right, TMPro.FontStyles.Bold);

            _tempPanel = Panel_(_root, "TempCompact", Color.clear, new Vector2(0.90f, 0.155f), new Vector2(0.98f, 0.185f));
            _tempLabel = Label(_tempPanel, "-- °C", 13, Vector2.zero, Vector2.one, _dim,
                               TMPro.TextAlignmentOptions.Right, TMPro.FontStyles.Bold);
        }

        /// <summary>Square "radar" nav readout, top-right — a framed instrument rather
        /// than a literal top-down camera view (which would need a second camera
        /// actively streaming Cesium 3D tiles, unverified without an Editor). North-up,
        /// fixed at the drone (center, with a heading nose-line); the other tracked
        /// point is the OPERATOR marker — GameManager.SpawnLocalPosition, the exact
        /// ground X/Z DroneOperatorBuilder actually plants the operator figure/beacon
        /// at, cached once in Update() the first time it runs (position is fixed for
        /// the whole flight). This replaced an earlier version of this marker that
        /// assumed Unity world (0,0,0) was always the right point — true only for the
        /// map's own default spawn, wrong the moment a Flying Conditions ▸ Spawn
        /// Location preset was picked, since that offsets the actual launch point away
        /// from world origin. An upright square (vs. Landmarks' 45°-rotated diamonds
        /// below) keeps it visually distinct from every other marker on the dial at a
        /// glance. Shared by all three HUD styles (Military/Civilian/CargoLogistics) —
        /// only _primary/_dim differ, so this one change updates every style's minimap.</summary>
        private void BuildMinimap()
        {
            var frame = Panel_(_root, "MinimapFrame", new Color(_primary.r, _primary.g, _primary.b, 0.5f),
                                new Vector2(0.98f, 0.83f), new Vector2(0.98f, 0.83f),
                                new Vector2(-168f, -168f), new Vector2(0f, 0f));
            _minimapFrame = frame;

            Label(frame, "NAV", 11, new Vector2(0f, 0.91f), new Vector2(1f, 1f),
                  _dim, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            _minimapOperatorLabel = Label(frame, "OPERATOR --", 11, new Vector2(0f, 0f), new Vector2(1f, 0.09f),
                                          _dim, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

            var box = Panel_(frame, "MinimapBg", new Color(0, 0, 0, 0.45f), new Vector2(0f, 0.09f), new Vector2(1f, 0.91f),
                              new Vector2(2f, 2f), new Vector2(-2f, -2f));

            // Decorative range rings (nested translucent squares, not true circles —
            // same reasoning as the frame itself) giving a rough sense of scale, each
            // labeled with its real-world radius so the rings mean something at a glance.
            Panel_(box, "RingOuter", new Color(1, 1, 1, 0.05f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(-MinimapRadiusPx, -MinimapRadiusPx), new Vector2(MinimapRadiusPx, MinimapRadiusPx));
            Panel_(box, "RingInner", new Color(1, 1, 1, 0.07f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(-MinimapRadiusPx * 0.5f, -MinimapRadiusPx * 0.5f), new Vector2(MinimapRadiusPx * 0.5f, MinimapRadiusPx * 0.5f));
            var ringLabel = Label(box, $"{MinimapRangeM:0}M", 8, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                  _dim, TMPro.TextAlignmentOptions.Center);
            ringLabel.enableWordWrapping = false;
            ringLabel.raycastTarget = false;
            var ringLabelRt = ringLabel.rectTransform;
            ringLabelRt.sizeDelta = new Vector2(40f, 12f);
            ringLabelRt.anchoredPosition = new Vector2(0f, MinimapRadiusPx - 7f);

            // Fixed "N" cardinal at the top of the dial — north-up, so this never moves
            // or rotates; the only orientation cue a first-time player needs to read
            // the dial correctly.
            var northLabel = Label(box, "N", 10, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                   _primary, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            northLabel.enableWordWrapping = false;
            northLabel.raycastTarget = false;
            var northRt = northLabel.rectTransform;
            northRt.sizeDelta = new Vector2(16f, 12f);
            northRt.anchoredPosition = new Vector2(0f, -8f);

            // Operator marker — upright square (deliberately NOT rotated, unlike the
            // diamond-shaped Landmarks below, so it reads as visually distinct at a
            // glance), clamped to the ring's edge once the real distance exceeds
            // MinimapRangeM (classic off-scale radar behavior).
            _minimapOperator = Panel_(box, "Operator", _primary, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                      new Vector2(-4.5f, -4.5f), new Vector2(4.5f, 4.5f));

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
        /// map — same off-scale ring-edge clamp treatment as the OPERATOR marker (most
        /// real landmarks sit well outside MinimapRangeM, so this mainly reads as "which
        /// direction to fly," same as OPERATOR already does past 400 m). Offsets are
        /// computed once here via the flat-earth approximation (MapDefinition.
        /// FlatOffsetMeters) since neither the map nor its landmarks change mid-flight.</summary>
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

                var marker = Panel_(box, "Landmark_" + lm.Name, _primary, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                    new Vector2(-3f, -3f), new Vector2(3f, 3f));
                marker.localRotation = Quaternion.Euler(0, 0, 45f);
                marker.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
                _minimapLandmarks[i] = marker;

                var label = Label(marker, lm.Name, 8, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                  _primary, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
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

        /// <summary>Centered, self-fading event callout just above the reticle — e.g.
        /// ParachuteController's "PARACHUTE DEPLOYED" / "TOO LOW TO DEPLOY" pings (see
        /// ShowFlightMessage). Starts hidden; Update() fades it out and deactivates it
        /// once its timer runs out.</summary>
        private void BuildFlightMessage()
        {
            _flightMessageLabel = Label(_root, "", 22, new Vector2(0.25f, 0.685f), new Vector2(0.75f, 0.735f),
                                       _primary, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            _flightMessageLabel.gameObject.SetActive(false);
        }

        /// <summary>Shows a brief on-screen callout for a one-off flight event (parachute
        /// deploy/deny, and available for similar future events) — separate from
        /// _warning, which is a persistent, continuously-recomputed power-state banner,
        /// not a one-shot ping. Themed in the HUD's own primary/warn color so it reads
        /// consistently across all three HUD styles. Restarting the timer on a repeat
        /// call (rather than queuing) is deliberate: only the latest event matters.</summary>
        public void ShowFlightMessage(string text, bool isWarning = false)
        {
            if (_flightMessageLabel == null) return;
            _flightMessageLabel.text = text;
            _flightMessageLabel.color = isWarning ? _warnColor : _primary;
            _flightMessageLabel.gameObject.SetActive(true);
            _flightMessageTimer = FlightMessageDurationSec;
        }

        /// <summary>Bottom-center control-hint bar + live FOV/exposure readout, shown only
        /// while DroneCameraRig's detached Photo mode is active (see SetPhotoModeActive/
        /// UpdatePhotoModeReadout, driven every frame from DroneCameraRig.UpdatePhotoMode).</summary>
        private void BuildPhotoModeOverlay()
        {
            _photoPanel = Panel_(_root, "PhotoModeBar", new Color(0, 0, 0, 0.55f),
                                 new Vector2(0.24f, 0.02f), new Vector2(0.76f, 0.095f));
            Label(_photoPanel, "PHOTO MODE", 15, new Vector2(0.03f, 0.52f), new Vector2(0.30f, 0.94f),
                  _primary, TMPro.TextAlignmentOptions.MidlineLeft, TMPro.FontStyles.Bold);
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
                _ => _primary,
            };
            _reticleCross.GetComponent<UnityEngine.UI.Image>().color = reticleColor;
            _reticleV.GetComponent<UnityEngine.UI.Image>().color = reticleColor;
        }

        /// <summary>Live-toggled from Settings ▸ Game while already in flight.</summary>
        public void SetVisible(bool visible) => _root.gameObject.SetActive(visible);

        /// <summary>Applies each per-element HUD visibility toggle from Settings ▸ Game.
        /// Called once from Init() and again live any time one is flipped from the
        /// pause menu mid-flight. Every widget lookup is null-guarded since which ones
        /// exist depends on the drone's HUD style (e.g. speed tapes only exist on
        /// Military, gauges only on CargoLogistics). Narrator (voice+text) isn't handled
        /// here — it's gated directly in NarratorController.Enqueue().</summary>
        public void ApplyHudElementSettings()
        {
            var s = GameManager.Instance.Settings;
            if (_speed != null) _speed.gameObject.SetActive(s.HudShowSpeed);
            if (_speedTapeArea != null) _speedTapeArea.gameObject.SetActive(s.HudShowSpeed);
            if (_alt != null) _alt.gameObject.SetActive(s.HudShowAltitude);
            if (_altTapeArea != null) _altTapeArea.gameObject.SetActive(s.HudShowAltitude);
            if (_altGaugeArea != null) _altGaugeArea.gameObject.SetActive(s.HudShowAltitude);
            _throttle.gameObject.SetActive(s.HudShowThrottle);
            _lat.gameObject.SetActive(s.HudShowGps);
            _lon.gameObject.SetActive(s.HudShowGps);
            _battery.gameObject.SetActive(s.HudShowBattery);
            if (_powerIconHolder != null) _powerIconHolder.gameObject.SetActive(s.HudShowBattery);
            _payloadLabel.gameObject.SetActive(s.HudShowPayload);
            if (_payloadPipsRow != null) _payloadPipsRow.gameObject.SetActive(s.HudShowPayload);
            if (_payloadGaugeArea != null) _payloadGaugeArea.gameObject.SetActive(s.HudShowPayload);
            if (_headingWidget != null) _headingWidget.gameObject.SetActive(s.HudShowCompass);
            if (_windDial != null) _windDial.gameObject.SetActive(s.HudShowWind);
            if (_tempPanel != null) _tempPanel.gameObject.SetActive(s.HudShowTemperature);
            if (_minimapFrame != null) _minimapFrame.gameObject.SetActive(s.HudShowMinimap);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private static float NormalizeAngle(float deg) => deg > 180f ? deg - 360f : deg;

        private void Update()
        {
            if (_flight == null || !_root.gameObject.activeSelf) return;

            float speedKmh = _flight.CurrentSpeedKmh;
            float altM = _flight.transform.position.y;
            float heading = _flight.transform.eulerAngles.y;

            if (_speed != null) _speed.text = $"{speedKmh:0} km/h";
            if (_alt != null) _alt.text = $"ALT {altM:0} m";
            _throttle.text = $"THR {_flight.Throttle01 * 100f:0}%";
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
                powerPct > 0.3f ? _primary : _warnColor;

            float payloadKg = _flight.Payload != null ? _flight.Payload.CurrentPayloadKg : 0f;
            bool loaded = payloadKg > 0f;
            // Multi-hardpoint drops are sequential now — show live stores-remaining
            // from PayloadDropper where one exists (kamikaze airframes have none).
            if (_dropper == null && !_kamikaze) _dropper = _flight.GetComponent<PayloadDropper>();
            int remaining = _dropper != null
                ? Mathf.RoundToInt(_dropper.StoresRemaining * (float)_hardpoints / Mathf.Max(1, _dropper.StoreCount))
                : loaded ? _hardpoints : 0;

            // AT-R4 Hornet only: while Warhead is the live-selected category ([J] to
            // switch — see PayloadDropper.TrySwitchPayloadKind), [I] self-destructs
            // instead of dropping a store (PayloadDropper.TryDrop), so the hint/label
            // need to say so instead of the normal drop wording — showing "[I] DROP"
            // here would be actively misleading about what the key now does.
            bool hornetSelfDestruct = _flight.Spec.Id == "at-r4" && _flight.EffectivePayloadKind == PayloadKind.Warhead;
            _payloadLabel.text = _kamikaze
                ? (loaded ? $"WARHEAD ARMED {payloadKg:0.#} kg" : "WARHEAD EXPENDED")
                : hornetSelfDestruct
                    ? $"WARHEAD ARMED {payloadKg:0.#} kg — SELF-DESTRUCT"
                    : loaded
                        ? $"{remaining}/{_hardpoints} {_flight.Spec.PayloadTypeName.ToUpperInvariant()} {payloadKg:0.#} kg"
                        : _hardpoints > 0 ? $"0/{_hardpoints} REARMING" : "PAYLOAD EMPTY";
            _dropHint.text = _kamikaze ? "IMPACT DETONATION" : hornetSelfDestruct ? "[I] SELF-DESTRUCT" : "[I] DROP";
            _dropHint.color = hornetSelfDestruct ? _warnColor // always available, regardless of remaining payload weight
                             : _kamikaze ? (loaded ? _warnColor : _dim)
                             : (loaded ? _primary : _dim);

            if (_payloadPips != null)
            {
                Color fillColor = _militaryPayload ? AccentWarn : Accent;
                Color emptyColor = new Color(1, 1, 1, 0.15f);
                for (int i = 0; i < _payloadPips.Length; i++)
                    _payloadPips[i].color = ((_kamikaze || hornetSelfDestruct) ? loaded : i < remaining) ? fillColor : emptyColor;
            }

            if (_payloadGaugeFill != null)
            {
                float maxKg = Mathf.Max(0.01f, _flight.Spec.MaxPayloadKg);
                _payloadGaugeFill.anchorMax = new Vector2(1f, Mathf.Clamp01(payloadKg / maxKg));
            }
            if (_altGaugeFill != null)
            {
                float ceilingM = Mathf.Max(1f, _flight.Spec.MaxAltitudeM);
                _altGaugeFill.anchorMax = new Vector2(1f, Mathf.Clamp01(altM / ceilingM));
            }

            UpdateHeadingTape(heading);
            if (_speedTapeArea != null) UpdateVerticalTape(speedKmh, 20f, _speedTickLabels, _speedCenterLabel);
            if (_altTapeArea != null) UpdateVerticalTape(altM, 20f, _altTickLabels, _altCenterLabel);

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

            if (_minimapOperator != null)
            {
                // GameManager.SpawnLocalPosition is the exact ground X/Z
                // DroneOperatorBuilder plants the operator figure/beacon at — fixed for
                // the whole flight, so it's read once and cached rather than every
                // frame. NOT the same as world (0,0,0) whenever a Flying Conditions ▸
                // Spawn Location preset offset the actual launch point away from the
                // map's own default origin.
                if (!_minimapOperatorPosCached)
                {
                    _minimapOperatorPosCached = true;
                    _minimapOperatorGroundPos = GameManager.Instance != null
                        ? GameManager.Instance.SpawnLocalPosition : Vector3.zero;
                }

                Vector3 pos = _flight.transform.position;
                // Landmarks are stored relative to the map's own origin (world XZ), so
                // their re-basing below still needs "origin minus drone" specifically —
                // kept separate from the operator's own offset, which uses the actual
                // spawn point instead and can legitimately differ from world origin.
                Vector2 originOffsetM = new Vector2(-pos.x, -pos.z);
                Vector2 operatorOffsetM = new Vector2(_minimapOperatorGroundPos.x - pos.x, _minimapOperatorGroundPos.z - pos.z);
                float operatorDistM = operatorOffsetM.magnitude;

                float scale = MinimapRadiusPx / MinimapRangeM;
                Vector2 rawOperator = operatorOffsetM * scale;
                _minimapOperator.anchoredPosition = rawOperator.magnitude > MinimapRadiusPx
                    ? rawOperator.normalized * MinimapRadiusPx : rawOperator;
                _minimapNose.localEulerAngles = new Vector3(0, 0, -heading);
                _minimapOperatorLabel.text = operatorDistM < 1000f
                    ? $"OPERATOR {operatorDistM:0} m" : $"OPERATOR {operatorDistM / 1000f:0.0} km";

                if (_minimapLandmarks != null)
                {
                    // Landmark offsets are stored relative to the map origin (world XZ);
                    // originOffsetM is "origin minus drone," so adding it re-bases the
                    // same offset onto "landmark minus drone" — the vector the marker needs.
                    for (int i = 0; i < _minimapLandmarks.Length; i++)
                    {
                        Vector2 lmRaw = (_minimapLandmarkOffsetsM[i] + originOffsetM) * scale;
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

            if (_flightMessageTimer > 0f)
            {
                _flightMessageTimer -= Time.deltaTime;
                float alpha = _flightMessageTimer > FlightMessageFadeSec
                    ? 1f : Mathf.Clamp01(_flightMessageTimer / FlightMessageFadeSec);
                var mc = _flightMessageLabel.color;
                _flightMessageLabel.color = new Color(mc.r, mc.g, mc.b, alpha);
                if (_flightMessageTimer <= 0f) _flightMessageLabel.gameObject.SetActive(false);
            }

            float roll = NormalizeAngle(_flight.transform.eulerAngles.z);
            float pitch = NormalizeAngle(_flight.transform.eulerAngles.x);
            if (_ladder != null)
            {
                _ladder.localEulerAngles = new Vector3(0, 0, -roll);
                _ladder.anchoredPosition = new Vector2(0, Mathf.Clamp(-pitch * LadderPxPerDeg, -100f, 100f));
            }
            if (_horizonLine != null)
            {
                var cue = (RectTransform)_horizonLine.parent;
                cue.localEulerAngles = new Vector3(0, 0, -roll);
                cue.anchoredPosition = new Vector2(0, Mathf.Clamp(-pitch * 0.6f, -25f, 25f));
            }

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

        /// <summary>Repositions the fixed pool of heading-tick labels every frame by their
        /// signed angular delta from the current heading — see BuildHeadingTape's remarks.</summary>
        private void UpdateHeadingTape(float heading)
        {
            if (_headingRibbon == null) return;
            _headingCenterLabel.text = $"{heading:000}";

            float ribbonWidth = _headingRibbon.rect.width;
            float pxPerDeg = (ribbonWidth * 0.5f) / HeadingHalfWindowDeg;
            for (int i = 0; i < _headingTickDegrees.Length; i++)
            {
                float delta = Mathf.DeltaAngle(heading, _headingTickDegrees[i]);
                bool visible = Mathf.Abs(delta) <= HeadingHalfWindowDeg + 12f;
                _headingTickLabels[i].gameObject.SetActive(visible);
                if (visible)
                    _headingTickLabels[i].rectTransform.anchoredPosition = new Vector2(delta * pxPerDeg, 0f);
            }
        }

        /// <summary>Recomputes a 5-tick scrolling window (nearest step ± 2 steps) around
        /// the live value — see BuildVerticalTape's remarks for why this fakes the scroll
        /// via per-frame repositioning instead of a real masked-content scroll.</summary>
        private static void UpdateVerticalTape(float value, float step, TMPro.TextMeshProUGUI[] ticks, TMPro.TextMeshProUGUI centerLabel)
        {
            const float spacingPx = 34f;
            float baseVal = Mathf.Round(value / step) * step;
            for (int i = 0; i < ticks.Length; i++)
            {
                int offsetIdx = i - ticks.Length / 2;
                float tickVal = baseVal + offsetIdx * step;
                bool visible = tickVal >= 0f;
                ticks[i].gameObject.SetActive(visible);
                if (!visible) continue;
                ticks[i].text = $"{tickVal:0}";
                ticks[i].rectTransform.anchoredPosition = new Vector2(0, (tickVal - value) / step * spacingPx);
            }
            centerLabel.text = $"{value:0}";
        }
    }
}
