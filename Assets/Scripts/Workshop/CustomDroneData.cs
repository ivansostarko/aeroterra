using System;
using UnityEngine;

namespace AeroTerra.Workshop
{
    /// <summary>A saved workshop configuration for one of the base drones.</summary>
    [Serializable]
    public class CustomDroneData
    {
        public string CustomName;
        public string BaseSpecId;      // links back to DroneSpecification.Id
        public float BatteryWh;        // used when the base spec's PowerSystem == Battery
        public float FuelL;            // used when the base spec's PowerSystem == Fuel
        public float PayloadKg;

        /// <summary>Procedural pattern id from DroneSkinBuilder (e.g. "stock", "camo",
        /// "stripes", "splitfade", "digital"), painted over whichever body color applies
        /// — the base spec's fixed DefaultBodyColor, or BodyR/G/B below if
        /// HasCustomBodyColor is set via the Workshop's MAIN COLOR picker.</summary>
        public string SkinId = "stock";

        public bool SmokeScreenEquipped;
        public AeroTerra.Drone.CommsType Comms = AeroTerra.Drone.CommsType.Radio;

        /// <summary>Gates BodyR/G/B below — false (the default, and what every save
        /// written before the MAIN COLOR picker existed deserializes to, since
        /// JsonUtility defaults a missing bool to false) means "not set — use the base
        /// spec's own DefaultBodyColor", so pre-existing saves don't change appearance.</summary>
        public bool HasCustomBodyColor;
        public float BodyR = 0.5f, BodyG = 0.5f, BodyB = 0.5f;

        // Legacy fields from the old accent color picker — no longer written by the
        // Workshop UI, kept only so older saved configs still deserialize cleanly.
        public float AccentR = 0.1f, AccentG = 0.1f, AccentB = 0.1f;

        public string CreatedUtc;
    }
}
