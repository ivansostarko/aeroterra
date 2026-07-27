using UnityEngine;
using AeroTerra.Core;

namespace AeroTerra.Drone
{
    /// <summary>
    /// Plays the drone's unique engine loop; pitch and volume follow throttle
    /// so every drone has a distinct, dynamic sound signature.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class DroneAudioController : MonoBehaviour
    {
        private AudioSource _src;
        private DroneFlightController _flight;

        private void Awake()
        {
            _src = GetComponent<AudioSource>();
            _flight = GetComponent<DroneFlightController>();
            _src.loop = true;
            _src.spatialBlend = 1f;
            _src.dopplerLevel = 0.6f;
            _src.minDistance = 4f;
            _src.maxDistance = 220f;
        }

        private void Start()
        {
            var spec = _flight.Spec;
            _src.clip = spec.EngineLoop != null ? spec.EngineLoop : ProceduralEngineClip(spec);
            _src.Play();
            if (Core.AudioManager.Instance != null && Core.AudioManager.Instance.Mixer != null)
                _src.outputAudioMixerGroup = Core.AudioManager.Instance.Mixer.FindMatchingGroups("Master")[0];
        }

        private void Update()
        {
            if (_flight == null || _src.clip == null) return;
            var spec = _flight.Spec;
            float t = _flight.IsPowerEmpty ? 0f : _flight.Throttle01;
            if (_flight.Boosting) t = Mathf.Min(1f, t + 0.25f); // audible spool-up under boost
            _src.pitch = Mathf.Lerp(spec.EnginePitchMin, spec.EnginePitchMax, t);
            float sfx = Core.AudioManager.Instance != null ? Core.AudioManager.Instance.SfxVolume01 : 1f;
            _src.volume = Mathf.Lerp(0.15f, spec.EngineVolumeMax, t) * sfx;
        }

        /// <summary>
        /// Fallback: synthesize a rotor hum unique to the drone (seeded by Id) so
        /// every drone sounds different even before real recordings are imported.
        /// </summary>
        private AudioClip ProceduralEngineClip(DroneSpecification spec)
        {
            int seed = spec.Id != null ? spec.Id.GetHashCode() : 1234;
            var rnd = new System.Random(seed);
            int sampleRate = 44100;
            float seconds = 2f;
            int n = (int)(sampleRate * seconds);
            float baseHz = 55f + (float)rnd.NextDouble() * 60f;        // fundamental differs per drone
            float bladeHz = spec.RotorCount * (10f + (float)rnd.NextDouble() * 8f);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float time = i / (float)sampleRate;
                float s =
                    0.45f * Mathf.Sin(2 * Mathf.PI * baseHz * time) +
                    0.25f * Mathf.Sin(2 * Mathf.PI * baseHz * 2f * time) +
                    0.15f * Mathf.Sin(2 * Mathf.PI * bladeHz * time) +
                    0.10f * ((float)rnd.NextDouble() * 2f - 1f);       // rotor wash noise
                data[i] = s * 0.5f;
            }
            var clip = AudioClip.Create($"engine_{spec.Id}", n, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
