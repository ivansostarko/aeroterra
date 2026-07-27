using UnityEngine;
using AeroTerra.Core;
using AeroTerra.Procedural;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// Entry point of the Flight scene: builds the Cesium world (MapManager),
    /// sky & weather, spawns the selected drone at spawn altitude, attaches the
    /// chase camera, HUD, pause menu, and instant-replay/screenshot capture.
    /// </summary>
    public class FlightSceneController : MonoBehaviour
    {
        private GameObject _drone;
        private AeroTerra.Drone.DroneFlightController _flightController;
        private FlightHUD _hud;
        private bool _paused;
        private bool _gameOverShown;
        private RectTransform _pausePanel;
        private Canvas _canvas;
        private SettingsUI _settingsUI;
        private NarratorController _narrator;
        private InstantReplayController _replay;

        private void Start()
        {
            var gm = GameManager.Instance;
            AudioManager.Instance?.StopMenuMusic();
            AudioManager.Instance?.PlayWeatherAmbience(gm.Settings.Weather);
            CustomCursor.Reset();

            // World systems
            var world = new GameObject("World");
            world.AddComponent<Map.MapManager>();
            world.AddComponent<Map.SkySystem>();
            world.AddComponent<Map.WeatherSystem>();

            // Camera
            if (Camera.main == null)
            {
                var camGo = new GameObject("MainCamera") { tag = "MainCamera" };
                camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            // Default far clip (1000 m) truncates the streamed city — from 150 m
            // altitude the visible horizon is tens of kilometers away.
            Camera.main.farClipPlane = 30000f;
            Camera.main.nearClipPlane = 0.3f;

            // Drone at spawn altitude above the georeference origin. Falls back to the
            // stock Pelican if the scene was entered without going through Free Flight
            // (e.g. pressing Play with the Flight scene open directly in the Editor) —
            // mirrors the SelectedMap ?? London fallback just above.
            float alt = (float)(gm.SelectedSpawnAltitudeOverride ?? gm.SelectedMap?.SpawnAltitudeMeters ?? 150);
            float heading = gm.SelectedMap?.SpawnHeadingDeg ?? 0f;
            var droneSpec = gm.SelectedDrone != null
                ? gm.SelectedDrone
                : Resources.Load<AeroTerra.Drone.DroneSpecification>("Drones/AT-C1_Pelican");
            _drone = DroneFactory.Spawn(droneSpec, gm.SelectedCustomConfig,
                                        new Vector3(0, alt, 0), flyable: true, out _, out _);
            _drone.transform.rotation = Quaternion.Euler(0, heading, 0);
            _flightController = _drone.GetComponent<AeroTerra.Drone.DroneFlightController>();

            var camRig = Camera.main.gameObject.AddComponent<DroneCameraRig>();
            camRig.Target = _drone.transform;

            // HUD + mobile overlay
            _canvas = RootCanvas("FlightCanvas");
            _hud = gameObject.AddComponent<FlightHUD>();
            _hud.Init(_canvas, _flightController);
#if UNITY_ANDROID || UNITY_IOS
            gameObject.AddComponent<TouchOverlay>().Init(_canvas);
#endif

            _narrator = gameObject.AddComponent<NarratorController>();
            _narrator.Init(_canvas, _flightController);

            _replay = gameObject.AddComponent<InstantReplayController>();
            _replay.Init(_flightController, camRig, Camera.main, _canvas);
        }

        private void Update()
        {
            if (!_gameOverShown && _flightController != null && _flightController.JustCrashedFromDeadBattery)
            {
                ShowBatteryDeadModal();
                return;
            }
            if (_gameOverShown) return;

            var im = AeroTerra.Input.InputManager.Instance;
            if (im == null) return;
            // While Settings is open (opened from the pause menu below), it owns Escape
            // itself and steps back to the pause menu — don't also toggle pause here.
            bool settingsOpen = _settingsUI != null && _settingsUI.IsOpen;
            if (!settingsOpen && im.PauseAction.WasPressedThisFrame()) TogglePause();
            if (!_paused && im.ResetAction.WasPressedThisFrame()) ResetDrone();
        }

        private void ResetDrone()
        {
            var map = GameManager.Instance.SelectedMap;
            float alt = (float)(map?.SpawnAltitudeMeters ?? 150);
            float heading = map?.SpawnHeadingDeg ?? 0f;
            var rb = _drone.GetComponent<Rigidbody>();
            rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            _drone.transform.SetPositionAndRotation(new Vector3(0, alt, 0), Quaternion.Euler(0, heading, 0));
            // Re-launch cleanly: multirotors re-enter hover, fixed wings get their
            // cruise-speed hand-launch back (they can't recover from a dead stop).
            _flightController?.OnRespawn();
            _narrator?.NotifyRespawned();
        }

        private void TogglePause()
        {
            _paused = !_paused;
            Time.timeScale = _paused ? 0f : 1f;
            // Global mute while paused — engine loop, weather ambience and music all go
            // silent; the pause menu's own button click/hover sfx opts out of this via
            // AudioSource.ignoreListenerPause (see AudioManager.EnsureUiSfxLoaded), so
            // menu navigation still has audio feedback.
            AudioListener.pause = _paused;
            if (_paused) { ShowPauseMenu(); CustomCursor.Apply(); }
            else { if (_pausePanel != null) Destroy(_pausePanel.gameObject); CustomCursor.Reset(); }
        }

        /// <summary>End-of-flight modal: the drone ran out of power and touched down.
        /// Freezes gameplay and returns to the Main Menu on OK — no HUD/pause interplay
        /// needed since _gameOverShown gates Update() from here on.</summary>
        private void ShowBatteryDeadModal()
        {
            _gameOverShown = true;
            Time.timeScale = 0f;
            CustomCursor.Apply();

            bool fuelPowered = _flightController != null &&
                               _flightController.Spec.PowerSystem == AeroTerra.Drone.PowerSystemType.Fuel;

            var overlay = Panel_(_canvas.transform, "GameOver", new Color(0, 0, 0, 0.8f), Vector2.zero, Vector2.one);
            var box = Panel_(overlay, "Box", Panel, new Vector2(0.30f, 0.32f), new Vector2(0.70f, 0.68f));

            Label(box, fuelPowered ? "FUEL DEPLETED" : "BATTERY DEPLETED", 32,
                  new Vector2(0.05f, 0.66f), new Vector2(0.95f, 0.85f),
                  AccentWarn, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            Label(box, fuelPowered ? "Your drone ran out of fuel and went down."
                                   : "Your drone ran out of power and went down.", 18,
                  new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.62f),
                  TextDim, TMPro.TextAlignmentOptions.Center);

            Button_(box, "OK", new Vector2(0.32f, 0.14f), new Vector2(0.68f, 0.32f),
                    () => { Time.timeScale = 1f; GameManager.Instance.ReturnToMenu(); }, Accent, 22);
        }

        private void ShowPauseMenu()
        {
            _pausePanel = Panel_(_canvas.transform, "Pause", new Color(0, 0, 0, 0.75f), Vector2.zero, Vector2.one);
            var box = Panel_(_pausePanel, "Box", Panel, new Vector2(0.34f, 0.22f), new Vector2(0.66f, 0.78f));

            Label(box, "PAUSED", 46, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.95f),
                  TextMain, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

            Button_(box, "RESUME", new Vector2(0.10f, 0.635f), new Vector2(0.90f, 0.75f), TogglePause, Accent, 24);
            Button_(box, "SETTINGS", new Vector2(0.10f, 0.495f), new Vector2(0.90f, 0.61f), () =>
            {
                _settingsUI = gameObject.GetComponent<SettingsUI>() ?? gameObject.AddComponent<SettingsUI>();
                _pausePanel.gameObject.SetActive(false);
                _settingsUI.Open(() => _pausePanel.gameObject.SetActive(true));
            }, PanelAlt, 24);
            Button_(box, "RESTART", new Vector2(0.10f, 0.355f), new Vector2(0.90f, 0.47f), () =>
            {
                TogglePause(); // unpauses, tears down this panel, restores the cursor
                ResetDrone(); // teleports back to the map's default spawn point/heading
            }, PanelAlt, 24);
            Button_(box, "MAIN MENU", new Vector2(0.10f, 0.215f), new Vector2(0.90f, 0.33f), () =>
            {
                Time.timeScale = 1f;
                AudioListener.pause = false; // this bypasses TogglePause(), which would otherwise do it
                GameManager.Instance.ReturnToMenu();
            }, AccentWarn, 24);
        }
    }
}
