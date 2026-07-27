using UnityEngine;

namespace AeroTerra.Drone
{
    /// <summary>
    /// Slow idle pan/tilt for a camera gimbal ball — makes the sensor feel
    /// alive both on the Workshop turntable and in flight. Pure visual.
    /// </summary>
    public class GimbalScanner : MonoBehaviour
    {
        public float YawRangeDeg = 35f;
        public float PitchRangeDeg = 8f;
        public float Speed = 0.35f;

        private Quaternion _base;
        private float _seed;

        private void Start()
        {
            _base = transform.localRotation;
            _seed = (GetInstanceID() & 0xFF) * 0.13f;   // desync multiple gimbals
        }

        private void Update()
        {
            float t = Time.time * Speed + _seed;
            transform.localRotation = _base * Quaternion.Euler(
                Mathf.Sin(t * 0.63f) * PitchRangeDeg,
                Mathf.Sin(t) * YawRangeDeg, 0f);
        }
    }
}
