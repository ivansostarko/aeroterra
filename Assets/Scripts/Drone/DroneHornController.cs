using UnityEngine;

namespace AeroTerra.Drone
{
    /// <summary>Sounds the Workshop "Horn" loadout item's warning horn in flight via
    /// InputManager.HornAction (keyboard H, gamepad dpad-right) — equipping it in the
    /// Workshop (or flying the stock manufacturer-default loadout, see
    /// DroneFactory.Spawn) only makes the capability available; the pilot presses H to
    /// sound it. A plain one-shot 3D clip at the drone's position each press — no
    /// toggle/hold state, same "one press, one honk" behavior a real vehicle horn has.</summary>
    public class DroneHornController : MonoBehaviour
    {
        private void Update()
        {
            var im = AeroTerra.Input.InputManager.Instance;
            if (im == null || im.HornAction == null || !im.HornAction.WasPressedThisFrame()) return;
            AeroTerra.Core.AudioManager.Instance?.PlayDroneHorn(transform.position);
        }
    }
}
