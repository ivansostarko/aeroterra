using System.Collections;
using UnityEngine;

namespace AeroTerra.Drone
{
    /// <summary>Handles the Workshop "Parachute" loadout item's in-flight deploy — G key
    /// (InputManager.ParachuteAction), gated on altitude
    /// (DroneFlightController.ParachuteMinDeployAltitudeM) and not already deployed this
    /// flight (a real canopy doesn't re-pack itself mid-air). Owns the canopy's opening
    /// animation; the actual physics response (motors off, slow controlled sink) lives
    /// on DroneFlightController itself (see DeployParachute/TickParachuteDescent) since
    /// that's shared regardless of how the deploy was triggered. Only ever added to a
    /// drone whose CustomDroneData.ParachuteEquipped is true — see DroneFactory.Spawn.</summary>
    public class ParachuteController : MonoBehaviour
    {
        private const float DeployDurationSec = 0.6f;

        private Transform _canopyRoot;
        private DroneFlightController _flight;
        private bool _deploying;
        private bool _lastKnownDeployed;

        public void Configure(Transform canopyRoot, DroneFlightController flight)
        {
            _canopyRoot = canopyRoot;
            _flight = flight;
        }

        private void Update()
        {
            if (_canopyRoot == null || _flight == null) return;

            // A respawn/reset clears DroneFlightController.ParachuteDeployed — snap the
            // canopy back to collapsed/hidden the instant that happens, whatever this
            // controller's own opening animation was mid-way through.
            if (_lastKnownDeployed && !_flight.ParachuteDeployed)
            {
                _deploying = false;
                _canopyRoot.localScale = Vector3.zero;
            }
            _lastKnownDeployed = _flight.ParachuteDeployed;

            var im = AeroTerra.Input.InputManager.Instance;
            if (im == null || _flight.ParachuteDeployed || _deploying) return;
            if (im.ParachuteAction == null || !im.ParachuteAction.WasPressedThisFrame()) return;
            if (transform.position.y < DroneFlightController.ParachuteMinDeployAltitudeM) return;

            _flight.DeployParachute();
            _deploying = true;
            AeroTerra.Core.AudioManager.Instance?.PlayParachuteOpen(transform.position);
            StartCoroutine(DeployAnimation());
        }

        /// <summary>Canopy scale animates 0 → 1 with a "back-out" ease (a brief overshoot
        /// past full size before settling back) — reads as a real parachute punching
        /// open under load and snapping taut, not a linear/robotic grow.</summary>
        private IEnumerator DeployAnimation()
        {
            float t = 0f;
            while (t < DeployDurationSec)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / DeployDurationSec);
                if (_canopyRoot != null) _canopyRoot.localScale = Vector3.one * BackOut(p);
                yield return null;
            }
            if (_canopyRoot != null) _canopyRoot.localScale = Vector3.one;
            _deploying = false;
        }

        private static float BackOut(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }
    }
}
