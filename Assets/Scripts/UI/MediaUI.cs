using System.IO;
using UnityEngine;
using UnityEngine.UI;
using AeroTerra.Core;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// Main menu ▸ Media: browse and manage every screenshot captured in Free Flight
    /// (F9 / InstantReplayController.TakeScreenshot), each shown with its metadata
    /// overlay (drone, city, capture time, altitude — see ScreenshotMeta). RECORDINGS
    /// is a placeholder tab — this build has no video capture pipeline, only
    /// screenshots and the in-flight Instant Replay (which never writes a file).
    /// Same screen-controller shape as every other main-menu entry (see CreditsUI).
    /// </summary>
    public class MediaUI : MonoBehaviour
    {
        private RectTransform _root;
        private System.Action _onBack;
        private int _tab; // 0 = screenshots, 1 = recordings

        private Canvas Canvas => GetComponent<MainMenuUI>().Canvas;

        public void Open(System.Action onBack)
        {
            _onBack = onBack;
            _tab = 0;
            Build();
        }

        private void Update()
        {
            if (_root == null) return;
            var im = AeroTerra.Input.InputManager.Instance;
            if (im != null && im.PauseAction.WasPressedThisFrame()) GoBack();
        }

        private void GoBack()
        {
            Clear();
            _onBack?.Invoke();
        }

        private void Build()
        {
            Clear();
            _root = Panel_(Canvas.transform, "Media", Bg, Vector2.zero, Vector2.one);

            BackButton_(_root, new Vector2(0.02f, 0.90f), new Vector2(0.075f, 0.965f), GoBack);
            var title = Label(_root, "MEDIA", 40, new Vector2(0.10f, 0.88f), new Vector2(0.95f, 0.965f),
                              TextMain, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            title.characterSpacing = 4;

            string[] tabs = { "SCREENSHOTS", "RECORDINGS" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int idx = i;
                float tx0 = 0.10f + i * 0.155f, tx1 = tx0 + 0.14f;
                Button_(_root, tabs[i], new Vector2(tx0, 0.815f), new Vector2(tx1, 0.865f),
                        () => { if (_tab != idx) { _tab = idx; Build(); } },
                        _tab == i ? Accent : PanelAlt, 14);
            }

            var content = Panel_(_root, "Content", Color.clear, new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.80f));
            if (_tab == 0) BuildScreenshotsTab(content);
            else BuildRecordingsTab(content);
        }

        // ---------------------------------------------------------------- screenshots

        private static string ScreenshotsFolder() => Path.Combine(Application.persistentDataPath, "Screenshots");

        private void BuildScreenshotsTab(Transform panel)
        {
            string folder = ScreenshotsFolder();
            string[] pngFiles = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.png") : new string[0];
            System.Array.Sort(pngFiles);
            System.Array.Reverse(pngFiles); // filenames are timestamp-ordered — newest first

            if (pngFiles.Length == 0)
            {
                Label(panel, "No screenshots yet.\n\nPress F9 in Free Flight to capture one — it shows up here automatically.",
                      16, new Vector2(0.10f, 0.45f), new Vector2(0.90f, 0.70f), TextDim,
                      TMPro.TextAlignmentOptions.Center);
                return;
            }

            const float rowH = 170f, gap = 14f, scrollbarW = 0.02f;
            var (viewport, content, scrollRect) = ScrollList(panel, "Shots",
                new Vector2(0f, 0f), new Vector2(1f - scrollbarW, 1f));

            float totalH = pngFiles.Length * (rowH + gap);
            content.sizeDelta = new Vector2(0f, totalH);

            for (int i = 0; i < pngFiles.Length; i++)
                BuildScreenshotRow(content, pngFiles[i], i * (rowH + gap), rowH);

            float maxScrollY = Mathf.Max(0f, totalH - viewport.rect.height);
            if (maxScrollY > 0f)
            {
                var scrollbar = VScrollbar_(panel, new Vector2(1f - scrollbarW + 0.004f, 0f), new Vector2(1f, 1f));
                scrollRect.verticalScrollbar = scrollbar;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }
        }

        private void BuildScreenshotRow(Transform content, string pngPath, float topY, float height)
        {
            var row = Panel_(content, "Shot_" + Path.GetFileNameWithoutExtension(pngPath), Panel,
                             new Vector2(0f, 1f), new Vector2(1f, 1f),
                             new Vector2(0f, -(topY + height)), new Vector2(0f, -topY));

            var thumbArea = Panel_(row, "Thumb", new Color(0, 0, 0, 0.4f), new Vector2(0.01f, 0.06f), new Vector2(0.28f, 0.94f));
            var tex = LoadThumbnail(pngPath);
            if (tex != null)
            {
                var imgGo = new GameObject("Img", typeof(RawImage));
                imgGo.transform.SetParent(thumbArea, false);
                var rt = (RectTransform)imgGo.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                imgGo.GetComponent<RawImage>().texture = tex;
            }

            string jsonPath = Path.ChangeExtension(pngPath, ".json");
            ScreenshotMeta meta = null;
            if (File.Exists(jsonPath))
            {
                try { meta = JsonUtility.FromJson<ScreenshotMeta>(File.ReadAllText(jsonPath)); }
                catch (System.Exception e) { Debug.LogWarning($"[MediaUI] metadata read failed for {jsonPath}: {e.Message}"); }
            }

            Label(row, Path.GetFileName(pngPath), 14, new Vector2(0.30f, 0.72f), new Vector2(0.90f, 0.94f),
                  TextMain, TMPro.TextAlignmentOptions.MidlineLeft, TMPro.FontStyles.Bold);

            string caption = meta != null
                ? $"{meta.DroneName}  ·  {meta.City}  ·  {FormatWhen(meta.CapturedAtIso)}\nALT {meta.AltitudeM:0} m"
                : "No metadata recorded for this screenshot.";
            Label(row, caption, 13, new Vector2(0.30f, 0.10f), new Vector2(0.90f, 0.70f), TextDim,
                  TMPro.TextAlignmentOptions.MidlineLeft);

            Button_(row, "DELETE", new Vector2(0.905f, 0.30f), new Vector2(0.995f, 0.70f),
                    () => DeleteScreenshot(pngPath, jsonPath), AccentWarn, 12);
        }

        private static Texture2D LoadThumbnail(string path)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes)) return tex;
            }
            catch (System.Exception e) { Debug.LogWarning($"[MediaUI] thumbnail load failed for {path}: {e.Message}"); }
            return null;
        }

        private static string FormatWhen(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "--";
            return System.DateTime.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture,
                       System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
                ? dt.ToString("yyyy-MM-dd HH:mm") : "--";
        }

        private void DeleteScreenshot(string pngPath, string jsonPath)
        {
            try
            {
                if (File.Exists(pngPath)) File.Delete(pngPath);
                if (File.Exists(jsonPath)) File.Delete(jsonPath);
            }
            catch (System.Exception e) { Debug.LogWarning($"[MediaUI] delete failed: {e.Message}"); }
            Build();
        }

        // ---------------------------------------------------------------- recordings

        private void BuildRecordingsTab(Transform panel)
        {
            Label(panel, "No recordings yet.\n\nVideo capture isn't available in this build — Free Flight currently " +
                         "supports screenshots (F9) and the in-flight Instant Replay (F10, playback only, not saved " +
                         "to disk). Recorded clips will appear here once that capability ships.",
                  16, new Vector2(0.10f, 0.45f), new Vector2(0.90f, 0.75f), TextDim,
                  TMPro.TextAlignmentOptions.Center);
        }

        private void Clear() { if (_root != null) Destroy(_root.gameObject); }
    }
}
