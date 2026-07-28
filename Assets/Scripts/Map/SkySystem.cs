using UnityEngine;
using AeroTerra.Core;

namespace AeroTerra.Map
{
    /// <summary>
    /// Day / Dawn / Dusk / Night presets: sun angle, light color & intensity,
    /// ambient light and fog color for a cohesive time-of-day look.
    /// </summary>
    public class SkySystem : MonoBehaviour
    {
        public static SkySystem Instance { get; private set; }
        public Light Sun;

        private Material _skyboxMat;
        private bool _skyboxShaderChecked;

        private void Awake() => Instance = this;

        private void Start()
        {
            if (Sun == null)
            {
                var go = new GameObject("Sun");
                Sun = go.AddComponent<Light>();
                Sun.type = LightType.Directional;
                Sun.shadows = LightShadows.Soft;
            }
            Apply(GameManager.Instance.Settings.Sky);
        }

        public void Apply(SkyPreset preset)
        {
            switch (preset)
            {
                case SkyPreset.Day:
                    Set(preset, sunPitch: 55f, color: new Color(1f, 0.96f, 0.88f), intensity: 1.25f,
                        ambient: new Color(0.55f, 0.6f, 0.7f), fog: new Color(0.75f, 0.82f, 0.92f));
                    break;
                case SkyPreset.Dawn:
                    Set(preset, 10f, new Color(1f, 0.62f, 0.4f), 0.8f,
                        new Color(0.45f, 0.4f, 0.5f), new Color(0.95f, 0.7f, 0.55f));
                    break;
                case SkyPreset.Dusk:
                    Set(preset, 6f, new Color(1f, 0.5f, 0.35f), 0.65f,
                        new Color(0.4f, 0.33f, 0.45f), new Color(0.85f, 0.55f, 0.5f));
                    break;
                case SkyPreset.Night:
                    Set(preset, -30f, new Color(0.55f, 0.65f, 0.95f), 0.18f,
                        new Color(0.08f, 0.1f, 0.18f), new Color(0.05f, 0.07f, 0.12f));
                    break;
            }
        }

        private void Set(SkyPreset preset, float sunPitch, Color color, float intensity, Color ambient, Color fog)
        {
            Sun.transform.rotation = Quaternion.Euler(sunPitch, -35f, 0f);
            Sun.color = color;
            Sun.intensity = intensity;

            // Drive the procedural skybox's sun disk & sky tint from our sun.
            RenderSettings.sun = Sun;

            // Trilight ambient reads far more naturally than flat: bright sky
            // above, mid tone at the horizon, darker bounce from the ground.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambient * 1.15f;
            RenderSettings.ambientEquatorColor = ambient;
            RenderSettings.ambientGroundColor = ambient * 0.55f;

            // Gentle exponential haze = aerial perspective; distant city fades
            // into the horizon instead of popping against the skybox.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.00012f;
            RenderSettings.fogColor = fog;

            ApplyProceduralSkybox(preset);

            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.Skybox;
                Camera.main.backgroundColor = fog;
            }
        }

        /// <summary>Actually drives the visible sky dome. RenderSettings.skybox was
        /// never assigned anywhere in this project before this fix — no matter which
        /// preset was picked, the sky itself always rendered as whatever Unity's scene
        /// default happened to be (or a flat fog-colored background if none was set at
        /// all), which is exactly why picking Dawn/Dusk/Night from Settings ▸ Flying
        /// Conditions or the Free Flight conditions screen visibly did nothing: only the
        /// ground/building lighting subtly shifted, never the sky you're actually
        /// looking at. Unity's built-in "Skybox/Procedural" shader (still shipped and
        /// usable under URP — it's a simple analytic sky dome, not tied to the Built-in
        /// Render Pipeline) reads RenderSettings.sun for the sun disk's position/color
        /// automatically; this drives its tint/ground/atmosphere/exposure properties per
        /// preset so the dome itself now visibly differs too — most importantly Night's
        /// much lower Exposure, since the shader has no other way to actually darken the
        /// sky after dark. Gracefully no-ops (falls back to the plain Camera.
        /// backgroundColor fill above) if the shader isn't available — e.g. a build that
        /// stripped it for never being referenced by a scene-authored material; add
        /// "Skybox/Procedural" to Project Settings ▸ Graphics ▸ Always Included Shaders
        /// if that happens.</summary>
        private void ApplyProceduralSkybox(SkyPreset preset)
        {
            if (!_skyboxShaderChecked)
            {
                _skyboxShaderChecked = true;
                var shader = Shader.Find("Skybox/Procedural");
                if (shader != null) _skyboxMat = new Material(shader);
            }
            if (_skyboxMat == null) return;
            RenderSettings.skybox = _skyboxMat;

            (Color skyTint, Color groundColor, float atmosphere, float exposure) = preset switch
            {
                SkyPreset.Day => (new Color(0.55f, 0.65f, 0.85f), new Color(0.35f, 0.38f, 0.42f), 1.0f, 1.3f),
                SkyPreset.Dawn => (new Color(0.85f, 0.55f, 0.4f), new Color(0.3f, 0.24f, 0.22f), 2.2f, 1.0f),
                SkyPreset.Dusk => (new Color(0.75f, 0.4f, 0.35f), new Color(0.22f, 0.16f, 0.18f), 2.6f, 0.85f),
                SkyPreset.Night => (new Color(0.04f, 0.05f, 0.10f), new Color(0.02f, 0.02f, 0.03f), 0.4f, 0.2f),
                _ => (new Color(0.5f, 0.5f, 0.5f), new Color(0.3f, 0.3f, 0.3f), 1f, 1f),
            };

            _skyboxMat.SetFloat("_SunDisk", 2f); // High Quality — a visible sun disk that tracks RenderSettings.sun
            _skyboxMat.SetFloat("_SunSize", 0.04f);
            _skyboxMat.SetFloat("_SunSizeConvergence", 5f);
            _skyboxMat.SetFloat("_AtmosphereThickness", atmosphere);
            _skyboxMat.SetColor("_SkyTint", skyTint);
            _skyboxMat.SetColor("_GroundColor", groundColor);
            _skyboxMat.SetFloat("_Exposure", exposure);

            // The baked ambient probe otherwise stays stuck on whatever the skybox
            // looked like at scene start, mismatching this preset's lighting.
            DynamicGI.UpdateEnvironment();
        }
    }
}
