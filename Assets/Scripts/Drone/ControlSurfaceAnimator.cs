using System.Collections.Generic;
using UnityEngine;

namespace AeroTerra.Drone
{
    /// <summary>
    /// Animates control surfaces on winged drones from live flight input:
    /// parts named "ElevonL"/"ElevonR" get elevon mixing (pitch ± roll) around
    /// their local X, parts named "Rudder" deflect around local Y with yaw.
    /// On a display model (no DroneFlightController in the parents) surfaces
    /// simply rest at neutral, so it is safe to attach everywhere.
    /// </summary>
    public class ControlSurfaceAnimator : MonoBehaviour
    {
        public float MaxDeflectionDeg = 22f;
        public float SlewDegPerSec = 240f;

        private DroneFlightController _flight;
        private readonly List<Transform> _left = new List<Transform>();
        private readonly List<Transform> _right = new List<Transform>();
        private readonly List<Transform> _rudders = new List<Transform>();
        private readonly List<Quaternion> _left0 = new List<Quaternion>();
        private readonly List<Quaternion> _right0 = new List<Quaternion>();
        private readonly List<Quaternion> _rudders0 = new List<Quaternion>();
        private float _curL, _curR, _curY;

        private void Start()
        {
            _flight = GetComponentInParent<DroneFlightController>();
            foreach (var tr in GetComponentsInChildren<Transform>(true))
            {
                switch (tr.name)
                {
                    case "ElevonL": _left.Add(tr); _left0.Add(tr.localRotation); break;
                    case "ElevonR": _right.Add(tr); _right0.Add(tr.localRotation); break;
                    case "Rudder": _rudders.Add(tr); _rudders0.Add(tr.localRotation); break;
                }
            }
        }

        private void Update()
        {
            float pitch = _flight != null ? _flight.PitchInput : 0f;
            float roll = _flight != null ? _flight.RollInput : 0f;
            float yaw = _flight != null ? _flight.YawInput : 0f;

            float targetL = Mathf.Clamp(pitch + roll, -1f, 1f) * MaxDeflectionDeg;
            float targetR = Mathf.Clamp(pitch - roll, -1f, 1f) * MaxDeflectionDeg;
            float targetY = Mathf.Clamp(yaw, -1f, 1f) * MaxDeflectionDeg;

            float step = SlewDegPerSec * Time.deltaTime;
            _curL = Mathf.MoveTowards(_curL, targetL, step);
            _curR = Mathf.MoveTowards(_curR, targetR, step);
            _curY = Mathf.MoveTowards(_curY, targetY, step);

            for (int i = 0; i < _left.Count; i++)
                _left[i].localRotation = _left0[i] * Quaternion.Euler(_curL, 0f, 0f);
            for (int i = 0; i < _right.Count; i++)
                _right[i].localRotation = _right0[i] * Quaternion.Euler(_curR, 0f, 0f);
            for (int i = 0; i < _rudders.Count; i++)
                _rudders[i].localRotation = _rudders0[i] * Quaternion.Euler(0f, _curY, 0f);
        }
    }
}
