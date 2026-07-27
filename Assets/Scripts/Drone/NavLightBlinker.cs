using UnityEngine;

namespace AeroTerra.Drone
{
    /// <summary>
    /// Aviation-style blink patterns for the small emissive nav-light spheres
    /// (see DroneMeshBuilder.NavLight): white lights double-flash like an
    /// anti-collision strobe, red/green position lights pulse slowly. Purely
    /// cosmetic (toggles the renderer), works on flight and display models alike;
    /// a random phase offset keeps a fleet of lights from blinking in lockstep.
    /// </summary>
    public class NavLightBlinker : MonoBehaviour
    {
        /// <summary>True for white anti-collision strobes, false for steady-pulse
        /// red/green position lights.</summary>
        public bool Strobe;

        private Renderer _renderer;
        private float _phase;

        private void Start()
        {
            _renderer = GetComponent<Renderer>();
            _phase = Random.Range(0f, 10f);
        }

        private void Update()
        {
            if (_renderer == null) return;
            float t = (Time.time + _phase) % (Strobe ? 1.4f : 2.0f);
            _renderer.enabled = Strobe
                ? t < 0.07f || (t > 0.16f && t < 0.23f) // double-flash, long dark gap
                : t < 1.55f;                            // long on, short off pulse
        }
    }
}
