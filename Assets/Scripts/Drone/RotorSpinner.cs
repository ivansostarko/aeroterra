using UnityEngine;

namespace AeroTerra.Drone
{
    /// <summary>Spins rotor meshes proportionally to throttle, and fades in a
    /// translucent "BlurDisc" sibling at high RPM so the spin reads as a motion
    /// blur instead of always-distinct blades (see DroneMeshBuilder.Rotor).</summary>
    public class RotorSpinner : MonoBehaviour
    {
        public float MaxDegPerSec = 4000f;
        public int Direction = 1; // alternate per rotor for realism
        public Material BlurMaterial;

        /// <summary>Local axis the rotor spins around. Every procedural builder mounts
        /// its rotors flat (blade disc in the local XZ plane), so the default of
        /// Vector3.up is correct for all of them unchanged. ImportedDroneBuilder is the
        /// one caller that overrides this — an imported FBX can mount its propeller
        /// facing any direction (e.g. a nose/tail-mounted pusher prop spins around the
        /// fuselage's forward axis, not straight up), and there's no Editor available
        /// here to eyeball the right axis, so it's measured from the mesh's own local
        /// bounds instead of assumed.</summary>
        public Vector3 SpinAxis = Vector3.up;

        private DroneFlightController _flight;

        private void Start() => _flight = GetComponentInParent<DroneFlightController>();

        private void Update()
        {
            float t = _flight != null ? _flight.Throttle01 : 0.5f;
            transform.Rotate(SpinAxis, Direction * MaxDegPerSec * Mathf.Max(0.15f, t) * Time.deltaTime, Space.Self);

            if (BlurMaterial == null) return;
            float blurAlpha = Mathf.Clamp01((t - 0.35f) / 0.5f) * 0.55f;
            if (BlurMaterial.HasProperty("_Color"))
            {
                var c = BlurMaterial.color; c.a = blurAlpha; BlurMaterial.color = c;
            }
            if (BlurMaterial.HasProperty("_BaseColor"))
            {
                var c = BlurMaterial.GetColor("_BaseColor"); c.a = blurAlpha; BlurMaterial.SetColor("_BaseColor", c);
            }
        }
    }
}
