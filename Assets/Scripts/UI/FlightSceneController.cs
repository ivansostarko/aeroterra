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
        private FlightLogTracker _flightLog;
        private DroneCameraRig _camRig;
        private GameConsoleUI _console;

        // Crash sequence — see OnDroneCrashed/ShowCrashCta/HideCrashCtaAndRespawn.
        private bool _crashCtaVisible;
        private RectTransform _crashCtaPanel;
        private TMPro.TextMeshProUGUI _crashCtaPrompt;

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

            // Drone at spawn altitude above the georeference origin — or, if the player
            // picked a SPAWN LOCATION preset on the Flying Conditions screen, above the
            // flat-earth offset from that origin (GameManager.SpawnLocalPosition). Falls
            // back to the stock Pelican if the scene was entered without going through
            // Free Flight (e.g. pressing Play with the Flight scene open directly in the
            // Editor) — mirrors the SelectedMap ?? London fallback just above.
            float heading = gm.SelectedMap?.SpawnHeadingDeg ?? 0f;
            var droneSpec = gm.SelectedDrone != null
                ? gm.SelectedDrone
                : Resources.Load<AeroTerra.Drone.DroneSpecification>("Drones/AT-C1_Pelican");
            _drone = DroneFactory.Spawn(droneSpec, gm.SelectedCustomConfig,
                                        gm.SpawnLocalPosition, flyable: true, out _, out _);
            _drone.transform.rotation = Quaternion.Euler(0, heading, 0);
            _flightController = _drone.GetComponent<AeroTerra.Drone.DroneFlightController>();
            _flightController.Crashed += OnDroneCrashed;

            if (gm.Settings.ShowOperatorArea) SpawnOperatorArea(gm, droneSpec, heading);

            _camRig = Camera.main.gameObject.AddComponent<DroneCameraRig>();
            _camRig.Target = _drone.transform;

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
            _replay.Init(_flightController, _camRig, Camera.main, _canvas);

            _flightLog = gameObject.AddComponent<FlightLogTracker>();
            _flightLog.Init(_flightController);

            _console = gameObject.AddComponent<GameConsoleUI>();
            _console.Init(_canvas, _flightController);
        }

        /// <summary>Settings ▸ Game ▸ "Preview operator area" (default on): a procedural
        /// operator figure standing at the flight's ground spawn point (same X/Z as the
        /// drone, sea-level Y instead of spawn altitude), plus a boundary-circle graphic
        /// at the drone's actual max range — the live BatterySystem/FuelSystem capacity
        /// this build was actually configured with (falls back to the spec's own max if
        /// somehow neither resolved), not just the spec's theoretical maximum. Purely a
        /// visual reference — nothing here clamps the drone to the circle.</summary>
        private void SpawnOperatorArea(GameManager gm, AeroTerra.Drone.DroneSpecification droneSpec, float heading)
        {
            Vector3 spawnPos = gm.SpawnLocalPosition;
            Vector3 groundPos = new Vector3(spawnPos.x, 0f, spawnPos.z);

            float rangeKm;
            if (droneSpec.PowerSystem == AeroTerra.Drone.PowerSystemType.Fuel)
            {
                float capL = _flightController.Fuel != null ? _flightController.Fuel.CapacityL : droneSpec.MaxFuelL;
                rangeKm = droneSpec.FuelRangeKm(capL);
            }
            else
            {
                float capWh = _flightController.Battery != null ? _flightController.Battery.CapacityWh : droneSpec.MaxBatteryWh;
                rangeKm = droneSpec.RangeKm(capWh);
            }
            float radiusM = Mathf.Max(50f, rangeKm * 1000f);

            var operatorGo = DroneOperatorBuilder.BuildOperator(groundPos, heading);
            operatorGo.GetComponent<AeroTerra.Drone.DroneOperatorAnimator>().Target = _drone.transform;
            DroneOperatorBuilder.BuildBoundaryCircle(groundPos, radiusM);
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

            // Post-crash prompt owns Space while it's up — Brake is otherwise the
            // in-flight action bound to that key, irrelevant to a drone that's already
            // down, so reusing it here needs no new binding.
            if (_crashCtaVisible)
            {
                if (im.BrakeAction != null && im.BrakeAction.WasPressedThisFrame()) HideCrashCtaAndRespawn();
                return;
            }

            // While Settings is open (opened from the pause menu below), it owns Escape
            // itself and steps back to the pause menu — don't also toggle pause here.
            bool settingsOpen = _settingsUI != null && _settingsUI.IsOpen;
            if (!settingsOpen && im.PauseAction.WasPressedThisFrame()) TogglePause();
            if (!_paused && im.ResetAction.WasPressedThisFrame()) ResetDrone();
        }

        private void ResetDrone()
        {
            // Manual R-triggered reset can happen while the crash CTA is still up (the
            // player didn't wait for it) — tear it down the same way Space would so it
            // doesn't linger on screen after a reset it didn't itself trigger.
            if (_crashCtaVisible) HideCrashCta();

            var gm = GameManager.Instance;
            float heading = gm.SelectedMap?.SpawnHeadingDeg ?? 0f;
            var rb = _drone.GetComponent<Rigidbody>();
            rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            _drone.transform.SetPositionAndRotation(gm.SpawnLocalPosition, Quaternion.Euler(0, heading, 0));
            // Re-launch cleanly: multirotors re-enter hover, fixed wings get their
            // cruise-speed hand-launch back (they can't recover from a dead stop).
            _flightController?.OnRespawn();
            _narrator?.NotifyRespawned();
            _camRig?.EndCrashSequence();
        }

        /// <summary>DroneFlightController.Crashed handler — starts the cinematic camera
        /// pull-back immediately, then shows the PRESS SPACE TO RESTART prompt after a
        /// short beat so the initial blast reads clearly before UI covers the screen.
        /// Doesn't freeze Time.timeScale: the fire keeps burning, smoke keeps drifting
        /// and the camera keeps easing outward while the player decides when to
        /// restart, rather than the world going static under the prompt.</summary>
        private void OnDroneCrashed(Vector3 point)
        {
            _camRig?.PlayCrashSequence(point);
            StartCoroutine(ShowCrashCtaAfterDelay(1.2f));
        }

        private System.Collections.IEnumerator ShowCrashCtaAfterDelay(float delaySec)
        {
            yield return new WaitForSeconds(delaySec);
            ShowCrashCta();
        }

        private void ShowCrashCta()
        {
            _crashCtaVisible = true;
            _crashCtaPanel = Panel_(_canvas.transform, "CrashCta", Color.clear, Vector2.zero, Vector2.one);

            var box = Panel_(_crashCtaPanel, "Box", new Color(0.05f, 0.03f, 0.02f, 0.72f),
                             new Vector2(0.30f, 0.40f), new Vector2(0.70f, 0.60f));
            Panel_(box, "TopStripe", AccentWarn, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -3), new Vector2(0, 0));

            Label(box, "DRONE CRASHED", 24, new Vector2(0.05f, 0.56f), new Vector2(0.95f, 0.86f),
                  AccentWarn, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            _crashCtaPrompt = Label(box, "PRESS  SPACE  TO  RESTART", 19, new Vector2(0.05f, 0.20f), new Vector2(0.95f, 0.50f),
                                    TextMain, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            Label(box, "You'll respawn at the flight's spawn point.", 12, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.18f),
                  TextDim, TMPro.TextAlignmentOptions.Center);

            StartCoroutine(PulseCrashCtaPrompt());
        }

        /// <summary>Gentle attention-drawing alpha "breathing" on the restart line —
        /// stops on its own once _crashCtaPrompt is torn down (HideCrashCta destroys the
        /// whole panel, so the null check ends the coroutine next tick).</summary>
        private System.Collections.IEnumerator PulseCrashCtaPrompt()
        {
            while (_crashCtaPrompt != null)
            {
                float a = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 3f);
                var c = _crashCtaPrompt.color;
                _crashCtaPrompt.color = new Color(c.r, c.g, c.b, a);
                yield return null;
            }
        }

        private void HideCrashCta()
        {
            _crashCtaVisible = false;
            _crashCtaPrompt = null;
            if (_crashCtaPanel != null) Destroy(_crashCtaPanel.gameObject);
        }

        private void HideCrashCtaAndRespawn()
        {
            HideCrashCta();
            _camRig?.EndCrashSequence();
            _flightController?.RespawnAfterCrash();
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
            if (_paused)
            {
                ShowPauseMenu();
                CustomCursor.Apply();
                AudioManager.Instance?.PlayPauseMenuMusic();
            }
            else
            {
                if (_pausePanel != null) Destroy(_pausePanel.gameObject);
                CustomCursor.Reset();
                AudioManager.Instance?.StopPauseMenuMusic();
            }
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
                    () => { Time.timeScale = 1f; _flightLog?.Flush(); GameManager.Instance.ReturnToMenu(); }, Accent, 22);
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
                // A full level restart, not just a teleport — reloads the Flight scene
                // from scratch (fresh weather, no leftover fire/craters/debris, replay
                // buffer and flight log reset) and respawns at this flight's preselected
                // map/drone/spawn point/spawn altitude, same as GameManager.StartFreeFlight
                // originally launched it. Bypasses TogglePause() the same way MAIN MENU
                // below does, since this scene instance is about to be torn down anyway.
                Time.timeScale = 1f;
                AudioListener.pause = false;
                AudioManager.Instance?.StopPauseMenuMusic();
                _flightLog?.Flush();
                GameManager.Instance.RestartFlight();
            }, PanelAlt, 24);
            Button_(box, "MAIN MENU", new Vector2(0.10f, 0.215f), new Vector2(0.90f, 0.33f), () =>
            {
                Time.timeScale = 1f;
                AudioListener.pause = false; // this bypasses TogglePause(), which would otherwise do it
                AudioManager.Instance?.StopPauseMenuMusic(); // ditto — TogglePause() isn't called here either
                _flightLog?.Flush();
                GameManager.Instance.ReturnToMenu();
            }, AccentWarn, 24);
        }
    }
}
