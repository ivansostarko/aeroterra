using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using AeroTerra.Drone;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// Lightweight 3D drone-preview stage for the Free Flight drone gallery — ports
    /// WorkshopUI's camera/lighting/drag-input pattern (see WorkshopUI.cs) without
    /// any of the Workshop's own editing UI (color pickers, save, tabs). One drone
    /// is shown at a time; ShowDrone swaps which spec/custom is staged. The camera
    /// and lighting rig persist across 2D UI rebuilds — only BuildDragSurface needs
    /// re-calling each time the host screen's root panel is rebuilt.
    /// </summary>
    public class DroneGalleryStage : MonoBehaviour
    {
        private Workshop.WorkshopController _ctrl;
        private Camera _cam;
        private RenderTexture _stageRT;    // stage camera renders here, not to the screen — see BuildDragSurface
        private GameObject _stageRig;
        private float _camDist = 2.9f;

        private static readonly Vector3 CamTarget = new Vector3(0f, 1.15f, 0f);
        private static readonly Vector3 CamDir = new Vector3(0.25f, 0.38f, -1f).normalized;

        /// <summary>One-time setup: display model controller, camera, lighting rig.</summary>
        public void Init()
        {
            var ctrlGo = new GameObject("GalleryController");
            _ctrl = ctrlGo.AddComponent<Workshop.WorkshopController>();
            _ctrl.EnsureDrones();
            var dp = new GameObject("GalleryDisplayPoint");
            dp.transform.position = new Vector3(0, 1.2f, 0);
            _ctrl.DisplayPoint = dp.transform;

            var camGo = new GameObject("GalleryCamera");
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.03f, 0.045f, 0.07f);
            // targetTexture assigned lazily in BuildDragSurface, once the drag surface's
            // rect (and so the texture's size) is known — see its remarks for why this
            // renders to a texture instead of straight to the screen.
            UpdateCamera();

            _stageRig = new GameObject("GalleryStage");

            var key = new GameObject("KeyLight").AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.2f;
            key.transform.SetParent(_stageRig.transform);
            key.transform.rotation = Quaternion.Euler(40f, -35f, 0);

            var fill = new GameObject("FillLight").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = 0.4f;
            fill.color = new Color(0.6f, 0.75f, 1f);
            fill.transform.SetParent(_stageRig.transform);
            fill.transform.rotation = Quaternion.Euler(10f, 150f, 0);

            // No display pedestal — matches WorkshopUI's stage (see its SetupStage
            // remarks): used to have a flat disc + Accent-tinted ring under the model,
            // removed so the stage shows only the drone itself.
        }

        /// <summary>Re-creates the drag/scroll surface as a child of the host screen's
        /// current root panel — call again every time that root is rebuilt (the surface
        /// itself is cheap 2D UI; the stage above is not). Shows the stage camera's
        /// RenderTexture rather than being an invisible Color.clear surface: two cameras
        /// (this one plus the menu scene's own) both drawing directly to the screen left
        /// the Screen Space Overlay UI Canvas invisible under URP even though it was still
        /// fully built and interactive — rendering to a texture and displaying it via a
        /// RawImage inside this same Canvas removes that compositing ambiguity entirely.
        /// The texture is created once, sized from the first call's rect, and reused —
        /// anchorMin/anchorMax don't change between calls in practice.</summary>
        public void BuildDragSurface(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (_stageRT == null)
            {
                int rtW = Mathf.Max(2, Mathf.RoundToInt(Screen.width * (anchorMax.x - anchorMin.x)));
                int rtH = Mathf.Max(2, Mathf.RoundToInt(Screen.height * (anchorMax.y - anchorMin.y)));
                _stageRT = new RenderTexture(rtW, rtH, 24) { name = "GalleryStageRT" };
                _cam.targetTexture = _stageRT;
            }

            var surfGo = new GameObject("GalleryDragSurface", typeof(RawImage));
            surfGo.transform.SetParent(parent, false);
            var surf = (RectTransform)surfGo.transform;
            surf.anchorMin = anchorMin; surf.anchorMax = anchorMax;
            surf.offsetMin = Vector2.zero; surf.offsetMax = Vector2.zero;
            surfGo.GetComponent<RawImage>().texture = _stageRT;

            var trigger = surf.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.BeginDrag, _ => _ctrl.BeginDrag());
            AddTrigger(trigger, EventTriggerType.Drag, d => _ctrl.DragBy(((PointerEventData)d).delta));
            AddTrigger(trigger, EventTriggerType.EndDrag, _ => _ctrl.EndDrag());
            AddTrigger(trigger, EventTriggerType.Scroll, d => OnScroll(((PointerEventData)d).scrollDelta.y));
        }

        /// <summary>Switches the staged model to this spec (or saved custom) and re-frames
        /// the camera — the fleet ranges from a 0.6 m quad to a 12 m UCAV.</summary>
        public void ShowDrone(DroneSpecification spec, Workshop.CustomDroneData custom)
        {
            if (custom != null)
            {
                _ctrl.LoadConfig(custom);
            }
            else
            {
                int i = System.Array.FindIndex(_ctrl.BaseDrones, s => s.Id == spec.Id);
                _ctrl.Show(Mathf.Max(0, i));
            }
            _camDist = Mathf.Clamp(_ctrl.ModelRadius * 2.2f, 1.0f, MaxCamDist);
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            if (_cam == null) return;
            _cam.transform.position = CamTarget + CamDir * _camDist;
            _cam.transform.LookAt(CamTarget);
        }

        /// <summary>Furthest the stage camera is allowed to back away to — see WorkshopUI's
        /// identical MaxCamDist for why this scales with the model instead of a flat cap.</summary>
        private float MaxCamDist => Mathf.Max(6f, _ctrl.ModelRadius * 3f);

        private void OnScroll(float delta)
        {
            delta = Mathf.Clamp(delta, -3f, 3f);
            _camDist = Mathf.Clamp(_camDist - delta * 0.25f, 1.0f, MaxCamDist);
            UpdateCamera();
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(d => action(d));
            trigger.triggers.Add(entry);
        }

        /// <summary>Tears down the camera, lighting rig and display controller (not the
        /// 2D drag surface — that's owned by whatever root panel it was parented to).</summary>
        public void Close()
        {
            if (_cam != null) Destroy(_cam.gameObject);
            if (_stageRT != null) { _stageRT.Release(); Destroy(_stageRT); _stageRT = null; }
            if (_stageRig != null) Destroy(_stageRig);
            if (_ctrl != null)
            {
                if (_ctrl.DisplayPoint != null) Destroy(_ctrl.DisplayPoint.gameObject);
                Destroy(_ctrl.gameObject); // controller destroys its display model
            }
        }
    }
}
