namespace AeroTerra.Workshop
{
    /// <summary>Per-airframe cumulative flight statistics, aggregated across every
    /// Free Flight session and every saved loadout of that base spec — keyed by
    /// DroneSpecification.Id (a "hours flown" figure for the AT-C1 Pelican, not for one
    /// particular custom build of it). Written by FlightLogTracker.Flush() when a Free
    /// Flight session ends, read by WorkshopUI's Specs ▸ Systems tab.</summary>
    [System.Serializable]
    public class DroneFlightLog
    {
        public string DroneId;
        public float TotalHours;
        public float TotalDistanceKm;
        public int Landings;
    }
}
