using System;
using System.Collections.Generic;

namespace AeroTerra.Core
{
    public enum ControlScheme { Keyboard, KeyboardMouse, Gamepad, Gyroscope }
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
        public SkyPreset Sky = SkyPreset.Day;
        public WeatherPreset Weather = WeatherPreset.Clear;
        // Manual wind override — when enabled, WeatherSystem uses this fixed speed
        // (along its constant WindDirection) instead of the weather preset's own wind.
        public bool ManualWindEnabled = false;
        public float ManualWindSpeedMs = 5f;

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

        // Controls
        public ControlScheme Scheme = ControlScheme.KeyboardMouse;
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
