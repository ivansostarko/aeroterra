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
        /// Screen canister, Comms radio) — set once by DroneFactory.Spawn from the
        /// CustomDroneData, folded into ApplyMass() like every other loadout choice.</summary>
        [HideInInspector] public float ExtraLoadoutMassKg;

        private Rigidbody _rb;
        private AeroTerra.Input.InputManager _input;
        /// <summary>Whichever of Battery/Fuel this airframe actually has, resolved once
        /// Spec is available (Start(), not Awake() — DroneFactory assigns Spec right
        /// after AddComponent, which runs Awake() synchronously before Spec is set).</summary>
        private IPowerSource _power;

        private const float CrashSpeedThreshold = 8f; // m/s relative velocity to count as a hard crash, not a landing
        private const float CrashCooldownSec = 1.5f;
        private const float SeaLevelY = 0f;           // hard floor — altitude (world y) never goes negative
        private const float CeilingBandM = 150f;      // thrust/lift fade band below Spec.MaxAltitudeM
        private const float KamikazeRespawnDelaySec = 2.6f;
        private const float CrashRespawnDelaySec = 2f;
        private const float BoostFactor = 1.3f;

        private float _lastCrashTime = -999f;
        private bool _crashRespawnPending;
        private float _headingDeg;                    // multirotor commanded heading (yaw-rate integrated)

        /// <summary>True once a drone that ran out of usable power (battery empty, or
        /// fuel empty for a PowerSystemType.Fuel airframe) actually touches down — the
        /// trigger for FlightSceneController's end-of-flight modal. Name kept from when
        /// only batteries existed; FlightSceneController branches its modal text on
        /// Spec.PowerSystem for the fuel case.</summary>
        public bool JustCrashedFromDeadBattery { get; private set; }

        /// <summary>True while a kamikaze airframe is blown up and waiting to respawn.</summary>
        public bool IsDetonated { get; private set; }

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
            // A reset teleports the airframe — wipe any world-space trails so they
            // don't draw a kilometer-long ribbon from the old position to the new.
            foreach (var trail in GetComponentsInChildren<TrailRenderer>()) trail.Clear();
            if (FlightModel == FlightModelType.FixedWing)
            {
                Throttle01 = 0.65f;
                if (_rb != null) _rb.linearVelocity = transform.forward * (Spec.MaxSpeedKmh / 3.6f * 0.55f);
            }
            else
            {
                Throttle01 = _rb != null && Spec.MaxThrustN > 0f
                    ? (_rb.mass * Physics.gravity.magnitude) / Spec.MaxThrustN : 0.5f;
            }
        }

        private void FixedUpdate()
        {
            if (_input == null || (_power != null && _power.IsEmpty) || IsDetonated)
            {
                // dead battery/fuel: gravity wins, keep light drag
                PitchInput = RollInput = YawInput = 0f;
                Boosting = Braking = false;
                EnforceAltitudeFloor();
                return;
            }

            var axes = _input.ReadFlightAxes();
            Boosting = _input.BoostHeld && !_input.BrakeHeld;
            Braking = _input.BrakeHeld;

            float pitch = axes.Pitch * (GameManager.Instance.Settings.InvertPitch ? -1f : 1f);
            PitchInput = pitch; RollInput = axes.Roll; YawInput = axes.Yaw;

            if (FlightModel == FlightModelType.FixedWing)
                TickFixedWing(axes, pitch);
            else
                TickMultirotor(axes, pitch);

            // Wind from weather
            if (WeatherSystem.Instance != null)
                _rb.AddForce(WeatherSystem.Instance.CurrentWind, ForceMode.Force);

            // Speed clamp (boost stretches it)
            float maxMs = Spec.MaxSpeedKmh / 3.6f * (Boosting ? BoostFactor : 1f);
            if (_rb.linearVelocity.magnitude > maxMs)
                _rb.linearVelocity = _rb.linearVelocity.normalized * maxMs;

            EnforceAltitudeFloor();

            // Power drain: base cruise + throttle load, scaled by payload mass ratio;
            // boosting burns noticeably hotter. Same Watts figure feeds either a
            // BatterySystem or a FuelSystem via the shared IPowerSource contract.
            float loadFactor = _rb.mass / Mathf.Max(0.1f, Spec.EmptyMassKg);
            float watts = (Spec.CruisePowerW + Spec.PowerPerThrottleW * Throttle01) * loadFactor
                        * (Boosting ? 1.5f : 1f);
            _power?.Drain(watts, Time.fixedDeltaTime);
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
                float cruiseMs = Spec.MaxSpeedKmh / 3.6f * 0.6f;
                float fwd = Mathf.Max(0f, Vector3.Dot(_rb.linearVelocity, transform.forward));
                float wingLiftN = Mathf.Min(_rb.mass * g, _rb.mass * g * (fwd * fwd) / (cruiseMs * cruiseMs))
                                * CeilingFactor();
                _rb.AddForce(transform.up * wingLiftN, ForceMode.Force);
                thrustDemandN -= wingLiftN;
            }

            float thrustN = Mathf.Clamp(thrustDemandN, 0f, Spec.MaxThrustN * boost);
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
            float maxMs = Spec.MaxSpeedKmh / 3.6f;
            float cruiseMs = maxMs * 0.5f;
            float stallMs = maxMs * 0.26f;

            // ---- Engine: W/S trims a persistent power setting (idle keeps the prop
            // turning; a plane never zeroes its throttle mid-air by tapping S) ----
            Throttle01 = Mathf.Clamp(Throttle01 + axes.Throttle * dt * 0.5f, 0.12f, 1f);
            float thrustN = Throttle01 * Spec.MaxThrustN * (Boosting ? BoostFactor : 1f) * CeilingFactor();
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

        /// <summary>Civilian airframes (cargo, racing, survey, VTOL logistics, camera
        /// quad) never get pyrotechnics — explosions on crash/drop are strictly a
        /// military-drone thing (see DroneSpecification.IsMilitaryClass).</summary>
        private bool IsMilitary => Spec.IsMilitaryClass;

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

            if (collision.relativeVelocity.magnitude < CrashSpeedThreshold) return;
            if (Time.time - _lastCrashTime < CrashCooldownSec) return;
            _lastCrashTime = Time.time;
            AeroTerra.UI.NarratorController.Instance?.NotifyCrashed();

            if (IsMilitary)
            {
                // Crashing into an already-burning site feeds the fire and scales the
                // blast, same stacking rule as dropped ordnance (DroppedPayloadImpact).
                var site = FireSite.RegisterHit(point);
                ExplosionEffect.Spawn(point, 1f + 0.25f * (site.Intensity - 1));
                AeroTerra.Core.AudioManager.Instance?.PlayBombExplosion(point);
            }
            else
            {
                // Cargo / racing drones just thud down in a dust cloud.
                ExplosionEffect.SpawnDustPuff(point);
                AeroTerra.Core.AudioManager.Instance?.PlayImpactThud(point);
                AeroTerra.UI.DroneCameraRig.Instance?.Shake(0.25f, 0.35f);
            }

            // Unlike a kamikaze detonation, a regular hard crash doesn't hide/freeze the
            // airframe — it just sits there afterward. Free Flight has no permadeath, so
            // auto-respawn it at the map's spawn point instead of leaving the player to
            // notice and press R. Skipped if the crash was the dead-battery touchdown
            // (JustCrashedFromDeadBattery) — FlightSceneController is about to freeze
            // Time.timeScale and show the end-of-flight modal, so respawning here would
            // just be a drone popping back up under a "game over" screen.
            if (!JustCrashedFromDeadBattery && !_crashRespawnPending)
            {
                _crashRespawnPending = true;
                StartCoroutine(RespawnAfterCrash());
            }
        }

        /// <summary>2 seconds after a hard (non-kamikaze) crash, teleport back to the
        /// map's spawn point/heading — same target every other respawn path uses
        /// (RespawnAfterDetonation, FlightSceneController.ResetDrone). No hide/freeze/
        /// unhide dance here since a regular crash never hid the airframe or froze its
        /// physics to begin with.</summary>
        private IEnumerator RespawnAfterCrash()
        {
            yield return new WaitForSeconds(CrashRespawnDelaySec);

            var map = GameManager.Instance != null ? GameManager.Instance.SelectedMap : null;
            float alt = (float)(map?.SpawnAltitudeMeters ?? 150);
            float heading = map?.SpawnHeadingDeg ?? 0f;
            transform.SetPositionAndRotation(new Vector3(0, alt, 0), Quaternion.Euler(0, heading, 0));

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

        private IEnumerator RespawnAfterDetonation()
        {
            yield return new WaitForSeconds(KamikazeRespawnDelaySec);

            var map = GameManager.Instance != null ? GameManager.Instance.SelectedMap : null;
            float alt = (float)(map?.SpawnAltitudeMeters ?? 150);
            float heading = map?.SpawnHeadingDeg ?? 0f;
            transform.SetPositionAndRotation(new Vector3(0, alt, 0), Quaternion.Euler(0, heading, 0));

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
