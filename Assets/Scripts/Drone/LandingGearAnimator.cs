using System.Collections.Generic;
using UnityEngine;

namespace AeroTerra.Drone
{
    /// <summary>
    /// Retractable landing gear for fixed-wing airframes: every child named
    /// "GearPivot*" folds rearward-up once the aircraft is flying fast enough,
    /// and extends again when it slows down or descends low — the classic
    /// wheels-up moment after takeoff. On a display model (no flight controller
    /// in the parents) the gear simply stays extended, same convention as
    /// ControlSurfaceAnimator.
    /// </summary>
    public class LandingGearAnimator : MonoBehaviour
    {
        public float RetractSpeedKmh = 65f;  // faster than this → tuck the gear
        public float ExtendAltitudeM = 40f;  // below this altitude → gear back out
        public float FoldAngleDeg = 95f;
        public float FoldDegPerSec = 60f;

        private DroneFlightController _flight;
        private readonly List<Transform> _pivots = new List<Transform>();
        private readonly List<Quaternion> _extended = new List<Quaternion>();
        private float _foldDeg;

        private void Start()
        {
            _flight = GetComponentInParent<DroneFlightController>();
            foreach (var tr in GetComponentsInChildren<Transform>(true))
                if (tr.name.StartsWith("GearPivot"))
                {
                    _pivots.Add(tr);
                    _extended.Add(tr.localRotation);
                }
        }

        private void Update()
        {
            if (_pivots.Count == 0) return;

            bool up = _flight != null
                   && _flight.CurrentSpeedKmh > RetractSpeedKmh
                   && _flight.transform.position.y > ExtendAltitudeM;

            _foldDeg = Mathf.MoveTowards(_foldDeg, up ? FoldAngleDeg : 0f, FoldDegPerSec * Time.deltaTime);

            for (int i = 0; i < _pivots.Count; i++)
                _pivots[i].localRotation = _extended[i] * Quaternion.Euler(_foldDeg, 0f, 0f);
        }
    }
}
