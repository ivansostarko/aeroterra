using UnityEngine;
using UnityEngine.Video;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using AeroTerra.Core;

namespace AeroTerra.UI
{
    /// <summary>
    /// First scene in the build (after Unity's own splash, before MainMenu): plays
    /// Resources/Videos/game_intro.mp4 full-screen, then loads MainMenu. Any
    /// keyboard/mouse/gamepad press skips straight to MainMenu. Deliberately
    /// self-contained — GameManager/AudioManager aren't bootstrapped yet this early,
    /// so this scene doesn't touch either.
    /// </summary>
    public class IntroSceneController : MonoBehaviour
    {
        private VideoPlayer _player;
        private bool _advancing;

        private void Start()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("MainCamera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                camGo.AddComponent<AudioListener>();
            }

            var clip = Resources.Load<VideoClip>("Videos/game_intro");
            if (clip == null) { GoToMainMenu(); return; }

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.source = VideoSource.VideoClip;
            _player.clip = clip;
            _player.renderMode = VideoRenderMode.CameraFarPlane;
            _player.targetCamera = cam;
            _player.aspectRatio = VideoAspectRatio.FitOutside;
            _player.isLooping = false;
            _player.loopPointReached += _ => GoToMainMenu();

            var audioSrc = gameObject.AddComponent<AudioSource>();
            _player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _player.SetTargetAudioSource(0, audioSrc);

            _player.Play();
        }

        private void Update()
        {
            if (_advancing) return;
            if (AnyInputPressed()) GoToMainMenu();
        }

        private static bool AnyInputPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;

            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame ||
                                   mouse.rightButton.wasPressedThisFrame ||
                                   mouse.middleButton.wasPressedThisFrame)) return true;

            foreach (var gp in Gamepad.all)
                foreach (var control in gp.allControls)
                    if (control is UnityEngine.InputSystem.Controls.ButtonControl b && b.wasPressedThisFrame)
                        return true;

            return false;
        }

        private void GoToMainMenu()
        {
            if (_advancing) return;
            _advancing = true;
            if (_player != null) _player.Stop();
            SceneManager.LoadScene(GameManager.SceneMainMenu);
        }
    }
}
