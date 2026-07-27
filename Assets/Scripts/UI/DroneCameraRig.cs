using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using AeroTerra.Core;

namespace AeroTerra.UI
{
    /// <summary>Flight camera view, cycled with InputManager.CameraAction (key C).
    /// Photo is NOT part of that cycle — it's a separate detached mode toggled with
    /// InputManager.PhotoModeAction (key F8), see DroneCameraRig.TogglePhotoMode.</summary>
    public enum CamMode { ChaseDefault, ChaseDetails, Front, Bottom, Thermal, Photo }

    /// <summary>
    /// Drives the single flight Camera through five attached view modes: smooth
    /// 3rd-person ChaseDefault (default), a much closer ChaseDetails follow cam framed
    /// tight enough to show off the airframe while still keeping the whole model in
    /// frame regardless of its size (see ConfigureForTarget's FOV/bounding-radius math),
    /// nose-mounted front view, belly-mounted surveillance/bombing view, and a stylized
    /// thermal look layered on the front view via a URP color grading Volume — plus a
    /// sixth, detached Photo mode: a free-fly camera with mouse-look, WASD/QE movement,
    /// and live FOV/exposure controls (see UpdatePhotoMode), for composing shots away
    /// from the drone's own flight path. Notifies FlightHUD on every mode change so its
    /// overlay matches.
    /// </summary>
    public class DroneCameraRig : MonoBehaviour
    {
        /// <summary>Single flight camera per scene — lets systems that have no direct
        /// reference to it (ExplosionEffect is static, DroneFlightController shouldn't
        /// need to be wired to it) trigger shake without a scene-wide event bus.</summary>
        public static DroneCameraRig Instance { get; private set; }

        public Transform Target;

        public CamMode Mode { get; private set; } = CamMode.ChaseDefault;

        private Volume _thermalVolume;
        private ColorAdjustments _thermalColor;
        private Vignette _thermalVignette;
        private Bloom _thermalBloom;
        private FilmGrain _thermalGrain;
        private Camera _cam;
        private float _baseFov;
        private AeroTerra.Drone.DroneFlightController _flight;
        private Rigidbody _targetRb;

        // Per-airframe chase tuning, derived from the spec + real model bounds the
        // first frame we have a target (a 12 m UCAV needs a very different camera
        // than a 26 cm racing quad). See ConfigureForTarget.
        private bool _configured;
        private float _chaseDist = 6.5f, _chaseHeight = 2.2f;
        private float _posLerp = 5f, _rotLerp = 6f;
        private float _bankFollow;              // 0 = horizon stays level, 1 = full roll with target
        private float _lookAhead;               // seconds of velocity lead in the aim point
        private float _noseOffset = 0.4f, _bellyOffset = 0.3f, _aimHeight = 1f;
        private Vector3 _targetSize = Vector3.one; // cached model bounds, reused by ChaseDetails' framing math

        // ChaseDetails — a much tighter follow cam than ChaseDefault. Distance is derived
        // (not a fixed constant) from the model's own bounding radius and the camera's
        // vertical FOV, so it's always the closest distance that still keeps the WHOLE
        // airframe in frame, whatever its actual size — see ConfigureForTarget.
        private float _chaseDetailsDist = 2f, _chaseDetailsHeight = 0.5f;
        private const float ChaseDetailsMargin = 1.2f; // headroom beyond the exact FOV-fit distance

        private float _shakeMagnitude, _shakeDuration, _shakeTimer;

        // Photo mode — see TogglePhotoMode/UpdatePhotoMode.
        private bool _photoActive;
        private CamMode _prePhotoMode;
        private float _photoYaw, _photoPitch;
        private float _photoExposureEv;
        private Volume _photoVolume;
        private ColorAdjustments _photoColor;
        private const float PhotoMoveSpeed = 6f, PhotoMoveSpeedFast = 18f;
        private const float PhotoLookSensitivity = 0.15f;
        private const float PhotoFovMin = 20f, PhotoFovMax = 100f;
        private const float PhotoExposureMin = -3f, PhotoExposureMax = 3f;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            _cam = GetComponent<Camera>();
            _baseFov = _cam.fieldOfView;
            var camData = _cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;

            // Stylized "thermal" fake built entirely from stock URP Volume overrides
            // (no custom shader/imported asset needed — same "ships-with-Unity only"
            // convention as every material in this project): desaturated cool-green
            // color grade, a dark optic-style vignette, sensor grain, and bloom that
            // blows out bright/hot elements. Intensity scales up at night (see
            // UpdateThermalIntensity) — thermal is meant to read as a "sees in the
            // dark" sensor, not just a color filter that looks the same regardless of
            // ambient light.
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            _thermalColor = profile.Add<ColorAdjustments>(true);
            _thermalColor.saturation.Override(-100f);
            _thermalColor.contrast.Override(35f);
            _thermalColor.colorFilter.Override(new Color(0.55f, 0.95f, 0.65f));
            _thermalColor.postExposure.Override(0f);

            _thermalVignette = profile.Add<Vignette>(true);
            _thermalVignette.color.Override(Color.black);
            _thermalVignette.intensity.Override(0.4f);
            _thermalVignette.smoothness.Override(0.6f);

            _thermalBloom = profile.Add<Bloom>(true);
            _thermalBloom.threshold.Override(0.3f);
            _thermalBloom.intensity.Override(2f);
            _thermalBloom.tint.Override(new Color(0.7f, 1f, 0.75f));

            _thermalGrain = profile.Add<FilmGrain>(true);
            _thermalGrain.type.Override(FilmGrainLookup.Thin2);
            _thermalGrain.intensity.Override(0.3f);
            _thermalGrain.response.Override(0.6f);

            var volumeGo = new GameObject("ThermalVolume");
            volumeGo.transform.SetParent(transform, false);
            _thermalVolume = volumeGo.AddComponent<Volume>();
            _thermalVolume.isGlobal = true;
            _thermalVolume.weight = 0f;
            _thermalVolume.profile = profile;

            // Separate, minimal Volume for Photo mode's exposure slider — kept apart from
            // the thermal profile above so the two can't fight over ColorAdjustments (only
            // one of Thermal/Photo is ever active at once via Mode, but sharing a profile
            // would still mean each mode's Start()-time Override calls stomp the other's).
            var photoProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _photoColor = photoProfile.Add<ColorAdjustments>(true);
            _photoColor.postExposure.Override(0f);

            var photoVolumeGo = new GameObject("PhotoVolume");
            photoVolumeGo.transform.SetParent(transform, false);
            _photoVolume = photoVolumeGo.AddComponent<Volume>();
            _photoVolume.isGlobal = true;
            _photoVolume.weight = 0f;
            _photoVolume.profile = photoProfile;
        }

        private void Update()
        {
            var im = AeroTerra.Input.InputManager.Instance;
            if (im == null) return;

            if (im.PhotoModeAction.WasPressedThisFrame()) TogglePhotoMode();
            if (_photoActive) return; // C-cycle is disabled while the detached camera is active — O exits it

            if (!im.CameraAction.WasPressedThisFrame()) return;

            // Skip modes the airframe has no camera for — a drone with only a front
            // camera never lands on Bottom, one with front+back cycles both. The two
            // Chase modes are external spectator views, not physical cameras, so they're
            // always reachable regardless of loadout (worst case the loop lands back on
            // one of them). Bounded to the first 5 CamMode values (ChaseDefault,
            // ChaseDetails, Front, Bottom, Thermal) — Photo is never reached via this
            // cycle, only via TogglePhotoMode.
            CamMode next = Mode;
            for (int i = 0; i < 5; i++)
            {
                next = (CamMode)(((int)next + 1) % 5);
                if (IsAvailable(next)) break;
            }
            Mode = next;

            _thermalVolume.weight = Mode == CamMode.Thermal ? 1f : 0f;
            FlightHUD.Instance?.SetCameraMode(Mode);
        }

        /// <summary>Enters/exits the detached free-fly Photo mode (key F8), remembering
        /// whichever attached mode was active so exiting restores it exactly rather than
        /// always dropping back to Chase.</summary>
        private void TogglePhotoMode()
        {
            _photoActive = !_photoActive;
            if (_photoActive)
            {
                _prePhotoMode = Mode;
                Mode = CamMode.Photo;
                Vector3 euler = transform.eulerAngles;
                _photoYaw = euler.y;
                _photoPitch = euler.x > 180f ? euler.x - 360f : euler.x;
                _photoExposureEv = 0f;
                _photoColor.postExposure.Override(0f);
                _photoVolume.weight = 1f;
            }
            else
            {
                Mode = _prePhotoMode;
                _photoVolume.weight = 0f;
            }
            FlightHUD.Instance?.SetCameraMode(Mode);
            FlightHUD.Instance?.SetPhotoModeActive(_photoActive);
        }

        /// <summary>Thermal is a genuine "sees in the dark" sensor — at night (when the
        /// ordinary scene is barely lit) it should look brighter and more dramatic than
        /// in daylight, not tinted by the same fixed amount regardless of ambient light.
        /// Scales exposure/contrast/vignette/bloom/grain by how dark the current
        /// SkyPreset is. Runs every frame while thermal is active so changing the sky
        /// preset mid-flight (pause menu ▸ Settings) updates it live.</summary>
        private void UpdateThermalIntensity()
        {
            var sky = GameManager.Instance != null ? GameManager.Instance.Settings.Sky : SkyPreset.Day;
            float night = sky switch
            {
                SkyPreset.Night => 1f,
                SkyPreset.Dusk => 0.5f,
                SkyPreset.Dawn => 0.35f,
                _ => 0f,
            };
            _thermalColor.postExposure.Override(Mathf.Lerp(0.1f, 2.2f, night));
            _thermalColor.contrast.Override(Mathf.Lerp(35f, 60f, night));
            _thermalVignette.intensity.Override(Mathf.Lerp(0.35f, 0.55f, night));
            _thermalBloom.intensity.Override(Mathf.Lerp(1.6f, 3f, night));
            _thermalGrain.intensity.Override(Mathf.Lerp(0.22f, 0.4f, night));
        }

        private bool IsAvailable(CamMode mode)
        {
            if (_flight == null && Target != null) _flight = Target.GetComponent<AeroTerra.Drone.DroneFlightController>();
            var spec = _flight != null ? _flight.Spec : null;
            if (spec == null) return true; // spec not resolved yet — don't lock the player out

            return mode switch
            {
                CamMode.Front => spec.HasFrontCamera,
                CamMode.Thermal => spec.HasThermalCamera,
                CamMode.Bottom => spec.HasBackCamera,
                _ => true, // ChaseDefault / ChaseDetails
            };
        }

        /// <summary>Derive chase-camera character from the airframe: distance/height
        /// scale with the real model bounds, responsiveness and bank-follow come from
        /// the drone class — a racing quad gets a tight, snappy, rolling camera, the
        /// cargo octocopter a calm level one, and the winged UAVs a far cinematic
        /// chase that leans into banked turns like an aircraft cam.</summary>
        private void ConfigureForTarget()
        {
            _configured = true;
            _flight = Target.GetComponent<AeroTerra.Drone.DroneFlightController>();
            _targetRb = Target.GetComponent<Rigidbody>();

            var rends = Target.GetComponentsInChildren<Renderer>();
            Vector3 size = Vector3.one;
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                size = b.size;
            }
            _targetSize = size;
            float span = Mathf.Max(size.x, size.z);

            // Pulled in closer than the old 4.5 / 1.6 / 0.32 figures — those left even
            // small airframes with a distant, floaty chase view.
            _chaseDist = Mathf.Max(3.0f, span * 1.15f);
            _chaseHeight = _chaseDist * 0.26f;
            _noseOffset = size.z * 0.55f + 0.25f;
            _bellyOffset = size.y * 0.55f + 0.2f;
            _aimHeight = Mathf.Clamp(size.y, 0.4f, 3f);

            // ChaseDetails: the closest distance that still fits the WHOLE bounding
            // sphere inside the camera's vertical FOV (the narrower of the two axes on
            // a typical widescreen aspect, so it's the safe one to solve for), plus a
            // flat margin so the drone doesn't touch the frame edges. Scales correctly
            // from a 0.26 m racing quad up to a 12 m UCAV — same math regardless of
            // airframe size, unlike a fixed distance constant.
            float modelRadius = size.magnitude * 0.5f;
            float halfFovRad = _baseFov * 0.5f * Mathf.Deg2Rad;
            _chaseDetailsDist = Mathf.Max(0.5f, modelRadius / Mathf.Sin(halfFovRad) * ChaseDetailsMargin);
            _chaseDetailsHeight = _chaseDetailsDist * 0.22f;

            var spec = _flight != null ? _flight.Spec : null;
            switch (spec != null ? spec.Class : AeroTerra.Drone.DroneClass.CargoDelivery)
            {
                case AeroTerra.Drone.DroneClass.RacingDrone:
                    _chaseDist *= 0.85f; _posLerp = 11f; _rotLerp = 12f; _bankFollow = 0.30f; _lookAhead = 0.10f;
                    break;
                case AeroTerra.Drone.DroneClass.FpvStrike:
                case AeroTerra.Drone.DroneClass.CameraQuad:
                    _posLerp = 8f; _rotLerp = 9f; _bankFollow = 0.20f; _lookAhead = 0.12f;
                    break;
                case AeroTerra.Drone.DroneClass.KamikazeStrike:
                    _posLerp = 6f; _rotLerp = 7f; _bankFollow = 0.45f; _lookAhead = 0.25f;
                    break;
                case AeroTerra.Drone.DroneClass.JetStrike:
                    // Fastest thing in the roster: hang further back, lead the shot
                    // harder, roll hard with the wings.
                    _chaseDist *= 1.15f; _posLerp = 6.5f; _rotLerp = 7.5f; _bankFollow = 0.50f; _lookAhead = 0.30f;
                    break;
                case AeroTerra.Drone.DroneClass.LoiteringMunition:
                case AeroTerra.Drone.DroneClass.ReconStrike:
                case AeroTerra.Drone.DroneClass.SurveyMapping:
                case AeroTerra.Drone.DroneClass.UtilityStrike:
                    // Steady wings: slow, distant, cinematic follow that banks along.
                    _chaseDist *= 1.1f; _posLerp = 3.5f; _rotLerp = 4.5f; _bankFollow = 0.40f; _lookAhead = 0.30f;
                    break;
                default: // CargoDelivery / VtolCargo — calm, level, deliberate
                    _posLerp = 5f; _rotLerp = 6f; _bankFollow = 0f; _lookAhead = 0.15f;
                    break;
            }
        }

        private void LateUpdate()
        {
            if (Target == null) return;
            if (!_configured) ConfigureForTarget();

            if (Mode == CamMode.Photo) { UpdatePhotoMode(); return; }

            if (Mode == CamMode.Thermal) UpdateThermalIntensity();

            switch (Mode)
            {
                case CamMode.Front:
                case CamMode.Thermal:
                    // Sit just ahead of the physical nose, whatever the airframe size.
                    transform.SetPositionAndRotation(
                        Target.position + Target.forward * _noseOffset + Target.up * 0.15f, Target.rotation);
                    break;

                case CamMode.Bottom:
                    transform.SetPositionAndRotation(
                        Target.position - Target.up * _bellyOffset,
                        Quaternion.LookRotation(-Target.up, Target.forward));
                    break;

                case CamMode.ChaseDetails:
                    // Same chase-cam shape as ChaseDefault below, just much closer
                    // (see ConfigureForTarget) and with faster response — a tight
                    // detail shot needs to keep up with turns more precisely or the
                    // airframe clips out of frame.
                    Vector3 flatFwdD = Vector3.ProjectOnPlane(Target.forward, Vector3.up).normalized;
                    if (flatFwdD.sqrMagnitude < 0.01f) flatFwdD = Vector3.forward;
                    Vector3 desiredD = Target.position
                        + Quaternion.LookRotation(flatFwdD) * new Vector3(0, _chaseDetailsHeight, -_chaseDetailsDist);
                    transform.position = Vector3.Lerp(transform.position, desiredD, Time.deltaTime * _posLerp * 1.6f);

                    Vector3 aimD = Target.position + Vector3.up * (_targetSize.y * 0.35f);
                    Vector3 upD = _bankFollow > 0f
                        ? Vector3.Slerp(Vector3.up, Target.up, _bankFollow) : Vector3.up;
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(aimD - transform.position, upD),
                        Time.deltaTime * _rotLerp * 1.6f);
                    break;

                default: // ChaseDefault
                    Vector3 flatFwd = Vector3.ProjectOnPlane(Target.forward, Vector3.up).normalized;
                    if (flatFwd.sqrMagnitude < 0.01f) flatFwd = Vector3.forward;
                    Vector3 desired = Target.position
                        + Quaternion.LookRotation(flatFwd) * new Vector3(0, _chaseHeight, -_chaseDist);
                    transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * _posLerp);

                    // Aim slightly ahead of the drone along its velocity so fast flight
                    // reads as leading the shot, and roll part-way with the target's
                    // bank on winged airframes so turns feel like an aircraft chase cam.
                    Vector3 vel = _targetRb != null ? _targetRb.linearVelocity : Vector3.zero;
                    Vector3 aim = Target.position + vel * _lookAhead + Vector3.up * _aimHeight * 0.5f;
                    Vector3 up = _bankFollow > 0f
                        ? Vector3.Slerp(Vector3.up, Target.up, _bankFollow) : Vector3.up;
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(aim - transform.position, up),
                        Time.deltaTime * _rotLerp);
                    break;
            }

            ApplySpeedFov();
            ApplyShake();
        }

        /// <summary>Photo mode's per-frame drive: right-mouse-held look (yaw/pitch, no
        /// roll — matches every other camera mode staying level), WASD + Q/E fly
        /// (forward/right/up relative to the camera's own current facing, Shift for a
        /// fast-move multiplier), [ / ] for live FOV, and - / = for exposure (EV, via
        /// the dedicated _photoVolume's ColorAdjustments.postExposure — see Start()).
        /// Doesn't touch Time.timeScale — the drone keeps flying under whatever input
        /// is still held, same as detaching a camera drone from a moving subject rather
        /// than pausing the world to take the shot.</summary>
        private void UpdatePhotoMode()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue() * PhotoLookSensitivity;
                _photoYaw += delta.x;
                _photoPitch = Mathf.Clamp(_photoPitch - delta.y, -85f, 85f);
            }
            transform.rotation = Quaternion.Euler(_photoPitch, _photoYaw, 0f);

            var kb = Keyboard.current;
            if (kb != null)
            {
                Vector3 move = Vector3.zero;
                if (kb.wKey.isPressed) move += transform.forward;
                if (kb.sKey.isPressed) move -= transform.forward;
                if (kb.aKey.isPressed) move -= transform.right;
                if (kb.dKey.isPressed) move += transform.right;
                if (kb.eKey.isPressed) move += Vector3.up;
                if (kb.qKey.isPressed) move -= Vector3.up;
                if (move.sqrMagnitude > 0.0001f)
                {
                    float speed = kb.leftShiftKey.isPressed ? PhotoMoveSpeedFast : PhotoMoveSpeed;
                    transform.position += move.normalized * speed * Time.unscaledDeltaTime;
                }

                if (kb.leftBracketKey.isPressed)
                    _cam.fieldOfView = Mathf.Clamp(_cam.fieldOfView - 30f * Time.unscaledDeltaTime, PhotoFovMin, PhotoFovMax);
                if (kb.rightBracketKey.isPressed)
                    _cam.fieldOfView = Mathf.Clamp(_cam.fieldOfView + 30f * Time.unscaledDeltaTime, PhotoFovMin, PhotoFovMax);

                if (kb.minusKey.isPressed)
                    _photoExposureEv = Mathf.Clamp(_photoExposureEv - 1.5f * Time.unscaledDeltaTime, PhotoExposureMin, PhotoExposureMax);
                if (kb.equalsKey.isPressed)
                    _photoExposureEv = Mathf.Clamp(_photoExposureEv + 1.5f * Time.unscaledDeltaTime, PhotoExposureMin, PhotoExposureMax);
                _photoColor.postExposure.Override(_photoExposureEv);
            }

            FlightHUD.Instance?.UpdatePhotoModeReadout(_cam.fieldOfView, _photoExposureEv);
        }

        /// <summary>Widens FOV with speed — a cheap, well-worn "sense of speed" trick
        /// that needs no change to the actual flight model to make fast flight feel
        /// faster and hovering feel calmer.</summary>
        private void ApplySpeedFov()
        {
            if (_flight == null) _flight = Target.GetComponent<AeroTerra.Drone.DroneFlightController>();
            if (_flight == null || _flight.Spec == null) return;

            float speedRatio = Mathf.Clamp01(_flight.CurrentSpeedKmh / Mathf.Max(1f, _flight.Spec.MaxSpeedKmh));
            float targetFov = Mathf.Lerp(_baseFov, _baseFov + 14f, speedRatio);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, Time.deltaTime * 3f);
        }

        /// <summary>Starts (or, if already shaking harder, ignores) a decaying camera
        /// shake. Call directly for a shake with a known strength (e.g. a hard crash
        /// under the drone itself); call ShakeFromPoint for a shake that should fall
        /// off with distance from an explosion elsewhere in the world.</summary>
        public void Shake(float magnitude, float duration)
        {
            if (magnitude <= _shakeMagnitude * (1f - _shakeTimer / Mathf.Max(0.01f, _shakeDuration))) return;
            _shakeMagnitude = magnitude;
            _shakeDuration = duration;
            _shakeTimer = 0f;
        }

        public void ShakeFromPoint(Vector3 point, float blastScale)
        {
            float dist = Vector3.Distance(transform.position, point);
            float reach = 45f * blastScale;
            float falloff = Mathf.Clamp01(1f - dist / reach);
            if (falloff <= 0f) return;
            Shake(0.45f * blastScale * falloff, 0.5f);
        }

        private void ApplyShake()
        {
            if (_shakeTimer >= _shakeDuration) return;
            _shakeTimer += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(_shakeTimer / _shakeDuration);
            float amt = _shakeMagnitude * k;

            transform.position += transform.right * (Random.Range(-1f, 1f) * amt * 0.3f)
                                 + transform.up * (Random.Range(-1f, 1f) * amt * 0.3f);
            transform.rotation *= Quaternion.Euler(
                Random.Range(-1f, 1f) * amt * 2.5f, Random.Range(-1f, 1f) * amt * 2.5f, 0f);
        }
    }
}
