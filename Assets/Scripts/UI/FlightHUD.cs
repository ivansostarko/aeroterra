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
    /// speed), a payload-drop key hint, and a low-battery warning banner.
    /// </summary>
    public class FlightHUD : MonoBehaviour
    {
        public static FlightHUD Instance { get; private set; }

        private DroneFlightController _flight;
        private RectTransform _root;
        private TMPro.TextMeshProUGUI _speed, _alt, _throttle, _battery, _heading, _vspeed, _lat, _lon, _warning;
        private TMPro.TextMeshProUGUI _camModeLabel, _payloadLabel, _dropHint, _fpsLabel;
        private RectTransform _batteryFill;
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

        private float _fpsSmoothed = 60f;

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
            BuildFpsCounter();

            _warning = Label(_root, "", 34, new Vector2(0.2f, 0.83f), new Vector2(0.8f, 0.89f),
                             AccentWarn, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

            SetCameraMode(CamMode.Chase);
            SetVisible(GameManager.Instance.Settings.ShowHud);
        }

        private void BuildTopStrip()
        {
            var top = Panel_(_root, "TopBar", new Color(0, 0, 0, 0.35f), new Vector2(0, 0.90f), new Vector2(1, 1f));
            Panel_(top, "BottomBorder", Accent, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, -2), new Vector2(0, 0));

            Label(top, _flight.Spec.DisplayName, 22, new Vector2(0.02f, 0), new Vector2(0.35f, 1), TextMain);
            _camModeLabel = Label(top, "CHASE", 22, new Vector2(0.35f, 0), new Vector2(0.65f, 1),
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

            var kind = _flight.Spec.PayloadKind;
            const float x0 = 0.80f, x1 = 0.98f;
            float slot = (x1 - x0) / _hardpoints;
            _payloadPips = new UnityEngine.UI.Image[_hardpoints];
            for (int i = 0; i < _hardpoints; i++)
            {
                float cx0 = x0 + i * slot, cx1 = cx0 + slot;
                _payloadPips[i] = BuildPayloadPip(bar, new Vector2(cx0, 0f), new Vector2(cx1, 0.48f), kind);
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

        private void BuildFpsCounter()
        {
            _fpsLabel = Label(_root, "FPS --", 16, new Vector2(0.85f, 0.855f), new Vector2(0.98f, 0.895f),
                              TextDim, TMPro.TextAlignmentOptions.Right, TMPro.FontStyles.Bold);
        }

        /// <summary>Called by DroneCameraRig on every camera-mode change (key C).</summary>
        public void SetCameraMode(CamMode mode)
        {
            _camModeLabel.text = mode switch
            {
                CamMode.Front => "FRONT",
                CamMode.Bottom => "BOTTOM — SURVEILLANCE",
                CamMode.Thermal => "THERMAL",
                _ => "CHASE",
            };

            bool showReticle = mode != CamMode.Chase;
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
            _warning.text = powerEmpty ? $"{powerLabel} DEPLETED"
                          : powerPct < 0.15f ? $"LOW {powerLabel} — RETURN NOW" : "";
        }
    }
}
