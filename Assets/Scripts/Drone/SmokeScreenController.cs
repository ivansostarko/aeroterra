using UnityEngine;

namespace AeroTerra.Drone
{
    /// <summary>Toggles the Workshop "Smoke Screen" loadout item's trailing particle
    /// effect on/off in flight via InputManager.SmokeScreenAction (keyboard U, gamepad
    /// South) — equipping it in the Workshop only makes the capability available; the
    /// effect starts stopped (see DroneFactory.BuildSmokeScreen's main.playOnAwake =
    /// false) and the pilot switches it on/off on demand, the same "equip vs. use"
    /// split PayloadDropper already has for its drop key.</summary>
    public class SmokeScreenController : MonoBehaviour
    {
        private ParticleSystem _smoke;
        private bool _active;

        public void Configure(ParticleSystem smoke) => _smoke = smoke;

        private void Update()
        {
            var im = AeroTerra.Input.InputManager.Instance;
            if (im == null || _smoke == null || !im.SmokeScreenAction.WasPressedThisFrame()) return;

            _active = !_active;
            if (_active) _smoke.Play();
            else _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting); // existing puffs fade out naturally
        }
    }
}
