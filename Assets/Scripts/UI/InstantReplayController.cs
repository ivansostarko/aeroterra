using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using AeroTerra.Core;
using AeroTerra.Drone;
using AeroTerra.Input;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// In-flight Replay &amp; Screenshot. Continuously records the drone's
    /// position/rotation into a rolling buffer (last ~90 seconds); pressing Replay
    /// (F10 / gamepad D-pad up) freezes the live drone and flies a smoothed chase
    /// camera back along that recorded path, like a sports-broadcast instant
    /// replay, then resumes live flight. Screenshot (F9 / gamepad Select) saves a
    /// PNG to Application.persistentDataPath/Screenshots and shows a brief
    /// flash+toast confirmation. Self-contained — no save files, no scrub/seek UI.
    /// </summary>
    public class InstantReplayController : MonoBehaviour
    {
        private struct ReplaySample
        {
            public float Time;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private const float SampleIntervalSec = 0.1f;
        private const float BufferDurationSec = 90f;
        private const float CameraFollowLerpSpeed = 4f;

        private DroneFlightController _flight;
        private DroneCameraRig _camRig;
        private Camera _cam;
        private Rigidbody _rb;

        private readonly List<ReplaySample> _buffer = new List<ReplaySample>();
        private List<ReplaySample> _replaySnapshot;
        private float _sampleTimer;

        private bool _isReplaying;
        private float _replayT;
        private float _replayDuration;
        private bool _rbWasKinematic;

        private RectTransform _replayBanner;
        private TMPro.TextMeshProUGUI _replayLabel;
        private RectTransform _flashPanel;
        private TMPro.TextMeshProUGUI _toastLabel;

        public void Init(DroneFlightController flight, DroneCameraRig camRig, Camera cam, Canvas canvas)
        {
            _flight = flight;
            _camRig = camRig;
            _cam = cam;
            _rb = flight != null ? flight.GetComponent<Rigidbody>() : null;
            BuildOverlay(canvas);
        }

        private void BuildOverlay(Canvas canvas)
        {
            var root = Panel_(canvas.transform, "ReplayOverlay", Color.clear, Vector2.zero, Vector2.one);

            _replayBanner = Panel_(root, "ReplayBanner", new Color(0, 0, 0, 0.55f),
                                    new Vector2(0.32f, 0.90f), new Vector2(0.68f, 0.965f));
            var dot = Panel_(_replayBanner, "Dot", AccentWarn, new Vector2(0.06f, 0.5f), new Vector2(0.06f, 0.5f),
                              new Vector2(-6f, -6f), new Vector2(6f, 6f));
            dot.localRotation = Quaternion.Euler(0, 0, 45f);
            _replayLabel = Label(_replayBanner, "REPLAY", 18, new Vector2(0.16f, 0f), new Vector2(0.98f, 1f),
                                 TextMain, TMPro.TextAlignmentOptions.MidlineLeft, TMPro.FontStyles.Bold);
            _replayBanner.gameObject.SetActive(false);

            _flashPanel = Panel_(root, "ScreenshotFlash", Color.white, Vector2.zero, Vector2.one);
            _flashPanel.GetComponent<Image>().raycastTarget = false;
            SetAlpha(_flashPanel, 0f);

            _toastLabel = Label(root, "", 16, new Vector2(0.25f, 0.10f), new Vector2(0.75f, 0.15f),
                                TextMain, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            _toastLabel.raycastTarget = false;
            _toastLabel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_flight == null) return;
            var im = InputManager.Instance;
            if (im == null) return;

            if (im.ScreenshotAction.WasPressedThisFrame()) TakeScreenshot();

            if (_isReplaying)
            {
                UpdateReplayPlayback();
                if (im.ReplayAction.WasPressedThisFrame()) EndReplay(); // press again to cut it short
                return;
            }

            RecordSample();
            if (im.ReplayAction.WasPressedThisFrame() && _buffer.Count > 1) BeginReplay();
        }

        // ---------------------------------------------------------------- recording

        private void RecordSample()
        {
            _sampleTimer -= Time.deltaTime;
            if (_sampleTimer > 0f) return;
            _sampleTimer = SampleIntervalSec;

            _buffer.Add(new ReplaySample
            {
                Time = Time.time,
                Position = _flight.transform.position,
                Rotation = _flight.transform.rotation,
            });

            float cutoff = Time.time - BufferDurationSec;
            while (_buffer.Count > 0 && _buffer[0].Time < cutoff) _buffer.RemoveAt(0);
        }

        // ---------------------------------------------------------------- replay

        private void BeginReplay()
        {
            _isReplaying = true;
            _replaySnapshot = new List<ReplaySample>(_buffer);
            _replayT = 0f;
            _replayDuration = Mathf.Max(0.01f,
                _replaySnapshot[_replaySnapshot.Count - 1].Time - _replaySnapshot[0].Time);

            // Freeze the live drone in place for the duration — the replay camera flies
            // an independent recorded path, so the real airframe just holds position
            // rather than drifting/falling under gravity with nobody flying it.
            if (_rb != null)
            {
                _rbWasKinematic = _rb.isKinematic;
                _rb.isKinematic = true;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            if (_camRig != null) _camRig.enabled = false;

            _replayBanner.gameObject.SetActive(true);
            AudioManager.Instance?.PlayButtonClick();
        }

        private void UpdateReplayPlayback()
        {
            _replayT += Time.unscaledDeltaTime;
            float sampleTime = _replaySnapshot[0].Time + _replayT;
            (Vector3 pos, Quaternion rot) = SampleAt(sampleTime);

            // Smoothed chase offset behind + above the recorded position — a simple
            // cinematic flythrough, not a scrubbable/free camera.
            Vector3 camTarget = pos - rot * Vector3.forward * 6f + Vector3.up * 2.5f;
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, camTarget,
                Time.unscaledDeltaTime * CameraFollowLerpSpeed);
            Vector3 lookDir = pos - _cam.transform.position;
            if (lookDir.sqrMagnitude > 0.01f)
                _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation,
                    Quaternion.LookRotation(lookDir.normalized, Vector3.up), Time.unscaledDeltaTime * CameraFollowLerpSpeed);

            _replayLabel.text = $"REPLAY   {_replayT:0.0}s / {_replayDuration:0.0}s   ·   [F10] SKIP";

            if (_replayT >= _replayDuration) EndReplay();
        }

        private (Vector3, Quaternion) SampleAt(float worldTime)
        {
            var list = _replaySnapshot;
            for (int i = 0; i < list.Count - 1; i++)
            {
                if (worldTime < list[i].Time || worldTime > list[i + 1].Time) continue;
                float span = Mathf.Max(0.0001f, list[i + 1].Time - list[i].Time);
                float k = (worldTime - list[i].Time) / span;
                return (Vector3.Lerp(list[i].Position, list[i + 1].Position, k),
                        Quaternion.Slerp(list[i].Rotation, list[i + 1].Rotation, k));
            }
            var last = list[list.Count - 1];
            return (last.Position, last.Rotation);
        }

        private void EndReplay()
        {
            _isReplaying = false;
            if (_rb != null) _rb.isKinematic = _rbWasKinematic;
            if (_camRig != null) _camRig.enabled = true;
            _replayBanner.gameObject.SetActive(false);

            // The camera (and, briefly, real time) moved on while we were watching the
            // past — resume recording from a clean slate rather than let the next
            // sample implicitly "teleport" across the gap.
            _buffer.Clear();
            AudioManager.Instance?.PlayButtonClick();
        }

        // ---------------------------------------------------------------- screenshot

        private void TakeScreenshot()
        {
            string folder = Path.Combine(Application.persistentDataPath, "Screenshots");
            Directory.CreateDirectory(folder);
            string stamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = $"AeroTerra_{stamp}.png";
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, fileName));

            // Metadata overlay sidecar (drone/city/date-time/altitude) — same base name,
            // .json instead of .png — read back by the Media screen's gallery (MediaUI).
            var meta = new ScreenshotMeta
            {
                DroneName = _flight.Spec.DisplayName,
                City = GameManager.Instance.SelectedMap != null ? GameManager.Instance.SelectedMap.DisplayName : "Unknown",
                AltitudeM = _flight.transform.position.y,
                CapturedAtIso = System.DateTime.Now.ToString("o"),
            };
            try { File.WriteAllText(Path.Combine(folder, $"AeroTerra_{stamp}.json"), JsonUtility.ToJson(meta, true)); }
            catch (System.Exception e) { Debug.LogWarning($"[InstantReplayController] screenshot metadata write failed: {e.Message}"); }

            StartCoroutine(FlashAndToast(fileName));
        }

        private IEnumerator FlashAndToast(string fileName)
        {
            yield return null; // let the actual capture (end of THIS frame) happen before the flash shows

            SetAlpha(_flashPanel, 0.6f);
            _toastLabel.text = $"SCREENSHOT SAVED — {fileName}";
            _toastLabel.gameObject.SetActive(true);
            AudioManager.Instance?.PlayButtonClick();

            float t = 0f;
            const float fadeSec = 0.25f;
            while (t < fadeSec)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(_flashPanel, Mathf.Lerp(0.6f, 0f, t / fadeSec));
                yield return null;
            }
            SetAlpha(_flashPanel, 0f);

            yield return new WaitForSecondsRealtime(2.5f);
            _toastLabel.gameObject.SetActive(false);
        }

        private static void SetAlpha(RectTransform rt, float a)
        {
            var img = rt.GetComponent<Image>();
            var c = img.color; c.a = a; img.color = c;
        }
    }
}
