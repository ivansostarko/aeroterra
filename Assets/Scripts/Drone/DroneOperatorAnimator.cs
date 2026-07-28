using UnityEngine;

namespace AeroTerra.Drone
{
    /// <summary>
    /// Idle life for the procedural ground-operator prop (see
    /// AeroTerra.Procedural.DroneOperatorBuilder) — a gentle weight-shift sway plus
    /// slowly turning to face and lean back toward wherever the drone currently is, so
    /// the whole figure visibly "watches" the flight instead of standing frozen. Drives
    /// a single transform (Torso, which the builder reparents the head/cap/visor/arms/
    /// controller/vest-stripes under) rather than the head alone: the head is a plain
    /// featureless sphere with no face, so rotating it in isolation wouldn't read as
    /// "looking up" to a player at all — turning the whole upper body is what's actually
    /// visible. Purely cosmetic; the beacon pole/light DroneOperatorBuilder plants
    /// beside the figure is what actually makes the spawn point spottable from altitude,
    /// not this.
    /// </summary>
    public class DroneOperatorAnimator : MonoBehaviour
    {
        /// <summary>The flying drone to track — left null (idle sway only, no turning)
        /// if never assigned; FlightSceneController sets this right after spawning both
        /// the drone and the operator.</summary>
        public Transform Target;

        private const float MaxYawDeg = 85f;      // how far the figure can twist from its spawn-facing direction
        private const float MaxLeanBackDeg = 32f;  // how far it cranes back to watch something climb overhead
        private const float TrackSpeed = 2f;

        public Transform Torso;

        private float _swaySeed;

        private void Start()
        {
            if (Torso == null) Torso = transform.Find("Torso");
            _swaySeed = Random.Range(0f, 100f);
        }

        private void Update()
        {
            if (Torso == null) return;

            float sway = Mathf.Sin(Time.time * 0.6f + _swaySeed) * 2f;
            Quaternion idle = Quaternion.Euler(0f, 0f, sway);

            if (Target == null)
            {
                Torso.localRotation = idle;
                return;
            }

            Vector3 toTarget = Target.position - Torso.position;
            Vector3 flatDir = toTarget; flatDir.y = 0f;

            float yaw = 0f, leanBack = 0f;
            if (flatDir.sqrMagnitude > 0.01f)
            {
                yaw = Mathf.Clamp(Vector3.SignedAngle(transform.forward, flatDir.normalized, Vector3.up), -MaxYawDeg, MaxYawDeg);
                float elevation = Vector3.Angle(flatDir, toTarget); // 0 = level, 90 = straight up
                leanBack = Mathf.Clamp(elevation * 0.45f, 0f, MaxLeanBackDeg);
            }

            Quaternion tracking = Quaternion.Euler(-leanBack, yaw, 0f) * idle;
            Torso.localRotation = Quaternion.Slerp(Torso.localRotation, tracking, Time.deltaTime * TrackSpeed);
        }
    }
}
