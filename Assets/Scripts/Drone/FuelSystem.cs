using UnityEngine;

namespace AeroTerra.Drone
{
    /// <summary>
    /// Liquid-fuel equivalent of BatterySystem, for combustion-engine airframes
    /// (AT-L3 Locust, AT-J9 Wraith, AT-U11 Bison) instead of an electric battery pack.
    /// Drives the same Watts-based Drain(watts, dt) call DroneFlightController already
    /// makes on a power source — the wattage figure is a shared "how hard is the engine
    /// working" signal, converted here into a fuel burn rate via a fixed specific-energy
    /// constant, so the flight controller never needs to know which power source it has.
    /// </summary>
    public class FuelSystem : MonoBehaviour, IPowerSource
    {
        /// <summary>Default fuel density (~0.74 kg/L, typical light aviation gasoline) —
        /// used unless a specific FuelVariant supplies its own. Lets tank size contribute
        /// physically to total mass, same as a battery pack.</summary>
        private const float DefaultDensityKgPerL = 0.74f;

        /// <summary>Specific energy used to convert the flight controller's electrical-
        /// equivalent Watts draw into a fuel burn rate (Wh delivered per liter burned).
        /// Tuned for gameplay pacing (comparable endurance to an electric loadout of
        /// similar tank/battery size), not real combustion efficiency.</summary>
        private const float WhPerLiter = 900f;

        public float CapacityL = 8f;
        public float RemainingL { get; private set; }
        private float _densityKgPerL = DefaultDensityKgPerL;
        private float _lastWatts = 1f;

        public float Percent => CapacityL <= 0 ? 0 : Mathf.Clamp01(RemainingL / CapacityL);
        public bool IsEmpty => RemainingL <= 0f;

        public float EstimatedMinutesLeft
        {
            get
            {
                if (_lastWatts <= 1f) return 999f;
                float literPerHour = _lastWatts / WhPerLiter;
                return literPerHour <= 0f ? 999f : (RemainingL / literPerHour) * 60f;
            }
        }

        public float MassKg => CapacityL * _densityKgPerL;

        private void Awake() => RemainingL = CapacityL;

        public void Configure(float capacityL) => Configure(capacityL, DefaultDensityKgPerL);

        /// <summary>Overload used when picking a specific FuelVariant — a compact
        /// high-density tank vs. a bulkier lightweight one can carry their own kg/L
        /// instead of all sharing the one default density.</summary>
        public void Configure(float capacityL, float densityKgPerL)
        {
            CapacityL = capacityL;
            RemainingL = capacityL;
            _densityKgPerL = densityKgPerL > 0f ? densityKgPerL : DefaultDensityKgPerL;
            var flight = GetComponent<DroneFlightController>();
            if (flight != null) flight.ApplyMass();
        }

        public void Drain(float watts, float dt)
        {
            _lastWatts = watts;
            float literPerSec = (watts / WhPerLiter) / 3600f;
            RemainingL = Mathf.Max(0f, RemainingL - literPerSec * dt);
        }
    }
}
