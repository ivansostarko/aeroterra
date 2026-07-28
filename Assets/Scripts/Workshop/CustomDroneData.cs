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

        /// <summary>Recovery parachute — extra weight only (see LoadoutExtras.ParachuteKg),
        /// no deploy-on-crash behavior yet. No initializer needed: WorkshopController.Show()
        /// explicitly sets this true for every freshly-opened config (the new default), while
        /// JsonUtility still zero-inits it to false for any save written before this field
        /// existed — same "explicit-default-in-Show(), implicit-false-on-old-saves" pattern
        /// HasCustomBodyColor established above.</summary>
        public bool ParachuteEquipped;

        /// <summary>AI-assisted sensor pod — extra weight only (see LoadoutExtras.AiSensorKg),
        /// purely descriptive like Comms. Defaults false both for old saves and fresh
        /// configs (WorkshopController.Show() sets it explicitly for clarity even though
        /// it matches the implicit default).</summary>
        public bool AiSensorEquipped;

        /// <summary>Gates SelectedPayloadKind below — false (the default, and what every
        /// save written before the AMMUNITION PAYLOAD category picker existed deserializes
        /// to) means "not set — use the base spec's own PayloadKind", same
        /// HasCustomBodyColor-style gating. Only meaningful for drones whose
        /// DroneSpecification.AvailablePayloadKinds has 2+ entries (currently only AT-R4
        /// Hornet) — see WorkshopController.SetPayloadKind and
        /// DroneFactory.Spawn's EffectivePayloadKind resolution.</summary>
        public bool HasSelectedPayloadKind;
        public AeroTerra.Drone.PayloadKind SelectedPayloadKind;

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
