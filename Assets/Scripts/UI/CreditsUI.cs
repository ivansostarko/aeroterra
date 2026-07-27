using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using AeroTerra.Core;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// Credits screen: studio/attribution info, app version, project website. Static
    /// content only — own background photo (slider_4) and music track (swapped in on
    /// Open, back to menu music on GoBack), same pattern as every other main-menu screen.
    /// </summary>
    public class CreditsUI : MonoBehaviour
    {
        private RectTransform _root;
        private System.Action _onBack;

        private Canvas Canvas => GetComponent<MainMenuUI>().Canvas;

        public void Open(System.Action onBack)
        {
            _onBack = onBack;
            AudioManager.Instance?.PlayCreditsMusic();
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
            AudioManager.Instance?.StopCreditsMusic();
            AudioManager.Instance?.PlayMenuMusic();
            _onBack?.Invoke();
        }

        private void Build()
        {
            Clear();
            _root = Panel_(Canvas.transform, "Credits", Bg, Vector2.zero, Vector2.one);

            _root.gameObject.AddComponent<BackgroundSlider>().Init(_root,
                new[] { "Images/Backgrounds/main-menu/slider_4" });
            Panel_(_root, "Scrim", new Color(0f, 0f, 0f, 0.55f), Vector2.zero, Vector2.one);

            BackButton_(_root, new Vector2(0.02f, 0.90f), new Vector2(0.075f, 0.965f), GoBack);
            var title = Label(_root, "CREDITS", 44, new Vector2(0.10f, 0.86f), new Vector2(0.95f, 0.95f),
                  TextMain, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            title.characterSpacing = 6;

            var box = Panel_(_root, "Box", Panel, new Vector2(0.30f, 0.16f), new Vector2(0.70f, 0.80f));
            Panel_(box, "Stripe", new Color(Accent.r, Accent.g, Accent.b, 0.6f), Vector2.zero, new Vector2(1f, 0.01f));

            float y = 0.95f;
            const float rowH = 0.15f;
            AddRow(box, "CREATED BY", "Teški Život d.o.o. - izbubljeni ali ne zaboravljeni.", ref y, rowH);
            AddRow(box, "MUSIC BY", "Internet", ref y, rowH);
            AddRow(box, "DESIGN BY", "Cloud AI", ref y, rowH);
            AddRow(box, "COPYRIGHT", "© Teški Život d.o.o.", ref y, rowH);
            AddRow(box, "VERSION", Application.version, ref y, rowH);

            const string url = "https://uav.sostarko.me";
            Label(box, "WEBSITE", 12, new Vector2(0.06f, y - 0.045f), new Vector2(0.94f, y),
                  Accent, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            BuildLink(box, url, new Vector2(0.06f, y - rowH), new Vector2(0.94f, y - 0.05f));
        }

        private static void AddRow(Transform box, string caption, string value, ref float y, float rowH)
        {
            Label(box, caption, 12, new Vector2(0.06f, y - 0.045f), new Vector2(0.94f, y),
                  Accent, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            Label(box, value, 19, new Vector2(0.06f, y - rowH), new Vector2(0.94f, y - 0.05f),
                  TextMain, TMPro.TextAlignmentOptions.Center);
            y -= rowH;
        }

        /// <summary>Clickable "<u>url</u>" label — same underline-link styling Cesium's own
        /// credit popup uses. The whole label area is clickable (TextMeshProUGUI is itself a
        /// Graphic, so it can be a Button's targetGraphic directly — no separate hit box needed).</summary>
        private static void BuildLink(Transform parent, string url, Vector2 anchorMin, Vector2 anchorMax)
        {
            var label = Label(parent, $"<u>{url}</u>", 17, anchorMin, anchorMax,
                              Accent, TMPro.TextAlignmentOptions.Center);
            var btn = label.gameObject.AddComponent<Button>();
            btn.targetGraphic = label;
            btn.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayButtonClick();
                Application.OpenURL(url);
            });
            var trigger = label.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, _ => AudioManager.Instance?.PlayButtonHover());
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(d => action(d));
            trigger.triggers.Add(entry);
        }

        private void Clear() { if (_root != null) Destroy(_root.gameObject); }
    }
}
