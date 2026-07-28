using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
        private string _pendingDeletePng, _pendingDeleteJson; // non-null while the delete-confirm modal is up
        private string _viewingImagePng; // non-null while the full-size image viewer modal is up

        private Canvas Canvas => GetComponent<MainMenuUI>().Canvas;

        public void Open(System.Action onBack)
        {
            _onBack = onBack;
            _tab = 0;
            _pendingDeletePng = null;
            _pendingDeleteJson = null;
            _viewingImagePng = null;
            Build();
        }

        private void Update()
        {
            if (_root == null) return;
            var im = AeroTerra.Input.InputManager.Instance;
            if (im == null || !im.PauseAction.WasPressedThisFrame()) return;
            if (_viewingImagePng != null) CloseImageViewer();
            else GoBack();
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

            _root.gameObject.AddComponent<BackgroundSlider>().Init(_root,
                new[] { "Images/Backgrounds/main-menu/slider_5" });
            Panel_(_root, "Scrim", new Color(0f, 0f, 0f, 0.55f), Vector2.zero, Vector2.one);

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

            if (_pendingDeletePng != null) BuildDeleteConfirmOverlay();
            if (_viewingImagePng != null) BuildImageViewerOverlay();
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
            string jsonPath = Path.ChangeExtension(pngPath, ".json");

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
            BuildImageContextMenu(row, thumbArea, pngPath, jsonPath);

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

            BuildDeleteIconButton(row, new Vector2(0.905f, 0.30f), new Vector2(0.995f, 0.70f),
                    () => { _pendingDeletePng = pngPath; _pendingDeleteJson = jsonPath; Build(); });
        }

        /// <summary>Right-click on the thumbnail opens a small popup with "LOCATE IN
        /// FOLDER" / "DELETE" — same self-contained catcher+popup pattern
        /// UIBuilder.Dropdown_ uses (no top-level Build() needed to show/hide it,
        /// except for Delete, which stages the confirm overlay — see
        /// BuildDeleteConfirmOverlay).</summary>
        private void BuildImageContextMenu(Transform row, Transform imageArea, string pngPath, string jsonPath)
        {
            RectTransform ctxMenu = null;
            GameObject ctxCatcher = null;

            void CloseCtx()
            {
                if (ctxMenu != null) { Destroy(ctxMenu.gameObject); ctxMenu = null; }
                if (ctxCatcher != null) { Destroy(ctxCatcher); ctxCatcher = null; }
            }

            void OpenCtx()
            {
                if (ctxMenu != null) { CloseCtx(); return; }

                var catcherRt = Panel_(row, "CtxCatcher", Color.clear, Vector2.zero, Vector2.one);
                ctxCatcher = catcherRt.gameObject;
                var catchBtn = catcherRt.gameObject.AddComponent<Button>();
                catchBtn.transition = Selectable.Transition.None;
                catchBtn.onClick.AddListener(CloseCtx);

                ctxMenu = Panel_(row, "CtxMenu", PanelAlt, new Vector2(0.01f, 0.06f), new Vector2(0.28f, 0.60f));
                Button_(ctxMenu, "LOCATE IN FOLDER", new Vector2(0f, 0.52f), new Vector2(1f, 1f),
                        () => { LocateInFolder(pngPath); CloseCtx(); }, PanelAlt, 10);
                Button_(ctxMenu, "DELETE", new Vector2(0f, 0f), new Vector2(1f, 0.48f),
                        () => { _pendingDeletePng = pngPath; _pendingDeleteJson = jsonPath; Build(); }, AccentWarn, 10);
            }

            var trigger = imageArea.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(data =>
            {
                var button = ((PointerEventData)data).button;
                if (button == PointerEventData.InputButton.Right) OpenCtx();
                else if (button == PointerEventData.InputButton.Left) { _viewingImagePng = pngPath; Build(); }
            });
            trigger.triggers.Add(entry);
        }

        /// <summary>Full-size lightbox for the clicked screenshot — dark backdrop,
        /// aspect-correct image, click anywhere outside the image (or the ✕/Esc) to
        /// dismiss. Same staged-field + rebuild pattern as BuildDeleteConfirmOverlay.</summary>
        private void BuildImageViewerOverlay()
        {
            var overlay = Panel_(_root, "ImageViewer", new Color(0, 0, 0, 0.92f), Vector2.zero, Vector2.one);
            var backdropBtn = overlay.gameObject.AddComponent<Button>();
            backdropBtn.transition = Selectable.Transition.None;
            backdropBtn.onClick.AddListener(CloseImageViewer);

            var frame = Panel_(overlay, "Frame", Color.clear, new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.88f));
            frame.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // swallows clicks on the image itself

            var tex = LoadThumbnail(_viewingImagePng);
            if (tex != null)
            {
                var imgGo = new GameObject("Img", typeof(RawImage), typeof(AspectRatioFitter));
                imgGo.transform.SetParent(frame, false);
                var rt = (RectTransform)imgGo.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                imgGo.GetComponent<RawImage>().texture = tex;
                var fitter = imgGo.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = (float)tex.width / Mathf.Max(1, tex.height);
            }

            Label(overlay, Path.GetFileName(_viewingImagePng), 16, new Vector2(0.08f, 0.90f), new Vector2(0.92f, 0.97f),
                  TextMain, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            Label(overlay, "Click anywhere outside the image (or press Esc) to close", 12,
                  new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.07f), TextDim, TMPro.TextAlignmentOptions.Center);
            Button_(overlay, "✕", new Vector2(0.94f, 0.90f), new Vector2(0.985f, 0.955f), CloseImageViewer, PanelAlt, 18);
        }

        private void CloseImageViewer() { _viewingImagePng = null; Build(); }

        /// <summary>Opens the OS file browser with the file pre-selected where possible
        /// (Windows Explorer, macOS Finder); falls back to just opening the containing
        /// folder elsewhere. No existing pattern for this in the codebase to follow —
        /// Application.OpenURL elsewhere is only ever used for http(s) attribution
        /// links — so this is a new, self-contained usage.</summary>
        private static void LocateInFolder(string filePath)
        {
            try
            {
#if UNITY_STANDALONE_WIN
                System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + filePath.Replace('/', '\\') + "\"");
#elif UNITY_STANDALONE_OSX
                System.Diagnostics.Process.Start("open", "-R \"" + filePath + "\"");
#else
                Application.OpenURL("file://" + Path.GetDirectoryName(filePath));
#endif
            }
            catch (System.Exception e) { Debug.LogWarning($"[MediaUI] locate in folder failed: {e.Message}"); }
        }

        private static Texture2D _deleteIconCache;
        private static bool _deleteIconChecked;

        /// <summary>Loads Assets/Resources/Images/ui/Menu/delete-icon.png once and
        /// caches it; returns null (silently) if the icon hasn't been imported yet,
        /// same fallback spirit as UIBuilder.BackButton_ / WorkshopUI's identical
        /// helper (duplicated locally — that one's private to WorkshopUI).</summary>
        private static Texture2D LoadDeleteIcon()
        {
            if (!_deleteIconChecked)
            {
                _deleteIconChecked = true;
                _deleteIconCache = Resources.Load<Texture2D>("Images/ui/Menu/delete-icon");
            }
            return _deleteIconCache;
        }

        /// <summary>Icon-only delete button — falls back to a plain "✕" text button if
        /// delete-icon.png hasn't been imported yet. Doesn't delete on click; it only
        /// stages _pendingDeletePng/Json and rebuilds to show the confirm overlay.</summary>
        private void BuildDeleteIconButton(Transform parent, Vector2 anchorMin, Vector2 anchorMax, System.Action onClick)
        {
            var icon = LoadDeleteIcon();
            if (icon == null) { Button_(parent, "✕", anchorMin, anchorMax, onClick, AccentWarn, 14); return; }

            var rt = Panel_(parent, "DeleteBtn", AccentWarn, anchorMin, anchorMax, new Vector2(4, 4), new Vector2(-4, -4));
            var iconGo = new GameObject("Icon", typeof(RawImage));
            iconGo.transform.SetParent(rt, false);
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = new Vector2(0.24f, 0.22f); iconRt.anchorMax = new Vector2(0.76f, 0.78f);
            iconRt.offsetMin = Vector2.zero; iconRt.offsetMax = Vector2.zero;
            iconGo.GetComponent<RawImage>().texture = icon;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = rt.GetComponent<Image>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.7f, 0.15f, 0.05f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); onClick?.Invoke(); });
            var trigger = rt.gameObject.AddComponent<EventTrigger>();
            var hover = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            hover.callback.AddListener(_ => AudioManager.Instance?.PlayButtonHover());
            trigger.triggers.Add(hover);
        }

        /// <summary>Confirmation modal for deleting a screenshot — set
        /// _pendingDeletePng/Json then Build() to show it, null them out (Cancel/Delete)
        /// then Build() to dismiss. Same shape as WorkshopUI's BuildDeleteConfirmOverlay.</summary>
        private void BuildDeleteConfirmOverlay()
        {
            var overlay = Panel_(_root, "DeleteConfirm", new Color(0, 0, 0, 0.75f), Vector2.zero, Vector2.one);
            var box = Panel_(overlay, "Box", Panel, new Vector2(0.32f, 0.36f), new Vector2(0.68f, 0.64f));

            var icon = LoadDeleteIcon();
            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RawImage));
                iconGo.transform.SetParent(box, false);
                var iconRt = (RectTransform)iconGo.transform;
                iconRt.anchorMin = new Vector2(0.42f, 0.66f); iconRt.anchorMax = new Vector2(0.58f, 0.90f);
                iconRt.offsetMin = Vector2.zero; iconRt.offsetMax = Vector2.zero;
                iconGo.GetComponent<RawImage>().texture = icon;
            }

            Label(box, $"DELETE “{Path.GetFileName(_pendingDeletePng)}”?", 16, new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.60f),
                  TextMain, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            Label(box, "This screenshot will be permanently deleted. This can't be undone.", 12,
                  new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.40f), TextDim, TMPro.TextAlignmentOptions.Center);

            Button_(box, "CANCEL", new Vector2(0.08f, 0.08f), new Vector2(0.46f, 0.24f),
                    () => { _pendingDeletePng = null; _pendingDeleteJson = null; Build(); }, PanelAlt, 14);
            Button_(box, "DELETE", new Vector2(0.54f, 0.08f), new Vector2(0.92f, 0.24f), () =>
            {
                // Null out BEFORE deleting — DeleteScreenshot() ends with its own
                // Build(), and that rebuild must not see a still-pending delete for a
                // file that's already gone (which would just re-show this same modal).
                string png = _pendingDeletePng, json = _pendingDeleteJson;
                _pendingDeletePng = null;
                _pendingDeleteJson = null;
                DeleteScreenshot(png, json);
            }, AccentWarn, 14);
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
