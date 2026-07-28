using UnityEngine;
using UnityEngine.InputSystem;
using AeroTerra.Core;

namespace AeroTerra.Input
{
    public struct FlightAxes
    {
        public float Throttle; // -1..1 (up/down)
        public float Pitch;    // -1..1 (forward/back)
        public float Roll;     // -1..1 (left/right)
        public float Yaw;      // -1..1 (rotate)
    }

    /// <summary>
    /// Unified input layer implementing all four control schemes.
    /// Built on the Unity Input System with runtime-generated actions so the
    /// project compiles without a .inputactions asset; ControlsTab rebinds
    /// these actions and overrides persist through SettingsData.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        public InputAction ThrottleAction { get; private set; }
        public InputAction PitchAction { get; private set; }
        public InputAction RollAction { get; private set; }
        public InputAction YawAction { get; private set; }
        public InputAction PauseAction { get; private set; }
        public InputAction CameraAction { get; private set; }
        public InputAction ResetAction { get; private set; }
        public InputAction PayloadDropAction { get; private set; }
        public InputAction BoostAction { get; private set; }
        public InputAction BrakeAction { get; private set; }
        public InputAction SmokeScreenAction { get; private set; }
        public InputAction ScreenshotAction { get; private set; }
        public InputAction ReplayAction { get; private set; }
        public InputAction PhotoModeAction { get; private set; }
        public InputAction DroneFlipAction { get; private set; }
        public InputAction ParachuteAction { get; private set; }

        /// <summary>Held-state helpers for the flight model (boost = sprint,
        /// brake = airbrake / hover-hold depending on airframe).</summary>
        public bool BoostHeld => BoostAction != null && BoostAction.IsPressed();
        public bool BrakeHeld => BrakeAction != null && BrakeAction.IsPressed();

        private bool _gyroEnabled;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildActions();
            ApplyScheme(GameManager.Instance != null
                ? GameManager.Instance.Settings.Scheme : ControlScheme.Keyboard);
        }

        private void BuildActions()
        {
            // Throttle: Up/Down arrows, gamepad left stick Y
            ThrottleAction = new InputAction("Throttle", InputActionType.Value);
            ThrottleAction.AddCompositeBinding("1DAxis")
                .With("Positive", "<Keyboard>/upArrow").With("Negative", "<Keyboard>/downArrow");
            ThrottleAction.AddBinding("<Gamepad>/leftStick/y");

            // Pitch ("fly forward/back"): W/S. Gamepad right stick Y, mouse Y
            // (scheme-gated in ReadFlightAxes) unchanged.
            PitchAction = new InputAction("Pitch", InputActionType.Value);
            PitchAction.AddCompositeBinding("1DAxis")
                .With("Positive", "<Keyboard>/w").With("Negative", "<Keyboard>/s");
            PitchAction.AddBinding("<Gamepad>/rightStick/y");

            // Roll ("fly left/right"): A/D. Gamepad right stick X unchanged.
            RollAction = new InputAction("Roll", InputActionType.Value);
            RollAction.AddCompositeBinding("1DAxis")
                .With("Positive", "<Keyboard>/d").With("Negative", "<Keyboard>/a");
            RollAction.AddBinding("<Gamepad>/rightStick/x");

            // Yaw: gamepad-only now — this control scheme has no keyboard left/right
            // yaw at all (removed per design). Multirotor/VTOL heading rotation is only
            // player-commandable on gamepad as a result; fixed-wing turning is
            // unaffected (it turns via bank-to-turn coordination, not yaw — see
            // DroneFlightController.TickFixedWing). Kept as a real InputAction (not
            // deleted) so gamepad/AllActions()/ReadFlightAxes don't need special-casing.
            YawAction = new InputAction("Yaw", InputActionType.Value);
            YawAction.AddBinding("<Gamepad>/leftStick/x");

            PauseAction = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            PauseAction.AddBinding("<Keyboard>/p");
            PauseAction.AddBinding("<Gamepad>/start");

            CameraAction = new InputAction("Camera", InputActionType.Button, "<Keyboard>/c");
            CameraAction.AddBinding("<Gamepad>/buttonNorth");

            ResetAction = new InputAction("Reset", InputActionType.Button, "<Keyboard>/r");
            ResetAction.AddBinding("<Gamepad>/buttonEast");

            PayloadDropAction = new InputAction("PayloadDrop", InputActionType.Button, "<Keyboard>/i");
            PayloadDropAction.AddBinding("<Gamepad>/buttonWest");

            // Boost: temporary extra thrust/speed (multirotor sprint, fixed-wing full power)
            BoostAction = new InputAction("Boost", InputActionType.Button, "<Keyboard>/leftShift");
            BoostAction.AddBinding("<Gamepad>/rightTrigger");

            // Brake: multirotor position-hold hard stop, fixed-wing airbrake
            BrakeAction = new InputAction("Brake", InputActionType.Button, "<Keyboard>/space");
            BrakeAction.AddBinding("<Gamepad>/leftTrigger");

            // Smoke screen: toggles the Workshop loadout item's trailing smoke on/off
            // in flight (equipping it just makes the capability available — see
            // SmokeScreenController). Gamepad South is the one face button not already
            // claimed (Camera=North, Reset=East, PayloadDrop=West).
            SmokeScreenAction = new InputAction("SmokeScreen", InputActionType.Button, "<Keyboard>/u");
            SmokeScreenAction.AddBinding("<Gamepad>/buttonSouth");

            // Screenshot / Instant Replay: not part of any control scheme's flight
            // model, always available regardless of scheme, same as Pause/Camera/Reset.
            ScreenshotAction = new InputAction("Screenshot", InputActionType.Button, "<Keyboard>/f9");
            ScreenshotAction.AddBinding("<Gamepad>/select");

            ReplayAction = new InputAction("Replay", InputActionType.Button, "<Keyboard>/f10");
            ReplayAction.AddBinding("<Gamepad>/dpad/up");

            // Photo mode: detached free-fly camera (see DroneCameraRig.CamMode.Photo) —
            // F8, not part of the C-cycle so it can't be accidentally cycled into
            // mid-flight; dpad/down is the one face of the pad Replay's dpad/up hasn't
            // claimed.
            PhotoModeAction = new InputAction("PhotoMode", InputActionType.Button, "<Keyboard>/f8");
            PhotoModeAction.AddBinding("<Gamepad>/dpad/down");

            // Drone flip: a scripted barrel-roll trick, momentary press — not held.
            // Right shoulder is the one gamepad button not already claimed (triggers
            // are Boost/Brake, face buttons are Camera/Reset/PayloadDrop/SmokeScreen).
            DroneFlipAction = new InputAction("DroneFlip", InputActionType.Button, "<Keyboard>/b");
            DroneFlipAction.AddBinding("<Gamepad>/rightShoulder");

            // Parachute: momentary press, deploys if equipped in the Workshop and above
            // the safe-deploy altitude (see DroneFlightController.DeployParachute).
            // Gamepad dpad/left is the one dpad face not already claimed (up=Replay,
            // down=PhotoMode).
            ParachuteAction = new InputAction("Parachute", InputActionType.Button, "<Keyboard>/g");
            ParachuteAction.AddBinding("<Gamepad>/dpad/left");

            foreach (var a in AllActions()) a.Enable();
            ApplySavedOverrides();
        }

        public InputAction[] AllActions() => new[]
            { ThrottleAction, PitchAction, RollAction, YawAction, PauseAction, CameraAction, ResetAction, PayloadDropAction, BoostAction, BrakeAction, SmokeScreenAction, ScreenshotAction, ReplayAction, PhotoModeAction, DroneFlipAction, ParachuteAction };

        public void ApplyScheme(ControlScheme scheme)
        {
            _gyroEnabled = scheme == ControlScheme.Gyroscope;
#if UNITY_ANDROID || UNITY_IOS
            if (UnityEngine.InputSystem.Gyroscope.current != null)
            {
                if (_gyroEnabled) InputSystem.EnableDevice(UnityEngine.InputSystem.Gyroscope.current);
                else InputSystem.DisableDevice(UnityEngine.InputSystem.Gyroscope.current);
            }
            if (AttitudeSensor.current != null && _gyroEnabled)
                InputSystem.EnableDevice(AttitudeSensor.current);
#endif
        }

        public FlightAxes ReadFlightAxes()
        {
            var scheme = GameManager.Instance.Settings.Scheme;
            var axes = new FlightAxes
            {
                Throttle = ThrottleAction.ReadValue<float>(),
                Pitch = PitchAction.ReadValue<float>(),
                Roll = RollAction.ReadValue<float>(),
                Yaw = YawAction.ReadValue<float>()
            };

            switch (scheme)
            {
                case ControlScheme.Gyroscope:
                    float sens = GameManager.Instance.Settings.GyroSensitivity;
#if UNITY_ANDROID || UNITY_IOS
                    if (UnityEngine.InputSystem.Gyroscope.current != null)
                    {
                        Vector3 rate = UnityEngine.InputSystem.Gyroscope.current.angularVelocity.ReadValue();
                        axes.Pitch = Mathf.Clamp(-rate.x * sens, -1f, 1f);
                        axes.Roll  = Mathf.Clamp( rate.y * sens, -1f, 1f);
                    }
#else
                    // Desktop fallback so the scheme is still testable in editor
                    if (Mouse.current != null)
                    {
                        Vector2 d = Mouse.current.delta.ReadValue() * 0.02f * sens;
                        axes.Roll = Mathf.Clamp(axes.Roll + d.x, -1f, 1f);
                        axes.Pitch = Mathf.Clamp(axes.Pitch + d.y, -1f, 1f);
                    }
#endif
                    break;
            }
            return axes;
        }

        // ---- Rebinding ------------------------------------------------------
        public void ApplySavedOverrides()
        {
            if (GameManager.Instance == null) return;
            foreach (var o in GameManager.Instance.Settings.BindingOverrides)
            {
                // Pause is never rebindable (see SettingsUI) — skip any override saved
                // before that rule existed so Escape can't be stuck bound elsewhere.
                if (o.ActionName == PauseAction.name) continue;
                var action = System.Array.Find(AllActions(), a => a.name == o.ActionName);
                if (action != null && o.BindingIndex < action.bindings.Count)
                    action.ApplyBindingOverride(o.BindingIndex, o.OverridePath);
            }
        }

        public void StoreOverride(InputAction action, int bindingIndex)
        {
            var s = GameManager.Instance.Settings;
            string path = action.bindings[bindingIndex].overridePath;
            s.BindingOverrides.RemoveAll(o => o.ActionName == action.name && o.BindingIndex == bindingIndex);
            if (!string.IsNullOrEmpty(path))
                s.BindingOverrides.Add(new KeyBindingOverride
                    { ActionName = action.name, BindingIndex = bindingIndex, OverridePath = path });
            GameManager.Instance.SaveSettings();
        }
    }
}
