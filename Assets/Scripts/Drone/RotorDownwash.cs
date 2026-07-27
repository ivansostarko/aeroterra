using UnityEngine;

namespace AeroTerra.Drone
{
    /// <summary>
    /// Ground dust kicked up beneath the drone by its own rotor wash — present
    /// whenever the drone is low and under real thrust, not just a fixed effect
    /// under a flying object. A single ring-shaped particle system is repositioned
    /// onto the ground point directly under the drone every frame (via raycast)
    /// and its emission rate/size scale with how close to the ground and how hard
    /// the drone is thrusting, so a slow low hover kicks up a light haze while a
    /// hard low-altitude thrust throws a full dust cloud.
    /// </summary>
    [RequireComponent(typeof(DroneFlightController))]
    public class RotorDownwash : MonoBehaviour
    {
        private const float MaxHeightM = 12f;
        private const float RaycastRangeM = 60f;

        private DroneFlightController _flight;
        private ParticleSystem _dust;
        private Transform _dustTransform;

        private void Start()
        {
            _flight = GetComponent<DroneFlightController>();
            BuildDust();
        }

        private void Update()
        {
            if (_dustTransform == null) return;

            if (!Physics.Raycast(transform.position, Vector3.down, out var hit, RaycastRangeM))
            {
                var offEmission = _dust.emission;
                offEmission.rateOverTime = 0f;
                return;
            }

            _dustTransform.position = hit.point + Vector3.up * 0.05f;

            float heightFactor = 1f - Mathf.Clamp01(hit.distance / MaxHeightM);
            float throttleFactor = Mathf.Clamp01((_flight.Throttle01 - 0.3f) / 0.6f);
            float intensity = heightFactor * throttleFactor;

            var emission = _dust.emission;
            emission.rateOverTime = intensity * 26f;
            _dustTransform.localScale = Vector3.one * (0.6f + intensity * 1.6f);
        }

        private void BuildDust()
        {
            var go = new GameObject("RotorDownwashDust");
            _dustTransform = go.transform;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // ring lies flat on the ground
            _dust = go.AddComponent<ParticleSystem>();

            var main = _dust.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.55f, 0.48f, 0.38f, 0.35f), new Color(0.4f, 0.35f, 0.28f, 0.25f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _dust.emission;
            emission.rateOverTime = 0f;

            var shape = _dust.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.2f;

            var sol = _dust.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.8f));

            var col = _dust.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.5f, 0.44f, 0.35f), 0f), new GradientColorKey(new Color(0.3f, 0.27f, 0.23f), 1f) },
                new[] { new GradientAlphaKey(0.35f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var noise = _dust.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.6f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = ExplosionEffect.BuildMat(Color.white);
            r.renderMode = ParticleSystemRenderMode.Billboard;

            _dust.Play();
        }

        private void OnDestroy()
        {
            if (_dustTransform != null) Destroy(_dustTransform.gameObject);
        }
    }
}
