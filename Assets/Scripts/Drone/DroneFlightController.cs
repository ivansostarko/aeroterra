using System.Collections;
using UnityEngine;
using AeroTerra.Core;
using AeroTerra.Map;

namespace AeroTerra.Drone
{
    /// <summary>Which physics model an airframe flies with (derived from ModelKind).</summary>
    public enum FlightModelType
    {
        /// <summary>Rotary-wing: angle-mode attitude hold, altitude-rate throttle,
        /// position-hold braking. Pelican / Hornet / Velocity / Pixel.</summary>
        Multirotor,
        /// <summary>Winged: forward thrust, airspeed-dependent lift and control
        /// authority, coordinated banked turns, stall. Vespid / Locust / Kestrel /
        /// Manta / Wraith / Bison.</summary>
        FixedWing,
        /// <summary>Quad-plane hybrid: flies the multirotor model, but a fixed wing
        /// progressively carries the weight as forward airspeed builds, letting the
        /// lift rotors wind down in cruise. Osprey.</summary>
        VtolHybrid,
    }

    /// <summary>
    /// Class-aware flight physics driven by normalized input axes from InputManager.
    ///
    /// Multirotors fly in "angle mode": the pitch/roll stick commands a target lean
    /// angle (so holding the forward arrow tilts the nose down and drives the drone
    /// forward), throttle commands a climb/descent rate with automatic hover hold,
    /// and centering the sticks brakes to a stable hover.
    ///
    /// Fixed-wing UAVs fly like aircraft: W/S sets an engine power level, wings make
    /// lift from airspeed (below stall speed the nose drops and the plane sinks),
    /// control surfaces lose authority as airspeed decays, and banking the wings
    /// pulls the nose around in a coordinated turn.
    ///
    /// Shared: wind (WeatherSystem), battery drain, boost/brake modifiers, a hard
    /// sea-level floor (altitude can never go negative) and a soft service ceiling
    /// at Spec.MaxAltitudeM. Kamikaze airframes (Vespid/Locust) detonate on impact.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DroneFlightController : MonoBehaviour
    {
        public DroneSpecification Spec;
        [HideInInspector] public BatterySystem Battery;
        [HideInInspector] public FuelSystem Fuel;
        [HideInInspector] public PayloadSystem Payload;

        /// <summary>Combined weight of the Workshop's "additional loadout" slots (Smoke
        /// Screen canister, Comms radio, Parachute, AI Sensor) — set once by
        /// DroneFactory.Spawn from the CustomDroneData, folded into ApplyMass() like
        /// every other loadout choice.</summary>
        [HideInInspector] public float ExtraLoadoutMassKg;

        /// <summary>True if this build has a parachute equipped (CustomDroneData.
        /// ParachuteEquipped) — set once by DroneFactory.Spawn, same pattern as
        /// ExtraLoadoutMassKg. Gates ParachuteController's G-key deploy (a drone with
        /// none fitted has no ParachuteController at all — see DroneFactory.Spawn — but
        /// this is checked again here as a safety belt against calling DeployParachute
        /// directly).</summary>
        [HideInInspector] public bool HasParachute;

        /// <summary>Set once by DroneFactory.Spawn (WrapVisualForFlip) — every rendered
        /// mesh part, parented one level under this instead of directly under the
        /// airframe's own transform. TickFlip spins only this transform for the B-key
        /// barrel-roll trick, so the actual Rigidbody/collider — the same transform the
        /// chase camera tracks as its Target — never rotates during the trick at all;
        /// the camera holds steady while just the model performs the flip. Null (with a
        /// direct-rigidbody-rotation fallback in TickFlip) only if a caller ever spawns a
        /// flyable drone through some other path that skips DroneFactory.Spawn.</summary>
        [HideInInspector] public Transform FlipVisualRoot;

        /// <summary>Resolved once by DroneFactory.Spawn — CustomDroneData.
        /// SelectedPayloadKind if the player picked one from Spec.AvailablePayloadKinds'
        /// category picker (currently only possible on AT-R4 Hornet), else just
        /// Spec.PayloadKind unchanged. PayloadDropper reads this instead of
        /// Spec.PayloadKind directly so a drone with no picker (the other eleven) behaves
        /// identically to before this field existed.</summary>
        [HideInInspector] public PayloadKind EffectivePayloadKind;

        /// <summary>Dev console "speed" cheat (see GameConsoleUI/docs/10-CHEATS.md) — null
        /// means no cheat is active, use Spec.MaxSpeedKmh unchanged (see
        /// EffectiveMaxSpeedKmh). Deliberately NOT cleared by OnRespawn — a cheat typed
        /// into the console should stick through an R-key reset or a crash respawn within
        /// the same flight, same as any other dev-console override would; only leaving
        /// the flight (a real scene reload) clears it.</summary>
        public float? MaxSpeedOverrideKmh;

        /// <summary>What TickMultirotor/TickFixedWing's speed clamp actually enforces —
        /// Spec.MaxSpeedKmh unless the console's "speed" cheat has overridden it.</summary>
        public float EffectiveMaxSpeedKmh => MaxSpeedOverrideKmh ?? Spec.MaxSpeedKmh;

        /// <summary>Sets the "speed" cheat's max-speed override — floor-clamped to 0 (a
        /// negative or zero cap is a valid, if extreme, thing to type; the clamp just
        /// keeps it non-negative rather than rejecting it outright). Called by
        /// GameConsoleUI's "speed &lt;km/h&gt;" command.</summary>
        public void SetMaxSpeedOverride(float kmh) => MaxSpeedOverrideKmh = Mathf.Max(0f, kmh);

        private Rigidbody _rb;
        private AeroTerra.Input.InputManager _input;
        /// <summary>Whichever of Battery/Fuel this airframe actually has, resolved once
        /// Spec is available (Start(), not Awake() — DroneFactory assigns Spec right
        /// after AddComponent, which runs Awake() synchronously before Spec is set).</summary>
        private IPowerSource _power;

        /// <summary>Battery-only thrust-ceiling multiplier from the current air
        /// temperature (Settings ▸ Flying Conditions), recomputed once per FixedUpdate —
        /// see BatterySystem.PerformanceFactor. Always 1 for Fuel-powered airframes.</summary>
        private float _batteryPerfFactor = 1f;

        private const float CrashSpeedThreshold = 8f; // m/s relative velocity to count as a hard crash, not a landing
        private const float CrashCooldownSec = 1.5f;
        private const float SeaLevelY = 0f;           // hard floor — altitude (world y) never goes negative
        private const float CeilingBandM = 150f;      // thrust/lift fade band below Spec.MaxAltitudeM
        private const float KamikazeRespawnDelaySec = 2.6f;
        private const float BoostFactor = 1.3f;
        private const float FlipDurationSec = 0.65f; // one full 360° barrel-roll trick

        /// <summary>Minimum altitude (world Y — sea level is 0, same "altitude" convention
        /// NarratorController's high-altitude check uses) the parachute is allowed to
        /// deploy at — matches the request's "more than 100m" gate; too low to safely
        /// inflate below this.</summary>
        public const float ParachuteMinDeployAltitudeM = 100f;
        private const float ParachuteTargetDescentMs = -3.5f;   // gentle "under canopy" sink rate
        private const float ParachuteVerticalConverge = 2.5f;   // how fast vertical speed eases toward the target
        private const float ParachuteHorizontalDrag = 1.6f;     // bleeds off residual horizontal speed once open
        private const float ParachuteLevelSpeed = 1.4f;         // how fast attitude settles level under canopy

        private const float LandingCooldownSec = 2f; // suppresses repeat fires from resting/jittering ground contact

        private float _lastCrashTime = -999f;
        private float _lastLandingTime = -999f;
        private bool _crashRespawnPending;
        private bool _isFlipping;
        private float _flipTimer;

        // Power-failure fall: once the active power source (battery or fuel) hits
        // empty, control authority is gone and the airframe tumbles down out of
        // control until it hits something — see BeginPowerFailureFall/TickPowerFailureFall.
        private const float PowerFailureSpinRampSec = 1.5f; // time to reach full tumble rate
        private bool _powerFailureActive;
        private float _powerFailureTimer;
        private float _fallSpinSign; // randomized per failure so every fall tumbles a different way

        /// <summary>Fired on a gentle touchdown (below CrashSpeedThreshold — i.e. NOT a
        /// hard crash), cooldown-gated same as the crash path. Used by FlightLogTracker
        /// to count landings for the Workshop's per-drone flight log. Best-effort: any
        /// collision under the threshold counts, including e.g. a wingtip scrape while
        /// taxiing, not just a clean touchdown — there's no separate "on the ground"
        /// state to check against.</summary>
        public event System.Action Landed;

        /// <summary>Fired exactly once per hard crash, at the moment of impact (the
        /// Vector3 is the impact point) — NOT re-fired while the wreck settles/bounces
        /// before the player restarts (see the _crashRespawnPending guard in
        /// OnCollisionEnter). FlightSceneController subscribes to run the crash
        /// cinematic (camera pull-back, CTA) and calls RespawnAfterCrash() once the
        /// player presses Space, instead of the old fixed-delay auto-respawn.</summary>
        public event System.Action<Vector3> Crashed;
        private float _headingDeg;                    // multirotor commanded heading (yaw-rate integrated)

        /// <summary>True once a drone that ran out of usable power (battery empty, or
        /// fuel empty for a PowerSystemType.Fuel airframe) actually touches down — the
        /// trigger for FlightSceneController's end-of-flight modal. Name kept from when
        /// only batteries existed; FlightSceneController branches its modal text on
        /// Spec.PowerSystem for the fuel case.</summary>
        public bool JustCrashedFromDeadBattery { get; private set; }

        /// <summary>True while a kamikaze airframe is blown up and waiting to respawn.</summary>
        public bool IsDetonated { get; private set; }

        /// <summary>True once the parachute has been deployed this flight (see
        /// DeployParachute) — from then on FixedUpdate hands off to TickParachuteDescent
        /// instead of the normal flight-model tick, until the next respawn/reset.</summary>
        public bool ParachuteDeployed { get; private set; }

        public FlightModelType FlightModel { get; private set; }

        /// <summary>True when the resolved power source (battery or fuel, whichever this
        /// airframe has) is empty — for consumers (DroneAudioController, HUD) that just
        /// need a bool without caring which system it is.</summary>
        public bool IsPowerEmpty => _power != null && _power.IsEmpty;

        public float CurrentSpeedKmh => _rb ? _rb.linearVelocity.magnitude * 3.6f : 0f;
        public float VerticalSpeedMs => _rb ? _rb.linearVelocity.y : 0f;
        public float ForwardSpeedMs => _rb ? Vector3.Dot(_rb.linearVelocity, transform.forward) : 0f;
        public float Throttle01 { get; private set; }
        public bool Boosting { get; private set; }
        public bool Braking { get; private set; }

        /// <summary>Signed bank angle in degrees (positive = right wing down).</summary>
        public float BankDeg
        {
            get
            {
                float z = transform.eulerAngles.z;
                return z > 180f ? 360f - z : -z;
            }
        }

        // Last applied stick inputs (-1..1), exposed for visual animation
        // (control surfaces, gimbals). Zero when idle or battery-dead.
        public float PitchInput { get; private set; }
        public float RollInput { get; private set; }
        public float YawInput { get; private set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            Payload = GetComponent<PayloadSystem>() ?? gameObject.AddComponent<PayloadSystem>();
            // Battery/Fuel are NOT resolved here: DroneFactory decides which one to add
            // (based on Spec.PowerSystem) and does so AFTER AddComponent<DroneFlightController>()
            // — which is what triggers this Awake() — so neither component exists yet.
        }

        private void Start()
        {
            _input = AeroTerra.Input.InputManager.Instance;
            Battery = GetComponent<BatterySystem>();
            Fuel = GetComponent<FuelSystem>();
            _power = Spec.PowerSystem == PowerSystemType.Fuel ? (IPowerSource)Fuel : Battery;
            switch (Spec.ModelKind)
            {
                case DroneModelKind.StrikeDelta:
                case DroneModelKind.LoiteringDelta:
                case DroneModelKind.TwinBoomUcav:
                case DroneModelKind.FlyingWing:
                case DroneModelKind.JetSwept:
                case DroneModelKind.LightUcav:
                    FlightModel = FlightModelType.FixedWing; break;
                case DroneModelKind.QuadPlane:
                case DroneModelKind.ImportedMesh:
                    FlightModel = FlightModelType.VtolHybrid; break;
                default:
                    FlightModel = FlightModelType.Multirotor; break;
            }

            ApplyMass();
            // Fixed wings glide — parasitic drag is applied explicitly per-tick, so keep
            // built-in damping low there; multirotors keep the spec's draggy feel.
            _rb.linearDamping = FlightModel == FlightModelType.FixedWing ? Spec.LinearDrag * 0.4f : Spec.LinearDrag;
            _rb.angularDamping = Spec.AngularDrag;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            OnRespawn();
        }

        /// <summary>Total mass = airframe + power source (battery energy density or fuel
        /// density, whichever this airframe has — see BatterySystem/FuelSystem) + payload.
        /// All factor into thrust-to-weight, so a heavier loadout is a real, felt
        /// performance cost — not just a cosmetic Workshop stat. Uses fresh GetComponent
        /// lookups (not the cached Battery/Fuel/_power fields) because BatterySystem/
        /// FuelSystem.Configure() call this synchronously from DroneFactory.Spawn(),
        /// before this component's own Start() (where those fields get resolved) has run.</summary>
        public void ApplyMass()
        {
            var battery = GetComponent<BatterySystem>();
            var fuel = GetComponent<FuelSystem>();
            float powerMassKg = battery != null ? battery.MassKg : (fuel != null ? fuel.MassKg : 0f);
            _rb.mass = Spec.EmptyMassKg + powerMassKg + (Payload ? Payload.CurrentPayloadKg : 0f) + ExtraLoadoutMassKg;
        }

        /// <summary>Reinitialize flight state after a spawn/reset: multirotors start in a
        /// stable hover, fixed wings are hand-launched at cruise speed (they cannot hover,
        /// so a dead-stop spawn would just be a 150 m plunge while building airspeed).</summary>
        public void OnRespawn()
        {
            _headingDeg = transform.eulerAngles.y;
            JustCrashedFromDeadBattery = false;
            ParachuteDeployed = false;
            _powerFailureActive = false;
            _powerFailureTimer = 0f;
            // A respawn mid-flip would otherwise leave the mesh rotated away from the
            // freshly teleported (and always upright) Rigidbody/collider/camera Target —
            // visually broken in a way the old whole-body rotation never was as obviously.
            _isFlipping = false;
            if (FlipVisualRoot != null) FlipVisualRoot.localRotation = Quaternion.identity;
            // A reset teleports the airframe — wipe any world-space trails so they
            // don't draw a kilometer-long ribbon from the old position to the new.
            foreach (var trail in GetComponentsInChildren<TrailRenderer>()) trail.Clear();
            if (FlightModel == FlightModelType.FixedWing)
            {
                Throttle01 = 0.65f;
                if (_rb != null) _rb.linearVelocity = transform.forward * (EffectiveMaxSpeedKmh / 3.6f * 0.55f);
            }
            else
            {
                Throttle01 = _rb != null && Spec.MaxThrustN > 0f
                    ? (_rb.mass * Physics.gravity.magnitude) / Spec.MaxThrustN : 0.5f;
            }
        }

        /// <summary>Deploys the parachute — called by ParachuteController once it's
        /// confirmed the G-key press, HasParachute, and the altitude gate. From the next
        /// FixedUpdate on, TickParachuteDescent takes over from the normal flight-model
        /// tick until the next respawn/reset. The "jerk" of a real canopy snapping open
        /// is a hard, sudden deceleration — halving whatever velocity the drone had the
        /// instant it opens, rather than converging from full speed smoothly.</summary>
        public void DeployParachute()
        {
            if (ParachuteDeployed || IsDetonated) return;
            ParachuteDeployed = true;
            _isFlipping = false;
            if (_rb != null) _rb.linearVelocity *= 0.5f;
        }

        private void FixedUpdate()
        {
            if (_input == null || (_power != null && _power.IsEmpty) || IsDetonated)
            {
                // dead battery/fuel: gravity wins, keep light drag
                PitchInput = RollInput = YawInput = 0f;
                Boosting = Braking = false;

                // Under an already-open canopy, a dead battery/tank doesn't change
                // anything — it's already a powerless, controlled descent, so it
                // should keep hanging level rather than start tumbling.
                if (_power != null && _power.IsEmpty && !IsDetonated && !ParachuteDeployed)
                {
                    if (!_powerFailureActive) BeginPowerFailureFall();
                    TickPowerFailureFall(Time.fixedDeltaTime);
                }

                EnforceAltitudeFloor();
                return;
            }

            if (ParachuteDeployed)
            {
                // Under canopy: no stick/throttle input, no boost/brake/flip — just the
                // slow controlled sink TickParachuteDescent drives.
                PitchInput = RollInput = YawInput = 0f;
                Boosting = Braking = false;
                TickParachuteDescent(Time.fixedDeltaTime);
            }
            else
            {
                var axes = _input.ReadFlightAxes();
                Boosting = _input.BoostHeld && !_input.BrakeHeld;
                Braking = _input.BrakeHeld;

                // Boost = instant full-throttle "very fast mode": pressing Shift snaps
                // throttle to max regardless of what the throttle stick/keys are doing,
                // stacking with the BoostFactor thrust/speed-ceiling multiplier below.
                if (Boosting) axes.Throttle = 1f;

                if (!_isFlipping && _input.DroneFlipAction != null && _input.DroneFlipAction.WasPressedThisFrame())
                {
                    _isFlipping = true;
                    _flipTimer = 0f;
                    _rb.angularVelocity = Vector3.zero;
                }

                float pitch = axes.Pitch * (GameManager.Instance.Settings.InvertPitch ? -1f : 1f);
                PitchInput = pitch; RollInput = axes.Roll; YawInput = axes.Yaw;

                _batteryPerfFactor = Spec.PowerSystem == PowerSystemType.Battery
                    ? BatterySystem.PerformanceFactor(GameManager.Instance.Settings.TemperatureC) : 1f;

                if (_isFlipping)
                {
                    _flipTimer += Time.fixedDeltaTime;
                    if (_flipTimer >= FlipDurationSec)
                    {
                        _isFlipping = false;
                        // Hard-snap to identity rather than trusting 60 accumulated
                        // Quaternion multiplications to land exactly back on zero —
                        // a clean finish, no residual floating-point tilt on the mesh.
                        if (FlipVisualRoot != null) FlipVisualRoot.localRotation = Quaternion.identity;
                    }
                    else TickFlip(Time.fixedDeltaTime);
                }
                if (!_isFlipping)
                {
                    if (FlightModel == FlightModelType.FixedWing)
                        TickFixedWing(axes, pitch);
                    else
                        TickMultirotor(axes, pitch);
                }
            }

            // Wind from weather — still applies under canopy, in fact a parachute
            // should read as more wind-affected than powered flight, not less.
            if (WeatherSystem.Instance != null)
                _rb.AddForce(WeatherSystem.Instance.CurrentWind, ForceMode.Force);

            // Speed clamp (boost stretches it) — skipped under canopy, where
            // TickParachuteDescent already converges to its own much slower target speed.
            if (!ParachuteDeployed)
            {
                float maxMs = EffectiveMaxSpeedKmh / 3.6f * (Boosting ? BoostFactor : 1f);
                if (_rb.linearVelocity.magnitude > maxMs)
                    _rb.linearVelocity = _rb.linearVelocity.normalized * maxMs;
            }

            EnforceAltitudeFloor();

            // Power drain: base cruise + throttle load, scaled by payload mass ratio;
            // boosting burns noticeably hotter. Same Watts figure feeds either a
            // BatterySystem or a FuelSystem via the shared IPowerSource contract.
            // Skipped under canopy — the motors are off, nothing draws power.
            if (!ParachuteDeployed)
            {
                float loadFactor = _rb.mass / Mathf.Max(0.1f, Spec.EmptyMassKg);
                float watts = (Spec.CruisePowerW + Spec.PowerPerThrottleW * Throttle01) * loadFactor
                            * (Boosting ? 1.5f : 1f);
                _power?.Drain(watts, Time.fixedDeltaTime);
            }
        }

        /// <summary>0 → at/above the service ceiling, 1 → below the fade band. Scales
        /// climb thrust/lift so every airframe tops out smoothly at Spec.MaxAltitudeM.</summary>
        private float CeilingFactor() =>
            Mathf.Clamp01((Spec.MaxAltitudeM - transform.position.y) / CeilingBandM);

        /// <summary>The drone can never fly at negative altitude: clamp to sea level
        /// (world y = 0, which is what the HUD altitude readout displays) and kill any
        /// remaining downward velocity so it skims instead of tunnelling.</summary>
        private void EnforceAltitudeFloor()
        {
            if (transform.position.y >= SeaLevelY) return;
            var p = transform.position;
            p.y = SeaLevelY;
            transform.position = p;
            var v = _rb.linearVelocity;
            if (v.y < 0f) { v.y = 0f; _rb.linearVelocity = v; }
        }

        // ------------------------------------------------------------------
        // Power-failure fall: dead battery/fuel with no canopy out — the airframe
        // has no active control authority left and tumbles down until impact (see
        // OnCollisionEnter, which already gives any hard-enough landing the full
        // fire/smoke/explosion treatment regardless of why the drone went down).
        // ------------------------------------------------------------------

        /// <summary>Called once on the exact FixedUpdate the power source is first
        /// found empty — picks a random tumble direction so no two falls play out
        /// identically, and starts winding the rotors down (see TickPowerFailureFall).</summary>
        private void BeginPowerFailureFall()
        {
            _powerFailureActive = true;
            _powerFailureTimer = 0f;
            _fallSpinSign = Random.value < 0.5f ? -1f : 1f;
            AeroTerra.Core.AudioManager.Instance?.PlayPowerFailureAlarm(transform.position);
        }

        /// <summary>Drives the actual tumble every FixedUpdate while falling powerless.
        /// Directly setting angularVelocity (rather than adding torque) is a deliberate
        /// scripted-motion choice, same technique TickFlip/TickParachuteDescent already
        /// use above, so the fall reads as a designed "loss of control" spiral instead of
        /// whatever leftover spin AngularDrag happens to leave it with. Winged airframes
        /// get a classic nose-down-then-spin departure stall; rotor airframes tumble end
        /// over end on a wandering combination of all three axes, since dead rotors give
        /// no gyroscopic stability at all. Either way the spin ramps up over
        /// PowerFailureSpinRampSec rather than snapping straight to full rate, and
        /// residual horizontal drift bleeds off so it plummets instead of gliding.</summary>
        private void TickPowerFailureFall(float dt)
        {
            _powerFailureTimer += dt;
            float rampUp = Mathf.Clamp01(_powerFailureTimer / PowerFailureSpinRampSec);

            Vector3 spinDegPerSec;
            if (FlightModel == FlightModelType.FixedWing)
            {
                float yawRate = _fallSpinSign * Mathf.Lerp(15f, 140f, rampUp);
                float rollRate = _fallSpinSign * Mathf.Lerp(20f, 160f, rampUp);
                float pitchRate = Mathf.Lerp(35f, 5f, rampUp); // nose drops hard first, then the spin takes over
                spinDegPerSec = new Vector3(pitchRate, yawRate, rollRate);
            }
            else
            {
                float yawRate = _fallSpinSign * Mathf.Lerp(20f, 200f, rampUp);
                float rollRate = _fallSpinSign * Mathf.Lerp(10f, 90f, rampUp) * Mathf.Sin(_powerFailureTimer * 2.6f);
                float pitchRate = Mathf.Lerp(0f, 70f, rampUp) * Mathf.Sin(_powerFailureTimer * 2.1f + 1f);
                spinDegPerSec = new Vector3(pitchRate, yawRate, rollRate);
            }
            _rb.angularVelocity = spinDegPerSec * Mathf.Deg2Rad;

            Vector3 horizontal = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            _rb.AddForce(-horizontal * 0.5f, ForceMode.Acceleration);

            // RotorSpinner reads Throttle01 for spin speed (floored at 15% so dead
            // rotors windmill/autorotate rather than snapping to a dead stop) — winding
            // it down here, instead of leaving it frozen at whatever it was the instant
            // power died, is what makes the rotors visibly spin down during the fall.
            Throttle01 = Mathf.MoveTowards(Throttle01, 0f, dt * 0.6f);
        }

        // ------------------------------------------------------------------
        // Drone Flip: a cosmetic-only trick — FlipVisualRoot (every rendered mesh
        // part, see DroneFactory.WrapVisualForFlip) spins a full 360°, while the
        // Rigidbody itself — and so the chase camera, which tracks this same
        // transform — never rotates and holds its normal attitude throughout. Bypasses
        // the normal attitude controller for FlipDurationSec purely so stick input
        // doesn't fight PitchInput/RollInput/YawInput while the trick plays; every
        // flight model falls back into its own Tick* method once it ends, unaffected
        // since the actual attitude never changed.
        // ------------------------------------------------------------------
        private void TickFlip(float dt)
        {
            float rateDeg = 360f / FlipDurationSec;
            if (FlipVisualRoot != null)
                FlipVisualRoot.localRotation *= Quaternion.Euler(rateDeg * dt, 0f, 0f);
            else
                _rb.MoveRotation(_rb.rotation * Quaternion.Euler(rateDeg * dt, 0, 0)); // legacy fallback, no visual root wired

            // Roughly hover-equivalent thrust so the trick doesn't sink while it
            // plays — the trick no longer touches the real attitude at all, so this
            // is purely "hold altitude for the ~0.65s stunt," not counteracting any
            // actual loss of lift from tumbling.
            float hoverN = _rb.mass * Physics.gravity.magnitude;
            _rb.AddForce(transform.up * Mathf.Min(hoverN, Spec.MaxThrustN * _batteryPerfFactor), ForceMode.Force);
            PitchInput = 1f; RollInput = 0f; YawInput = 0f;
        }

        // ------------------------------------------------------------------
        // Parachute descent: motors off, hanging under canopy. Bypasses the normal
        // flight-model tick entirely (like TickFlip) for any airframe, multirotor or
        // fixed-wing alike — a deployed canopy overrides however this drone would
        // otherwise fly. Vertical speed eases toward a slow, safe sink rate; residual
        // horizontal speed bleeds off under canopy drag; attitude settles level, like a
        // real canopy swinging to rest overhead. No player input applies once deployed.
        // ------------------------------------------------------------------
        private void TickParachuteDescent(float dt)
        {
            Vector3 v = _rb.linearVelocity;
            v.y = Mathf.Lerp(v.y, ParachuteTargetDescentMs, dt * ParachuteVerticalConverge);
            float horizontalDamp = Mathf.Clamp01(1f - dt * ParachuteHorizontalDrag);
            v.x *= horizontalDamp;
            v.z *= horizontalDamp;
            _rb.linearVelocity = v;
            _rb.angularVelocity = Vector3.Lerp(_rb.angularVelocity, Vector3.zero, dt * ParachuteLevelSpeed);

            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            Quaternion level = flatForward.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(flatForward.normalized, Vector3.up)
                : transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, level, dt * ParachuteLevelSpeed);
        }

        // ------------------------------------------------------------------
        // Multirotor: angle mode + altitude-rate hold
        // ------------------------------------------------------------------
        private void TickMultirotor(AeroTerra.Input.FlightAxes axes, float pitch)
        {
            float dt = Time.fixedDeltaTime;
            float boost = Boosting ? BoostFactor : 1f;

            // ---- Attitude: stick commands a lean angle, PD torque tracks it ----
            // Agility scales with the spec's torque rating so the racing quad snaps
            // while the heavy-lift octocopter leans in deliberately.
            float agility = Mathf.Clamp(Spec.PitchRollTorque / Mathf.Max(1f, _rb.mass), 0.8f, 6f);
            float maxTiltDeg = (Boosting ? 35f : 25f) * Mathf.Clamp(0.7f + agility * 0.12f, 0.8f, 1.5f);
            if (Braking) { pitch = 0f; axes.Roll = 0f; } // brake = level out and stop

            // Yaw is a rate command integrated into a target heading, so the drone
            // holds heading when the stick is released instead of weather-vaning.
            // Keyboard has no yaw binding at all (InputManager.BuildActions) — keyboard
            // pilots fly multirotor/VTOL airframes at a fixed heading, only ever
            // re-oriented by external forces (see the resync clause just below).
            // Gamepad still yaws normally via the left stick's X axis.
            float yawRateDeg = 70f + Spec.YawTorque * 8f;
            _headingDeg += axes.Yaw * yawRateDeg * dt;
            _headingDeg = Mathf.Repeat(_headingDeg, 360f);
            // If external forces (blast knockback, collisions) spun us away, drag the
            // commanded heading toward reality so it doesn't unwind for seconds.
            float actualHeading = transform.eulerAngles.y;
            if (Mathf.Abs(Mathf.DeltaAngle(_headingDeg, actualHeading)) > 45f)
                _headingDeg = Mathf.MoveTowardsAngle(_headingDeg, actualHeading, 360f * dt);

            Quaternion desired = Quaternion.Euler(pitch * maxTiltDeg, _headingDeg, -axes.Roll * maxTiltDeg);
            Quaternion error = desired * Quaternion.Inverse(transform.rotation);
            error.ToAngleAxis(out float errDeg, out Vector3 errAxis);
            if (errDeg > 180f) errDeg -= 360f;
            if (!float.IsNaN(errAxis.x) && Mathf.Abs(errDeg) > 0.01f)
            {
                // ForceMode.Acceleration keeps the response consistent across airframe
                // masses; Kp/Kd tuned for a crisp but non-oscillating attitude track.
                Vector3 correction = errAxis.normalized * (errDeg * Mathf.Deg2Rad) * (14f + agility * 3f)
                                   - _rb.angularVelocity * 6f;
                _rb.AddTorque(correction, ForceMode.Acceleration);
            }

            // ---- Vertical: throttle stick commands climb rate, hover is automatic ----
            float climbMax = Spec.MaxAscentRateMs * boost;
            float targetVy = axes.Throttle * climbMax;
            if (targetVy > 0f) targetVy *= CeilingFactor();
            float g = Physics.gravity.magnitude;
            float desiredAccel = g + (targetVy - _rb.linearVelocity.y) * 3f;
            // Thrust acts along body up — compensate for lean so forward flight
            // doesn't sink, but never divide by a near-zero cosine when flipped.
            float cosTilt = Mathf.Max(0.35f, Vector3.Dot(transform.up, Vector3.up));
            float thrustDemandN = _rb.mass * desiredAccel / cosTilt;

            if (FlightModel == FlightModelType.VtolHybrid)
            {
                // Quad-plane: the wing progressively carries the weight as forward
                // airspeed builds (fully at ~60% max speed), so the lift rotors wind
                // down in cruise exactly like a real VTOL hybrid transitioning.
                // Floored well above zero: the "speed" console cheat can legally drive
                // EffectiveMaxSpeedKmh down to 0, and cruiseMs gets squared as a divisor
                // just below — an unfloored 0 there would divide by zero into NaN/Infinity
                // and break this airframe's physics outright, not just "cap its speed."
                float cruiseMs = Mathf.Max(0.5f, EffectiveMaxSpeedKmh / 3.6f * 0.6f);
                float fwd = Mathf.Max(0f, Vector3.Dot(_rb.linearVelocity, transform.forward));
                float wingLiftN = Mathf.Min(_rb.mass * g, _rb.mass * g * (fwd * fwd) / (cruiseMs * cruiseMs))
                                * CeilingFactor();
                _rb.AddForce(transform.up * wingLiftN, ForceMode.Force);
                thrustDemandN -= wingLiftN;
            }

            // Brake = full emergency motor cutoff, not just a hover-hold: thrust drops
            // to zero and gravity takes over, so holding Space actually drops the
            // drone instead of just parking it in place.
            float thrustN = Braking ? 0f : Mathf.Clamp(thrustDemandN, 0f, Spec.MaxThrustN * boost * _batteryPerfFactor);
            _rb.AddForce(transform.up * thrustN, ForceMode.Force);
            Throttle01 = Mathf.Clamp01(thrustN / Mathf.Max(1f, Spec.MaxThrustN));

            // ---- Horizontal: quadratic drag + stick-release position hold ----
            Vector3 hv = _rb.linearVelocity; hv.y = 0f;
            bool sticksCentered = Mathf.Abs(pitch) < 0.05f && Mathf.Abs(axes.Roll) < 0.05f;
            if (Braking)
                _rb.AddForce(-hv * 3.2f, ForceMode.Acceleration);        // hard air-anchor
            else if (sticksCentered)
                _rb.AddForce(-hv * 1.1f, ForceMode.Acceleration);        // GPS-style drift arrest
            _rb.AddForce(-hv * hv.magnitude * 0.01f, ForceMode.Acceleration); // parasitic drag
        }

        // ------------------------------------------------------------------
        // Fixed wing: thrust + airspeed lift + coordinated turns + stall
        // ------------------------------------------------------------------
        private void TickFixedWing(AeroTerra.Input.FlightAxes axes, float pitch)
        {
            float dt = Time.fixedDeltaTime;
            float g = Physics.gravity.magnitude;
            float maxMs = EffectiveMaxSpeedKmh / 3.6f;
            // Same zero-divisor guard as TickMultirotor's VtolHybrid branch above —
            // cruiseMs gets squared as a divisor below (liftN), and the "speed" console
            // cheat can legally set EffectiveMaxSpeedKmh (and so maxMs) to exactly 0.
            float cruiseMs = Mathf.Max(0.5f, maxMs * 0.5f);
            float stallMs = maxMs * 0.26f;

            // ---- Engine: W/S trims a persistent power setting (idle keeps the prop
            // turning; a plane never zeroes its throttle mid-air by tapping S). Boost
            // snaps straight to full power instead of ramping through the trim rate —
            // "very fast mode" should feel instant, not a two-second spool-up.
            Throttle01 = Boosting ? 1f : Mathf.Clamp(Throttle01 + axes.Throttle * dt * 0.5f, 0.12f, 1f);
            float thrustN = Throttle01 * Spec.MaxThrustN * (Boosting ? BoostFactor : 1f) * CeilingFactor() * _batteryPerfFactor;
            _rb.AddForce(transform.forward * thrustN, ForceMode.Force);

            Vector3 v = _rb.linearVelocity;
            float fwdSpeed = Mathf.Max(0f, Vector3.Dot(v, transform.forward));

            // ---- Lift: weight is fully carried at cruise speed, grows with airspeed²,
            // capped at 1.5 g so dives can be pulled out of without infinite lift ----
            float liftN = _rb.mass * g * (fwdSpeed * fwdSpeed) / (cruiseMs * cruiseMs);
            liftN = Mathf.Min(liftN, _rb.mass * g * 1.5f) * CeilingFactor();
            _rb.AddForce(transform.up * liftN, ForceMode.Force);

            // ---- Control authority fades with airspeed: full above stall, mushy
            // below — exactly why a stalled airframe stops answering the stick ----
            float authority = Mathf.Clamp01(fwdSpeed / Mathf.Max(1f, stallMs));
            authority *= authority;

            float agility = Mathf.Clamp(Spec.PitchRollTorque * 0.06f, 0.6f, 2f);
            float pitchRate = pitch * 55f * agility;
            float rollRate = axes.Roll * 95f * agility;
            float yawRate = axes.Yaw * 28f;

            // Coordinated turn: banking pulls the nose around at the rate real
            // aerodynamics would (ω = g·tan(bank)/v), so turns fly like an aircraft
            // instead of a drifting hovercraft.
            yawRate += Mathf.Rad2Deg * (g * Mathf.Tan(Mathf.Clamp(BankDeg * Mathf.Deg2Rad, -1.2f, 1.2f))
                                         / Mathf.Max(8f, fwdSpeed)) * 0.9f;

            bool sticksCentered = Mathf.Abs(pitch) < 0.05f && Mathf.Abs(axes.Roll) < 0.05f;
            if (sticksCentered)
            {
                // Wing leveler + gentle pitch trim toward a slight nose-up cruise
                // attitude — the stability augmentation a UAV autopilot provides.
                float pitchDeg = transform.eulerAngles.x; if (pitchDeg > 180f) pitchDeg -= 360f;
                rollRate = Mathf.Clamp(-BankDeg * 1.4f, -40f, 40f);
                pitchRate = Mathf.Clamp((-2f - pitchDeg) * 1.2f, -20f, 20f);
            }

            // Stall: below flying speed the nose drops no matter what the stick says.
            if (fwdSpeed < stallMs)
                pitchRate = Mathf.Lerp(30f, pitchRate, authority);

            Vector3 wLocal = transform.InverseTransformDirection(_rb.angularVelocity);
            Vector3 targetLocal = new Vector3(pitchRate, yawRate, -rollRate) * Mathf.Deg2Rad;
            targetLocal.x *= authority; targetLocal.z *= authority;
            Vector3 torqueLocal = (targetLocal - wLocal) * 5f;
            _rb.AddTorque(transform.TransformDirection(torqueLocal), ForceMode.Acceleration);

            // ---- Drag: parasitic (v²) + induced (lift proportional) + airbrake ----
            float dragK = 0.0035f * Mathf.Max(0.3f, Spec.LinearDrag);
            Vector3 drag = -v * v.magnitude * dragK
                           - v.normalized * (liftN / Mathf.Max(1f, _rb.mass)) * 0.055f;
            if (Braking) drag -= v * 0.9f; // spoilers out
            _rb.AddForce(drag, ForceMode.Acceleration);
        }

        // ------------------------------------------------------------------
        // Impact handling
        // ------------------------------------------------------------------

        private bool IsKamikaze => Spec.IsKamikazeClass;

        private void OnCollisionEnter(Collision collision)
        {
            // Out of power, and this is the touchdown that ends the flight — flag it
            // regardless of impact speed; FlightSceneController shows the end-of-flight
            // modal on the next frame it sees this true.
            if (_power != null && _power.IsEmpty && !JustCrashedFromDeadBattery)
                JustCrashedFromDeadBattery = true;

            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;

            // Kamikaze airframes ARE the munition: any solid hit above a gentle bump
            // detonates the integrated warhead — there is no payload to release.
            if (IsKamikaze && !IsDetonated && collision.relativeVelocity.magnitude > 5f)
            {
                Detonate(point);
                return;
            }

            if (collision.relativeVelocity.magnitude < CrashSpeedThreshold)
            {
                if (collision.relativeVelocity.magnitude > 0.4f && Time.time - _lastLandingTime > LandingCooldownSec)
                {
                    _lastLandingTime = Time.time;
                    Landed?.Invoke();
                }
                return;
            }
            if (Time.time - _lastCrashTime < CrashCooldownSec) return;
            _lastCrashTime = Time.time;
            // Already mid-crash (waiting on the player's restart) — a settling/bouncing
            // wreck re-colliding with the ground shouldn't re-trigger the whole sequence
            // (explosion/narrator/CTA) a second time before the first one resolves.
            if (_crashRespawnPending) return;

            AeroTerra.UI.NarratorController.Instance?.NotifyCrashed();

            // Every hard crash — civilian or military — gets the full fire/smoke/blast
            // treatment now (previously only military airframes did; civilians just got
            // a dust puff): a real crash should read as dramatic regardless of drone
            // class. Crashing into an already-burning site feeds the fire and scales the
            // blast further, same stacking rule dropped ordnance already used.
            var site = FireSite.RegisterHit(point);
            ExplosionEffect.Spawn(point, 1.6f + 0.25f * (site.Intensity - 1));
            AeroTerra.Core.AudioManager.Instance?.PlayBombExplosion(point);
            AeroTerra.Core.AudioManager.Instance?.PlayDroneCrashExplosion(point);

            // Unlike a kamikaze detonation, a regular hard crash doesn't hide/freeze the
            // airframe — it just sits there afterward, burning, while FlightSceneController
            // runs the crash cinematic (camera pull-back) and shows a PRESS SPACE TO
            // RESTART prompt — see the Crashed event. Skipped if the crash was the
            // dead-battery touchdown (JustCrashedFromDeadBattery) — FlightSceneController
            // is about to freeze Time.timeScale and show the end-of-flight modal instead,
            // so a crash CTA here would just fight that screen.
            if (!JustCrashedFromDeadBattery)
            {
                _crashRespawnPending = true;
                Crashed?.Invoke(point);
            }
        }

        /// <summary>Teleports back to the map's spawn point/heading — same target every
        /// other respawn path uses (RespawnAfterDetonation, FlightSceneController.
        /// ResetDrone). Called by FlightSceneController the instant the player presses
        /// Space on the post-crash "PRESS SPACE TO RESTART" prompt (see the Crashed
        /// event) — no fixed delay of its own; the cinematic pull-back + CTA fade-in are
        /// what give the moment its pacing instead. No hide/freeze/unhide dance here
        /// since a regular crash never hid the airframe or froze its physics to begin
        /// with.</summary>
        public void RespawnAfterCrash()
        {
            var gm = GameManager.Instance;
            Vector3 pos = gm != null ? gm.SpawnLocalPosition : new Vector3(0, 150f, 0);
            float heading = gm != null ? (gm.SelectedMap?.SpawnHeadingDeg ?? 0f) : 0f;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0, heading, 0));

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            _crashRespawnPending = false;
            OnRespawn();
        }

        /// <summary>One-way attack profile: the whole airframe explodes on impact —
        /// blast scaled by the simulated warhead mass — then respawns at the map's
        /// spawn point after a short delay (Free Flight has no permadeath).</summary>
        private void Detonate(Vector3 point)
        {
            IsDetonated = true;
            AeroTerra.UI.NarratorController.Instance?.NotifyCrashed();
            PitchInput = RollInput = YawInput = 0f;
            Throttle01 = 0f;

            float warheadKg = Payload != null ? Payload.CurrentPayloadKg : 0f;
            float scale = 1.6f + warheadKg * 0.08f;
            var site = FireSite.RegisterHit(point);
            ExplosionEffect.Spawn(point, scale + 0.25f * (site.Intensity - 1));
            AeroTerra.Core.AudioManager.Instance?.PlayBombExplosion(point);

            // Hide the airframe and freeze physics while "destroyed". Blinkers must
            // stop first — they toggle their renderer every frame and would otherwise
            // flash the nav lights of an invisible wreck.
            foreach (var b in GetComponentsInChildren<NavLightBlinker>()) b.enabled = false;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            var src = GetComponent<AudioSource>();
            if (src != null) src.mute = true;
            _rb.isKinematic = true;

            StartCoroutine(RespawnAfterDetonation());
        }

        /// <summary>Voluntary self-destruct at the drone's own current position — same
        /// full detonation sequence (FireSite/ExplosionEffect fire+blast, audio, hidden
        /// wreck, respawn after KamikazeRespawnDelaySec) as a real kamikaze impact, just
        /// triggered on demand instead of by a collision. Used by PayloadDropper for
        /// AT-R4 Hornet's live "Warhead" payload mode (press [I] with Warhead armed —
        /// see PayloadDropper.TryDrop) rather than dropping a separate munition.</summary>
        public void DetonateAtCurrentPosition()
        {
            if (IsDetonated) return;
            Detonate(transform.position);
        }

        private IEnumerator RespawnAfterDetonation()
        {
            yield return new WaitForSeconds(KamikazeRespawnDelaySec);

            var gm = GameManager.Instance;
            Vector3 pos = gm != null ? gm.SpawnLocalPosition : new Vector3(0, 150f, 0);
            float heading = gm != null ? (gm.SelectedMap?.SpawnHeadingDeg ?? 0f) : 0f;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0, heading, 0));

            _rb.isKinematic = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = true;
            foreach (var b in GetComponentsInChildren<NavLightBlinker>()) b.enabled = true;
            var src = GetComponent<AudioSource>();
            if (src != null) src.mute = false;

            IsDetonated = false;
            OnRespawn();
        }
    }
}
