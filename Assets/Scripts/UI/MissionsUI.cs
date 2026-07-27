using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using AeroTerra.Core;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// Missions hub: four mode cards (Training / Cargo Delivery / Combat Missions /
    /// Racing) — each opens its own detail screen. None of the four modes are built
    /// yet, so the detail screen is a shared "under development" placeholder rather
    /// than four near-identical stubs; swap BuildDetail's content out for a real
    /// mode-specific screen as each one gets implemented.
    /// </summary>
    public class MissionsUI : MonoBehaviour
    {
        private enum Screen { Hub, Detail }

        private readonly struct Category
        {
            public readonly string Id, DisplayName, ArtFile;
            public Category(string id, string displayName, string artFile)
            {
                Id = id; DisplayName = displayName; ArtFile = artFile;
            }
        }

        private static readonly Category[] Categories =
        {
            new Category("training", "TRAINING", "training"),
            new Category("cargo", "CARGO DELIVERY", "cargo"),
            new Category("combat", "COMBAT MISSIONS", "combat"),
            new Category("racing", "RACING", "racing"),
        };

        private RectTransform _root;
        private System.Action _onBack;
        private Screen _screen;
        private Category _selected;

        private Canvas Canvas => GetComponent<MainMenuUI>().Canvas;

        public void Open(System.Action onBack)
        {
            _onBack = onBack;
            BuildHub();
        }

        private void Update()
        {
            if (_root == null) return;
            var im = AeroTerra.Input.InputManager.Instance;
            if (im != null && im.PauseAction.WasPressedThisFrame()) GoBack();
        }

        private void GoBack()
        {
            if (_screen == Screen.Detail) { BuildHub(); return; }
            Clear();
            _onBack?.Invoke();
        }

        // ---------- Hub: four mode cards ----------
        private void BuildHub()
        {
            Clear();
            _screen = Screen.Hub;
            _root = Panel_(Canvas.transform, "Missions_Hub", Bg, Vector2.zero, Vector2.one);
            Label(_root, "MISSIONS — SELECT MODE", 44, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.97f),
                  TextMain, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

            const float areaX0 = 0.05f, areaX1 = 0.95f, areaY0 = 0.14f, areaY1 = 0.82f;
            const float gap = 0.015f;
            float cellW = (areaX1 - areaX0 - (Categories.Length - 1) * gap) / Categories.Length;

            for (int i = 0; i < Categories.Length; i++)
            {
                float x0 = areaX0 + i * (cellW + gap), x1 = x0 + cellW;
                BuildCard(Categories[i], x0, x1, areaY0, areaY1);
            }

            Button_(_root, "< BACK", new Vector2(0.03f, 0.03f), new Vector2(0.15f, 0.1f), GoBack);
        }

        private void BuildCard(Category cat, float x0, float x1, float y0, float y1)
        {
            var card = Panel_(_root, "Card_" + cat.Id, Panel, new Vector2(x0, y0), new Vector2(x1, y1));

            var art = Resources.Load<Sprite>("Images/Backgrounds/missions/" + cat.ArtFile);
            var iconGo = new GameObject("Art", typeof(Image));
            iconGo.transform.SetParent(card, false);
            var icon = iconGo.GetComponent<Image>();
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = Vector2.zero; iconRt.offsetMax = Vector2.zero;
            if (art != null)
            {
                icon.sprite = art;
                Panel_(card, "Scrim", new Color(0f, 0f, 0f, 0.30f), Vector2.zero, Vector2.one); // keeps the title legible over any art
            }
            else
            {
                icon.color = PanelAlt; // flat fallback if the art hasn't been imported yet
            }

            Panel_(card, "Bottom", new Color(0f, 0f, 0f, 0.55f), Vector2.zero, new Vector2(1f, 0.22f));
            Label(card, cat.DisplayName, 19, new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.20f),
                  TextMain, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

            var btn = card.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = Accent;
            btn.colors = colors;
            btn.targetGraphic = icon;
            var picked = cat;
            btn.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayButtonClick();
                _selected = picked;
                BuildDetail();
            });
            var trigger = card.gameObject.AddComponent<EventTrigger>();
            var hover = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            hover.callback.AddListener(_ => AudioManager.Instance?.PlayButtonHover());
            trigger.triggers.Add(hover);
        }

        // ---------- Detail: shared "under development" placeholder ----------
        private void BuildDetail()
        {
            Clear();
            _screen = Screen.Detail;
            _root = Panel_(Canvas.transform, "Missions_Detail", Bg, Vector2.zero, Vector2.one);

            Label(_root, _selected.DisplayName, 44, new Vector2(0.05f, 0.56f), new Vector2(0.95f, 0.66f),
                  TextMain, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            Label(_root, "UNDER DEVELOPMENT", 20, new Vector2(0.05f, 0.46f), new Vector2(0.95f, 0.54f),
                  Accent, TMPro.TextAlignmentOptions.Center);

            Button_(_root, "< BACK", new Vector2(0.03f, 0.03f), new Vector2(0.15f, 0.1f), GoBack);
        }

        private void Clear() { if (_root != null) Destroy(_root.gameObject); }
    }
}
