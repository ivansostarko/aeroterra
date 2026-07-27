using UnityEngine;
using AeroTerra.Procedural;

namespace AeroTerra.Drone
{
    /// <summary>
    /// Explosion: fire + smoke from the Vefects "Free Fire VFX URP" asset pack
    /// (Assets/Vefects/Free Fire VFX URP — VFX_Fire_01_Big_Smoke.prefab, moved to
    /// Assets/Resources/VFX/Fire so it loads by Resources path like every other
    /// runtime asset in this project), plus a fading point-light flash, spark/debris/
    /// dust-ring particle systems and an expanding shockwave disc, all still built
    /// procedurally at runtime (see DroneMeshBuilder for the project's usual fully-
    /// procedural convention — fire/smoke is the one deliberate exception, per user
    /// request). Triggered by DroneFlightController on a hard crash and by
    /// PayloadDropper when an armed (non-cargo) payload lands.
    /// </summary>
    public static class ExplosionEffect
    {
        private static GameObject _fireSmokePrefab;
        private static bool _fireSmokePrefabLoaded;

        private static GameObject FireSmokePrefab()
        {
            if (!_fireSmokePrefabLoaded)
            {
                _fireSmokePrefabLoaded = true;
                _fireSmokePrefab = Resources.Load<GameObject>("VFX/Fire/VFX_Fire_01_Big_Smoke");
            }
            return _fireSmokePrefab;
        }

        /// <summary>Detonation at a point. scale grows the whole event — stacked hits
        /// on an existing FireSite pass scale > 1 so repeat strikes on the same spot
        /// visibly escalate. Lingering fire/smoke is FireSite.RegisterHit's job.</summary>
        public static void Spawn(Vector3 position, float scale = 1f)
        {
            var root = new GameObject("ExplosionFX");
            root.transform.position = position;

            BuildFlash(root.transform, scale);
            BuildFireAndSmoke(root.transform, scale);
            BuildSparks(root.transform, scale);
            BuildDebris(root.transform, scale);
            BuildDustRing(root.transform, scale);
            BuildShockwave(root.transform, scale);
            ApplyBlastForce(position, scale);
            AeroTerra.UI.DroneCameraRig.Instance?.ShakeFromPoint(position, scale);

            Object.Destroy(root, 4.5f);
        }

        /// <summary>Fire + smoke burst at the blast point, from the imported asset
        /// pack rather than a procedural particle system. The prefab loops on its own,
        /// so it just rides along until Spawn's root object (and everything under it)
        /// is destroyed at the 4.5s mark.</summary>
        private static void BuildFireAndSmoke(Transform parent, float scale)
        {
            var prefab = FireSmokePrefab();
            if (prefab == null) return; // pack not present — rest of the blast still reads fine
            var vfx = Object.Instantiate(prefab, parent);
            vfx.transform.localPosition = Vector3.zero;
            vfx.transform.localScale = Vector3.one * scale;
        }

        /// <summary>Soft ground-contact effect for an unarmed cargo pod: a low dust
        /// kick-up and nothing else — no fire, no blast physics.</summary>
        public static void SpawnDustPuff(Vector3 position)
        {
            var root = new GameObject("DustPuffFX");
            root.transform.position = position;
            BuildDustRing(root.transform, 0.45f);
            Object.Destroy(root, 3f);
        }

        /// <summary>Real physics kick: shove every rigidbody near the blast (the player
        /// drone included — flying low over your own strike is now punished). Terrain
        /// and building colliders have no Rigidbody and are unaffected.</summary>
        private static void ApplyBlastForce(Vector3 position, float scale)
        {
            float radius = 10f * scale;
            var hits = Physics.OverlapSphere(position, radius);
            var pushed = new System.Collections.Generic.HashSet<Rigidbody>();
            foreach (var col in hits)
            {
                var rb = col.attachedRigidbody;
                if (rb == null || rb.isKinematic || !pushed.Add(rb)) continue;
                rb.AddExplosionForce(160f * scale, position, radius, 1.2f, ForceMode.Impulse);
            }
        }

        internal static Material BuildMat(Color color)
        {
            var m = new Material(DroneMeshBuilder.TransparentShader());
            if (m.HasProperty("_Color")) m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            return m;
        }

        private static void BuildFlash(Transform parent, float scale)
        {
            var go = new GameObject("Flash");
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.65f, 0.25f);
            light.intensity = 9f * scale;
            light.range = 18f * scale;
            go.AddComponent<FadeLight>().Init(light, 0.35f);
        }

        private static void BuildSparks(Transform parent, float scale)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f * scale, 9f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.4f, 0.1f));
            main.gravityModifier = 1.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(24 * scale)) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.1f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f), new GradientColorKey(new Color(0.5f, 0.1f, 0f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = BuildMat(Color.white);
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;

            ps.Play();
        }

        /// <summary>Heavy dark fragments hurled outward on ballistic arcs — reads as
        /// actual matter thrown by the blast, not just glow.</summary>
        private static void BuildDebris(Transform parent, float scale)
        {
            var go = new GameObject("Debris");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f * scale, 12f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.15f, 0.13f, 0.11f), new Color(0.05f, 0.05f, 0.05f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(16 * scale)) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 0.2f;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // spray upward

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = BuildMat(Color.white);
            r.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
        }

        /// <summary>Tan dust ring racing outward along the ground from the impact
        /// point. Also reused (small) as the cargo-pod landing puff.</summary>
        private static void BuildDustRing(Transform parent, float scale)
        {
            var go = new GameObject("DustRing");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * 0.2f;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // circle plane horizontal
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f * scale, 9f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f * scale, 1.6f * scale);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.5f, 0.44f, 0.35f, 0.55f), new Color(0.35f, 0.31f, 0.26f, 0.45f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(18 * Mathf.Max(0.5f, scale))) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.4f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.7f, 1, 2.2f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.5f, 0.44f, 0.35f), 0f), new GradientColorKey(new Color(0.3f, 0.27f, 0.23f), 1f) },
                new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = BuildMat(Color.white);
            r.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
        }

        private static void BuildShockwave(Transform parent, float scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Shockwave";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(0.1f, 0.02f, 0.1f);

            var mat = BuildMat(new Color(1f, 0.75f, 0.4f, 0.5f));
            go.GetComponent<Renderer>().sharedMaterial = mat;

            go.AddComponent<ShockwavePulse>().Init(mat, 7f * scale, 0.45f);
        }

        /// <summary>Fades a point light out over its lifetime, then disables it.</summary>
        private class FadeLight : MonoBehaviour
        {
            private Light _light;
            private float _duration;
            private float _startIntensity;
            private float _t;

            public void Init(Light light, float duration)
            {
                _light = light;
                _duration = duration;
                _startIntensity = light.intensity;
            }

            private void Update()
            {
                _t += Time.deltaTime;
                float k = Mathf.Clamp01(_t / _duration);
                _light.intensity = Mathf.Lerp(_startIntensity, 0f, k);
                if (k >= 1f) enabled = false;
            }
        }

        /// <summary>Scales a flat disc outward while fading it to transparent.</summary>
        private class ShockwavePulse : MonoBehaviour
        {
            private Material _mat;
            private float _targetScale;
            private float _duration;
            private float _t;
            private Color _startColor;

            public void Init(Material mat, float targetScale, float duration)
            {
                _mat = mat;
                _targetScale = targetScale;
                _duration = duration;
                _startColor = mat.HasProperty("_Color") ? mat.color : Color.white;
            }

            private void Update()
            {
                _t += Time.deltaTime;
                float k = Mathf.Clamp01(_t / _duration);
                float scale = Mathf.Lerp(0.1f, _targetScale, 1f - Mathf.Pow(1f - k, 3f));
                transform.localScale = new Vector3(scale, 0.02f, scale);

                var c = _startColor;
                c.a = Mathf.Lerp(_startColor.a, 0f, k);
                if (_mat.HasProperty("_Color")) _mat.color = c;
                if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", c);

                if (k >= 1f) enabled = false;
            }
        }
    }
}
