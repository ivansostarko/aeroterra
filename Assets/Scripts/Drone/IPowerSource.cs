namespace AeroTerra.Drone
{
    /// <summary>
    /// Common contract for a drone's onboard energy store — BatterySystem (electric
    /// packs) or FuelSystem (combustion tanks, e.g. AT-L3 Locust/AT-J9 Wraith/AT-U11
    /// Bison). DroneFlightController talks to whichever one is present through this
    /// interface instead of branching on drone/power-system type throughout its code.
    /// </summary>
    public interface IPowerSource
    {
        float Percent { get; }
        bool IsEmpty { get; }
        float EstimatedMinutesLeft { get; }
        float MassKg { get; }
        void Drain(float watts, float dt);
    }
}
