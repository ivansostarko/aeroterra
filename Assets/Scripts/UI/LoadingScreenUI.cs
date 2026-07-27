using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// Full-screen overlay shown while a scene loads asynchronously (Main Menu &lt;-&gt; Flight).
    /// Lives on the GameManager object (DontDestroyOnLoad) so it survives the scene switch it covers.
    /// </summary>
    public class LoadingScreenUI : MonoBehaviour
    {
        /// <summary>Per-city loader photo, keyed by MapDefinition.Id. Files live at
        /// Assets/Resources/Images/Backgrounds/open-fly/{value}.png.</summary>
        private static readonly System.Collections.Generic.Dictionary<string, string> MapBackgrounds =
            new System.Collections.Generic.Dictionary<string, string>
        {
            { "barcelona", "barcelona_loader_background" },
            { "dubai", "dubai_loader_background" },
            { "london", "london_loader_background" },
            { "new-york", "new_york_loader_background" },
            { "paris", "paris_loader_background" },
            { "riyadh", "riyadh_loader_background" },
            { "tokyo", "tokyo_loader_background" },
            { "zagreb", "zagreb_loader_background" },
        };

        public static LoadingScreenUI Instance { get; private set; }

        private Canvas _canvas;
        private RectTransform _fill;
        private TextMeshProUGUI _statusLabel;
        private TextMeshProUGUI _percentLabel;
        private RectTransform _spinner;
        private GameObject _bgImageGo;
        private RawImage _bgImage;
        private RectTransform _scrim;

        public static LoadingScreenUI GetOrCreate(GameObject host)
        {
            if (Instance != null) return Instance;
            Instance = host.AddComponent<LoadingScreenUI>();
            return Instance;
        }

        public void Show(string label, string mapId)
        {
            if (_canvas == null) Build();
            _canvas.gameObject.SetActive(true);

            Texture2D bg = mapId != null && MapBackgrounds.TryGetValue(mapId, out var file)
                ? Resources.Load<Texture2D>("Images/Backgrounds/open-fly/" + file)
                : null;
            _bgImage.texture = bg;
            _bgImageGo.SetActive(bg != null);
            _scrim.gameObject.SetActive(bg != null);

            _statusLabel.text = string.IsNullOrEmpty(label) ? "LOADING" : $"LOADING {label.ToUpperInvariant()}";
            SetProgress(0f);
        }

        public void SetProgress(float t)
        {
            t = Mathf.Clamp01(t);
            _fill.anchorMax = new Vector2(t, 1f);
            _percentLabel.text = Mathf.RoundToInt(t * 100f) + "%";
        }

        public void Hide()
        {
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_spinner != null && _canvas.gameObject.activeSelf)
                _spinner.Rotate(0f, 0f, -140f * Time.unscaledDeltaTime);
        }

        private void Build()
        {
            _canvas = RootCanvas("LoadingCanvas");
            _canvas.sortingOrder = 1000;
            DontDestroyOnLoad(_canvas.gameObject);

            var panel = Panel_(_canvas.transform, "LoadingBg", Bg, Vector2.zero, Vector2.one);

            _bgImageGo = new GameObject("LoadingBgImage", typeof(RawImage));
            _bgImageGo.transform.SetParent(panel, false);
            var bgRt = _bgImageGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            _bgImage = _bgImageGo.GetComponent<RawImage>();
            _bgImageGo.SetActive(false);

            _scrim = Panel_(panel, "Scrim", new Color(0.02f, 0.03f, 0.05f, 0.55f), Vector2.zero, Vector2.one);
            _scrim.gameObject.SetActive(false);

            Label(panel, "AEROTERRA", 60, new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.68f),
                  TextMain, TextAlignmentOptions.Center, FontStyles.Bold);

            _spinner = Panel_(panel, "Spinner", Accent, new Vector2(0.5f, 0.535f), new Vector2(0.5f, 0.535f));
            _spinner.sizeDelta = new Vector2(14, 14);

            _statusLabel = Label(panel, "LOADING", 24, new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.50f),
                  Accent, TextAlignmentOptions.Center);

            // Bar "housing": a soft black scrim behind the track gives the bar a floating,
            // backlit look over any loader photo instead of sitting flush against it.
            var barHousing = Panel_(panel, "ProgressBarHousing", new Color(0f, 0f, 0f, 0.45f),
                  new Vector2(0.29f, 0.372f), new Vector2(0.71f, 0.428f));
            var track = Panel_(barHousing, "ProgressTrack", PanelAlt, new Vector2(0.015f, 0.18f), new Vector2(0.985f, 0.82f));
            _fill = Panel_(track, "Fill", Accent, Vector2.zero, new Vector2(0f, 1f));

            _percentLabel = Label(panel, "0%", 16, new Vector2(0.30f, 0.335f), new Vector2(0.70f, 0.365f),
                  TextDim, TextAlignmentOptions.Center);

            _canvas.gameObject.SetActive(false);
        }
    }
}
