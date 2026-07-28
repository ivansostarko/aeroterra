namespace AeroTerra.Drone
{
    /// <summary>Onboard link a drone carries. Doesn't drive a jamming/interference
    /// mechanic (none exists in this game) — it's a real weight-affecting loadout
    /// choice with a descriptive "Signal" spec-sheet stat, same spirit as livery used
    /// to be purely cosmetic before skins made it a real customization axis.</summary>
    public enum CommsType { Radio, FiveG, AnalogWire }

    /// <summary>Static data for the "additional loadout" slots every drone can equip
    /// in the Workshop: a smoke-screen canister, a comms radio, a recovery parachute,
    /// and an AI sensor pod.</summary>
    public static class LoadoutExtras
    {
        /// <summary>Weight of an equipped smoke-screen canister — when equipped, the
        /// airframe trails a continuous colored smoke plume in flight (DroneFactory).</summary>
        public const float SmokeScreenKg = 0.35f;

        /// <summary>Weight of an equipped recovery parachute — canister, lines and
        /// deployment mechanism, real weight only (no deploy-on-crash behavior yet),
        /// same "descriptive loadout choice" precedent Comms already established.</summary>
        public const float ParachuteKg = 0.45f;

        /// <summary>Weight of an equipped AI sensor pod — a compute/sensor payload,
        /// real weight only, purely descriptive like Comms.</summary>
        public const float AiSensorKg = 0.25f;

        /// <summary>Weight of an equipped horn/speaker unit — a small onboard warning
        /// horn (H key in flight, Assets/Resources/Audio/sfx/drone/horn.mp3, see
        /// DroneHornController), lightest of the additional-loadout items since it's
        /// just a small speaker, not a canister/pod/tank.</summary>
        public const float HornKg = 0.08f;

        public static float CommsWeightKg(CommsType type) => type switch
        {
            CommsType.Radio => 0.15f,
            CommsType.FiveG => 0.08f,
            CommsType.AnalogWire => 0.30f,
            _ => 0f,
        };

        public static string CommsLabel(CommsType type) => type switch
        {
            CommsType.Radio => "RADIO",
            CommsType.FiveG => "5G",
            CommsType.AnalogWire => "ANALOG (WIRE)",
            _ => type.ToString().ToUpperInvariant(),
        };

        /// <summary>Flavor-only reliability rating (1-5), shown next to the comms
        /// picker — Analog never drops but is heaviest/tethered, 5G is lightest.</summary>
        public static int CommsSignalStars(CommsType type) => type switch
        {
            CommsType.Radio => 3,
            CommsType.FiveG => 5,
            CommsType.AnalogWire => 5,
            _ => 3,
        };
    }
}
