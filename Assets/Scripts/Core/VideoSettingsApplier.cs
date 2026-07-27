using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AeroTerra.Core
{
    /// <summary>
    /// Applies the Video tab of SettingsData to the engine. Called once at startup
    /// (GameManager) and again whenever the player changes a Video setting or hits
    /// Save in the Settings screen. Every setting maps to a plain Unity/URP API —
    /// there is no custom render pipeline asset in this project, so anything that
    /// needs the active URP asset (render scale, MSAA) resolves it at call time via
    /// GraphicsSettings rather than a serialized reference.
    /// </summary>
    public static class VideoSettingsApplier
    {
        public static void ApplyAll(SettingsData s)
        {
            ApplyWindow(s);
            ApplyNonWindow(s);
        }

        /// <summary>
        /// Runs once at game boot (GameManager.Awake). Deliberately skips ApplyWindow:
        /// forcing Screen.SetResolution/fullscreen-mode on every single launch caused a
        /// disruptive display-mode switch before the first scene even finished loading
        /// (visible as a broken/frozen-looking window in the Editor and in builds).
        /// Window/resolution changes only take effect when the player explicitly picks
        /// one in Settings ▸ Video (ApplyAll, above) or hits Save.
        /// </summary>
        public static void ApplyAtStartup(SettingsData s) => ApplyNonWindow(s);

        private static void ApplyNonWindow(SettingsData s)
        {
            ApplyFrameRateLimit(s);
            ApplyQuality(s);
            ApplyRenderScale(s);
            ApplyAntiAliasing(s);
            ApplyShadows(s);
            ApplyTextures(s);
            // Effects (particle density) and view distance (Cesium LOD) are applied by
            // WeatherSystem/MapManager themselves, since they only exist once a map/flight
            // is loaded — see WeatherSystem.ApplyEffectsQuality / MapManager.ApplyViewDistance.
        }

        public static void ApplyWindow(SettingsData s)
        {
            var mode = s.WindowMode == WindowMode.Fullscreen
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;
            Screen.SetResolution(s.ResolutionWidth, s.ResolutionHeight, mode);
        }

        public static void ApplyFrameRateLimit(SettingsData s)
        {
            Application.targetFrameRate = s.FrameRateLimit <= 0 ? -1 : s.FrameRateLimit;
        }

        public static void ApplyQuality(SettingsData s)
        {
            int index = Mathf.Clamp((int)s.Quality, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            QualitySettings.SetQualityLevel(index, applyExpensiveChanges: true);
        }

        public static void ApplyRenderScale(SettingsData s)
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.renderScale = Mathf.Clamp(s.RenderScale, 0.5f, 2.0f);
        }

        public static void ApplyAntiAliasing(SettingsData s)
        {
            if (Camera.main == null) return;
            var camData = Camera.main.GetUniversalAdditionalCameraData();
            if (camData == null) return;
            camData.antialiasing = s.AntiAliasing switch
            {
                AntiAliasingMode.FXAA => AntialiasingMode.FastApproximateAntialiasing,
                AntiAliasingMode.SMAA => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                AntiAliasingMode.TAA => AntialiasingMode.TemporalAntiAliasing,
                _ => AntialiasingMode.None
            };
        }

        public static void ApplyShadows(SettingsData s)
        {
            QualitySettings.shadows = s.Shadows == ShadowDetail.Off ? UnityEngine.ShadowQuality.Disable : UnityEngine.ShadowQuality.All;
            QualitySettings.shadowResolution = s.Shadows switch
            {
                ShadowDetail.Low => UnityEngine.ShadowResolution.Low,
                ShadowDetail.Medium => UnityEngine.ShadowResolution.Medium,
                ShadowDetail.High => UnityEngine.ShadowResolution.High,
                _ => UnityEngine.ShadowResolution.Low
            };
            if (Map.SkySystem.Instance != null && Map.SkySystem.Instance.Sun != null)
                Map.SkySystem.Instance.Sun.shadows = s.Shadows == ShadowDetail.Off ? LightShadows.None : LightShadows.Soft;
        }

        public static void ApplyTextures(SettingsData s)
        {
            // 0 = full resolution, higher values apply a mip bias (lower effective resolution).
            QualitySettings.globalTextureMipmapLimit = s.Textures switch
            {
                TextureQuality.Full => 0,
                TextureQuality.High => 0,
                TextureQuality.Medium => 1,
                _ => 2
            };
        }

        /// <summary>Distinct, sorted resolutions the current display actually supports.</summary>
        public static (int width, int height)[] AvailableResolutions()
        {
            return Screen.resolutions
                .Select(r => (r.width, r.height))
                .Distinct()
                .OrderByDescending(r => r.width * r.height)
                .ToArray();
        }
    }
}
