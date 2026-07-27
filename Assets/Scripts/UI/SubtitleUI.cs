using UnityEngine;
using TMPro;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// Bottom-center subtitle bar for mission narration (intro/weather voice lines).
    /// Sits just above FlightHUD's telemetry bar and is a sibling of it, not a child —
    /// so toggling Settings ▸ Game ▸ Show HUD doesn't also hide narration subtitles.
    /// Visibility is gated by the caller checking Settings.SubtitlesEnabled before
    /// calling Show(); voice audio itself always plays regardless of that setting.
    /// </summary>
    public class SubtitleUI : MonoBehaviour
    {
        private RectTransform _panel;
        private TextMeshProUGUI _label;

        public void Init(Canvas canvas)
        {
            _panel = Panel_(canvas.transform, "Subtitle", new Color(0f, 0f, 0f, 0.6f),
                             new Vector2(0.18f, 0.135f), new Vector2(0.82f, 0.195f));
            _label = Label(_panel, "", 20, Vector2.zero, Vector2.one, TextMain,
                           TextAlignmentOptions.Center, FontStyles.Bold);
            _panel.gameObject.SetActive(false);
        }

        public void Show(string text)
        {
            if (_panel == null) return;
            _label.text = text;
            _panel.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_panel != null) _panel.gameObject.SetActive(false);
        }
    }
}
