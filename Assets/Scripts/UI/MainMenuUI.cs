using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using AeroTerra.Core;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// Main menu: FREE FLIGHT · WORKSHOP · SETTINGS (+ QUIT).
    /// Attach to an empty GameObject in the MainMenu scene (bootstrap does this).
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        private Canvas _canvas;
        private FreeFlightMenuUI _freeFlight;
        private MissionsUI _missions;
        private WorkshopUI _workshop;
        private CreditsUI _credits;
        private SettingsUI _settings;

        private void Start()
        {
            CustomCursor.Apply();
            _canvas = RootCanvas("MainMenuCanvas");
            BuildHome();
            _freeFlight = gameObject.AddComponent<FreeFlightMenuUI>();
            _missions = gameObject.AddComponent<MissionsUI>();
            _workshop = gameObject.AddComponent<WorkshopUI>();
            _credits = gameObject.AddComponent<CreditsUI>();
            _settings = gameObject.AddComponent<SettingsUI>();
            AudioManager.Instance?.PlayMenuMusic();
        }

        private RectTransform _home;
        private RectTransform _exitModal;

        // ---- Keyboard navigation: arrows/Tab move the highlight, Enter activates,
        // Esc opens/closes the exit modal (via the existing PauseAction). No UI-nav
        // InputAction exists project-wide (InputManager is flight-focused), and arrow
        // keys are only ever read by DroneFlightController mid-flight, so polling
        // Keyboard.current directly here — same pattern IntroSceneController already
        // uses to skip the intro — is safe and doesn't collide with flight input. ----
        private Button[] _homeButtons;
        private int _homeIndex;
        private Button[] _modalButtons;
        private int _modalIndex;

        private void Update()
        {
            var im = AeroTerra.Input.InputManager.Instance;
            if (im != null && im.PauseAction.WasPressedThisFrame())
            {
                if (_exitModal != null) CloseExitModal();
                else if (_home.gameObject.activeSelf) ShowExitModal();
                return; // don't also process it as a nav key below
            }

            var kb = Keyboard.current;
            if (kb == null) return;
            if (_exitModal != null) HandleModalNav(kb);
            else if (_home.gameObject.activeSelf) HandleHomeNav(kb);
        }

        private void HandleHomeNav(Keyboard kb)
        {
            if (_homeButtons == null || _homeButtons.Length == 0) return;

            bool prev = kb.upArrowKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame ||
                        (kb.tabKey.wasPressedThisFrame && kb.shiftKey.isPressed);
            bool next = kb.downArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame ||
                        (kb.tabKey.wasPressedThisFrame && !kb.shiftKey.isPressed);
            if (prev || next)
            {
                _homeIndex = (_homeIndex + (prev ? -1 : 1) + _homeButtons.Length) % _homeButtons.Length;
                SetSelectionVisual(_homeButtons, _homeIndex);
                AudioManager.Instance?.PlayButtonHover();
            }

            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                _homeButtons[_homeIndex].onClick.Invoke();
        }

        private void HandleModalNav(Keyboard kb)
        {
            if (_modalButtons == null) return;

            bool nav = kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame ||
                       kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame ||
                       kb.tabKey.wasPressedThisFrame;
            if (nav)
            {
                _modalIndex = 1 - _modalIndex; // only two options — either direction just swaps
                SetSelectionVisual(_modalButtons, _modalIndex);
                AudioManager.Instance?.PlayButtonHover();
            }

            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                _modalButtons[_modalIndex].onClick.Invoke();
        }

        /// <summary>Drives the same "Accent" hover bar Button_ wires up for
        /// PointerEnter/PointerExit, so keyboard selection reads identically to a
        /// mouse hover instead of introducing a second visual language.</summary>
        private static void SetSelectionVisual(Button[] buttons, int selected)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                var accent = buttons[i].transform.Find("Accent")?.GetComponent<Image>();
                if (accent == null) continue;
                accent.color = i == selected
                    ? Accent
                    : new Color(Accent.r, Accent.g, Accent.b, 0f);
            }
        }

        private void ShowExitModal()
        {
            _exitModal = Panel_(_canvas.transform, "ExitModal", new Color(0, 0, 0, 0.75f), Vector2.zero, Vector2.one);
            var box = Panel_(_exitModal, "Box", Panel, new Vector2(0.34f, 0.40f), new Vector2(0.66f, 0.60f));

            Label(box, "EXIT AEROTERRA?", 30, new Vector2(0.05f, 0.60f), new Vector2(0.95f, 0.85f),
                  TextMain, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            Label(box, "Are you sure you want to quit?", 20, new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.58f),
                  TextDim, TMPro.TextAlignmentOptions.Center);

            var quitBtn = Button_(box, "QUIT", new Vector2(0.10f, 0.12f), new Vector2(0.46f, 0.32f),
                    () => GameManager.Instance.QuitGame(), AccentWarn, 22);
            var cancelBtn = Button_(box, "CANCEL", new Vector2(0.54f, 0.12f), new Vector2(0.90f, 0.32f),
                    CloseExitModal, PanelAlt, 22);

            _modalButtons = new[] { quitBtn, cancelBtn };
            _modalIndex = 1; // default to CANCEL — a stray Enter shouldn't quit the game
            SetSelectionVisual(_modalButtons, _modalIndex);
        }

        private void CloseExitModal()
        {
            if (_exitModal != null) Destroy(_exitModal.gameObject);
            _exitModal = null;
            _modalButtons = null;
        }

        private void BuildHome()
        {
            _home = Panel_(_canvas.transform, "Home", Bg, Vector2.zero, Vector2.one);

            // Slowly crossfading photo backdrop + a uniform scrim so the title and
            // buttons below stay legible over any of the three photos.
            _home.gameObject.AddComponent<BackgroundSlider>().Init(_home, new[]
            {
                "Images/Backgrounds/main-menu/slider_1",
                "Images/Backgrounds/main-menu/slider_2",
                "Images/Backgrounds/main-menu/slider_3",
            });
            Panel_(_home, "Scrim", new Color(0f, 0f, 0f, 0.45f), Vector2.zero, Vector2.one);

            var title = Label(_home, "AEROTERRA", 92, new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.90f),
                  TextMain, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
            title.characterSpacing = 8;

            var subtitle = Label(_home, "TRUE-EARTH DRONE FLIGHT SIMULATOR", 24, new Vector2(0.05f, 0.685f),
                  new Vector2(0.95f, 0.735f), Accent, TMPro.TextAlignmentOptions.Center);
            subtitle.characterSpacing = 4;

            Panel_(_home, "Divider", new Color(Accent.r, Accent.g, Accent.b, 0.4f),
                   new Vector2(0.42f, 0.665f), new Vector2(0.58f, 0.668f));

            // Primary actions: even vertical stack, generous spacing so hover accents read clearly.
            var freeFlightBtn = Button_(_home, "FREE FLIGHT", new Vector2(0.34f, 0.505f), new Vector2(0.66f, 0.58f),
                    () => { _home.gameObject.SetActive(false); _freeFlight.Open(ShowHome); }, PanelAlt, 28);
            var missionsBtn = Button_(_home, "MISSIONS", new Vector2(0.34f, 0.415f), new Vector2(0.66f, 0.49f),
                    () => { _home.gameObject.SetActive(false); _missions.Open(ShowHome); }, PanelAlt, 28);
            var workshopBtn = Button_(_home, "WORKSHOP", new Vector2(0.34f, 0.325f), new Vector2(0.66f, 0.40f),
                    () => { _home.gameObject.SetActive(false); _workshop.Open(ShowHome); }, PanelAlt, 28);
            var creditsBtn = Button_(_home, "CREDITS", new Vector2(0.34f, 0.235f), new Vector2(0.66f, 0.31f),
                    () => { _home.gameObject.SetActive(false); _credits.Open(ShowHome); }, PanelAlt, 28);
            var settingsBtn = Button_(_home, "SETTINGS", new Vector2(0.34f, 0.145f), new Vector2(0.66f, 0.22f),
                    () => { _home.gameObject.SetActive(false); _settings.Open(ShowHome); }, PanelAlt, 28);

            // Quit is a secondary action — kept small and out of the primary flow.
            var quitBtn = Button_(_home, "QUIT", new Vector2(0.85f, 0.04f), new Vector2(0.97f, 0.09f),
                    ShowExitModal, AccentWarn, 18);

            Label(_home, "v1.0  ·  © AeroTerra", 16,
                  new Vector2(0.03f, 0.02f), new Vector2(0.7f, 0.06f), TextDim,
                  TMPro.TextAlignmentOptions.Left);

            _homeButtons = new[] { freeFlightBtn, missionsBtn, workshopBtn, creditsBtn, settingsBtn, quitBtn };
            _homeIndex = 0;
            SetSelectionVisual(_homeButtons, _homeIndex);
        }

        public Canvas Canvas => _canvas;

        private void ShowHome()
        {
            _home.gameObject.SetActive(true);
            _homeIndex = 0;
            if (_homeButtons != null) SetSelectionVisual(_homeButtons, _homeIndex);
        }
    }
}
