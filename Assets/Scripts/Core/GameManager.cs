using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using AeroTerra.UI;

namespace AeroTerra.Core
{
    /// <summary>
    /// Root singleton. Owns cross-scene state: selected map, selected drone,
    /// settings, and scene flow (MainMenu <-> Flight).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Runtime selection")]
        public MapDefinition SelectedMap;
        public Drone.DroneSpecification SelectedDrone;
        public Workshop.CustomDroneData SelectedCustomConfig; // null = stock config
        /// <summary>Set by the Flying Conditions screen's altitude slider; null = use
        /// SelectedMap.SpawnAltitudeMeters. Always (re)set fresh before every StartFreeFlight
        /// call, so there's no staleness risk across separate Free Flight attempts.</summary>
        public double? SelectedSpawnAltitudeOverride;

        /// <summary>Set by the Flying Conditions screen's SPAWN LOCATION tab; null = use
        /// SelectedMap's own Latitude/Longitude (the map's default launch point). Always
        /// (re)set fresh before every StartFreeFlight call, same contract as
        /// SelectedSpawnAltitudeOverride above. Only carries position (Name/Latitude/
        /// Longitude) — altitude for a picked preset is applied by the UI directly into
        /// SelectedSpawnAltitudeOverride, so this class stays the single place altitude
        /// is ever read from (see SpawnAltitudeM).</summary>
        public MapDefinition.SpawnLocation SelectedSpawnLocationOverride;

        /// <summary>The altitude every respawn/reset path should teleport to — the
        /// Flying Conditions override if the player set one for this flight, else the
        /// map's own default, else a hardcoded fallback. Single source of truth so
        /// FlightSceneController.ResetDrone and DroneFlightController's crash/detonation
        /// respawns can't drift out of sync with where the flight actually started.</summary>
        public double SpawnAltitudeM => SelectedSpawnAltitudeOverride ?? SelectedMap?.SpawnAltitudeMeters ?? 150;

        /// <summary>Local Unity-space spawn position (world X/Z, relative to the map's
        /// Cesium georeference origin) every respawn/reset path should teleport to — (0,
        /// alt, 0) at the map's own origin by default, or an offset computed from
        /// SelectedSpawnLocationOverride's real-world lat/long via the same flat-earth
        /// approximation the HUD minimap/Landmarks already use (MapDefinition.
        /// FlatOffsetMeters). Pure math, no dependency on MapManager/Cesium having
        /// finished initializing — see FlightSceneController.Start() for why that
        /// ordering matters. Single source of truth, same spirit as SpawnAltitudeM.</summary>
        public Vector3 SpawnLocalPosition
        {
            get
            {
                float alt = (float)SpawnAltitudeM;
                if (SelectedSpawnLocationOverride == null || SelectedMap == null) return new Vector3(0f, alt, 0f);
                Vector2 offset = MapDefinition.FlatOffsetMeters(
                    SelectedSpawnLocationOverride.Latitude, SelectedSpawnLocationOverride.Longitude,
                    SelectedMap.Latitude, SelectedMap.Longitude);
                return new Vector3(offset.x, alt, offset.y);
            }
        }

        public SettingsData Settings { get; private set; }

        public const string SceneMainMenu = "MainMenu";
        public const string SceneFlight   = "Flight";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Settings = SaveSystem.LoadSettings();
            VideoSettingsApplier.ApplyAtStartup(Settings);
        }

        public void StartFreeFlight(MapDefinition map, Drone.DroneSpecification drone,
                                    Workshop.CustomDroneData customConfig = null)
        {
            SelectedMap = map;
            SelectedDrone = drone;
            SelectedCustomConfig = customConfig;
            StartCoroutine(LoadSceneRoutine(SceneFlight, map.DisplayName, map.Id, freeze: true));
        }

        public void ReturnToMenu()
        {
            AudioManager.Instance?.StopWeatherAmbience();
            StartCoroutine(LoadSceneRoutine(SceneMainMenu, null, null, freeze: false));
        }

        /// <summary>Pause menu ▸ RESTART: a full reload of the current flight, not just a
        /// drone teleport (see FlightSceneController.ResetDrone for that lighter-weight
        /// in-place path, still used by the R-key quick-reset and the post-crash prompt).
        /// Reloading the Flight scene from scratch tears down and rebuilds every
        /// subsystem — weather, fire/crater state, dropped payload debris, replay buffer,
        /// flight log, camera, HUD — exactly like restarting a level in any other game,
        /// instead of trying to hand-reset each one and risk missing something. Reuses
        /// the same loading-screen routine/freeze beat StartFreeFlight uses; SelectedMap/
        /// SelectedDrone/SelectedCustomConfig/SelectedSpawnLocationOverride/
        /// SelectedSpawnAltitudeOverride are all still sitting on this DontDestroyOnLoad
        /// singleton from when the flight was first launched, so the drone comes back at
        /// exactly the same preselected map, drone, spawn point and spawn altitude with
        /// nothing needing to be re-selected.</summary>
        public void RestartFlight()
        {
            AudioManager.Instance?.StopWeatherAmbience();
            StartCoroutine(LoadSceneRoutine(SceneFlight, SelectedMap?.DisplayName, SelectedMap?.Id, freeze: true));
        }

        /// <summary>Free Flight only: how long the loading screen holds on "LOADING {map}"
        /// with the game world and audio frozen, giving the map/weather/engine-start moment
        /// a deliberate "mission start" beat instead of popping straight into a moving drone.</summary>
        private const float FreeFlightFreezeSeconds = 6f;

        private IEnumerator LoadSceneRoutine(string sceneName, string label, string mapId, bool freeze)
        {
            var loader = LoadingScreenUI.GetOrCreate(gameObject);
            loader.Show(label, mapId);

            // loader.Hide() must run no matter what — this overlay sits on sortingOrder 1000
            // and survives every scene load, so if anything below throws before reaching the
            // old final Hide() call, it stays stuck on top of literally everything forever
            // (looks exactly like "the whole game is frozen/blank", even on unrelated screens).
            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[GameManager] SceneManager.LoadSceneAsync(\"{sceneName}\") returned null — " +
                                "is this scene added to Build Settings?");
                loader.Hide();
                yield break;
            }

            try
            {
                op.allowSceneActivation = false;
                while (op.progress < 0.9f)
                {
                    loader.SetProgress(op.progress / 0.9f);
                    yield return null;
                }
                loader.SetProgress(1f);

                if (freeze)
                {
                    Time.timeScale = 0f;
                    AudioListener.pause = true;
                    yield return new WaitForSecondsRealtime(FreeFlightFreezeSeconds);
                }
                else
                {
                    yield return new WaitForSecondsRealtime(0.2f);
                }

                op.allowSceneActivation = true;
                while (!op.isDone) yield return null;
            }
            finally
            {
                // Unconditional, not just the freeze==true branch: guards against leaving
                // the game frozen/muted forever if something above throws mid-freeze.
                Time.timeScale = 1f;
                AudioListener.pause = false;
                loader.Hide();
            }
        }

        public void SaveSettings() => SaveSystem.SaveSettings(Settings);

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
