using System.Collections.Generic;
using UnityEngine;

namespace AeroTerra.Drone
{
    /// <summary>
    /// Wingtip vapor trails for winged airframes: thin translucent ribbons stream
    /// off both wingtips during hard banks or high-speed flight, fading in and out
    /// with maneuvering intensity — the classic airshow condensation effect that
    /// makes aggressive flying visibly aggressive. Emitters attach to the wingtip
    /// nav lights (the outermost points of every winged model). Does nothing on a
    /// display model (no flight controller → zero intensity).
    /// </summary>
    public class WingtipTrailEffect : MonoBehaviour
    {
        private const float MinSpeedKmh = 55f;

        private DroneFlightController _flight;
        private readonly List<TrailRenderer> _trails = new List<TrailRenderer>();

        private void Start()
        {
            _flight = GetComponentInParent<DroneFlightController>();

            // The wingtip nav lights are the two furthest-out points on every
            // winged builder — perfect free anchors, no per-model coordinates.
            Transform left = null, right = null;
            float maxL = 0.1f, maxR = 0.1f;
            foreach (var tr in GetComponentsInChildren<Transform>(true))
            {
                if (tr.name != "NavLight") continue;
                float x = tr.localPosition.x;
                if (x < -maxL) { maxL = -x; left = tr; }
                if (x > maxR) { maxR = x; right = tr; }
            }
            if (left != null) _trails.Add(BuildTrail(left));
            if (right != null) _trails.Add(BuildTrail(right));
        }

        private TrailRenderer BuildTrail(Transform tip)
        {
            var go = new GameObject("WingtipTrail");
            go.transform.SetParent(tip, false);
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.8f;
            trail.minVertexDistance = 0.25f;
            // TrailRenderer widths are world-space already — no WingspanM rescale needed.
            trail.startWidth = 0.09f;
            trail.endWidth = 0.01f;
            trail.material = ExplosionEffect.BuildMat(Color.white);
            trail.startColor = new Color(1f, 1f, 1f, 0f);
            trail.endColor = new Color(1f, 1f, 1f, 0f);
            trail.emitting = true;
            return trail;
        }

        private void Update()
        {
            if (_trails.Count == 0) return;

            // Intensity: how hard the airframe is maneuvering at speed.
            float intensity = 0f;
            if (_flight != null && _flight.CurrentSpeedKmh > MinSpeedKmh)
            {
                float speedFactor = Mathf.Clamp01(
                    (_flight.CurrentSpeedKmh - MinSpeedKmh) / Mathf.Max(30f, _flight.Spec.MaxSpeedKmh - MinSpeedKmh));
                float bankFactor = Mathf.Clamp01(Mathf.Abs(_flight.BankDeg) / 45f);
                float stickFactor = Mathf.Max(Mathf.Abs(_flight.PitchInput), Mathf.Abs(_flight.RollInput));
                intensity = speedFactor * Mathf.Max(bankFactor, stickFactor * 0.8f);
            }

            float alpha = Mathf.Clamp01(intensity) * 0.55f;
            foreach (var trail in _trails)
            {
                var c = trail.startColor; c.a = alpha; trail.startColor = c;
            }
        }
    }
}
