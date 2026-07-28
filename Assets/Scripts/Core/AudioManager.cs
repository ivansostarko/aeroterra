using UnityEngine;
using UnityEngine.Audio;

namespace AeroTerra.Core
{
    /// <summary>
    /// Master audio control plus per-category volume (Music/SFX/Voice) and the
    /// looping menu background music track. Master is applied as a global
    /// AudioListener multiplier (mapped logarithmically to dB) since the project
    /// has no AudioMixer asset; Music/SFX/Voice are plain 0..1 multipliers that
    /// individual sound sources (engine loop, menu music, future voice lines)
    /// read from here and apply to their own AudioSource.volume.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioMixer mixer;   // must expose "MasterVolume"
        public AudioMixer Mixer => mixer;

        public float MusicVolume01 { get; private set; } = 0.6f;
        public float SfxVolume01 { get; private set; } = 0.8f;
        public float VoiceVolume01 { get; private set; } = 0.8f;

        private AudioSource _musicSource;
        private string _currentMusicPath;
        private AudioSource _weatherMusicSource;
        private WeatherPreset? _currentWeatherMusic;
        private AudioSource _uiSfxSource;
        private AudioClip _buttonClickClip, _buttonHoverClip;
        private bool _uiSfxLoaded;
        private AudioClip _bombDropClip, _bombExplosionClip;
        private AudioClip _lowExplosionClip, _mediumExplosionClip, _largeExplosionClip;
        private bool _bombSfxLoaded;
        private AudioClip _droneCrashClip;
        private bool _explosionsSfxLoaded;
        private AudioSource _voiceSource;
        private AudioSource _warningSfxSource;
        private AudioClip _lowPowerClip;
        private bool _warningSfxLoaded;
        private AudioSource _pauseMusicSource;
        private AudioClip _parachuteOpenClip;
        private bool _parachuteSfxLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            var s = GameManager.Instance.Settings;
            ApplyMasterVolume(s.MasterVolume);
            ApplyMusicVolume(s.MusicVolume);
            ApplySfxVolume(s.SfxVolume);
            ApplyVoiceVolume(s.VoiceVolume);
        }

        public void ApplyMasterVolume(float linear01)
        {
            linear01 = Mathf.Clamp(linear01, 0.0001f, 1f);
            float dB = Mathf.Log10(linear01) * 20f;
            if (mixer != null) mixer.SetFloat("MasterVolume", dB);
            else AudioListener.volume = linear01; // fallback if no mixer assigned
        }

        public void ApplyMusicVolume(float linear01)
        {
            MusicVolume01 = Mathf.Clamp01(linear01);
            if (_musicSource != null) _musicSource.volume = MusicVolume01;
            if (_weatherMusicSource != null) _weatherMusicSource.volume = MusicVolume01;
            if (_pauseMusicSource != null) _pauseMusicSource.volume = MusicVolume01;
        }

        public void ApplySfxVolume(float linear01) => SfxVolume01 = Mathf.Clamp01(linear01);

        public void ApplyVoiceVolume(float linear01) => VoiceVolume01 = Mathf.Clamp01(linear01);

        /// <summary>
        /// One shared looping background-music source: only one track plays at a time
        /// (menu music and Free Flight music are mutually exclusive), swapped by resource
        /// path. Drop files under Assets/Resources/Audio/... — Resources.Load finds them
        /// by name, no manual wiring required.
        /// </summary>
        public void PlayMenuMusic() => PlayBackgroundTrack("Audio/background/game_menu_background");

        public void PlayFreeFlightMusic() => PlayBackgroundTrack("Audio/background/free-flight-background");

        public void PlayCreditsMusic() => PlayBackgroundTrack("Audio/background/credits_game_menu_background");

        /// <summary>Workshop showroom's per-drone track (see DroneSpecification.
        /// WorkshopMusicPath) — swaps whenever the selected airframe changes, same
        /// shared/mutually-exclusive _musicSource as the other PlayXMusic calls above.</summary>
        public void PlayWorkshopMusic(string resourcePath) => PlayBackgroundTrack(resourcePath);

        public void StopMenuMusic() => StopBackgroundTrack();

        public void StopFreeFlightMusic() => StopBackgroundTrack();

        public void StopCreditsMusic() => StopBackgroundTrack();

        public void StopWorkshopMusic() => StopBackgroundTrack();

        /// <summary>
        /// Pause-menu background track (FlightSceneController.TogglePause) — a dedicated
        /// AudioSource, NOT the shared _musicSource every PlayXMusic call above uses,
        /// for two reasons: (1) it needs AudioSource.ignoreListenerPause = true so it's
        /// actually audible while AudioListener.pause = true mutes everything else the
        /// instant the game pauses (same opt-out the UI click/hover sfx source uses), and
        /// (2) using the shared source would stomp whatever Free Flight background track
        /// was already playing, losing it on resume. File: Assets/Resources/Audio/
        /// background/pause_game_menu_background.mp3.
        /// </summary>
        public void PlayPauseMenuMusic()
        {
            if (_pauseMusicSource == null)
            {
                _pauseMusicSource = gameObject.AddComponent<AudioSource>();
                _pauseMusicSource.loop = true;
                _pauseMusicSource.playOnAwake = false;
                _pauseMusicSource.spatialBlend = 0f;
                _pauseMusicSource.ignoreListenerPause = true;
            }
            _pauseMusicSource.volume = MusicVolume01;
            if (_pauseMusicSource.clip == null)
            {
                var clip = Resources.Load<AudioClip>("Audio/background/pause_game_menu_background");
                if (clip == null) return;
                _pauseMusicSource.clip = clip;
            }
            _pauseMusicSource.Play();
        }

        public void StopPauseMenuMusic()
        {
            if (_pauseMusicSource != null) _pauseMusicSource.Stop();
        }

        private void PlayBackgroundTrack(string resourcePath)
        {
            if (_musicSource == null)
            {
                _musicSource = gameObject.AddComponent<AudioSource>();
                _musicSource.loop = true;
                _musicSource.playOnAwake = false;
                _musicSource.volume = MusicVolume01;
            }
            if (_currentMusicPath == resourcePath && _musicSource.isPlaying) return;

            var clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null) return;
            _currentMusicPath = resourcePath;
            _musicSource.clip = clip;
            _musicSource.Play();
        }

        private void StopBackgroundTrack()
        {
            if (_musicSource != null) _musicSource.Stop();
            _currentMusicPath = null;
        }

        /// <summary>
        /// Extra ambience layer over the Free Flight background track, only for
        /// weather that has one: Rain/Storm/Snow. Clear/Cloudy/Fog have no ambience
        /// track and just stop whatever was playing. Files: Assets/Resources/Audio/
        /// background/rain-background.mp3, storm-background.mp3, snow-background.mp3.
        /// </summary>
        public void PlayWeatherAmbience(WeatherPreset weather)
        {
            string path = weather switch
            {
                WeatherPreset.Rain => "Audio/background/rain-background",
                WeatherPreset.Storm => "Audio/background/storm-background",
                WeatherPreset.Snow => "Audio/background/snow-background",
                _ => null,
            };

            if (path == null) { StopWeatherAmbience(); return; }
            if (_currentWeatherMusic == weather && _weatherMusicSource != null && _weatherMusicSource.isPlaying) return;

            if (_weatherMusicSource == null)
            {
                _weatherMusicSource = gameObject.AddComponent<AudioSource>();
                _weatherMusicSource.loop = true;
                _weatherMusicSource.playOnAwake = false;
                _weatherMusicSource.spatialBlend = 0f;
            }

            var clip = Resources.Load<AudioClip>(path);
            if (clip == null) return;
            _currentWeatherMusic = weather;
            _weatherMusicSource.clip = clip;
            _weatherMusicSource.volume = MusicVolume01;
            _weatherMusicSource.Play();
        }

        public void StopWeatherAmbience()
        {
            if (_weatherMusicSource != null) _weatherMusicSource.Stop();
            _currentWeatherMusic = null;
        }

        /// <summary>
        /// One-shot narrated voice lines (mission intro, weather flavor lines) — distinct
        /// from the looping category sources above: never loops, one clip at a time,
        /// swapping mid-line if called again. Returns the clip actually started (or null
        /// if the resource is missing) so callers can time subtitles off AudioClip.length.
        /// </summary>
        public AudioClip PlayVoiceLine(string resourcePath)
        {
            var clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null) return null;

            EnsureVoiceSourceLoaded();
            _voiceSource.volume = VoiceVolume01;
            _voiceSource.clip = clip;
            _voiceSource.Play();
            return clip;
        }

        /// <summary>
        /// Weather-flavored narration, played once flight starts. Files: Assets/Resources/
        /// Audio/voices/weather/{clear,cloud,rain,storm,fog,snow}_weather.mp3 — note the
        /// Cloudy preset maps to "cloud_weather" (not "cloudy_weather"), hence the explicit
        /// switch rather than a ToString()-derived path.
        /// </summary>
        public AudioClip PlayWeatherVoiceLine(WeatherPreset weather)
        {
            string path = WeatherVoiceLinePath(weather);
            return path == null ? null : PlayVoiceLine(path);
        }

        /// <summary>Resource path for PlayWeatherVoiceLine's clip, exposed separately so
        /// callers that queue narration (see NarratorController) can resolve the path
        /// up front without triggering playback immediately.</summary>
        public static string WeatherVoiceLinePath(WeatherPreset weather) => weather switch
        {
            WeatherPreset.Clear => "Audio/voices/weather/clear_weather",
            WeatherPreset.Cloudy => "Audio/voices/weather/cloud_weather",
            WeatherPreset.Rain => "Audio/voices/weather/rain_weather",
            WeatherPreset.Storm => "Audio/voices/weather/storm_weather",
            WeatherPreset.Fog => "Audio/voices/weather/fog_weather",
            WeatherPreset.Snow => "Audio/voices/weather/snow_weather",
            _ => null,
        };

        private void EnsureVoiceSourceLoaded()
        {
            if (_voiceSource != null) return;
            _voiceSource = gameObject.AddComponent<AudioSource>();
            _voiceSource.playOnAwake = false;
            _voiceSource.spatialBlend = 0f;
            // Mission-start narration plays right after GameManager's post-load freeze
            // (LoadSceneRoutine's 6s freeze + AudioListener.pause = true), which is still
            // in effect for the first frame or two of the Flight scene — opt out like the
            // UI sfx source does so the intro line isn't silently swallowed.
            _voiceSource.ignoreListenerPause = true;
        }

        /// <summary>
        /// Global UI click/hover SFX, used by every button built through UIBuilder.Button_.
        /// Files: Assets/Resources/Audio/sfx/button/click-button.mp3 and hover-button.mp3.
        /// Volume follows the SFX Volume setting (Settings ▸ Audio) via SfxVolume01.
        /// </summary>
        public void PlayButtonClick() => PlayUiSfx(_buttonClickClip);

        public void PlayButtonHover() => PlayUiSfx(_buttonHoverClip);

        private void PlayUiSfx(AudioClip clip)
        {
            EnsureUiSfxLoaded();
            if (clip == null || _uiSfxSource == null) return;
            _uiSfxSource.PlayOneShot(clip, SfxVolume01);
        }

        private void EnsureUiSfxLoaded()
        {
            if (_uiSfxLoaded) return;
            _uiSfxLoaded = true;
            _buttonClickClip = Resources.Load<AudioClip>("Audio/sfx/button/click-button");
            _buttonHoverClip = Resources.Load<AudioClip>("Audio/sfx/button/hover-button");
            _uiSfxSource = gameObject.AddComponent<AudioSource>();
            _uiSfxSource.playOnAwake = false;
            _uiSfxSource.spatialBlend = 0f;
            // Pausing (AudioListener.pause, see FlightSceneController.TogglePause) mutes
            // every other source in the game, but button feedback in the pause menu
            // itself should still be audible — opt this one source out.
            _uiSfxSource.ignoreListenerPause = true;
        }

        /// <summary>
        /// Ordnance one-shots, positioned in the world (unlike the 2D UI sfx above):
        /// the drop release (PayloadDropper) and the ground/building impact
        /// (PayloadDropper's dropped-munition collision, DroneFlightController crash).
        /// Files: Assets/Resources/Audio/sfx/bomb/bomb-drop.mp3 and bomb-explosion.mp3.
        /// </summary>
        public void PlayBombDrop(Vector3 worldPos)
        {
            EnsureBombSfxLoaded();
            if (_bombDropClip != null) AudioSource.PlayClipAtPoint(_bombDropClip, worldPos, SfxVolume01);
        }

        public void PlayBombExplosion(Vector3 worldPos)
        {
            EnsureBombSfxLoaded();
            if (_bombExplosionClip != null) AudioSource.PlayClipAtPoint(_bombExplosionClip, worldPos, SfxVolume01);
        }

        /// <summary>Pitched variants of the two calls above — PayloadDropper uses these
        /// to give each PayloadKind (Warhead/GuidedAmmunition/DropAmmunition) a distinct
        /// aural signature from the same two clips, rather than needing new audio assets
        /// per type. PlayClipAtPoint has no pitch control, so this spawns a throwaway
        /// positional AudioSource instead — same pattern as PlayImpactThud below.</summary>
        public void PlayBombDrop(Vector3 worldPos, float pitch)
        {
            EnsureBombSfxLoaded();
            if (_bombDropClip != null) PlayPitchedOneShot(_bombDropClip, worldPos, SfxVolume01, pitch);
        }

        public void PlayBombExplosion(Vector3 worldPos, float pitch)
        {
            EnsureBombSfxLoaded();
            if (_bombExplosionClip != null) PlayPitchedOneShot(_bombExplosionClip, worldPos, SfxVolume01, pitch);
        }

        private void PlayPitchedOneShot(AudioClip clip, Vector3 worldPos, float volume, float pitch)
        {
            var go = new GameObject("PitchedSfx");
            go.transform.position = worldPos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.spatialBlend = 1f;
            src.Play();
            Destroy(go, clip.length / Mathf.Max(0.01f, pitch) + 0.1f);
        }

        /// <summary>
        /// Soft positional thud for an unarmed cargo pod landing (PayloadDropper's
        /// dropped-pod collision): the bomb-explosion clip pitched down and quiet
        /// reads as a heavy dull impact — no dedicated thud asset needed.
        /// </summary>
        public void PlayImpactThud(Vector3 worldPos)
        {
            EnsureBombSfxLoaded();
            if (_bombExplosionClip == null) return;

            var go = new GameObject("ThudSfx");
            go.transform.position = worldPos;
            var src = go.AddComponent<AudioSource>();
            src.clip = _bombExplosionClip;
            src.volume = SfxVolume01 * 0.3f;
            src.pitch = 0.55f;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 4f;
            src.maxDistance = 60f;
            src.dopplerLevel = 0f;
            src.Play();
            Destroy(go, _bombExplosionClip.length / src.pitch + 0.1f);
        }

        private void EnsureBombSfxLoaded()
        {
            if (_bombSfxLoaded) return;
            _bombSfxLoaded = true;
            _bombDropClip = Resources.Load<AudioClip>("Audio/sfx/bomb/bomb-drop");
            _bombExplosionClip = Resources.Load<AudioClip>("Audio/sfx/bomb/bomb-explosion");
            _lowExplosionClip = Resources.Load<AudioClip>("Audio/sfx/bomb/low_explosion");
            _mediumExplosionClip = Resources.Load<AudioClip>("Audio/sfx/bomb/medium_explosion");
            _largeExplosionClip = Resources.Load<AudioClip>("Audio/sfx/bomb/large_explosion");
        }

        /// <summary>Extra one-shot layered on top of PlayBombExplosion for the drone's
        /// own hard-crash sequence (DroneFlightController.OnCollisionEnter) — a distinct,
        /// heavier "the whole airframe just went down" sound on top of the generic blast
        /// clip, not a replacement for it. File: Assets/Resources/Audio/sfx/explosions/
        /// drone_crash.mp3.</summary>
        public void PlayDroneCrashExplosion(Vector3 worldPos)
        {
            EnsureExplosionsSfxLoaded();
            if (_droneCrashClip != null) AudioSource.PlayClipAtPoint(_droneCrashClip, worldPos, SfxVolume01);
        }

        private void EnsureExplosionsSfxLoaded()
        {
            if (_explosionsSfxLoaded) return;
            _explosionsSfxLoaded = true;
            _droneCrashClip = Resources.Load<AudioClip>("Audio/sfx/explosions/drone_crash");
        }

        /// <summary>Weight-tiered dropped-payload explosion — same 0.6/1.1 kg thresholds
        /// PayloadDropper.DroppedPayloadImpact.BaseBlastScale uses for the visual blast
        /// size, so the audio and the fireball scale together: a small low_explosion clip
        /// for a light munition, medium_explosion around 1 kg, large_explosion for
        /// anything heavier. Falls back to the flat bomb-explosion clip if massKg is 0
        /// (not recorded) or a tiered clip hasn't been imported yet, so this never goes
        /// silent even before the new assets are in place.</summary>
        public void PlayTieredExplosion(Vector3 worldPos, float pitch, float massKg)
        {
            EnsureBombSfxLoaded();
            AudioClip clip = massKg switch
            {
                <= 0f => _bombExplosionClip,
                <= 0.6f => _lowExplosionClip,
                <= 1.1f => _mediumExplosionClip,
                _ => _largeExplosionClip,
            };
            clip = clip != null ? clip : _bombExplosionClip;
            if (clip != null) PlayPitchedOneShot(clip, worldPos, SfxVolume01, pitch);
        }

        /// <summary>One-shot for the Parachute loadout item opening in flight (G key,
        /// ParachuteController) — positional so it's audible from wherever the drone is
        /// when it deploys. File: Assets/Resources/Audio/sfx/parachute/parachute_opening.mp3.</summary>
        public void PlayParachuteOpen(Vector3 worldPos)
        {
            EnsureParachuteSfxLoaded();
            if (_parachuteOpenClip != null) AudioSource.PlayClipAtPoint(_parachuteOpenClip, worldPos, SfxVolume01);
        }

        private void EnsureParachuteSfxLoaded()
        {
            if (_parachuteSfxLoaded) return;
            _parachuteSfxLoaded = true;
            _parachuteOpenClip = Resources.Load<AudioClip>("Audio/sfx/parachute/parachute_opening");
        }

        /// <summary>
        /// Repeating low-battery/fuel chirp, played by FlightHUD every few seconds while
        /// the active power source is below its low-power threshold (see FlightHUD.Update,
        /// AccentWarn-flashing "LOW BAT/FUEL — RETURN NOW" banner). A plain 2D one-shot,
        /// not ignoreListenerPause — unlike UI click/hover feedback, this warning belongs
        /// to the live flight and should go silent along with everything else while paused.
        /// File: Assets/Resources/Audio/sfx/warning/low-power-warning.mp3 — drop a short
        /// beep/chime there; until then this silently no-ops, same as any other missing clip.
        /// </summary>
        public void PlayLowPowerWarning()
        {
            EnsureWarningSfxLoaded();
            if (_lowPowerClip == null || _warningSfxSource == null) return;
            _warningSfxSource.PlayOneShot(_lowPowerClip, SfxVolume01);
        }

        private void EnsureWarningSfxLoaded()
        {
            if (_warningSfxLoaded) return;
            _warningSfxLoaded = true;
            _lowPowerClip = Resources.Load<AudioClip>("Audio/sfx/warning/low-power-warning");
            _warningSfxSource = gameObject.AddComponent<AudioSource>();
            _warningSfxSource.playOnAwake = false;
            _warningSfxSource.spatialBlend = 0f;
        }
    }
}
