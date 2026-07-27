using UnityEngine;

namespace AeroTerra.Drone
{
    /// <summary>Watt-hour battery model with drain and remaining-time estimate.</summary>
    public class BatterySystem : MonoBehaviour, IPowerSource
    {
        /// <summary>Default energy density for a drone LiPo/Li-ion pack (real packs run
        /// ~150-200 Wh/kg) — used unless a specific BatteryVariant supplies its own
        /// density. Lets the battery choice contribute physically to total mass, same as
        /// payload, so a bigger/denser pack is a real performance trade-off.</summary>
        private const float DefaultEnergyDensityWhPerKg = 180f;

        public float CapacityWh = 500f;
        public float RemainingWh { get; private set; }
        private float _energyDensityWhPerKg = DefaultEnergyDensityWhPerKg;
        private float _lastWatts = 1f;

        public float Percent => CapacityWh <= 0 ? 0 : Mathf.Clamp01(RemainingWh / CapacityWh);
        public bool IsEmpty => RemainingWh <= 0f;
        public float EstimatedMinutesLeft => _lastWatts <= 1f ? 999f : (RemainingWh / _lastWatts) * 60f;
        public float MassKg => CapacityWh / _energyDensityWhPerKg;

        private void Awake() => RemainingWh = CapacityWh;

        public void Configure(float capacityWh) => Configure(capacityWh, DefaultEnergyDensityWhPerKg);

        /// <summary>Overload used when picking a specific BatteryVariant — different
        /// packs (e.g. a lightweight racing cell vs. a dense long-range brick) can carry
        /// their own Wh/kg instead of all sharing the one default density.</summary>
        public void Configure(float capacityWh, float energyDensityWhPerKg)
        {
            CapacityWh = capacityWh;
            RemainingWh = capacityWh;
            _energyDensityWhPerKg = energyDensityWhPerKg > 0f ? energyDensityWhPerKg : DefaultEnergyDensityWhPerKg;
            var flight = GetComponent<DroneFlightController>();
            if (flight != null) flight.ApplyMass();
        }

        public void Drain(float watts, float dt)
        {
            _lastWatts = watts;
            RemainingWh = Mathf.Max(0f, RemainingWh - watts * dt / 3600f);
        }
    }
}
