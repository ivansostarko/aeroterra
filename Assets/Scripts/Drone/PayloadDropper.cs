using UnityEngine;
using AeroTerra.Procedural;

namespace AeroTerra.Drone
{
    /// <summary>
    /// Handles the payload-drop action (InputManager.PayloadDropAction, default key I):
    /// releases a physical copy of a payload store as a free-falling, tumbling object
    /// and reduces the carried payload mass via PayloadSystem.
    ///
    /// Multi-hardpoint airframes (the Kestrel's four underwing munitions) group each
    /// store as a "Store*" child of "PayloadVisual" — one keypress releases ONE store,
    /// so a full loadout is four separate drops, each with its own falling animation.
    /// Single-mount drones drop the whole PayloadVisual group as before. Ammunition is
    /// unlimited in Free Flight — once every store is away, a short rearm cooldown
    /// silently restores the full loadout. Kamikaze airframes (Vespid/Locust) never
    /// get this component: their warhead is integral and detonates on impact.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PayloadSystem))]
    public class PayloadDropper : MonoBehaviour
    {
        private const float DespawnDelaySec = 12f;
        private const float ReloadCooldownSec = 4f;

        private PayloadSystem _payload;
        private Rigidbody _droneRb;
        private DroneFlightController _flight;
        private float _originalPayloadKg;
        private bool _reloading;
        private float _reloadTimer;

        private readonly System.Collections.Generic.List<Transform> _stores =
            new System.Collections.Generic.List<Transform>();
        private int _droppedCount;

        /// <summary>True while every store is away and rearming — CurrentPayloadKg reads 0.</summary>
        public bool IsReloading => _reloading;

        /// <summary>Individual releasable stores (1 for a single belly mount/pod).</summary>
        public int StoreCount => _stores.Count;
        public int StoresRemaining => Mathf.Max(0, _stores.Count - _droppedCount);

        private void Awake()
        {
            _payload = GetComponent<PayloadSystem>();
            _droneRb = GetComponent<Rigidbody>();
            _flight = GetComponent<DroneFlightController>();
            _originalPayloadKg = _payload.CurrentPayloadKg; // captured before any drop ever zeroes it
        }

        private void Start()
        {
            // The model hierarchy exists by now (DroneFactory attaches this after the
            // builder ran). Prefer per-hardpoint "Store*" groups; fall back to dropping
            // the whole PayloadVisual as one store for single-mount airframes.
            var visual = DroneFactory.FindDeep(transform, "PayloadVisual");
            if (visual != null)
            {
                for (int i = 0; i < visual.childCount; i++)
                {
                    var child = visual.GetChild(i);
                    if (child.name.StartsWith("Store")) _stores.Add(child);
                }

                if (_stores.Count == 0)
                {
                    // Single-mount cargo-pod airframes (Pelican/Osprey) — the whole
                    // PayloadVisual already has its own dedicated pod model; never
                    // model-swap this branch.
                    _stores.Add(visual);
                }
                else if (_flight != null)
                {
                    // Multi-mount military airframes (Hornet/Kestrel/Bison) — give the
                    // assigned PayloadKind its own procedural munition model.
                    foreach (var store in _stores)
                        PayloadModelBuilder.Rebuild(store, _flight.Spec.PayloadKind,
                            _flight.Spec.DefaultBodyColor, _flight.Spec.DefaultAccentColor);
                }
            }
        }

        /// <summary>Distinct aural signature per PayloadKind from the same two shared
        /// clips (bomb-drop/bomb-explosion) — Warhead reads heavier/deeper, Guided
        /// reads crisper/higher-tech, Drop is the neutral baseline.</summary>
        private float PitchForKind() => _flight == null ? 1f : _flight.Spec.PayloadKind switch
        {
            PayloadKind.Warhead => 0.75f,
            PayloadKind.GuidedAmmunition => 1.15f,
            _ => 1f,
        };

        private void Update()
        {
            var im = AeroTerra.Input.InputManager.Instance;
            if (im != null && im.PayloadDropAction.WasPressedThisFrame()) TryDrop();

            if (_reloading)
            {
                _reloadTimer -= Time.deltaTime;
                if (_reloadTimer <= 0f) FinishReload();
            }
        }

        public void TryDrop()
        {
            if (_reloading || _originalPayloadKg <= 0f || _payload.CurrentPayloadKg <= 0f) return;
            if (_stores.Count == 0 || _droppedCount >= _stores.Count) return;

            // Only military classes drop live ordnance (with release audio and impact
            // detonation); civilian pods — cargo, VTOL logistics — fall inert.
            bool armed = _flight != null && _flight.Spec.IsMilitaryClass;
            if (armed)
            {
                AeroTerra.Core.AudioManager.Instance?.PlayBombDrop(transform.position, PitchForKind());
                AeroTerra.UI.NarratorController.Instance?.NotifyMilitaryPayloadDropped();
            }

            var store = _stores[_droppedCount];
            _droppedCount++;

            float massPerStore = _originalPayloadKg / _stores.Count;
            _payload.Configure(massPerStore * (_stores.Count - _droppedCount)); // updates flight mass immediately

            if (store != null)
            {
                // Drop a physical clone and hide the original in place — infinite
                // ammo means the original reappears once the rearm cooldown ends,
                // rather than being destroyed and needing to be rebuilt from scratch.
                var dropped = Instantiate(store.gameObject, store.position, store.rotation);
                dropped.name = "DroppedPayload";
                // Instantiate(original, pos, rot) only copies position/rotation — the clone
                // has no parent now, so its localScale IS its world scale. Without this it
                // would revert to the builder's unscaled reference size instead of matching
                // this drone's real-world scale (see DroneFactory's WingspanM scaling).
                dropped.transform.localScale = store.lossyScale;
                dropped.SetActive(true);
                PayloadKind kind = _flight != null ? _flight.Spec.PayloadKind : PayloadKind.Cargo;
                SetUpFallingPhysics(dropped.transform, armed, Mathf.Max(0.1f, massPerStore), kind);
                store.gameObject.SetActive(false);
            }

            if (_droppedCount >= _stores.Count)
            {
                _reloading = true;
                _reloadTimer = ReloadCooldownSec;
            }
        }

        private void SetUpFallingPhysics(Transform dropped, bool armed, float massKg, PayloadKind kind)
        {
            var rb = dropped.gameObject.AddComponent<Rigidbody>();
            rb.mass = massKg;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.angularDamping = 0.25f; // realistic tumble decay in air

            // A real release isn't just "same velocity as the carrier" — it separates
            // with a small downward kick relative to the drone, and tumbles instead of
            // falling perfectly stable, which is what actually reads as "dropped" rather
            // than "instantly teleported to an identical, parallel trajectory."
            Vector3 carrierVelocity = _droneRb != null ? _droneRb.linearVelocity : Vector3.zero;
            rb.linearVelocity = carrierVelocity - transform.up * 1.2f;
            rb.angularVelocity = new Vector3(
                Random.Range(-2.5f, 2.5f), Random.Range(-2.5f, 2.5f), Random.Range(-2.5f, 2.5f));

            var rends = dropped.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var bounds = rends[0].bounds;
                foreach (var r in rends) bounds.Encapsulate(r.bounds);
                var col = dropped.gameObject.AddComponent<BoxCollider>();
                col.center = dropped.InverseTransformPoint(bounds.center);
                Vector3 scale = dropped.lossyScale;
                col.size = new Vector3(
                    scale.x != 0f ? bounds.size.x / scale.x : bounds.size.x,
                    scale.y != 0f ? bounds.size.y / scale.y : bounds.size.y,
                    scale.z != 0f ? bounds.size.z / scale.z : bounds.size.z);
            }

            dropped.gameObject.AddComponent<DroppedPayloadAerodynamics>().Kind = kind;
            if (armed) AttachFallTrail(dropped);

            var impact = dropped.gameObject.AddComponent<DroppedPayloadImpact>();
            impact.Explosive = armed;
            impact.Kind = kind;
            Destroy(dropped.gameObject, DespawnDelaySec);
        }

        /// <summary>Thin grey vapor trail streaming off an armed munition as it falls —
        /// makes the drop readable from the chase camera all the way to impact.</summary>
        private static void AttachFallTrail(Transform dropped)
        {
            var go = new GameObject("FallTrail");
            go.transform.SetParent(dropped, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.75f, 0.75f, 0.75f, 0.4f), new Color(0.55f, 0.55f, 0.55f, 0.3f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            // parent clone can be arbitrarily scaled (WingspanM) — keep puffs world-sized
            main.scalingMode = ParticleSystemScalingMode.Local;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 6f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.4f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.gray, 1f) },
                new[] { new GradientAlphaKey(0.4f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = ExplosionEffect.BuildMat(Color.white);
            r.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
        }

        private void FinishReload()
        {
            _reloading = false;
            _droppedCount = 0;
            _payload.Configure(_originalPayloadKg);

            foreach (var store in _stores)
                if (store != null) store.gameObject.SetActive(true);
            var visual = DroneFactory.FindDeep(transform, "PayloadVisual");
            if (visual != null) visual.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Sits on a just-dropped payload copy while it falls. On first ground/building
    /// contact: armed ordnance detonates — blast (with physics knockback) scaled up
    /// by how many munitions already burn at that spot, plus a stacking FireSite
    /// (fire + smoke + looping fire audio) — and is removed immediately. An unarmed
    /// cargo pod thuds down with a dust puff and keeps its despawn timer.
    /// </summary>
    public class DroppedPayloadImpact : MonoBehaviour
    {
        public bool Explosive;
        public PayloadKind Kind;
        private bool _handled;

        private void OnCollisionEnter(Collision collision)
        {
            if (_handled) return;
            _handled = true;

            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            if (Explosive)
            {
                // Register the fire first: repeat hits merge into the existing site
                // and its grown Intensity feeds back into a bigger blast.
                var site = FireSite.RegisterHit(point);
                ExplosionEffect.Spawn(point, 1f + 0.25f * (site.Intensity - 1));
                float pitch = Kind switch { PayloadKind.Warhead => 0.75f, PayloadKind.GuidedAmmunition => 1.15f, _ => 1f };
                AeroTerra.Core.AudioManager.Instance?.PlayBombExplosion(point, pitch);
                Destroy(gameObject);
            }
            else
            {
                ExplosionEffect.SpawnDustPuff(point);
                AeroTerra.Core.AudioManager.Instance?.PlayImpactThud(point);
                AeroTerra.UI.NarratorController.Instance?.NotifyCargoDelivered();
                // settled — stop steering the pod so it can tip and rest naturally
                var aero = GetComponent<DroppedPayloadAerodynamics>();
                if (aero != null) Destroy(aero);
            }
        }
    }

    /// <summary>
    /// Simple aerodynamic stabilization for a falling store: it leaves the rail
    /// tumbling (PayloadDropper's random angular kick), then as the fins "bite"
    /// the nose swings smoothly onto the velocity vector — the classic bomb-drop
    /// arc instead of an end-over-end brick all the way down.
    /// </summary>
    public class DroppedPayloadAerodynamics : MonoBehaviour
    {
        /// <summary>Set by PayloadDropper right after AddComponent — drives the "custom
        /// animation" per payload type: Warhead tumbles longer before a slow, heavy
        /// correction (dumb iron bomb); GuidedAmmunition snaps onto its flight path
        /// almost immediately (reads as actively steering); DropAmmunition/Cargo use the
        /// original neutral baseline feel.</summary>
        public PayloadKind Kind;

        private Rigidbody _rb;
        private float _airTime;

        private void Awake() => _rb = GetComponent<Rigidbody>();

        private void FixedUpdate()
        {
            if (_rb == null) return;
            _airTime += Time.fixedDeltaTime;

            Vector3 v = _rb.linearVelocity;
            if (v.sqrMagnitude < 4f) return; // near rest — nothing to streamline

            (float delaySec, float rampSec, float gripSpeed) = Kind switch
            {
                PayloadKind.Warhead => (0.6f, 1.8f, 3f),
                PayloadKind.GuidedAmmunition => (0.15f, 0.6f, 8f),
                _ => (0.35f, 1.2f, 5f),
            };

            float grip = Mathf.Clamp01((_airTime - delaySec) / rampSec);
            if (grip <= 0f) return;

            // Munition meshes are built nose-along-+Z (capsules rotated X+90 in the
            // builders), so LookRotation points the nose into the airflow.
            Quaternion target = Quaternion.LookRotation(v.normalized, transform.up);
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, target, grip * gripSpeed * Time.fixedDeltaTime));
            _rb.angularVelocity = Vector3.Lerp(_rb.angularVelocity, Vector3.zero, grip * (gripSpeed * 0.6f) * Time.fixedDeltaTime);
        }
    }
}
