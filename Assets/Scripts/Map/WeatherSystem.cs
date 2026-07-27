using UnityEngine;
using AeroTerra.Core;
using AeroTerra.Procedural;

namespace AeroTerra.Map
{
    /// <summary>
    /// Clear / Cloudy / Rain / Storm / Fog / Snow. Drives fog density, particle
    /// precipitation, lightning flashes (storm), and a wind vector consumed by
    /// DroneFlightController for physically felt weather.
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }
        public Vector3 CurrentWind { get; private set; }

        private ParticleSystem _precip;
        private WeatherPreset _preset;
        private float _lightningTimer;
        private Vector3 _windBase;
        private float _effectsMultiplier = 1f;
        private float _lastRate, _lastMaxParticles = 6000;

        private void Awake() => Instance = this;

        private void Start()
        {
            BuildPrecipitation();
            ApplyEffectsQuality(GameManager.Instance.Settings.Effects);
            Apply(GameManager.Instance.Settings.Weather);
        }

        /// <summary>Scales precipitation particle density; called from Settings ▸ Video.</summary>
        public void ApplyEffectsQuality(EffectsQuality quality)
        {
            _effectsMultiplier = quality switch
            {
                EffectsQuality.Low => 0.35f,
                EffectsQuality.Medium => 0.7f,
                _ => 1f
            };
            if (_precip != null)
            {
                var main = _precip.main;
                main.maxParticles = Mathf.RoundToInt(6000 * _effectsMultiplier);
                var em = _precip.emission;
                em.rateOverTime = _lastRate * _effectsMultiplier;
            }
        }

        /// <summary>Steady-state wind vector for a weather preset, before the per-frame
        /// gust noise Update() layers on top. Shared with the Settings ▸ Game tab and the
        /// Free Flight Flying Conditions screen so their wind readout matches what the
        /// flight physics actually feel — wind is entirely weather-driven, never a
        /// separately editable setting.</summary>
        public static Vector3 BaseWindForPreset(WeatherPreset preset) => preset switch
        {
            WeatherPreset.Clear => new Vector3(0.5f, 0, 0.3f),
            WeatherPreset.Cloudy => new Vector3(2f, 0, 1f),
            WeatherPreset.Rain => new Vector3(4f, 0, 2f),
            WeatherPreset.Storm => new Vector3(9f, 0, 5f),
            WeatherPreset.Fog => new Vector3(0.8f, 0, 0.4f),
            WeatherPreset.Snow => new Vector3(1.5f, 0, 1f),
            _ => Vector3.zero,
        };

        public static float BaseWindSpeedMs(WeatherPreset preset) => BaseWindForPreset(preset).magnitude;

        /// <summary>Fixed prevailing-wind direction, shared by every weather preset's
        /// BaseWindForPreset (each just scales along it) and by the Settings ▸ Game tab's
        /// manual wind override — so a manually-set speed blows the same "world" direction
        /// weather-driven wind always has, rather than introducing a second, arbitrary axis.</summary>
        public static readonly Vector3 WindDirection = new Vector3(9f, 0f, 5f).normalized;

        public void Apply(WeatherPreset preset)
        {
            _preset = preset;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            _windBase = BaseWindForPreset(preset);

            switch (preset)
            {
                case WeatherPreset.Clear:
                    RenderSettings.fogDensity = 0.00015f;
                    SetPrecip(false); break;
                case WeatherPreset.Cloudy:
                    RenderSettings.fogDensity = 0.0007f;
                    SetPrecip(false); break;
                case WeatherPreset.Rain:
                    RenderSettings.fogDensity = 0.0018f;
                    SetPrecip(true, rate: 900, speed: 22f, size: 0.05f, stretch: true,
                              color: new Color(0.6f, 0.7f, 0.9f, 0.55f)); break;
                case WeatherPreset.Storm:
                    RenderSettings.fogDensity = 0.003f;
                    SetPrecip(true, rate: 1600, speed: 30f, size: 0.06f, stretch: true,
                              color: new Color(0.55f, 0.62f, 0.8f, 0.6f)); break;
                case WeatherPreset.Fog:
                    RenderSettings.fogDensity = 0.008f;
                    SetPrecip(false); break;
                case WeatherPreset.Snow:
                    RenderSettings.fogDensity = 0.0025f;
                    SetPrecip(true, rate: 700, speed: 3.5f, size: 0.09f, stretch: false,
                              color: Color.white); break;
            }
        }

        private void Update()
        {
            // Manual override (Settings ▸ Game): a fixed, steady speed along the same
            // prevailing WindDirection every weather preset uses — no gust noise, so a
            // manually-set value reads exactly as set rather than fluctuating. Read live
            // every frame (same pull pattern DroneFlightController already uses for
            // InvertPitch) so the Settings UI just needs to save, no explicit apply call.
            var settings = GameManager.Instance != null ? GameManager.Instance.Settings : null;
            if (settings != null && settings.ManualWindEnabled)
            {
                CurrentWind = WindDirection * settings.ManualWindSpeedMs;
            }
            else
            {
                // Gusting wind
                float gust = Mathf.PerlinNoise(Time.time * 0.3f, 0.7f) * (_preset == WeatherPreset.Storm ? 8f : 2f);
                CurrentWind = _windBase + new Vector3(gust, 0f, gust * 0.5f);
            }

            // Storm lightning
            if (_preset == WeatherPreset.Storm && SkySystem.Instance != null)
            {
                _lightningTimer -= Time.deltaTime;
                if (_lightningTimer <= 0f)
                {
                    StartCoroutine(LightningFlash());
                    _lightningTimer = Random.Range(4f, 12f);
                }
            }

            // Keep precipitation above the camera
            if (_precip != null && Camera.main != null)
                _precip.transform.position = Camera.main.transform.position + Vector3.up * 30f;
        }

        private System.Collections.IEnumerator LightningFlash()
        {
            var sun = SkySystem.Instance.Sun;
            float prev = sun.intensity;
            sun.intensity = prev + 2.5f;
            yield return new WaitForSeconds(0.08f);
            sun.intensity = prev;
            yield return new WaitForSeconds(0.06f);
            sun.intensity = prev + 1.5f;
            yield return new WaitForSeconds(0.05f);
            sun.intensity = prev;
        }

        private void BuildPrecipitation()
        {
            var go = new GameObject("Precipitation");
            go.transform.SetParent(transform, false);
            _precip = go.AddComponent<ParticleSystem>();
            var shape = _precip.shape; shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(120f, 1f, 120f);
            var main = _precip.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 6000;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = new Material(DroneMeshBuilder.TransparentShader());
        }

        private void SetPrecip(bool on, float rate = 0, float speed = 0, float size = 0.05f,
                               bool stretch = false, Color color = default)
        {
            if (_precip == null) return;
            _lastRate = rate;
            var em = _precip.emission; em.rateOverTime = on ? rate * _effectsMultiplier : 0f;
            if (!on) { _precip.Clear(); return; }
            var main = _precip.main;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.startLifetime = 4f;
            main.gravityModifier = stretch ? 1.5f : 0.15f;
            main.maxParticles = Mathf.RoundToInt(6000 * _effectsMultiplier);
            var r = _precip.GetComponent<ParticleSystemRenderer>();
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.velocityScale = stretch ? 0.06f : 0f;
            if (!_precip.isPlaying) _precip.Play();
        }
    }
}
