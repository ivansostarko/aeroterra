using System;
using System.Collections.Generic;

namespace AeroTerra.Core
{
    // Explicit values (skipping 1, the old KeyboardMouse) so existing settings.json saves
    // with Scheme: 2 (Gamepad) or Scheme: 3 (Gyroscope) — JsonUtility serializes enums as
    // their int, not by name — still deserialize correctly rather than silently shifting.
    public enum ControlScheme { Keyboard = 0, Gamepad = 2, Gyroscope = 3 }
    public enum MapStyle { Liberty, Terrain, Satellite, OsmStandard, Dark }
    public enum SkyPreset { Day, Dawn, Dusk, Night }
    public enum WeatherPreset { Clear, Cloudy, Rain, Storm, Fog, Snow }
    public enum Language { English } // only option for now; UI is built to add more later

    public enum WindowMode { Windowed, Fullscreen }
    public enum GraphicsQuality { Low, Medium, High, Ultra }
    public enum AntiAliasingMode { Off, FXAA, SMAA, TAA }
    public enum ShadowDetail { Off, Low, Medium, High } // named to avoid clashing with UnityEngine.ShadowQuality
    public enum TextureQuality { Low, Medium, High, Full }
    public enum EffectsQuality { Low, Medium, High }

    /// <summary>All user-configurable settings. Serialized to JSON by SaveSystem.</summary>
    [Serializable]
    public class SettingsData
    {
        // Game
        public bool ShowHud = true;

        // Game — HUD elements: each independently toggleable, all visible by default.
        // FlightHUD.ApplyHudElementSettings() reads these; NarratorController.Enqueue()
        // reads HudShowNarrator (gates both the subtitle text and the voice line audio).
        public bool HudShowSpeed = true;
        public bool HudShowAltitude = true;
        public bool HudShowGps = true;
        public bool HudShowBattery = true;
        public bool HudShowNarrator = true;
        public bool HudShowPayload = true;
        public bool HudShowCompass = true;
        public bool HudShowMinimap = true;
        public bool HudShowWind = true;
        public bool HudShowThrottle = true;
        public bool HudShowTemperature = true;

        // Flying Conditions — Wind/Temperature/Humidity are each a free-standing slider
        // that gets reset to the picked WeatherPreset's typical value (see
        // WeatherSystem.BaseWindSpeedMs/BaseTemperatureC/BaseHumidityPercent) whenever
        // Weather changes, but stay independently adjustable afterward — weather type
        // is a starting point, not a lock.
        public SkyPreset Sky = SkyPreset.Day;
        public WeatherPreset Weather = WeatherPreset.Clear;
        public float WindSpeedMs = 5f;
        public float TemperatureC = 22f;
        public float HumidityPercent = 45f;

        // Video
        public WindowMode WindowMode = WindowMode.Fullscreen;
        public int ResolutionWidth = 1920;
        public int ResolutionHeight = 1080;
        public int FrameRateLimit = 60;      // -1 = uncapped
        public GraphicsQuality Quality = GraphicsQuality.High;
        public float RenderScale = 1.0f;     // 0.5 .. 2.0
        public float ViewDistance01 = 0.6f;  // 0 = near/fast .. 1 = far/detailed
        public AntiAliasingMode AntiAliasing = AntiAliasingMode.FXAA;
        public ShadowDetail Shadows = ShadowDetail.Medium;
        public TextureQuality Textures = TextureQuality.High;
        public EffectsQuality Effects = EffectsQuality.High;

        // Audio
        public float MasterVolume = 0.8f;   // 0..1, mapped to dB on the AudioMixer
        public float MusicVolume = 0.6f;
        public float SfxVolume = 0.8f;
        public float VoiceVolume = 0.8f;
        public Language AudioLanguage = Language.English;
        public Language SubtitleLanguage = Language.English;
        public bool SubtitlesEnabled = true;

        // Map
        public MapStyle Style = MapStyle.Satellite;
        public bool Enable3DBuildings = true;
        public bool Enable3DTerrain = true;
        public bool ShowMapPlaceLabels = true;
        /// <summary>Google Photorealistic 3D Tiles (Cesium ion's hosted asset, ID 2275207)
        /// — replaces (not layers with) World Terrain/OSM Buildings/raster imagery when on,
        /// since Google's tileset already bakes in terrain+buildings+imagery. Default off:
        /// it's a heavier stream than the classic terrain+buildings combo.</summary>
        public bool Enable3DTiles = false;

        // Controls
        public ControlScheme Scheme = ControlScheme.Keyboard;
        public float GyroSensitivity = 1.0f;
        public bool InvertPitch = false;
        public List<KeyBindingOverride> BindingOverrides = new List<KeyBindingOverride>();
    }

    [Serializable]
    public class KeyBindingOverride
    {
        public string ActionName;     // e.g. "Throttle"
        public int BindingIndex;
        public string OverridePath;   // e.g. "<Keyboard>/w"
    }
}
