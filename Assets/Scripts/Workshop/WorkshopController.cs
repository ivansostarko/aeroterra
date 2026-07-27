using System.Collections.Generic;
using UnityEngine;
using AeroTerra.Core;
using AeroTerra.Drone;
using AeroTerra.Procedural;

namespace AeroTerra.Workshop
{
    /// <summary>
    /// Workshop logic: presents every registered drone on a display stage,
    /// lets the user free-rotate the model, pick battery &amp; payload options,
    /// recolor body/accent, and save the result as a named custom drone
    /// (JSON on disk). The stage auto-spins slowly when the user is idle.
    /// </summary>
    public class WorkshopController : MonoBehaviour
    {
        public DroneSpecification[] BaseDrones;   // filled from Resources/Drones when left empty
        public Transform DisplayPoint;
        public float TurntableDegPerSec = 12f;
        public float IdleSpinDelaySec = 2.5f;

        private GameObject _model;
        private Material _bodyMat, _accentMat;
        private int _index;
        private float _yaw, _pitch;
        private float _lastInteraction = -999f;
        private bool _dragging;

        public CustomDroneData Working { get; private set; }
        public DroneSpecification CurrentSpec => BaseDrones[_index];
        public int CurrentIndex => _index;

        /// <summary>Body/main color as currently shown on the stage — the picked custom
        /// color if the MAIN COLOR picker has been used, else the spec's own default
        /// (so the UI can show which color is "active" before the user touches anything).</summary>
        public Color CurrentBodyColor => Working.HasCustomBodyColor
            ? new Color(Working.BodyR, Working.BodyG, Working.BodyB)
            : CurrentSpec.DefaultBodyColor;

        /// <summary>Whether the payload model (cargo pod / munitions) is shown on the stage.</summary>
        public bool ShowPayload { get; private set; } = true;

        /// <summary>Bounding radius of the current display model — used by the UI to frame the camera.</summary>
        public float ModelRadius { get; private set; } = 1f;

        /// <summary>True when the current model actually has a payload visual to toggle.</summary>
        public bool HasPayloadVisual =>
            _model != null && Procedural.DroneFactory.FindDeep(_model.transform, "PayloadVisual") != null;

        public void SetShowPayload(bool show)
        {
            ShowPayload = show;
            ApplyPayloadVisual();
        }

        private void ApplyPayloadVisual()
        {
            if (_model == null) return;
            var pv = Procedural.DroneFactory.FindDeep(_model.transform, "PayloadVisual");
            if (pv != null) pv.gameObject.SetActive(ShowPayload);
        }

        /// <summary>Every drone in the game, sorted by name — no hardcoded roster.</summary>
        public void EnsureDrones()
        {
            if (BaseDrones != null && BaseDrones.Length > 0) return;
            var specs = Resources.LoadAll<DroneSpecification>("Drones");
            System.Array.Sort(specs, (a, b) =>
                string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal));
            BaseDrones = specs;
        }

        private void Start()
        {
            EnsureDrones();
            if (Working == null && BaseDrones.Length > 0) Show(0);
        }

        private void Update()
        {
            // Gentle turntable resumes only after the user has stopped dragging for a moment.
            if (_model == null || _dragging) return;
            if (Time.unscaledTime - _lastInteraction < IdleSpinDelaySec) return;
            _yaw += TurntableDegPerSec * Time.deltaTime;
            _pitch = Mathf.MoveTowards(_pitch, 0f, 8f * Time.deltaTime);
            ApplyRotation();
        }

        public void Show(int index)
        {
            EnsureDrones();
            _index = Mathf.Clamp(index, 0, BaseDrones.Length - 1);
            var spec = BaseDrones[_index];
            Working = new CustomDroneData
            {
                BaseSpecId = spec.Id,
                CustomName = spec.DisplayName + " Custom",
                // Preconfigured maxed out: longest-range battery/tank, full payload/ammo load.
                BatteryWh = spec.MaxBatteryWh,
                FuelL = spec.MaxFuelL,
                PayloadKg = spec.MaxPayloadKg,
                SkinId = "stock",
                SmokeScreenEquipped = false,
                Comms = Drone.CommsType.Radio,
            };
            _yaw = 0f; _pitch = 0f;
            Rebuild();
        }

        /// <summary>Restore a previously saved configuration (switches drone if needed).</summary>
        public void LoadConfig(CustomDroneData saved)
        {
            EnsureDrones();
            int i = System.Array.FindIndex(BaseDrones, s => s.Id == saved.BaseSpecId);
            if (i < 0) { Debug.LogWarning($"[Workshop] Unknown base spec '{saved.BaseSpecId}'"); return; }
            _index = i;
            Working = JsonUtility.FromJson<CustomDroneData>(JsonUtility.ToJson(saved)); // deep copy
            Rebuild();
        }

        private void Rebuild()
        {
            if (_model != null) Destroy(_model);
            Vector3 pos = DisplayPoint != null ? DisplayPoint.position : new Vector3(0, 1.2f, 0);
            _model = DroneFactory.Spawn(CurrentSpec, Working, pos, flyable: false,
                                        out _bodyMat, out _accentMat);
            ApplyRotation();
            ApplyPayloadVisual();

            var rends = _model.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                ModelRadius = Mathf.Max(0.3f, b.extents.magnitude);
            }
        }

        // ---------- Free rotation (driven by the UI's drag surface) ----------

        public void BeginDrag() { _dragging = true; _lastInteraction = Time.unscaledTime; }
        public void EndDrag() { _dragging = false; _lastInteraction = Time.unscaledTime; }

        public void DragBy(Vector2 delta)
        {
            _yaw -= delta.x * 0.4f;
            _pitch = Mathf.Clamp(_pitch + delta.y * 0.3f, -40f, 60f);
            _lastInteraction = Time.unscaledTime;
            ApplyRotation();
        }

        private void ApplyRotation()
        {
            if (_model != null) _model.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        // ---------- Configuration ----------

        public void SetBattery(float wh) => Working.BatteryWh = wh;
        public void SetFuel(float litres) => Working.FuelL = litres;
        public void SetPayload(float kg) => Working.PayloadKg = kg;
        public void SetSmokeScreen(bool equipped) => Working.SmokeScreenEquipped = equipped;
        public void SetComms(Drone.CommsType comms) => Working.Comms = comms;

        /// <summary>Skin changes need a full Rebuild() (not a live material tweak like the
        /// old color sliders) since the pattern texture is generated fresh per skin id.</summary>
        public void SetSkin(string skinId)
        {
            Working.SkinId = skinId;
            Rebuild();
        }

        /// <summary>MAIN COLOR picker: sets the body color the skin pattern is painted
        /// over, replacing the spec's own default for this build only. Needs a full
        /// Rebuild() for the same reason SetSkin does — the pattern texture is generated
        /// fresh per (skin, body, accent) combination.</summary>
        public void SetBodyColor(Color c)
        {
            Working.HasCustomBodyColor = true;
            Working.BodyR = c.r; Working.BodyG = c.g; Working.BodyB = c.b;
            Rebuild();
        }

        public void SaveCustom(string name)
        {
            Working.CustomName = string.IsNullOrWhiteSpace(name) ? Working.CustomName : name.Trim();
            Working.CreatedUtc = System.DateTime.UtcNow.ToString("o");
            var list = SaveSystem.LoadCustomDrones();
            list.RemoveAll(d => d.CustomName == Working.CustomName);
            list.Add(JsonUtility.FromJson<CustomDroneData>(JsonUtility.ToJson(Working))); // deep copy
            SaveSystem.SaveCustomDrones(list);
            Debug.Log($"[Workshop] Saved custom drone '{Working.CustomName}'");
        }

        public void DeleteConfig(string customName)
        {
            var list = SaveSystem.LoadCustomDrones();
            list.RemoveAll(d => d.CustomName == customName);
            SaveSystem.SaveCustomDrones(list);
        }

        public static List<CustomDroneData> AllSaved() => SaveSystem.LoadCustomDrones();

        private void OnDestroy() { if (_model != null) Destroy(_model); }
    }
}
