using UnityEngine;

namespace AeroTerra.Drone
{
    /// <summary>
    /// Subtle reactive lean for a multirotor's rotor/arm assembly ("RotorRig"): on
    /// top of the real physics-driven body roll/pitch, the whole arm+rotor group
    /// tilts a few extra degrees toward the stick input, like a real quad visibly
    /// vectoring its rotor plane during a maneuver. Purely cosmetic; safe on a
    /// display model (no DroneFlightController in the parents) since it just rests
    /// at neutral, same convention as ControlSurfaceAnimator.
    /// </summary>
    public class RotorTiltAnimator : MonoBehaviour
    {
        public float MaxTiltDeg = 7f;
        public float SlewDegPerSec = 200f;

        private DroneFlightController _flight;
        private Quaternion _base;
        private float _curX, _curZ;

        private void Start()
        {
            _flight = GetComponentInParent<DroneFlightController>();
            _base = transform.localRotation;
        }

        private void Update()
        {
            float pitch = _flight != null ? _flight.PitchInput : 0f;
            float roll = _flight != null ? _flight.RollInput : 0f;

            float targetX = Mathf.Clamp(pitch, -1f, 1f) * MaxTiltDeg;
            float targetZ = Mathf.Clamp(-roll, -1f, 1f) * MaxTiltDeg;

            float step = SlewDegPerSec * Time.deltaTime;
            _curX = Mathf.MoveTowards(_curX, targetX, step);
            _curZ = Mathf.MoveTowards(_curZ, targetZ, step);

            transform.localRotation = _base * Quaternion.Euler(_curX, 0f, _curZ);
        }
    }
}
