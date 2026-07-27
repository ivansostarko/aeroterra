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
                    Set(sunPitch: 55f, color: new Color(1f, 0.96f, 0.88f), intensity: 1.25f,
                        ambient: new Color(0.55f, 0.6f, 0.7f), fog: new Color(0.75f, 0.82f, 0.92f));
                    break;
                case SkyPreset.Dawn:
                    Set(10f, new Color(1f, 0.62f, 0.4f), 0.8f,
                        new Color(0.45f, 0.4f, 0.5f), new Color(0.95f, 0.7f, 0.55f));
                    break;
                case SkyPreset.Dusk:
                    Set(6f, new Color(1f, 0.5f, 0.35f), 0.65f,
                        new Color(0.4f, 0.33f, 0.45f), new Color(0.85f, 0.55f, 0.5f));
                    break;
                case SkyPreset.Night:
                    Set(-30f, new Color(0.55f, 0.65f, 0.95f), 0.18f,
                        new Color(0.08f, 0.1f, 0.18f), new Color(0.05f, 0.07f, 0.12f));
                    break;
            }
        }

        private void Set(float sunPitch, Color color, float intensity, Color ambient, Color fog)
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

            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.Skybox;
                Camera.main.backgroundColor = fog;
            }
        }
    }
}
