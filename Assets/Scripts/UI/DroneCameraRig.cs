using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using AeroTerra.Core;

namespace AeroTerra.UI
{
    /// <summary>Flight camera view, cycled with InputManager.CameraAction (key C).</summary>
    public enum CamMode { Chase, Front, Bottom, Thermal }

    /// <summary>
    /// Drives the single flight Camera through four view modes: smooth 3rd-person
    /// chase (default), nose-mounted front view, belly-mounted surveillance/bombing
    /// view, and a stylized thermal look layered on the front view via a URP color
    /// grading Volume. Notifies FlightHUD on every mode change so its overlay matches.
    /// </summary>
    public class DroneCameraRig : MonoBehaviour
    {
        /// <summary>Single flight camera per scene — lets systems that have no direct
        /// reference to it (ExplosionEffect is static, DroneFlightController shouldn't
        /// need to be wired to it) trigger shake without a scene-wide event bus.</summary>
        public static DroneCameraRig Instance { get; private set; }

        public Transform Target;

        public CamMode Mode { get; private set; } = CamMode.Chase;

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

        private float _shakeMagnitude, _shakeDuration, _shakeTimer;

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
        }

        private void Update()
        {
            var im = AeroTerra.Input.InputManager.Instance;
            if (im == null || !im.CameraAction.WasPressedThisFrame()) return;

            // Skip modes the airframe has no camera for — a drone with only a front
            // camera never lands on Bottom, one with front+back cycles both. Chase
            // is the external spectator view, not a physical camera, so it's always
            // reachable regardless of loadout (worst case the loop lands back on it).
            CamMode next = Mode;
            for (int i = 0; i < 4; i++)
            {
                next = (CamMode)(((int)next + 1) % 4);
                if (IsAvailable(next)) break;
            }
            Mode = next;

            _thermalVolume.weight = Mode == CamMode.Thermal ? 1f : 0f;
            FlightHUD.Instance?.SetCameraMode(Mode);
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
                CamMode.Thermal => spec.HasFrontCamera, // thermal is a mode of the front camera, not a separate one
                CamMode.Bottom => spec.HasBackCamera,
                _ => true, // Chase
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
            float span = Mathf.Max(size.x, size.z);

            // Pulled in closer than the old 4.5 / 1.6 / 0.32 figures — those left even
            // small airframes with a distant, floaty chase view.
            _chaseDist = Mathf.Max(3.0f, span * 1.15f);
            _chaseHeight = _chaseDist * 0.26f;
            _noseOffset = size.z * 0.55f + 0.25f;
            _bellyOffset = size.y * 0.55f + 0.2f;
            _aimHeight = Mathf.Clamp(size.y, 0.4f, 3f);

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

                default: // Chase
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
