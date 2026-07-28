using System.Collections.Generic;
using UnityEngine;

namespace AeroTerra.Drone
{
    /// <summary>
    /// A persistent ground fire left behind by detonated ordnance (or a hard drone
    /// crash). Fire + smoke come from the Vefects "Free Fire VFX URP" asset pack
    /// (VFX_Fire_Floor_01_Smoke.prefab, moved to Assets/Resources/VFX/Fire — see
    /// ExplosionEffect's matching pack usage); everything driving it — a flickering
    /// point light, a looping spatialized fire sound (Assets/Resources/Audio/sfx/
    /// fire/fire.mp3 — audible only when the drone flies near), and a thermal
    /// updraft that physically pushes rigidbodies flying through the smoke column —
    /// stays procedural, same as before.
    ///
    /// Sites stack: a second munition landing within MergeRadiusM of an active
    /// site feeds it instead of spawning a new one — intensity (and with it flame
    /// scale, light, sound reach and updraft strength) grows, and the burn lifetime
    /// extends. Use RegisterHit(); never AddComponent directly.
    ///
    /// Every fire — even a single unfed hit — burns for at least BaseLifetimeSec
    /// (5 minutes), long enough to still be burning well after the drone's moved on
    /// in a normal flying session; stacking more hits on the same spot both grows the
    /// blaze (flame/smoke scale, light, sound reach) and extends it further, up to
    /// MaxLifetimeSec.
    /// </summary>
    public class FireSite : MonoBehaviour
    {
        private const float MergeRadiusM = 8f;
        private const int MaxIntensity = 10;
        private const float BaseLifetimeSec = 300f;
        private const float LifetimePerHitSec = 90f;
        private const float MaxLifetimeSec = 900f;
        private const float FadeDurationSec = 20f;
        private const float UpdraftHeightM = 28f;
        private const float UpdraftNewtonsPerIntensity = 12f;

        private static readonly List<FireSite> Active = new List<FireSite>();
        private static readonly HashSet<Rigidbody> PushedThisStep = new HashSet<Rigidbody>();
        private static AudioClip _fireClip;
        private static bool _fireClipLoaded;
        private static GameObject _fireVfxPrefab;
        private static bool _fireVfxPrefabLoaded;

        public int Intensity { get; private set; } = 1;

        private Transform _vfxRoot;
        private float _baseVfxScale = 1f;
        private Light _light;
        private float _baseLightIntensity;
        private AudioSource _audio;
        private float _baseVolume;
        private float _remainingSec;
        private float _flickerSeed;
        private bool _dying;

        /// <summary>Ordnance detonated (or a drone burned in) at this point: merge
        /// into a nearby active fire if one exists, else start a new one. Returns
        /// the site so callers can scale their blast by the stacked Intensity.</summary>
        public static FireSite RegisterHit(Vector3 point)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                if (!Active[i]._dying &&
                    (Active[i].transform.position - point).sqrMagnitude <= MergeRadiusM * MergeRadiusM)
                {
                    Active[i].AddFuel();
                    return Active[i];
                }
            }

            var go = new GameObject("FireSite");
            go.transform.position = point;
            return go.AddComponent<FireSite>();
        }

        private void Awake()
        {
            Active.Add(this);
            _flickerSeed = Random.Range(0f, 100f);
            _remainingSec = BaseLifetimeSec;

            _vfxRoot = InstantiateFireVfx();

            BuildLight();
            BuildAudio();
            ApplyIntensity();
        }

        private void OnDestroy() => Active.Remove(this);

        private void AddFuel()
        {
            Intensity = Mathf.Min(MaxIntensity, Intensity + 1);
            _remainingSec = Mathf.Min(MaxLifetimeSec, _remainingSec + LifetimePerHitSec);
            ApplyIntensity(); // the resulting scale step-up is the "feeding the blaze" cue
        }

        /// <summary>Re-derives everything intensity-driven: flame/smoke scale, light
        /// reach, audio loudness/reach and (via FixedUpdate) updraft. The 1.8x baseline
        /// (vs. the pack prefab's own authored size) is deliberate — even a single,
        /// unstacked hit should read as a big, dramatic fire, not the stock-sized
        /// prefab; each additional stacked hit grows it steeply from there.</summary>
        private void ApplyIntensity()
        {
            int extra = Intensity - 1;
            _baseVfxScale = 1.8f + 0.55f * extra;
            _vfxRoot.localScale = Vector3.one * _baseVfxScale;

            _light.range = 14f + 4f * Intensity;
            _baseLightIntensity = 3.2f + 1.6f * Intensity;

            _audio.minDistance = 6f + 2.5f * Intensity;
            _audio.maxDistance = 55f + 18f * Intensity;
            _baseVolume = Mathf.Min(1f, 0.5f + 0.12f * Intensity);
        }

        private void Update()
        {
            _remainingSec -= Time.deltaTime;
            float fade = Mathf.Clamp01(_remainingSec / FadeDurationSec);

            float flicker = 0.75f + 0.5f * Mathf.PerlinNoise(Time.time * 9f, _flickerSeed);
            _light.intensity = _baseLightIntensity * flicker * fade;

            float sfx = Core.AudioManager.Instance != null ? Core.AudioManager.Instance.SfxVolume01 : 1f;
            _audio.volume = _baseVolume * fade * sfx;

            if (_remainingSec <= 0f && !_dying)
            {
                _dying = true;
                _audio.Stop();
                // Stop emitting new particles but let whatever's already in flight
                // (embers, drifting smoke) finish its own lifetime before the object
                // is removed — same "let it drift out" intent as before, just driven
                // by the pack's particle systems instead of ones we built ourselves.
                foreach (var ps in _vfxRoot.GetComponentsInChildren<ParticleSystem>())
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(gameObject, 12f); // a bigger blaze's residual embers/smoke linger a little longer
            }
        }

        /// <summary>Thermal updraft: rigidbodies inside the hot column above the fire
        /// get lifted and buffeted — flying the drone through the smoke is felt, not
        /// just seen. Cesium terrain/building colliders have no Rigidbody and are
        /// skipped automatically.</summary>
        private void FixedUpdate()
        {
            if (_dying) return;

            float radius = 3.5f + Intensity;
            Vector3 basePos = transform.position;
            var hits = Physics.OverlapCapsule(basePos, basePos + Vector3.up * UpdraftHeightM, radius);
            if (hits.Length == 0) return;

            PushedThisStep.Clear();
            foreach (var col in hits)
            {
                var rb = col.attachedRigidbody;
                if (rb == null || rb.isKinematic || !PushedThisStep.Add(rb)) continue;

                float height01 = Mathf.Clamp01((rb.position.y - basePos.y) / UpdraftHeightM);
                float falloff = 1f - height01;
                float t = Time.time * 1.7f;
                Vector3 turbulence = new Vector3(
                    Mathf.PerlinNoise(t, _flickerSeed) - 0.5f, 0f,
                    Mathf.PerlinNoise(_flickerSeed, t) - 0.5f) * (2f * Intensity);
                rb.AddForce(Vector3.up * (UpdraftNewtonsPerIntensity * Intensity * falloff) + turbulence,
                    ForceMode.Force);
            }
        }

        // ---- construction -------------------------------------------------

        private static GameObject FireVfxPrefab()
        {
            if (!_fireVfxPrefabLoaded)
            {
                _fireVfxPrefabLoaded = true;
                _fireVfxPrefab = Resources.Load<GameObject>("VFX/Fire/VFX_Fire_Floor_01_Smoke");
            }
            return _fireVfxPrefab;
        }

        private Transform InstantiateFireVfx()
        {
            var prefab = FireVfxPrefab();
            if (prefab == null) return transform; // pack not present — light/audio/updraft still work
            var vfx = Instantiate(prefab, transform);
            vfx.transform.localPosition = Vector3.zero;
            return vfx.transform;
        }

        private void BuildLight()
        {
            var go = new GameObject("FireLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.6f;
            _light = go.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.55f, 0.18f);
        }

        private void BuildAudio()
        {
            if (!_fireClipLoaded)
            {
                _fireClipLoaded = true;
                _fireClip = Resources.Load<AudioClip>("Audio/sfx/fire/fire");
            }

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.clip = _fireClip;
            _audio.loop = true;
            _audio.playOnAwake = false;
            // Fully 3D with linear rolloff: silent past maxDistance, swells as the
            // drone approaches — this IS the "play fire sound when close" behavior,
            // no distance-polling code needed.
            _audio.spatialBlend = 1f;
            _audio.rolloffMode = AudioRolloffMode.Linear;
            _audio.dopplerLevel = 0f;
            if (_fireClip != null)
            {
                _audio.time = Random.Range(0f, _fireClip.length); // desync stacked sites
                _audio.Play();
            }
        }
    }
}
