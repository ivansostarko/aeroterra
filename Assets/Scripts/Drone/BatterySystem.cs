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

        // Cold slows the chemical reaction a LiPo/Li-ion pack relies on to deliver
        // current; excess heat gets the pack derated/throttled for safety — real packs
        // suffer both ways. 100% in a comfortable band, fading linearly to a 60% floor
        // at the extremes. Only ever consulted for Battery-powered airframes —
        // DroneFlightController.PowerSystem == Fuel skips this entirely (FuelSystem has
        // no equivalent; combustion engines aren't modeled as temperature-sensitive here).
        private const float ComfortMinC = 5f, ComfortMaxC = 35f;
        private const float ExtremeColdC = -20f, ExtremeHotC = 50f;
        private const float MinPerformanceFactor = 0.6f;

        /// <summary>Thrust-ceiling multiplier for the current air temperature — 1.0
        /// inside the comfortable band, dropping toward MinPerformanceFactor beyond it
        /// in either direction. See DroneFlightController's thrust clamp for where this
        /// is actually applied.</summary>
        public static float PerformanceFactor(float temperatureC)
        {
            if (temperatureC < ComfortMinC)
                return Mathf.Lerp(MinPerformanceFactor, 1f, Mathf.InverseLerp(ExtremeColdC, ComfortMinC, temperatureC));
            if (temperatureC > ComfortMaxC)
                return Mathf.Lerp(MinPerformanceFactor, 1f, Mathf.InverseLerp(ExtremeHotC, ComfortMaxC, temperatureC));
            return 1f;
        }
    }
}
