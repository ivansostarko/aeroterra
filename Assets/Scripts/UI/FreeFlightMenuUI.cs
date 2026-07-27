using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using AeroTerra.Core;
using AeroTerra.Drone;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// Free Flight flow: 1) choose map (London / Dubai)  2) choose drone with
    /// full configuration & specifications (stock + saved custom drones)
    /// 3) launch flight scene.
    /// </summary>
    public class FreeFlightMenuUI : MonoBehaviour
    {
        private enum Screen { Map, Drone, Conditions }

        // Left sidebar (DRONES / UAV / MY CUSTOM CONFIGURATIONS drone lists), 3D stage, and
        // the details panel form three side-by-side columns spanning the same vertical band.
        private const float ListX0 = 0.02f, ListX1 = 0.17f;
        private const float StageX0 = 0.19f, StageX1 = 0.68f;
        private const float SideX0 = 0.70f, SideX1 = 0.98f;
        private const float StageY0 = 0.12f, StageY1 = 0.87f;

        private const float AltitudeMinM = 20f, AltitudeMaxM = 1500f;

        /// <summary>Full-bleed card art, keyed by MapDefinition.Id. Files live at
        /// Assets/Resources/Images/Maps/Open-Fly/{value}.png. Maps with no entry (or a
        /// missing file) fall back to MapIconBuilder's procedural thumbnail + name/country
        /// labels, so a newly added city still gets a usable card with no art required.</summary>
        private static readonly Dictionary<string, string> MapCardArt = new Dictionary<string, string>
        {
            { "barcelona", "barcelona_menu_card" },
            { "dubai", "db_menu_card" },
            { "london", "ln_menu_card" },
            { "new-york", "ny_menu_card" },
            { "paris", "par_menu_card" },
            { "riyadh", "riy_menu_card" },
            { "tokyo", "tk_menu_card" },
            { "zagreb", "zg_menu_card" },
        };

        private RectTransform _root;
        private System.Action _onBack;
        private MapDefinition _pickedMap;
        private DroneSpecification _pickedSpec;
        private Workshop.CustomDroneData _pickedCustom;
        private Screen _screen;

        private DroneGalleryStage _gallery;
        private List<(DroneSpecification spec, Workshop.CustomDroneData custom)> _rows;
        private int _selectedRow;
        private RectTransform _stockContent, _customContent; // last sidebar ScrollList content per
                                                               // section — read for live scroll offset
                                                               // right before each rebuild replaces it

        /// <summary>Sidebar "TYPE" dropdown filter — partitions every DroneClass value
        /// so nothing falls through uncategorized (Military ∪ Cargo ∪ Civilian ∪ All).</summary>
        private enum DroneTypeFilter { All, Military, Cargo, Civilian }
        private DroneTypeFilter _typeFilter = DroneTypeFilter.All;

        private static bool MatchesFilter(DroneSpecification spec, DroneTypeFilter filter) => filter switch
        {
            DroneTypeFilter.Military => spec.IsMilitaryClass,
            DroneTypeFilter.Cargo => spec.Class == DroneClass.CargoDelivery || spec.Class == DroneClass.VtolCargo,
            DroneTypeFilter.Civilian => !spec.IsMilitaryClass &&
                spec.Class != DroneClass.CargoDelivery && spec.Class != DroneClass.VtolCargo,
            _ => true,
        };

        private static string FilterLabel(DroneTypeFilter f) => f switch
        {
            DroneTypeFilter.Military => "Military",
            DroneTypeFilter.Cargo => "Cargo / Logistics",
            DroneTypeFilter.Civilian => "Civilian",
            _ => "All Types",
        };
        private TMPro.TextMeshProUGUI _conditionsSummary;
        private TMPro.TextMeshProUGUI _altitudeLabel;
        private float _spawnAltitudeM;

        public void Open(System.Action onBack)
        {
            _onBack = onBack;
            AudioManager.Instance?.PlayFreeFlightMusic();
            BuildMapScreen();
        }

        private Canvas Canvas => GetComponent<MainMenuUI>().Canvas;

        private void Clear() { if (_root != null) Destroy(_root.gameObject); }

        private void CloseGallery()
        {
            if (_gallery == null) return;
            _gallery.Close();
            Destroy(_gallery);
            _gallery = null;
        }

        private void Update()
        {
            if (_root == null) return;
            var im = AeroTerra.Input.InputManager.Instance;
            if (im != null && im.PauseAction.WasPressedThisFrame()) GoBack();
        }

        private void GoBack()
        {
            if (_screen == Screen.Conditions) { BuildDroneScreen(); return; }
            if (_screen == Screen.Drone) { CloseGallery(); BuildMapScreen(); return; }

            Clear();
            AudioManager.Instance?.StopFreeFlightMusic();
            AudioManager.Instance?.PlayMenuMusic();
            _onBack?.Invoke();
        }

        // ---------- Screen 1: map selection ----------
        private void BuildMapScreen()
        {
            CloseGallery();
            Clear();
            _screen = Screen.Map;
            _root = Panel_(Canvas.transform, "FreeFlight_Maps", Bg, Vector2.zero, Vector2.one);
            BackButton_(_root, new Vector2(0.02f, 0.90f), new Vector2(0.075f, 0.965f), GoBack);
            Label(_root, "FREE FLIGHT — SELECT AREA", 44, new Vector2(0.10f, 0.88f), new Vector2(0.95f, 0.97f),
                  TextMain, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

            // Wrapping grid so the card count isn't hardcoded to 2 — MapDefinition.All can grow freely.
            var maps = MapDefinition.All;
            const int cols = 4;
            const float areaX0 = 0.03f, areaX1 = 0.97f, areaY0 = 0.10f, areaY1 = 0.84f;
            const float gap = 0.008f;
            int rows = Mathf.CeilToInt(maps.Length / (float)cols);
            float cellW = (areaX1 - areaX0) / cols;
            float cellH = (areaY1 - areaY0) / rows;

            for (int i = 0; i < maps.Length; i++)
            {
                var map = maps[i];
                int col = i % cols, row = i / cols;
                float x0 = areaX0 + col * cellW + gap, x1 = areaX0 + (col + 1) * cellW - gap;
                float y1 = areaY1 - row * cellH - gap, y0 = areaY1 - (row + 1) * cellH + gap;

                var card = Panel_(_root, "Map_" + map.Id, Panel, new Vector2(x0, y0), new Vector2(x1, y1));

                var cardArt = MapCardArt.TryGetValue(map.Id, out var artFile)
                    ? Resources.Load<Sprite>("Images/Maps/Open-Fly/" + artFile)
                    : null;

                var iconGo = new GameObject("Icon", typeof(Image));
                iconGo.transform.SetParent(card, false);
                var icon = iconGo.GetComponent<Image>();
                var iconRt = iconGo.GetComponent<RectTransform>();
                if (cardArt != null)
                {
                    // Full-bleed art card: the photo carries the whole card, no text overlay.
                    icon.sprite = cardArt;
                    iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
                }
                else
                {
                    // No art for this map yet — procedural thumbnail + name/country labels.
                    icon.sprite = MapIconBuilder.GetIcon(map);
                    iconRt.anchorMin = new Vector2(0.04f, 0.34f);
                    iconRt.anchorMax = new Vector2(0.96f, 0.95f);

                    Label(card, map.DisplayName, 24, new Vector2(0.04f, 0.20f), new Vector2(0.96f, 0.33f),
                          TextMain, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
                    Label(card, map.Country, 14, new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.20f),
                          Accent, TMPro.TextAlignmentOptions.Center);
                }
                iconRt.offsetMin = Vector2.zero; iconRt.offsetMax = Vector2.zero;

                // Whole card is clickable (not just the SELECT button below). Hover/press
                // tint targets the art image itself when present — it covers the whole
                // card, so tinting the card's own (hidden) background wouldn't be visible.
                var cardBtn = card.gameObject.AddComponent<Button>();
                var cardColors = cardBtn.colors;
                cardColors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
                cardColors.pressedColor = Accent;
                cardBtn.colors = cardColors;
                cardBtn.targetGraphic = cardArt != null ? icon : card.GetComponent<Image>();
                cardBtn.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlayButtonClick();
                    _pickedMap = map;
                    BuildDroneScreen();
                });
                var cardTrigger = card.gameObject.AddComponent<EventTrigger>();
                var hoverEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                hoverEntry.callback.AddListener(_ => AudioManager.Instance?.PlayButtonHover());
                cardTrigger.triggers.Add(hoverEntry);
            }
        }

        // ---------- Screen 2: 3D drone gallery ----------
        private void BuildDroneScreen()
        {
            CloseGallery();
            Clear();
            _screen = Screen.Drone;

            var specs = Resources.LoadAll<DroneSpecification>("Drones");
            var customs = Workshop.WorkshopController.AllSaved();

            // Stock drones with their custom builds interleaved — same roster the
            // old list screen used, now feeding gallery thumbnails instead of rows.
            _rows = new List<(DroneSpecification spec, Workshop.CustomDroneData custom)>();
            foreach (var spec in specs)
            {
                _rows.Add((spec, null));
                foreach (var c in customs)
                    if (c.BaseSpecId == spec.Id) _rows.Add((spec, c));
            }

            _gallery = gameObject.AddComponent<DroneGalleryStage>();
            _gallery.Init();

            _selectedRow = 0;
            RefreshDroneScreen();
        }

        /// <summary>Rebuilds the 2D drone-screen UI (title, thumbnails, details panel)
        /// without touching the 3D stage's camera/lighting/controller — those persist
        /// across thumbnail clicks, same as WorkshopUI's Build()/Clear() split.</summary>
        private void RefreshDroneScreen()
        {
            Clear();
            // Color.clear (not Bg) — the 3D gallery camera renders behind this panel
            // (matches WorkshopUI's identical Color.clear root, for the same reason:
            // an opaque root would blanket the 3D stage's camera output entirely).
            _root = Panel_(Canvas.transform, "FreeFlight_Drones", Color.clear, Vector2.zero, Vector2.one);
            BackButton_(_root, new Vector2(0.02f, 0.90f), new Vector2(0.075f, 0.965f), GoBack);
            Label(_root, $"SELECT DRONE — {_pickedMap.DisplayName.ToUpper()}", 34,
                  new Vector2(0.10f, 0.90f), new Vector2(0.95f, 0.98f), TextMain,
                  TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

            if (_rows.Count == 0)
            {
                Label(_root, "No drones registered.", 22, new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.6f),
                      TextDim, TMPro.TextAlignmentOptions.Center);
                return;
            }

            var (spec, custom) = _rows[_selectedRow];
            _gallery.ShowDrone(spec, custom);
            _gallery.BuildDragSurface(_root, new Vector2(StageX0, StageY0), new Vector2(StageX1, StageY1));

            Label(_root, "◐  DRAG TO ROTATE   ·   SCROLL TO ZOOM", 13,
                  new Vector2(StageX0, StageY1 + 0.005f), new Vector2(StageX1, StageY1 + 0.035f),
                  TextDim, TMPro.TextAlignmentOptions.Center).raycastTarget = false;

            BuildDroneSidebar();
            BuildDetailsPanel(spec, custom);
        }

        /// <summary>Left-edge vertical sidebar: a TYPE filter dropdown up top, then two
        /// stacked lists — DRONES / UAV (stock airframes) and MY CUSTOM CONFIGURATIONS
        /// below — replacing the old horizontal thumbnail strip that used to sit under
        /// the 3D stage. Split proportionally to the stock roster being the larger list;
        /// each list scrolls independently (ScrollList) rather than capping to whatever
        /// fits, so the roster can keep growing. The filter applies to both lists by the
        /// underlying spec's class, so a custom build of a military drone also hides
        /// under a Cargo/Civilian filter.</summary>
        private void BuildDroneSidebar()
        {
            const float filterH = 0.045f, filterGap = 0.018f;
            float filterY1 = StageY1, filterY0 = filterY1 - filterH;

            Label(_root, "FILTER BY TYPE", 11, new Vector2(ListX0, filterY1 - 0.018f), new Vector2(ListX1, filterY1),
                  TextDim, TMPro.TextAlignmentOptions.Left);
            var filterOptions = new[]
                { DroneTypeFilter.All, DroneTypeFilter.Military, DroneTypeFilter.Cargo, DroneTypeFilter.Civilian };
            Dropdown_(_root, new Vector2(ListX0, filterY0), new Vector2(ListX1, filterY1 - 0.020f),
                new Vector2(ListX0, filterY0 - filterOptions.Length * 0.032f), new Vector2(ListX1, filterY0),
                filterOptions, _typeFilter, f =>
                {
                    _typeFilter = f;
                    // The 3D stage keeps showing whatever was selected even if the new
                    // filter hides it from the list — unless nothing filtered is left at
                    // all, jump to the first still-visible row so the stage never shows
                    // a drone silently absent from its own sidebar.
                    if (!MatchesFilter(_rows[_selectedRow].spec, f))
                    {
                        int firstVisible = _rows.FindIndex(r => MatchesFilter(r.spec, f));
                        if (firstVisible >= 0) _selectedRow = firstVisible;
                    }
                    RefreshDroneScreen();
                }, FilterLabel);

            var stockIdx = new List<int>();
            var customIdx = new List<int>();
            for (int i = 0; i < _rows.Count; i++)
                if (MatchesFilter(_rows[i].spec, _typeFilter))
                    (_rows[i].custom == null ? stockIdx : customIdx).Add(i);

            const float headerH = 0.035f, headerGap = 0.01f, sectionGap = 0.03f;
            float totalListH = (StageY1 - StageY0) - filterH - filterGap - 2 * headerH - 2 * headerGap - sectionGap;
            float stockListH = totalListH * 0.58f;
            float customListH = totalListH - stockListH;

            float stockHeaderY1 = filterY0 - filterGap, stockHeaderY0 = stockHeaderY1 - headerH;
            float stockListY1 = stockHeaderY0 - headerGap, stockListY0 = stockListY1 - stockListH;

            float customHeaderY1 = stockListY0 - sectionGap, customHeaderY0 = customHeaderY1 - headerH;
            float customListY1 = customHeaderY0 - headerGap, customListY0 = customListY1 - customListH;

            Label(_root, "DRONES / UAV", 14, new Vector2(ListX0, stockHeaderY0), new Vector2(ListX1, stockHeaderY1),
                  TextDim, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            BuildThumbnailColumn("Stock", stockIdx, stockListY0, stockListY1, ref _stockContent);

            Label(_root, "MY CUSTOM CONFIGURATIONS", 12, new Vector2(ListX0, customHeaderY0), new Vector2(ListX1, customHeaderY1),
                  TextDim, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            BuildThumbnailColumn("Custom", customIdx, customListY0, customListY1, ref _customContent);
        }

        /// <summary>Scrollable (mouse-wheel + drag) list of drone rows via UIBuilder.ScrollList
        /// — every row in `indices` is built, none dropped, however many there are.
        /// prevContent is the section's ScrollList content transform from the previous
        /// rebuild (or null on first build) — its live anchoredPosition is read to restore
        /// scroll offset, then overwritten with the new content transform for next time.</summary>
        private void BuildThumbnailColumn(string listId, List<int> indices, float y0, float y1,
            ref RectTransform prevContent)
        {
            if (indices.Count == 0)
            {
                string msg = _typeFilter != DroneTypeFilter.All
                    ? $"— no {FilterLabel(_typeFilter).ToLower()} drones —"
                    : "— none saved yet, build one in the Workshop —";
                Label(_root, msg, 11, new Vector2(ListX0, y1 - 0.05f), new Vector2(ListX1, y1), TextDim,
                      TMPro.TextAlignmentOptions.MidlineLeft);
                prevContent = null;
                return;
            }

            const float rowH = 46f, gap = 6f; // pixels — 46 fits a two-line name+class row
            const float scrollbarW = 0.006f; // narrow — this sidebar column is tight on width
            var (viewport, content, scrollRect) = ScrollList(_root, "Thumb" + listId,
                new Vector2(ListX0, y0), new Vector2(ListX1 - scrollbarW, y1));

            float totalH = indices.Count * rowH + Mathf.Max(0, indices.Count - 1) * gap;
            content.sizeDelta = new Vector2(0f, totalH);

            // content.anchoredPosition.y is <= 0 once scrolled down (top-pivoted content
            // moving up to reveal lower rows), so the clamp range is [-maxScrollY, 0].
            // Restored by reading the OLD content's live position — Clear() (called at
            // the top of RefreshDroneScreen()) already Destroy()'d it, but Destroy() is
            // deferred to end of frame, so it's still fully valid to read here.
            float maxScrollY = Mathf.Max(0f, totalH - viewport.rect.height);
            float restoreY = prevContent != null
                ? Mathf.Clamp(prevContent.anchoredPosition.y, -maxScrollY, 0f) : 0f;
            content.anchoredPosition = new Vector2(0f, restoreY);
            prevContent = content;

            // A visible scroll-position indicator/handle was missing here — WorkshopUI's
            // hangar rail (same ScrollList primitive) always adds one when there's
            // anything to scroll; without it this list gave no visual sign it could
            // scroll at all, which read as "scrolling doesn't work."
            if (maxScrollY > 0f)
            {
                var scrollbar = VScrollbar_(_root, new Vector2(ListX1 - scrollbarW + 0.001f, y0), new Vector2(ListX1, y1));
                scrollRect.verticalScrollbar = scrollbar;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }

            for (int r = 0; r < indices.Count; r++)
            {
                int rowIndex = indices[r];
                var (spec, custom) = _rows[rowIndex];
                bool selected = rowIndex == _selectedRow;
                float topY = r * (rowH + gap);

                var card = Panel_(content, "Thumb_" + rowIndex, selected ? PanelAlt : Panel,
                                  new Vector2(0f, 1f), new Vector2(1f, 1f),
                                  new Vector2(0f, -(topY + rowH)), new Vector2(0f, -topY));
                Panel_(card, "Stripe", selected ? Accent : new Color(1, 1, 1, 0.08f),
                       Vector2.zero, new Vector2(0.045f, 1f));
                Label(card, custom != null ? custom.CustomName : spec.DisplayName, 12,
                      new Vector2(0.12f, 0.48f), new Vector2(0.96f, 0.92f),
                      selected ? TextMain : TextDim, TMPro.TextAlignmentOptions.MidlineLeft, TMPro.FontStyles.Bold);
                Label(card, custom != null ? spec.ClassLabel() : $"{spec.ClassLabel()} · {spec.RotorCount}R", 9,
                      new Vector2(0.12f, 0.08f), new Vector2(0.96f, 0.46f),
                      selected ? Accent : TextDim);

                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = card.GetComponent<Image>();
                var colors = btn.colors;
                colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
                colors.pressedColor = Accent;
                btn.colors = colors;
                int idx = rowIndex;
                btn.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlayButtonClick();
                    if (idx != _selectedRow) { _selectedRow = idx; RefreshDroneScreen(); }
                });
                var trigger = card.gameObject.AddComponent<EventTrigger>();
                var hover = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                hover.callback.AddListener(_ => AudioManager.Instance?.PlayButtonHover());
                trigger.triggers.Add(hover);
            }
        }

        /// <summary>Right-side drone info panel — redesigned to match WorkshopUI's SPECS
        /// tab visual language (section headers with an underline rule, a category
        /// glyph badge, a zebra-striped key-stats table) instead of the old flat stack
        /// of bare labels, while staying compact enough for a picker screen (a handful
        /// of headline stats, not the Workshop's full GENERAL/PERFORMANCE/SYSTEMS
        /// breakdown — that level of detail belongs in the Workshop, this screen's job
        /// is "pick one and go").</summary>
        private void BuildDetailsPanel(DroneSpecification spec, Workshop.CustomDroneData custom)
        {
            var panel = Panel_(_root, "Details", Panel, new Vector2(SideX0, StageY0), new Vector2(SideX1, StageY1 + 0.035f));

            string title = custom != null ? custom.CustomName : spec.DisplayName;
            Label(panel, title.ToUpper(), 24, new Vector2(0.06f, 0.930f), new Vector2(0.94f, 0.978f),
                  TextMain, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);

            // Category glyph badge + subtitle, same visual pairing WorkshopUI's stage
            // overlay and hangar cards use for at-a-glance Military/Cargo/Civilian read.
            var badge = Panel_(panel, "CategoryBadge", new Color(1, 1, 1, 0.06f),
                               new Vector2(0.06f, 0.888f), new Vector2(0.145f, 0.922f));
            PaintCategoryGlyph(badge, spec.Category);
            string sub = custom != null ? $"Custom build of {spec.DisplayName}" : spec.Manufacturer;
            Label(panel, $"{sub}  ·  {spec.CategoryLabel()}", 13, new Vector2(0.165f, 0.888f), new Vector2(0.94f, 0.922f),
                  Accent, TMPro.TextAlignmentOptions.MidlineLeft);

            bool fuelPowered = spec.PowerSystem == PowerSystemType.Fuel;
            string powerText = custom != null
                ? (fuelPowered ? $"Fuel {custom.FuelL:0.#} L" : $"Battery {custom.BatteryWh:0} Wh")
                : (fuelPowered ? $"Fuel {spec.MaxFuelL:0.#} L" : $"Battery {spec.MaxBatteryWh:0} Wh");
            string cfg = custom != null
                ? $"{powerText} · Payload {custom.PayloadKg:0.#} kg · custom loadout ({Procedural.DroneSkinBuilder.SkinLabel(custom.SkinId)} skin)"
                : $"{powerText} · Payload {spec.MaxPayloadKg:0.#} kg · stock loadout";
            Label(panel, cfg, 12, new Vector2(0.06f, 0.850f), new Vector2(0.94f, 0.882f), TextDim);

            Label(panel, spec.Description, 12, new Vector2(0.06f, 0.700f), new Vector2(0.94f, 0.842f), TextDim);

            SectionHeader(panel, "RATINGS", 0.655f);
            float ry1 = 0.615f;
            const float ratingRowH = 0.050f;
            foreach (var (label, stars) in spec.StarRatings())
            {
                Label(panel, label, 12, new Vector2(0.06f, ry1 - ratingRowH), new Vector2(0.42f, ry1),
                      TextDim, TMPro.TextAlignmentOptions.MidlineLeft);
                StarRow(panel, stars, 5, new Vector2(0.46f, ry1 - ratingRowH + 0.008f), new Vector2(0.94f, ry1 - 0.008f));
                ry1 -= ratingRowH;
            }

            float statsHeaderY = ry1 - 0.045f;
            SectionHeader(panel, "KEY STATS", statsHeaderY);
            float statsTop = statsHeaderY - 0.045f;
            float wh = custom != null ? custom.BatteryWh : spec.MaxBatteryWh;
            float l = custom != null ? custom.FuelL : spec.MaxFuelL;
            (string label, string value)[] stats =
            {
                ("CLASS", spec.ClassLabel()),
                ("TOP SPEED", $"{spec.MaxSpeedKmh:0} km/h"),
                ("SERVICE CEILING", $"{spec.MaxAltitudeM:0} m"),
                ("ENDURANCE", fuelPowered ? $"{spec.FuelEnduranceMinutes(l):0} min" : $"{spec.EnduranceMinutes(wh):0} min"),
                ("RANGE", fuelPowered ? $"{spec.FuelRangeKm(l):0} km" : $"{spec.RangeKm(wh):0} km"),
                ("EMPTY MASS", $"{spec.EmptyMassKg:0.#} kg"),
            };
            const float statRowH = 0.024f;
            for (int i = 0; i < stats.Length; i++)
            {
                float sy1 = statsTop - i * statRowH, sy0 = sy1 - statRowH;
                if (i % 2 == 0)
                    Panel_(panel, "StatRowBg", new Color(1, 1, 1, 0.035f), new Vector2(0.04f, sy0), new Vector2(0.96f, sy1));
                Label(panel, stats[i].label, 11, new Vector2(0.06f, sy0), new Vector2(0.55f, sy1),
                      TextDim, TMPro.TextAlignmentOptions.MidlineLeft);
                Label(panel, stats[i].value, 13, new Vector2(0.45f, sy0), new Vector2(0.94f, sy1),
                      TextMain, TMPro.TextAlignmentOptions.MidlineRight, TMPro.FontStyles.Bold);
            }

            Button_(panel, "FLY", new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.11f), () =>
            {
                _pickedSpec = spec;
                _pickedCustom = custom;
                BuildConditionsScreen();
            }, Accent, 24);
        }

        /// <summary>Accent-colored header + underline rule — same visual convention as
        /// WorkshopUI's SectionHeader (duplicated locally; it's private to that class).</summary>
        private static void SectionHeader(Transform panel, string text, float yTop)
        {
            Label(panel, text, 15, new Vector2(0.06f, yTop), new Vector2(0.94f, yTop + 0.035f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            Panel_(panel, "Underline", new Color(Accent.r, Accent.g, Accent.b, 0.25f),
                   new Vector2(0.06f, yTop - 0.004f), new Vector2(0.94f, yTop - 0.001f));
        }

        /// <summary>Small glyph distinguishing the three Workshop drone categories at a
        /// glance — same shapes/colors as WorkshopUI.PaintCategoryGlyph (duplicated
        /// locally; it's private to that class). Military = a chevron, Cargo/Logistics
        /// = a boxy crate silhouette, Civilian = a plain circle.</summary>
        private static void PaintCategoryGlyph(Transform area, DroneCategory category)
        {
            switch (category)
            {
                case DroneCategory.Military:
                    var chevA = Panel_(area, "ChevA", AccentWarn, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                       new Vector2(-9, 1), new Vector2(0, 9));
                    chevA.localRotation = Quaternion.Euler(0, 0, -35f);
                    var chevB = Panel_(area, "ChevB", AccentWarn, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                       new Vector2(0, 1), new Vector2(9, 9));
                    chevB.localRotation = Quaternion.Euler(0, 0, 35f);
                    break;
                case DroneCategory.CargoLogistics:
                    var crate = Panel_(area, "Glyph", new Color(0.15f, 0.45f, 0.75f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                       new Vector2(-9, -8), new Vector2(9, 8));
                    Panel_(crate, "Strap", new Color(0, 0, 0, 0.35f), new Vector2(0, 0.42f), new Vector2(1, 0.58f));
                    break;
                default: // Civilian
                    Panel_(area, "Glyph", Accent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                           new Vector2(-8, -8), new Vector2(8, 8));
                    break;
            }
        }

        // ---------- Screen 3: flying conditions ----------
        private void BuildConditionsScreen()
        {
            CloseGallery();
            Clear();
            _screen = Screen.Conditions;
            _root = Panel_(Canvas.transform, "FreeFlight_Conditions", Bg, Vector2.zero, Vector2.one);
            BackButton_(_root, new Vector2(0.02f, 0.90f), new Vector2(0.075f, 0.965f), GoBack);

            string droneName = _pickedCustom != null ? _pickedCustom.CustomName : _pickedSpec.DisplayName;
            Label(_root, $"FLYING CONDITIONS — {_pickedMap.DisplayName.ToUpper()} · {droneName.ToUpper()}", 32,
                  new Vector2(0.10f, 0.90f), new Vector2(0.95f, 0.98f), TextMain,
                  TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

            var s = GameManager.Instance.Settings;
            var content = Panel_(_root, "ConditionsContent", Color.clear,
                                 new Vector2(0.22f, 0.20f), new Vector2(0.78f, 0.86f));

            Label(content, "SKY", 20, new Vector2(0f, 0.90f), new Vector2(0.4f, 0.97f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            OptionRow(content, new[] { SkyPreset.Day, SkyPreset.Dawn, SkyPreset.Dusk, SkyPreset.Night },
                s.Sky, new Vector2(0f, 0.81f), new Vector2(1f, 0.89f),
                sky => { s.Sky = sky; GameManager.Instance.SaveSettings(); Map.SkySystem.Instance?.Apply(sky); RefreshConditionsSummary(); });

            Label(content, "WEATHER", 20, new Vector2(0f, 0.71f), new Vector2(0.5f, 0.78f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            TMPro.TextMeshProUGUI windValueLabel = null;
            RectTransform windFill = null;
            OptionRow(content,
                new[] { WeatherPreset.Clear, WeatherPreset.Cloudy, WeatherPreset.Rain,
                        WeatherPreset.Storm, WeatherPreset.Fog, WeatherPreset.Snow },
                s.Weather, new Vector2(0f, 0.62f), new Vector2(1f, 0.70f),
                w =>
                {
                    // Same weather-resets-Wind/Temperature/Humidity behavior as Settings
                    // ▸ Flying Conditions (WindSpeedMs is the only one with a readout on
                    // this screen, but all three should end up consistent regardless of
                    // which screen last touched Weather).
                    s.Weather = w;
                    s.WindSpeedMs = Map.WeatherSystem.BaseWindSpeedMs(w);
                    s.TemperatureC = Map.WeatherSystem.BaseTemperatureC(w);
                    s.HumidityPercent = Map.WeatherSystem.BaseHumidityPercent(w);
                    GameManager.Instance.SaveSettings();
                    Map.WeatherSystem.Instance?.Apply(w);
                    RefreshConditionsSummary();
                    SetWindRow(windValueLabel, windFill, s);
                });

            // Read-only readout of Settings ▸ Flying Conditions' WIND SPEED slider (same
            // value DroneFlightController's physics actually use via
            // WeatherSystem.CurrentWind) — picking a weather type above resets it to
            // that preset's typical speed there, but this screen has no slider of its
            // own to adjust it further, only Settings does.
            Label(content, "WIND", 16, new Vector2(0f, 0.575f), new Vector2(0.4f, 0.615f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            windValueLabel = Label(content, "", 15, new Vector2(0.4f, 0.575f), new Vector2(1f, 0.615f),
                                   TextMain, TMPro.TextAlignmentOptions.Right);
            var windTrack = Panel_(content, "WindTrack", PanelAlt, new Vector2(0f, 0.525f), new Vector2(1f, 0.56f));
            windFill = Panel_(windTrack, "WindFill", Accent, Vector2.zero, new Vector2(0f, 1f));
            SetWindRow(windValueLabel, windFill, s);

            Toggle_(content, "3D Buildings", new Vector2(0f, 0.44f), new Vector2(0.48f, 0.51f),
                    s.Enable3DBuildings, v =>
                    {
                        s.Enable3DBuildings = v;
                        GameManager.Instance.SaveSettings();
                        Map.MapManager.Instance?.ApplyMapSettings();
                    });
            Toggle_(content, "3D Terrain", new Vector2(0.52f, 0.44f), new Vector2(1f, 0.51f),
                    s.Enable3DTerrain, v =>
                    {
                        s.Enable3DTerrain = v;
                        GameManager.Instance.SaveSettings();
                        Map.MapManager.Instance?.ApplyMapSettings();
                    });

            Label(content, "SPAWN ALTITUDE", 20, new Vector2(0f, 0.32f), new Vector2(0.6f, 0.39f),
                  Accent, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Bold);
            _altitudeLabel = Label(content, "", 16, new Vector2(0.6f, 0.32f), new Vector2(1f, 0.39f),
                                   TextMain, TMPro.TextAlignmentOptions.Right, TMPro.FontStyles.Bold);
            _spawnAltitudeM = Mathf.Clamp((float)_pickedMap.SpawnAltitudeMeters, AltitudeMinM, AltitudeMaxM);
            Slider_(content, new Vector2(0f, 0.23f), new Vector2(1f, 0.30f),
                Mathf.InverseLerp(AltitudeMinM, AltitudeMaxM, _spawnAltitudeM),
                v => { _spawnAltitudeM = Mathf.Lerp(AltitudeMinM, AltitudeMaxM, v); RefreshAltitudeLabel(); });
            RefreshAltitudeLabel();

            _conditionsSummary = Label(content, "", 16, new Vector2(0f, 0.03f), new Vector2(1f, 0.15f),
                                       TextDim, TMPro.TextAlignmentOptions.Center);
            RefreshConditionsSummary();

            Button_(_root, "FLY", new Vector2(0.40f, 0.10f), new Vector2(0.60f, 0.18f), () =>
            {
                GameManager.Instance.SelectedSpawnAltitudeOverride = _spawnAltitudeM;
                GameManager.Instance.StartFreeFlight(_pickedMap, _pickedSpec, _pickedCustom);
            }, Accent, 30);
        }

        private void RefreshAltitudeLabel()
        {
            if (_altitudeLabel != null) _altitudeLabel.text = $"{_spawnAltitudeM:0} m";
        }

        /// <summary>Same non-interactive wind readout as Settings ▸ Flying Conditions
        /// (that's the only place with the actual WIND SPEED slider) — just reflects
        /// SettingsData.WindSpeedMs, whichever screen last set it.</summary>
        private const float MaxWindMeterMs = 10.5f;
        private const float WindMaxMs = 15f;

        private static void SetWindRow(TMPro.TextMeshProUGUI valueLabel, RectTransform fill, SettingsData s)
        {
            if (valueLabel == null || fill == null) return;
            valueLabel.text = $"{s.WindSpeedMs:0.0} m/s";
            fill.anchorMax = new Vector2(Mathf.Clamp01(s.WindSpeedMs / Mathf.Max(MaxWindMeterMs, WindMaxMs)), 1f);
        }

        private void RefreshConditionsSummary()
        {
            if (_conditionsSummary == null) return;
            var s = GameManager.Instance.Settings;
            string droneName = _pickedCustom != null ? _pickedCustom.CustomName : _pickedSpec.DisplayName;
            _conditionsSummary.text =
                $"Departing {_pickedMap.DisplayName} in the {droneName} — {s.Weather} skies, {s.Sky} time-of-day.";
        }
    }
}
