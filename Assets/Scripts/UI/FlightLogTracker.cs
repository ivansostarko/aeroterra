using UnityEngine;
using AeroTerra.Core;
using AeroTerra.Drone;

namespace AeroTerra.UI
{
    /// <summary>
    /// Accumulates this Free Flight session's flown time/distance/landings and writes
    /// them into the persistent per-airframe flight log (SaveSystem.LoadFlightLogs/
    /// SaveFlightLogs, keyed by DroneSpecification.Id — aggregated across every custom
    /// loadout of that base airframe, not per saved build) once the session ends. Flush()
    /// is called from FlightSceneController right before every GameManager.ReturnToMenu()
    /// call site (pause menu's MAIN MENU button, the battery-dead game-over modal's OK
    /// button). Read back by WorkshopUI's Specs ▸ Systems tab.
    /// </summary>
    public class FlightLogTracker : MonoBehaviour
    {
        private DroneFlightController _flight;
        private string _droneId;
        private Vector3 _lastPos;
        private float _sessionHours;
        private float _sessionDistanceKm;
        private int _sessionLandings;
        private bool _flushed;

        public void Init(DroneFlightController flight)
        {
            _flight = flight;
            _droneId = flight.Spec.Id;
            _lastPos = flight.transform.position;
            flight.Landed += OnLanded;
        }

        private void OnLanded() => _sessionLandings++;

        private void Update()
        {
            if (_flight == null) return;
            _sessionHours += Time.deltaTime / 3600f;

            Vector3 pos = _flight.transform.position;
            _sessionDistanceKm += Vector3.Distance(_lastPos, pos) / 1000f;
            _lastPos = pos;
        }

        /// <summary>Writes this session's accumulated stats into the persistent log.
        /// Safe to call more than once (defensively before every ReturnToMenu path) —
        /// only the first call actually has anything to add.</summary>
        public void Flush()
        {
            if (_flushed || _flight == null) return;
            _flushed = true;

            var logs = SaveSystem.LoadFlightLogs();
            var entry = logs.Find(l => l.DroneId == _droneId);
            if (entry == null)
            {
                entry = new Workshop.DroneFlightLog { DroneId = _droneId };
                logs.Add(entry);
            }
            entry.TotalHours += _sessionHours;
            entry.TotalDistanceKm += _sessionDistanceKm;
            entry.Landings += _sessionLandings;
            SaveSystem.SaveFlightLogs(logs);
        }

        private void OnDestroy()
        {
            if (_flight != null) _flight.Landed -= OnLanded;
        }
    }
}
