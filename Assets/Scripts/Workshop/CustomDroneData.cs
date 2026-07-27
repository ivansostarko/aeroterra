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
        /// "stripes", "splitfade", "digital") — replaces the old body/accent color
        /// pickers. The pattern is generated from the base spec's fixed
        /// DefaultBodyColor/DefaultAccentColor, so it's never hand-tinted per config.</summary>
        public string SkinId = "stock";

        public bool SmokeScreenEquipped;
        public AeroTerra.Drone.CommsType Comms = AeroTerra.Drone.CommsType.Radio;

        // Legacy fields from the old livery color pickers — no longer written by the
        // Workshop UI, kept only so older saved configs still deserialize cleanly.
        public float BodyR = 0.5f, BodyG = 0.5f, BodyB = 0.5f;
        public float AccentR = 0.1f, AccentG = 0.1f, AccentB = 0.1f;

        public string CreatedUtc;
    }
}
