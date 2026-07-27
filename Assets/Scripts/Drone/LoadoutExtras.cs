namespace AeroTerra.Drone
{
    /// <summary>Onboard link a drone carries. Doesn't drive a jamming/interference
    /// mechanic (none exists in this game) — it's a real weight-affecting loadout
    /// choice with a descriptive "Signal" spec-sheet stat, same spirit as livery used
    /// to be purely cosmetic before skins made it a real customization axis.</summary>
    public enum CommsType { Radio, FiveG, AnalogWire }

    /// <summary>Static data for the two "additional loadout" slots every drone can
    /// equip in the Workshop: a smoke-screen canister and a comms radio.</summary>
    public static class LoadoutExtras
    {
        /// <summary>Weight of an equipped smoke-screen canister — when equipped, the
        /// airframe trails a continuous colored smoke plume in flight (DroneFactory).</summary>
        public const float SmokeScreenKg = 0.35f;

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
