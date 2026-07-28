using UnityEngine;

namespace AeroTerra.Drone
{
    /// <summary>Fires the Workshop "Smoke Screen" loadout item's trailing particle
    /// effect in flight via InputManager.SmokeScreenAction (keyboard U, gamepad South)
    /// — equipping it in the Workshop (or flying the stock manufacturer-default
    /// loadout, see DroneFactory.Spawn) only makes the capability available; the
    /// effect starts stopped (see DroneFactory.BuildSmokeScreen's main.playOnAwake =
    /// false) and the pilot triggers it on demand. A press while inactive starts a
    /// fixed SmokeDurationSec burst that stops emitting and clears on its own —
    /// existing puffs finish their own particle lifetime rather than vanishing
    /// instantly — with no need to hold or re-press anything; a press while already
    /// active cancels it early instead, same "toggle" convenience the old behavior had.</summary>
    public class SmokeScreenController : MonoBehaviour
    {
        private const float SmokeDurationSec = 60f;

        private ParticleSystem _smoke;
        private bool _active;
        private float _remainingSec;

        public void Configure(ParticleSystem smoke) => _smoke = smoke;

        private void Update()
        {
            if (_smoke == null) return;

            var im = AeroTerra.Input.InputManager.Instance;
            if (im != null && im.SmokeScreenAction.WasPressedThisFrame())
            {
                if (_active) StopSmoke("SMOKE SCREEN OFF");
                else BeginSmoke();
            }

            if (!_active) return;
            _remainingSec -= Time.deltaTime;
            if (_remainingSec <= 0f) StopSmoke("SMOKE SCREEN DISSIPATED");
        }

        private void BeginSmoke()
        {
            _active = true;
            _remainingSec = SmokeDurationSec;
            _smoke.Play();
            AeroTerra.UI.FlightHUD.Instance?.ShowFlightMessage("SMOKE SCREEN ACTIVATED");
        }

        private void StopSmoke(string message)
        {
            _active = false;
            _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting); // existing puffs fade out naturally
            AeroTerra.UI.FlightHUD.Instance?.ShowFlightMessage(message);
        }
    }
}
