using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AeroTerra.Core;
using AeroTerra.Drone;

namespace AeroTerra.UI
{
    /// <summary>
    /// Flight's single narration voice: one serialized voice-line queue (so mission-
    /// intro, weather, and gameplay-event lines always play back to back, never
    /// overlapping or cutting each other off) driving a bottom-center subtitle bar,
    /// plus the state machines that watch drone telemetry each frame for the
    /// continuous gameplay triggers (low battery, dangerous altitude, engine-off
    /// freefall).
    ///
    /// Discrete events (crash, payload drop/delivery) are reported in by
    /// DroneFlightController/PayloadDropper via Instance?.NotifyX() calls at the
    /// exact moment they happen — the same "reach into a UI-owned singleton from
    /// Drone code" pattern already used for DroneCameraRig.Instance?.Shake(...).
    /// </summary>
    public class NarratorController : MonoBehaviour
    {
        public static NarratorController Instance { get; private set; }

        // ---- gameplay trigger tuning ----
        private const float LowBatteryThreshold01 = 0.05f;

        private const float HighAltitudeArmFrac = 0.85f;    // fires once altitude crosses this fraction of Spec.MaxAltitudeM
        private const float HighAltitudeRearmFrac = 0.65f;  // must drop back below this fraction to re-arm
        private const float HighAltitudeRetriggerCooldownSec = 20f;

        private const float FallingThrottleMax = 0.02f;     // "throttle is 0"
        private const float FallingVerticalSpeedMs = -3f;   // meaningfully descending, not just settling
        private const float FallingSustainSec = 1.5f;       // must hold for this long — filters brief throttle taps
        private const float FallingRetriggerCooldownSec = 8f;
        private const float SpawnGraceSec = 3f;              // ignore the falling check right after spawn/respawn

        private const float CrashNotifyCooldownSec = 2f;
        private const float PayloadNotifyCooldownSec = 4f;  // debounces multi-hardpoint drop bursts

        private SubtitleUI _subtitles;
        private DroneFlightController _flight;

        private readonly Queue<(string path, string subtitle)> _queue = new Queue<(string, string)>();
        private Coroutine _drainRoutine;

        private bool _lowBatteryWarned;
        private bool _highAltitudeArmed = true;
        private float _lastHighAltitudeTime = float.NegativeInfinity;
        private float _fallingTimer;
        private float _lastFallingNotifyTime = float.NegativeInfinity;
        private float _spawnGraceTimer;
        private float _lastCrashNotifyTime = float.NegativeInfinity;
        private float _lastPayloadNotifyTime = float.NegativeInfinity;

        /// <summary>Builds the subtitle bar, starts the mission-intro/weather lines,
        /// and begins polling flight telemetry. Called once from FlightSceneController.Start().</summary>
        public void Init(Canvas canvas, DroneFlightController flight)
        {
            Instance = this;
            _flight = flight;
            _subtitles = gameObject.AddComponent<SubtitleUI>();
            _subtitles.Init(canvas);
            _spawnGraceTimer = SpawnGraceSec;

            Enqueue("Audio/voices/open-fly/intro-voice",
                "Welcome to the open skies, my friend. Today, you can deliver vital cargo—or " +
                "drop a few bombs and create an war crime. The choice is yours.");

            var weather = GameManager.Instance.Settings.Weather;
            string weatherPath = AudioManager.WeatherVoiceLinePath(weather);
            if (weatherPath != null) Enqueue(weatherPath, WeatherSubtitle(weather));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Re-arms spawn-dependent narration state after a mission restart
        /// (FlightSceneController.ResetDrone) so a fresh takeoff doesn't immediately
        /// re-trigger the engine-off-freefall line, and the low-battery/high-altitude
        /// warnings are ready to fire again for the new attempt.</summary>
        public void NotifyRespawned()
        {
            _lowBatteryWarned = false;
            _highAltitudeArmed = true;
            _fallingTimer = 0f;
            _spawnGraceTimer = SpawnGraceSec;
        }

        /// <summary>Drone hit the ground hard — called from DroneFlightController's
        /// crash and kamikaze-detonation paths (never for the dead-battery touchdown,
        /// which already gets its own end-of-flight modal instead).</summary>
        public void NotifyCrashed()
        {
            if (Time.unscaledTime - _lastCrashNotifyTime < CrashNotifyCooldownSec) return;
            _lastCrashNotifyTime = Time.unscaledTime;
            Enqueue("Audio/voices/open-fly/voice_2", "Ouch. You crashed again. At least you’re consistent.");
        }

        /// <summary>Armed ordnance just left the drone — called from PayloadDropper.TryDrop
        /// at the moment of release ("bombs away" is said as the weapon leaves, not on impact).</summary>
        public void NotifyMilitaryPayloadDropped()
        {
            if (Time.unscaledTime - _lastPayloadNotifyTime < PayloadNotifyCooldownSec) return;
            _lastPayloadNotifyTime = Time.unscaledTime;
            Enqueue("Audio/voices/open-fly/voice_5", "Bombs away. Things are about to get complicated.");
        }

        /// <summary>Unarmed cargo landed safely — called from PayloadDropper's
        /// DroppedPayloadImpact on ground contact (the line references arrival, so it
        /// fires on delivery, not release).</summary>
        public void NotifyCargoDelivered()
        {
            if (Time.unscaledTime - _lastPayloadNotifyTime < PayloadNotifyCooldownSec) return;
            _lastPayloadNotifyTime = Time.unscaledTime;
            Enqueue("Audio/voices/open-fly/voice_4", "Cargo delivered. Somehow, it arrived in one piece.");
        }

        private void Update()
        {
            if (_flight == null) return;

            if (_spawnGraceTimer > 0f) _spawnGraceTimer -= Time.unscaledDeltaTime;

            CheckLowBattery();
            CheckHighAltitude();
            CheckFallingWithNoThrottle();
        }

        /// <summary>Same threshold/line for a fuel-powered airframe (Locust/Wraith/
        /// Bison) as for a battery one — there's only the one recorded voice line, and
        /// "low power, come back now" reads fine regardless of which tank is emptying.</summary>
        private void CheckLowBattery()
        {
            if (_lowBatteryWarned) return;
            IPowerSource power = _flight.Spec.PowerSystem == PowerSystemType.Fuel
                ? (IPowerSource)_flight.Fuel : _flight.Battery;
            if (power == null || power.Percent >= LowBatteryThreshold01) return;

            _lowBatteryWarned = true;
            Enqueue("Audio/voices/open-fly/voice_1",
                "You’ve got less than 5% battery left. Change it now—unless you’re planning for a hard landing!");
        }

        private void CheckHighAltitude()
        {
            float maxAlt = _flight.Spec != null ? _flight.Spec.MaxAltitudeM : 0f;
            if (maxAlt <= 0f) return;
            float altitude = _flight.transform.position.y;

            if (_highAltitudeArmed && altitude >= HighAltitudeArmFrac * maxAlt)
            {
                _highAltitudeArmed = false;
                _lastHighAltitudeTime = Time.unscaledTime;
                Enqueue("Audio/voices/open-fly/voice_3", "Slow down! We’re flying a drone, not launching it to the Moon.");
            }
            else if (!_highAltitudeArmed && altitude <= HighAltitudeRearmFrac * maxAlt &&
                     Time.unscaledTime - _lastHighAltitudeTime > HighAltitudeRetriggerCooldownSec)
            {
                _highAltitudeArmed = true;
            }
        }

        private void CheckFallingWithNoThrottle()
        {
            if (_spawnGraceTimer > 0f) { _fallingTimer = 0f; return; }

            bool falling = _flight.Throttle01 <= FallingThrottleMax && _flight.VerticalSpeedMs <= FallingVerticalSpeedMs;
            if (!falling) { _fallingTimer = 0f; return; }

            _fallingTimer += Time.unscaledDeltaTime;
            if (_fallingTimer < FallingSustainSec) return;
            if (Time.unscaledTime - _lastFallingNotifyTime < FallingRetriggerCooldownSec) return;

            _lastFallingNotifyTime = Time.unscaledTime;
            _fallingTimer = 0f;
            Enqueue("Audio/voices/open-fly/voice_6", "Hey, what are you waiting for? Start the drone and get moving.");
        }

        // ---- shared line queue ----

        private void Enqueue(string resourcePath, string subtitle)
        {
            // Settings ▸ Game ▸ HUD Elements ▸ Narrator (voice & text) — disabling it
            // stops both the voice line audio and the subtitle text at the source,
            // rather than muting/hiding each independently downstream.
            if (!GameManager.Instance.Settings.HudShowNarrator) return;
            _queue.Enqueue((resourcePath, subtitle));
            if (_drainRoutine == null) _drainRoutine = StartCoroutine(DrainQueue());
        }

        private IEnumerator DrainQueue()
        {
            while (_queue.Count > 0)
            {
                var (path, subtitle) = _queue.Dequeue();
                var clip = AudioManager.Instance?.PlayVoiceLine(path);
                if (clip == null) continue;

                if (GameManager.Instance.Settings.SubtitlesEnabled) _subtitles.Show(subtitle);
                yield return new WaitForSecondsRealtime(clip.length);
                _subtitles.Hide();
            }
            _drainRoutine = null;
        }

        private static string WeatherSubtitle(WeatherPreset weather) => weather switch
        {
            WeatherPreset.Clear => "Perfect visibility, calm winds, and absolutely no excuse for crashing.",
            WeatherPreset.Cloudy => "The clouds are here to make everything look more dramatic than it actually is.",
            WeatherPreset.Rain => "It is raining. The drone is waterproof—probably.",
            WeatherPreset.Storm => "Strong winds, heavy rain, and lightning. What could possibly go wrong?",
            WeatherPreset.Fog => "Visibility is near zero, but optimism remains dangerously high.",
            WeatherPreset.Snow => "Visibility is near zero, but optimism remains dangerously high.",
            _ => "",
        };
    }
}
