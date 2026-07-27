namespace AeroTerra.UI
{
    /// <summary>
    /// JSON sidecar written next to every screenshot PNG (same base filename, .json
    /// extension) by InstantReplayController.TakeScreenshot — read back by MediaUI to
    /// show the overlay caption in the gallery. CapturedAtIso uses DateTime "o" (round-
    /// trip ISO 8601) so it parses back exactly regardless of the player's locale.
    /// </summary>
    [System.Serializable]
    public class ScreenshotMeta
    {
        public string DroneName;
        public string City;
        public float AltitudeM;
        public string CapturedAtIso;
    }
}
