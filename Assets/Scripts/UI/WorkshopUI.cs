using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using AeroTerra.Drone;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// Workshop screen — a professional UAV showroom:
    ///  · left rail lists every registered airframe (hangar roster),
    ///  · center is an interactive 3D stage (drag to rotate, scroll to zoom)
    ///    with live performance chips,
    ///  · right panel has SPECS / LOADOUT / SAVED tabs: full specification
    ///    sheet, power cell/fuel + payload + skin + additional loadout
    ///    configuration, and management of saved builds (load / delete)
    ///    that are usable in Free Flight.
    /// </summary>
    public class WorkshopUI : MonoBehaviour
    {
        private const float RailX1 = 0.22f;    // left hangar rail width
        private const float SideX0 = 0.70f;    // right panel start
        private const float HeaderY0 = 0.92f;  // header bar bottom

        private RectTransform _root;
        private System.Action _onBack;
        private Workshop.WorkshopController _ctrl;
        private TMPro.TMP_InputField _nameField;
        private Camera _wsCam;
        private RenderTexture _stageRT;        // stage camera renders here, not to the screen — see SetupStage
        private GameObject _stageRig;          // lights + pedestal, destroyed on close
        private int _tab;                      // 0 = specs, 1 = loadout, 2 = saved
        private RectTransform _hangarContent;   // last hangar ScrollList content — read for its live
                                                 // scroll offset right before each rebuild replaces it

        // 3D stage camera orbit
        private float _camDist = 2.9f;
        private static readonly Vector3 CamTarget = new Vector3(0f, 1.15f, 0f);
        private static readonly Vector3 CamDir = new Vector3(0.25f, 0.38f, -1f).normalized;

        // Live-updating readouts (null when their tab isn't visible)
        private TMPro.TextMeshProUGUI _chipEndurance, _chipRange, _chipAuw;
        private TMPro.TextMeshProUGUI _powerLine, _massLine, _saveFeedback, _headerCounts;

        private Canvas Canvas => GetComponent<MainMenuUI>().Canvas;

        public void Open(System.Action onBack)
        {
            _onBack = onBack;
            _tab = 0;
            _framedIndex = -1;
            SetupStage();
            Build();
        }

        private void Update()
        {
            if (_root == null) return;
            var im = AeroTerra.Input.InputManager.Instance;
            if (im != null && im.PauseAction.WasPressedThisFrame()) Close();
        }

        // ---------------------------------------------------------------- stage

        private void SetupStage()
        {
            _ctrl = FindFirstObjectByType<Workshop.WorkshopController>();
            if (_ctrl == null)
            {
                var go = new GameObject("WorkshopController");
                _ctrl = go.AddComponent<Workshop.WorkshopController>();
                _ctrl.EnsureDrones();
                var dp = new GameObject("DisplayPoint");
                dp.transform.position = new Vector3(0, 1.2f, 0);
                _ctrl.DisplayPoint = dp.transform;
            }
            // Controller.Start() only runs next frame — make sure a drone is staged
            // before Build() reads CurrentSpec/Working.
            if (_ctrl.Working == null) _ctrl.Show(0);

            if (_wsCam == null)
            {
                var camGo = new GameObject("WorkshopCamera");
                _wsCam = camGo.AddComponent<Camera>();
                _wsCam.clearFlags = CameraClearFlags.SolidColor;
                _wsCam.backgroundColor = new Color(0.03f, 0.045f, 0.07f);
                // Renders to its own texture instead of straight to the screen. Two
                // cameras (this one plus the menu scene's own) both drawing directly to
                // the backbuffer left the Screen Space Overlay UI Canvas invisible under
                // URP — the whole 2D UI still built and stayed interactive (clicks landed
                // correctly), it just never got drawn. Displaying the texture via a
                // RawImage inside the SAME Canvas as everything else (see BuildViewport)
                // removes that camera-vs-Canvas compositing ambiguity entirely.
                int rtW = Mathf.Max(2, Mathf.RoundToInt(Screen.width * (SideX0 - RailX1)));
                int rtH = Mathf.Max(2, Mathf.RoundToInt(Screen.height * HeaderY0));
                _stageRT = new RenderTexture(rtW, rtH, 24) { name = "WorkshopStageRT" };
                _wsCam.targetTexture = _stageRT;
                UpdateCamera();
            }

            if (_stageRig == null)
            {
                _stageRig = new GameObject("WorkshopStage");

                var key = new GameObject("KeyLight").AddComponent<Light>();
                key.type = LightType.Directional; key.intensity = 1.2f;
                key.transform.SetParent(_stageRig.transform);
                key.transform.rotation = Quaternion.Euler(40f, -35f, 0);

                var fill = new GameObject("FillLight").AddComponent<Light>();
                fill.type = LightType.Directional; fill.intensity = 0.4f;
                fill.color = new Color(0.6f, 0.75f, 1f);
                fill.transform.SetParent(_stageRig.transform);
                fill.transform.rotation = Quaternion.Euler(10f, 150f, 0);

                // Display pedestal: dark disc + faint accent ring under the hovering drone.
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "Pedestal";
                Destroy(disc.GetComponent<Collider>());
                disc.transform.SetParent(_stageRig.transform);
                disc.transform.localPosition = new Vector3(0, 0.5f, 0);
                disc.transform.localScale = new Vector3(1.9f, 0.035f, 1.9f);
                disc.GetComponent<Renderer>().sharedMaterial =
                    Procedural.DroneMeshBuilder.MakeMat(new Color(0.09f, 0.11f, 0.15f), 0.3f, 0.35f);

                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "PedestalRing";
                Destroy(ring.GetComponent<Collider>());
                ring.transform.SetParent(_stageRig.transform);
                ring.transform.localPosition = new Vector3(0, 0.485f, 0);
                ring.transform.localScale = new Vector3(2.15f, 0.015f, 2.15f);
                ring.GetComponent<Renderer>().sharedMaterial =
                    Procedural.DroneMeshBuilder.MakeMat(new Color(Accent.r * 0.5f, Accent.g * 0.5f, Accent.b * 0.5f), 0.1f, 0.7f);
            }
        }

        private void UpdateCamera()
        {
            if (_wsCam == null) return;
            _wsCam.transform.position = CamTarget + CamDir * _camDist;
            _wsCam.transform.LookAt(CamTarget);
        }

        /// <summary>Furthest the stage camera is allowed to back away to. A flat 4.8m cap
        /// used to clip the largest airframes (the fleet now runs up to a 12 m UCAV) out of
        /// frame even at max zoom-out — scale with the actual model instead so every drone,
        /// current or future, can always be framed in full.</summary>
        private float MaxCamDist => Mathf.Max(6f, _ctrl.ModelRadius * 3f);

        private void OnScroll(float delta)
        {
            delta = Mathf.Clamp(delta, -3f, 3f);
            _camDist = Mathf.Clamp(_camDist - delta * 0.25f, 1.0f, MaxCamDist);
            UpdateCamera();
        }

        // ---------------------------------------------------------------- build

        private int _framedIndex = -1;

        private void Build()
        {
            Clear();
            var spec = _ctrl.CurrentSpec;

            // Auto-frame the camera when the displayed drone changes — the fleet
            // ranges from a 0.6 m quad to a 3.5 m UCAV. Tab switches keep the zoom.
            if (_framedIndex != _ctrl.CurrentIndex)
            {
                _framedIndex = _ctrl.CurrentIndex;
                _camDist = Mathf.Clamp(_ctrl.ModelRadius * 2.2f, 1.0f, MaxCamDist);
                UpdateCamera();
            }

            _root = Panel_(Canvas.transform, "Workshop", Color.clear, Vector2.zero, Vector2.one);

            BuildViewport();
            BuildHeader(spec);
            BuildHangarRail();
            BuildStageOverlays(spec);
            BuildSidePanel(spec);

            // Same bottom-left corner used by every other menu in the game.
            Button_(_root, "< BACK", new Vector2(0.02f, 0.02f), new Vector2(0.145f, 0.085f), Close);

            RefreshLive();
        }

        private void BuildHeader(DroneSpecification spec)
        {
            var header = Panel_(_root, "Header", Bg, new Vector2(0f, HeaderY0), Vector2.one);
            var title = Label(header, "WORKSHOP", 30, new Vector2(0.015f, 0.05f), new Vector2(0.16f, 0.95f),
                              TextMain, TMPro.TextAlignmentOptions.MidlineLeft, TMPro.FontStyles.Bold);
            title.characterSpacing = 4;
            Panel_(header, "Divider", new Color(Accent.r, Accent.g, Accent.b, 0.5f),
                   new Vector2(0.148f, 0.25f), new Vector2(0.1495f, 0.75f));
            Label(header, "UAV SHOWROOM · SPECIFICATIONS · LOADOUT · SKINS", 15,
                  new Vector2(0.158f, 0.05f), new Vector2(0.62f, 0.95f), TextDim,
                  TMPro.TextAlignmentOptions.MidlineLeft);
            _headerCounts = Label(header, "", 14, new Vector2(0.62f, 0.05f), new Vector2(0.985f, 0.95f),
                                  TextDim, TMPro.TextAlignmentOptions.MidlineRight);
        }

        // ----- left rail: every registered airframe -----

        private void BuildHangarRail()
        {
            var rail = Panel_(_root, "Hangar", Bg, Vector2.zero, new Vector2(RailX1, HeaderY0));
            Label(rail, "HANGAR", 18, new Vector2(0.08f, 0.945f), new Vector2(0.92f, 0.99f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            Label(rail, "ALL REGISTERED AIRFRAMES — SCROLL TO BROWSE", 11,
                  new Vector2(0.08f, 0.915f), new Vector2(0.92f, 0.945f), TextDim);

            // Real ScrollRect (mouse-wheel + drag) rather than a hand-rolled offset that
            // rebuilt the ENTIRE Workshop screen on every wheel tick — that teardown/
            // rebuild (destroying and recreating the 3D stage overlays, spec panels,
            // input fields, etc. every tick) is what made scrolling feel broken. The
            // roster outgrew one column a while back (12 stock airframes now) so this
            // has to actually scroll, not just fit-or-clip.
            var drones = _ctrl.BaseDrones;
            const float cardH = 92f, gap = 10f; // pixels
            const float listTop = 0.895f, listBottom = 0.03f, scrollbarW = 0.02f;

            var (viewport, content, scrollRect) = ScrollList(rail, "Hangar",
                new Vector2(0f, listBottom), new Vector2(1f - scrollbarW, listTop));

            float totalH = drones.Length * cardH + Mathf.Max(0, drones.Length - 1) * gap;
            content.sizeDelta = new Vector2(0f, totalH);

            // content.anchoredPosition.y is <= 0 once scrolled down (top-pivoted content
            // moving up to reveal lower rows), so the clamp range is [-maxScrollY, 0].
            // Restored by reading the OLD content's live position — Clear() (called at
            // the top of Build()) already Destroy()'d it, but Destroy() is deferred to
            // end of frame, so it's still fully valid to read from right here.
            float maxScrollY = Mathf.Max(0f, totalH - viewport.rect.height);
            float restoreY = _hangarContent != null
                ? Mathf.Clamp(_hangarContent.anchoredPosition.y, -maxScrollY, 0f) : 0f;
            content.anchoredPosition = new Vector2(0f, restoreY);
            _hangarContent = content;

            for (int i = 0; i < drones.Length; i++)
                BuildHangarCard(content, drones[i], i, i * (cardH + gap), cardH);

            if (maxScrollY > 0f)
            {
                var scrollbar = VScrollbar_(rail, new Vector2(1f - scrollbarW + 0.004f, listBottom), new Vector2(1f, listTop));
                scrollRect.verticalScrollbar = scrollbar;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }
        }

        private void BuildHangarCard(Transform content, DroneSpecification spec, int index, float topY, float height)
        {
            bool selected = index == _ctrl.CurrentIndex;
            var card = Panel_(content, "Card_" + spec.Id, selected ? PanelAlt : Panel,
                              new Vector2(0.04f, 1f), new Vector2(0.96f, 1f),
                              new Vector2(0f, -(topY + height)), new Vector2(0f, -topY));

            Panel_(card, "Stripe", selected ? Accent : new Color(1, 1, 1, 0.06f),
                   Vector2.zero, new Vector2(0.02f, 1f));

            Label(card, spec.DisplayName, 19, new Vector2(0.08f, 0.60f), new Vector2(0.94f, 0.95f),
                  selected ? TextMain : TextDim, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            Label(card, $"{spec.ClassLabel()} · {spec.RotorCount} ROTORS", 12,
                  new Vector2(0.08f, 0.36f), new Vector2(0.94f, 0.58f),
                  selected ? Accent : TextDim);
            Label(card, $"{spec.MaxSpeedKmh:0} km/h · {spec.EmptyMassKg:0.#} kg · {spec.MaxPayloadKg:0.#} kg payload",
                  12, new Vector2(0.08f, 0.08f), new Vector2(0.94f, 0.34f), TextDim);

            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card.GetComponent<Image>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = Accent;
            btn.colors = colors;
            int idx = index;
            btn.onClick.AddListener(() =>
            {
                Core.AudioManager.Instance?.PlayButtonClick();
                if (idx != _ctrl.CurrentIndex) { _ctrl.Show(idx); Build(); }
            });
            var trigger = card.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter,
                       _ => Core.AudioManager.Instance?.PlayButtonHover());
        }

        // ----- center: interactive 3D stage -----

        private void BuildViewport()
        {
            // Shows the stage camera's RenderTexture (see SetupStage) and feeds
            // drag/scroll to the controller, same as the invisible surface this replaced.
            var surfGo = new GameObject("Viewport", typeof(RawImage));
            surfGo.transform.SetParent(_root, false);
            var surf = (RectTransform)surfGo.transform;
            surf.anchorMin = new Vector2(RailX1, 0f);
            surf.anchorMax = new Vector2(SideX0, HeaderY0);
            surf.offsetMin = Vector2.zero; surf.offsetMax = Vector2.zero;
            surfGo.GetComponent<RawImage>().texture = _stageRT;

            var trigger = surf.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.BeginDrag, _ => _ctrl.BeginDrag());
            AddTrigger(trigger, EventTriggerType.Drag, d => _ctrl.DragBy(((PointerEventData)d).delta));
            AddTrigger(trigger, EventTriggerType.EndDrag, _ => _ctrl.EndDrag());
            AddTrigger(trigger, EventTriggerType.Scroll, d => OnScroll(((PointerEventData)d).scrollDelta.y));
        }

        private void BuildStageOverlays(DroneSpecification spec)
        {
            const float x0 = RailX1 + 0.02f, x1 = SideX0 - 0.02f;

            var name = Label(_root, spec.DisplayName.ToUpper(), 40, new Vector2(x0, 0.825f), new Vector2(x1, 0.90f),
                             TextMain, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            name.characterSpacing = 3; name.raycastTarget = false;
            var sub = Label(_root, $"{spec.Manufacturer}  ·  {spec.ClassLabel()} UAV", 16,
                            new Vector2(x0, 0.79f), new Vector2(x1, 0.828f), Accent);
            sub.raycastTarget = false;

            // Live performance chips.
            const int chips = 4; const float chipGap = 0.01f;
            float chipW = (x1 - x0 - (chips - 1) * chipGap) / chips;
            _chipEndurance = Chip(x0 + 0 * (chipW + chipGap), chipW, "ENDURANCE");
            _chipRange     = Chip(x0 + 1 * (chipW + chipGap), chipW, "MAX RANGE");
            var speed      = Chip(x0 + 2 * (chipW + chipGap), chipW, "TOP SPEED");
            _chipAuw       = Chip(x0 + 3 * (chipW + chipGap), chipW, "ALL-UP WEIGHT");
            speed.text = $"{spec.MaxSpeedKmh:0} KM/H";

            var hint = Label(_root, "◐  DRAG TO ROTATE   ·   SCROLL TO ZOOM", 13,
                             new Vector2(x0, 0.015f), new Vector2(x1, 0.055f), TextDim,
                             TMPro.TextAlignmentOptions.Center);
            hint.raycastTarget = false;
        }

        private TMPro.TextMeshProUGUI Chip(float x0, float w, string title)
        {
            var chip = Panel_(_root, "Chip_" + title, new Color(Bg.r, Bg.g, Bg.b, 0.72f),
                              new Vector2(x0, 0.07f), new Vector2(x0 + w, 0.145f));
            chip.GetComponent<Image>().raycastTarget = false;
            var t = Label(chip, title, 11, new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.92f), TextDim);
            t.raycastTarget = false;
            var v = Label(chip, "—", 17, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.52f),
                          TextMain, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            v.raycastTarget = false;
            return v;
        }

        // ----- right panel: SPECS / LOADOUT / SAVED tabs -----

        private void BuildSidePanel(DroneSpecification spec)
        {
            var panel = Panel_(_root, "Side", Bg, new Vector2(SideX0, 0f), new Vector2(1f, HeaderY0));

            string[] tabs = { "SPECS", "LOADOUT", "SAVED" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int idx = i;
                float tx0 = 0.04f + i * 0.31f, tx1 = tx0 + 0.29f;
                Button_(panel, tabs[i], new Vector2(tx0, 0.945f), new Vector2(tx1, 0.99f),
                        () => { if (_tab != idx) { _tab = idx; Build(); } },
                        _tab == i ? Accent : PanelAlt, 15);
            }

            switch (_tab)
            {
                case 0: BuildSpecsTab(panel, spec); break;
                case 1: BuildLoadoutTab(panel, spec); break;
                default: BuildSavedTab(panel); break;
            }
        }

        // Full specification sheet — every meaningful stat on the DroneSpecification.
        private void BuildSpecsTab(Transform panel, DroneSpecification spec)
        {
            Label(panel, spec.Description, 14, new Vector2(0.06f, 0.855f), new Vector2(0.94f, 0.935f), TextDim);

            SectionHeader(panel, "RATINGS", 0.835f);
            float ratingY1 = 0.795f;
            const float ratingRowH = 0.034f;
            foreach (var (label, stars) in spec.StarRatings())
            {
                Label(panel, label, 13, new Vector2(0.06f, ratingY1 - ratingRowH), new Vector2(0.40f, ratingY1),
                      TextDim, TMPro.TextAlignmentOptions.MidlineLeft);
                StarRow(panel, stars, 5, new Vector2(0.44f, ratingY1 - ratingRowH + 0.005f), new Vector2(0.94f, ratingY1 - 0.005f));
                ratingY1 -= ratingRowH;
            }

            var rows = new (string label, string value)[]
            {
                ("CLASS",              spec.ClassLabel()),
                ("EMPTY MASS",         $"{spec.EmptyMassKg:0.#} kg"),
                ("WINGSPAN",           $"{spec.WingspanM:0.##} m"),
                ("ROTORS",             $"{spec.RotorCount}"),
                ("AIRFRAME INTEGRITY", $"{spec.AirframeHP:0} HP"),
                ("TOP SPEED",          $"{spec.MaxSpeedKmh:0} km/h"),
                ("CLIMB RATE",         $"{spec.MaxAscentRateMs:0.#} m/s"),
                ("SERVICE CEILING",    $"{spec.MaxAltitudeM:0} m"),
                ("MAX THRUST",         $"{spec.MaxThrustN:0} N"),
                ("THRUST / WEIGHT",    $"{spec.ThrustToWeightRatio:0.0}×"),
                ("PITCH/ROLL TORQUE",  $"{spec.PitchRollTorque:0.#} N·m"),
                ("YAW TORQUE",         $"{spec.YawTorque:0.#} N·m"),
                ("CRUISE POWER",       $"{spec.CruisePowerW:0} W"),
                (spec.PowerSystem == PowerSystemType.Fuel ? "FUEL TANK OPTIONS" : "BATTERY OPTIONS",
                    spec.PowerSystem == PowerSystemType.Fuel
                        ? $"{string.Join(" / ", spec.FuelOptionsL)} L"
                        : $"{string.Join(" / ", spec.BatteryOptionsWh)} Wh"),
                ("PAYLOAD TYPE",       spec.PayloadTypeName),
                ("PAYLOAD OPTIONS",    $"{string.Join(" / ", spec.PayloadOptionsKg)} kg"),
                ("HARDPOINTS",         $"{spec.PayloadHardpoints}"),
                ("CAMERAS",            $"{spec.MaxCameras} ({spec.CameraLoadoutSummary()})"),
            };

            // Slightly tighter than the rating rows above (0.034) — two extra rows
            // (HARDPOINTS/CAMERAS) were added to this table and it was already using
            // nearly its full vertical budget down to the panel's bottom edge.
            const float rowH = 0.032f;
            float tableTop = ratingY1 - 0.015f;
            for (int i = 0; i < rows.Length; i++)
            {
                float ry1 = tableTop - i * rowH, ry0 = ry1 - rowH;
                if (i % 2 == 0)
                    Panel_(panel, "RowBg", new Color(1, 1, 1, 0.035f),
                           new Vector2(0.04f, ry0), new Vector2(0.96f, ry1));
                Label(panel, rows[i].label, 13, new Vector2(0.06f, ry0), new Vector2(0.55f, ry1),
                      TextDim, TMPro.TextAlignmentOptions.MidlineLeft);
                Label(panel, rows[i].value, 15, new Vector2(0.45f, ry0), new Vector2(0.94f, ry1),
                      TextMain, TMPro.TextAlignmentOptions.MidlineRight, TMPro.FontStyles.Bold);
            }
        }

        // Power cell (battery or fuel), payload, skin, additional loadout, save.
        private void BuildLoadoutTab(Transform panel, DroneSpecification spec)
        {
            bool fuelPowered = spec.PowerSystem == PowerSystemType.Fuel;

            SectionHeader(panel, fuelPowered ? "FUEL TANK" : "POWER CELL", 0.885f);
            _powerLine = Label(panel, "", 12, new Vector2(0.06f, 0.700f), new Vector2(0.94f, 0.733f), TextDim);
            if (fuelPowered)
                BuildFuelCards(panel, spec, 0.740f, 0.825f);
            else
                BuildBatteryCards(panel, spec, 0.740f, 0.825f);
            RefreshPowerLine(spec);

            SectionHeader(panel, $"PAYLOAD — {spec.PayloadTypeName.ToUpper()}", 0.655f);
            BuildPayloadRow(panel, spec, 0.565f, 0.615f);
            _massLine = Label(panel, "", 13, new Vector2(0.06f, 0.525f), new Vector2(0.94f, 0.563f), TextDim);

            // Only offered when the current airframe has a payload model to show.
            if (_ctrl.HasPayloadVisual)
                Toggle_(panel, "Display payload on model", new Vector2(0.06f, 0.478f), new Vector2(0.94f, 0.518f),
                        _ctrl.ShowPayload, v => _ctrl.SetShowPayload(v), 13);

            SectionHeader(panel, "SKIN", 0.440f);
            BuildSkinCards(panel, spec, 0.335f, 0.410f);

            SectionHeader(panel, "ADDITIONAL LOADOUT", 0.300f);
            Toggle_(panel, "Smoke screen", new Vector2(0.06f, 0.240f), new Vector2(0.48f, 0.280f),
                    _ctrl.Working.SmokeScreenEquipped, v => { _ctrl.SetSmokeScreen(v); RefreshLive(); }, 13);
            Label(panel, $"+{LoadoutExtras.SmokeScreenKg:0.##} kg", 12, new Vector2(0.50f, 0.240f), new Vector2(0.68f, 0.280f), TextDim);
            Label(panel, "COMMS", 12, new Vector2(0.06f, 0.205f), new Vector2(0.40f, 0.235f), Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            BuildCommsCards(panel, 0.115f, 0.195f);

            _nameField = Input_(panel, "Configuration name…", new Vector2(0.06f, 0.058f), new Vector2(0.94f, 0.100f));
            Button_(panel, "SAVE CONFIGURATION", new Vector2(0.06f, 0.0f), new Vector2(0.94f, 0.05f),
                    SaveConfig, Accent, 16);
            _saveFeedback = Label(panel, "", 11, new Vector2(0.06f, 0.100f), new Vector2(0.94f, 0.115f),
                                  Accent, TMPro.TextAlignmentOptions.Right);
        }

        /// <summary>Total mass across every loadout choice — airframe + power source +
        /// payload + smoke screen + comms — the exact same figure DroneFlightController.
        /// ApplyMass() computes in flight, so this is a real preview, not an estimate.</summary>
        private float TotalWeightKg(DroneSpecification spec)
        {
            float powerKg = spec.PowerSystem == PowerSystemType.Fuel
                ? _ctrl.Working.FuelL * FuelDensityForCapacity(spec, _ctrl.Working.FuelL)
                : _ctrl.Working.BatteryWh / DensityForCapacity(spec, _ctrl.Working.BatteryWh);
            float extraKg = (_ctrl.Working.SmokeScreenEquipped ? LoadoutExtras.SmokeScreenKg : 0f)
                          + LoadoutExtras.CommsWeightKg(_ctrl.Working.Comms);
            return spec.EmptyMassKg + powerKg + _ctrl.Working.PayloadKg + extraKg;
        }

        // Matches whichever synthesized variant's capacity is currently selected, so the
        // weight readout reflects that tier's density rather than always the default.
        private static float DensityForCapacity(DroneSpecification spec, float wh)
        {
            foreach (var v in spec.GetBatteryVariants())
                if (Mathf.Approximately(v.CapacityWh, wh)) return v.EnergyDensityWhPerKg;
            return 180f;
        }

        private static float FuelDensityForCapacity(DroneSpecification spec, float litres)
        {
            foreach (var v in spec.GetFuelVariants())
                if (Mathf.Approximately(v.CapacityL, litres)) return v.DensityKgPerL;
            return 0.74f;
        }

        private void RefreshPowerLine(DroneSpecification spec)
        {
            if (_powerLine == null) return;
            if (spec.PowerSystem == PowerSystemType.Fuel)
            {
                float l = _ctrl.Working.FuelL;
                _powerLine.text = $"Endurance {spec.FuelEnduranceMinutes(l):0} min · Range {spec.FuelRangeKm(l):0} km on {l:0.#} L";
            }
            else
            {
                float wh = _ctrl.Working.BatteryWh;
                _powerLine.text = $"Endurance {spec.EnduranceMinutes(wh):0} min · Range {spec.RangeKm(wh):0} km on {wh:0} Wh";
            }
        }

        private void BuildBatteryCards(Transform panel, DroneSpecification spec, float y0, float y1)
        {
            var variants = spec.GetBatteryVariants();
            float wh = _ctrl.Working.BatteryWh;
            int selected = 0;
            for (int i = 0; i < variants.Length; i++)
                if (Mathf.Approximately(variants[i].CapacityWh, wh)) selected = i;

            const float gap = 0.012f;
            float cellW = (0.94f - 0.06f - (variants.Length - 1) * gap) / variants.Length;
            for (int i = 0; i < variants.Length; i++)
            {
                var v = variants[i];
                float x0 = 0.06f + i * (cellW + gap), x1 = x0 + cellW;
                string hover = $"{v.Name}\n{v.CapacityWh:0} Wh · {v.MassKg:0.##} kg\n" +
                               $"{spec.EnduranceMinutes(v.CapacityWh):0} min flight";
                BuildPowerCard(panel, x0, x1, y0, y1, i == selected, v.Name, $"{v.CapacityWh:0} Wh", hover,
                    () => { _ctrl.SetBattery(v.CapacityWh); RefreshLive(); RefreshPowerLine(spec); });
            }
        }

        private void BuildFuelCards(Transform panel, DroneSpecification spec, float y0, float y1)
        {
            var variants = spec.GetFuelVariants();
            float l = _ctrl.Working.FuelL;
            int selected = 0;
            for (int i = 0; i < variants.Length; i++)
                if (Mathf.Approximately(variants[i].CapacityL, l)) selected = i;

            const float gap = 0.012f;
            float cellW = (0.94f - 0.06f - (variants.Length - 1) * gap) / variants.Length;
            for (int i = 0; i < variants.Length; i++)
            {
                var v = variants[i];
                float x0 = 0.06f + i * (cellW + gap), x1 = x0 + cellW;
                string hover = $"{v.Name}\n{v.CapacityL:0.#} L · {v.MassKg:0.##} kg\n" +
                               $"{spec.FuelEnduranceMinutes(v.CapacityL):0} min flight";
                BuildPowerCard(panel, x0, x1, y0, y1, i == selected, v.Name, $"{v.CapacityL:0.#} L", hover,
                    () => { _ctrl.SetFuel(v.CapacityL); RefreshLive(); RefreshPowerLine(spec); });
            }
        }

        /// <summary>One battery/fuel icon card: a simple battery-cell glyph (no imported
        /// icon assets — same "plain shapes over missing glyphs" approach as StarRow),
        /// filled proportionally to this card's position among its siblings so Light..Max
        /// Range visibly grows across the row. Hovering shows capacity/weight/endurance
        /// in the fixed _powerLine below the row (see RefreshPowerLine).</summary>
        private void BuildPowerCard(Transform panel, float x0, float x1, float y0, float y1, bool selected,
            string name, string caption, string hoverText, System.Action onPick)
        {
            var card = Panel_(panel, "Pwr_" + name, selected ? PanelAlt : Panel, new Vector2(x0, y0), new Vector2(x1, y1));
            Panel_(card, "Stripe", selected ? Accent : new Color(1, 1, 1, 0.08f), Vector2.zero, new Vector2(1f, 0.06f));

            var cell = Panel_(card, "Cell", new Color(1, 1, 1, 0.18f), new Vector2(0.30f, 0.42f), new Vector2(0.70f, 0.88f));
            Panel_(cell, "Nub", new Color(1, 1, 1, 0.18f), new Vector2(0.35f, 0.96f), new Vector2(0.65f, 1.04f));
            Panel_(cell, "Fill", selected ? Accent : TextDim, new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.92f));

            Label(card, caption, 13, new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.38f),
                  selected ? TextMain : TextDim, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card.GetComponent<Image>();
            var colors = btn.colors; colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f); colors.pressedColor = Accent;
            btn.colors = colors;
            btn.onClick.AddListener(() => { Core.AudioManager.Instance?.PlayButtonClick(); onPick(); Build(); });

            var trigger = card.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, _ =>
            {
                Core.AudioManager.Instance?.PlayButtonHover();
                if (_powerLine != null) _powerLine.text = hoverText.Replace("\n", "  ·  ");
            });
            AddTrigger(trigger, EventTriggerType.PointerExit, _ => RefreshLive());
        }

        private void BuildPayloadRow(Transform panel, DroneSpecification spec, float y0, float y1)
        {
            // Static badge for the assigned PayloadKind (not player-selectable — each
            // military drone is built around one munition type) beside the weight picker.
            var badge = Panel_(panel, "PayloadBadge", PanelAlt, new Vector2(0.06f, y0), new Vector2(0.20f, y1));
            PaintPayloadGlyph(badge, spec.PayloadKind);

            OptionRow(panel, spec.PayloadOptionsKg, _ctrl.Working.PayloadKg,
                new Vector2(0.22f, y0), new Vector2(0.94f, y1),
                kg => { _ctrl.SetPayload(kg); RefreshLive(); }, kg => $"{kg:0.#} kg");
        }

        /// <summary>Small glyph distinguishing the four PayloadKinds at a glance — plain
        /// shapes, matching FlightHUD's existing warhead-diamond/cargo-crate convention.</summary>
        private static void PaintPayloadGlyph(Transform area, PayloadKind kind)
        {
            switch (kind)
            {
                case PayloadKind.Warhead:
                    var d = Panel_(area, "Glyph", AccentWarn, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                   new Vector2(-13, -13), new Vector2(13, 13));
                    d.localRotation = Quaternion.Euler(0, 0, 45f);
                    break;
                case PayloadKind.GuidedAmmunition:
                    Panel_(area, "Glyph", Accent, new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.85f),
                           new Vector2(-4, 0), new Vector2(4, 0));
                    Panel_(area, "Tip", Accent, new Vector2(0.5f, 0.85f), new Vector2(0.5f, 0.85f),
                           new Vector2(-9, 0), new Vector2(9, 14));
                    break;
                case PayloadKind.DropAmmunition:
                    Panel_(area, "Glyph", new Color(0.55f, 0.58f, 0.4f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                           new Vector2(-11, -16), new Vector2(11, 16));
                    break;
                default:
                    var crate = Panel_(area, "Glyph", TextDim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                       new Vector2(-14, -11), new Vector2(14, 11));
                    Panel_(crate, "Strap", new Color(0, 0, 0, 0.35f), new Vector2(0, 0.42f), new Vector2(1, 0.58f));
                    break;
            }
        }

        private void BuildSkinCards(Transform panel, DroneSpecification spec, float y0, float y1)
        {
            var ids = Procedural.DroneSkinBuilder.SkinIds;
            const float gap = 0.010f;
            float cellW = (0.94f - 0.06f - (ids.Length - 1) * gap) / ids.Length;
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                bool selected = _ctrl.Working.SkinId == id;
                float x0 = 0.06f + i * (cellW + gap), x1 = x0 + cellW;
                var card = Panel_(panel, "Skin_" + id, selected ? PanelAlt : Panel, new Vector2(x0, y0), new Vector2(x1, y1));
                Panel_(card, "Stripe", selected ? Accent : new Color(1, 1, 1, 0.08f), Vector2.zero, new Vector2(1f, 0.06f));

                var swatchGo = new GameObject("Swatch", typeof(RawImage));
                swatchGo.transform.SetParent(card, false);
                var tex = Procedural.DroneSkinBuilder.GetTexture(id, spec.DefaultBodyColor, spec.DefaultAccentColor);
                swatchGo.GetComponent<RawImage>().texture = tex;
                var swatchRt = swatchGo.GetComponent<RectTransform>();
                swatchRt.anchorMin = new Vector2(0.14f, 0.32f); swatchRt.anchorMax = new Vector2(0.86f, 0.9f);
                swatchRt.offsetMin = Vector2.zero; swatchRt.offsetMax = Vector2.zero;

                Label(card, Procedural.DroneSkinBuilder.SkinLabel(id), 10, new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.30f),
                      selected ? TextMain : TextDim, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = card.GetComponent<Image>();
                var colors = btn.colors; colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f); colors.pressedColor = Accent;
                btn.colors = colors;
                btn.onClick.AddListener(() => { Core.AudioManager.Instance?.PlayButtonClick(); _ctrl.SetSkin(id); Build(); });
                var trigger = card.gameObject.AddComponent<EventTrigger>();
                AddTrigger(trigger, EventTriggerType.PointerEnter, _ => Core.AudioManager.Instance?.PlayButtonHover());
            }
        }

        private static readonly Drone.CommsType[] CommsOptions =
            { Drone.CommsType.Radio, Drone.CommsType.FiveG, Drone.CommsType.AnalogWire };

        private void BuildCommsCards(Transform panel, float y0, float y1)
        {
            var options = CommsOptions;
            const float gap = 0.012f;
            float cellW = (0.94f - 0.06f - (options.Length - 1) * gap) / options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                var type = options[i];
                bool selected = _ctrl.Working.Comms == type;
                float x0 = 0.06f + i * (cellW + gap), x1 = x0 + cellW;
                var card = Panel_(panel, "Comms_" + type, selected ? PanelAlt : Panel, new Vector2(x0, y0), new Vector2(x1, y1));
                Panel_(card, "Stripe", selected ? Accent : new Color(1, 1, 1, 0.08f), Vector2.zero, new Vector2(1f, 0.08f));

                Label(card, $"{Drone.LoadoutExtras.CommsLabel(type)}", 12, new Vector2(0.04f, 0.50f), new Vector2(0.96f, 0.90f),
                      selected ? TextMain : TextDim, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
                Label(card, $"+{Drone.LoadoutExtras.CommsWeightKg(type):0.##} kg", 10, new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.42f),
                      TextDim, TMPro.TextAlignmentOptions.Center);

                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = card.GetComponent<Image>();
                var colors = btn.colors; colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f); colors.pressedColor = Accent;
                btn.colors = colors;
                btn.onClick.AddListener(() => { Core.AudioManager.Instance?.PlayButtonClick(); _ctrl.SetComms(type); RefreshLive(); Build(); });
                var trigger = card.gameObject.AddComponent<EventTrigger>();
                AddTrigger(trigger, EventTriggerType.PointerEnter, _ => Core.AudioManager.Instance?.PlayButtonHover());
            }
        }

        // Saved builds: load back into the editor or delete; all usable in Free Flight.
        private void BuildSavedTab(Transform panel)
        {
            SectionHeader(panel, "SAVED CONFIGURATIONS", 0.885f);
            Label(panel, "Saved builds are selectable in FREE FLIGHT. Load one to keep editing it.",
                  12, new Vector2(0.06f, 0.838f), new Vector2(0.94f, 0.883f), TextDim);

            var saved = Workshop.WorkshopController.AllSaved();
            if (saved.Count == 0)
            {
                Label(panel, "No saved configurations yet.\n\nPick a power cell, payload and skin in the LOADOUT tab, then save it under a name.",
                      15, new Vector2(0.10f, 0.45f), new Vector2(0.90f, 0.75f), TextDim,
                      TMPro.TextAlignmentOptions.Center);
                return;
            }

            const float rowH = 0.105f, gap = 0.012f;
            float y1 = 0.825f;
            for (int i = 0; i < saved.Count; i++)
            {
                float y0 = y1 - rowH;
                if (y0 < 0.02f)
                {
                    Label(panel, $"+{saved.Count - i} more…", 13,
                          new Vector2(0.06f, y1 - 0.03f), new Vector2(0.94f, y1), TextDim);
                    break;
                }
                var d = saved[i];
                var row = Panel_(panel, "Saved_" + d.CustomName, Panel,
                                 new Vector2(0.04f, y0), new Vector2(0.96f, y1));

                var baseSpec = System.Array.Find(_ctrl.BaseDrones, s => s.Id == d.BaseSpecId);
                bool fuelPowered = baseSpec != null && baseSpec.PowerSystem == PowerSystemType.Fuel;
                string powerText = fuelPowered ? $"{d.FuelL:0.#} L" : $"{d.BatteryWh:0} Wh";
                Label(row, d.CustomName, 16, new Vector2(0.04f, 0.52f), new Vector2(0.60f, 0.94f),
                      TextMain, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
                Label(row, $"{(baseSpec != null ? baseSpec.DisplayName : d.BaseSpecId)} · {powerText} · {d.PayloadKg:0.#} kg",
                      12, new Vector2(0.04f, 0.06f), new Vector2(0.60f, 0.50f), TextDim);

                var data = d;
                Button_(row, "LOAD", new Vector2(0.62f, 0.18f), new Vector2(0.80f, 0.82f),
                        () => { _ctrl.LoadConfig(data); _tab = 1; Build(); }, PanelAlt, 13);
                Button_(row, "✕", new Vector2(0.83f, 0.18f), new Vector2(0.96f, 0.82f),
                        () => { _ctrl.DeleteConfig(data.CustomName); Build(); }, AccentWarn, 14);

                y1 = y0 - gap;
            }
        }

        // ---------------------------------------------------------------- helpers

        private static void SectionHeader(Transform panel, string text, float yTop)
        {
            Label(panel, text, 16, new Vector2(0.06f, yTop), new Vector2(0.94f, yTop + 0.04f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            Panel_(panel, "Underline", new Color(Accent.r, Accent.g, Accent.b, 0.25f),
                   new Vector2(0.06f, yTop - 0.004f), new Vector2(0.94f, yTop - 0.001f));
        }

        /// <summary>Refresh everything that depends on the selected power cell/payload/
        /// smoke/comms without a rebuild. Deliberately does NOT touch _powerLine — that's
        /// owned by RefreshPowerLine(spec), called separately, so a card hover (which
        /// temporarily repurposes _powerLine for its tooltip) isn't clobbered by every
        /// other loadout change firing this method.</summary>
        private void RefreshLive()
        {
            var spec = _ctrl.CurrentSpec;
            bool fuelPowered = spec.PowerSystem == PowerSystemType.Fuel;
            float endurance = fuelPowered ? spec.FuelEnduranceMinutes(_ctrl.Working.FuelL) : spec.EnduranceMinutes(_ctrl.Working.BatteryWh);
            float range = fuelPowered ? spec.FuelRangeKm(_ctrl.Working.FuelL) : spec.RangeKm(_ctrl.Working.BatteryWh);
            float totalWeight = TotalWeightKg(spec);
            float margin = totalWeight > 0f ? spec.MaxThrustN / (totalWeight * 9.81f) : 0f;

            if (_chipEndurance != null) _chipEndurance.text = $"{endurance:0} MIN";
            if (_chipRange != null) _chipRange.text = $"{range:0} KM";
            if (_chipAuw != null) _chipAuw.text = $"{totalWeight:0.#} KG";
            if (_massLine != null)
                _massLine.text = $"Total weight {totalWeight:0.#} kg (incl. payload, {(fuelPowered ? "fuel" : "battery")}, " +
                                  $"smoke/comms) · Thrust margin {margin:0.0}×";
            if (_headerCounts != null)
                _headerCounts.text = $"{_ctrl.BaseDrones.Length} AIRFRAMES REGISTERED · {Workshop.WorkshopController.AllSaved().Count} SAVED BUILDS";
        }

        private void SaveConfig()
        {
            _ctrl.SaveCustom(_nameField != null ? _nameField.text : null);
            if (_saveFeedback != null)
                _saveFeedback.text = $"✓ SAVED '{_ctrl.Working.CustomName.ToUpper()}' — SELECTABLE IN FREE FLIGHT";
            RefreshLive();
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type,
                                       System.Action<BaseEventData> action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(d => action(d));
            trigger.triggers.Add(entry);
        }

        private void Clear() { if (_root != null) Destroy(_root.gameObject); }

        private void Close()
        {
            Clear();
            if (_wsCam != null) Destroy(_wsCam.gameObject);
            if (_stageRT != null) { _stageRT.Release(); Destroy(_stageRT); _stageRT = null; }
            if (_stageRig != null) Destroy(_stageRig);
            if (_ctrl != null)
            {
                if (_ctrl.DisplayPoint != null) Destroy(_ctrl.DisplayPoint.gameObject);
                Destroy(_ctrl.gameObject);   // controller destroys its display model
            }
            _onBack?.Invoke();
        }
    }
}
