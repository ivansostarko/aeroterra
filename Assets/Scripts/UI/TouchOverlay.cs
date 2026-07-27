using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using static AeroTerra.UI.UIBuilder;

namespace AeroTerra.UI
{
    /// <summary>
    /// Mobile overlay for Gyroscope scheme: throttle slider (left), yaw buttons,
    /// camera and pause buttons. Pitch/roll come from device tilt.
    /// </summary>
    public class TouchOverlay : MonoBehaviour
    {
        public static float TouchThrottle;   // read via InputManager on mobile if needed
        public static float TouchYaw;

        public void Init(Canvas canvas)
        {
            var t = canvas.transform;
            var slider = Slider_(t, new Vector2(0.03f, 0.15f), new Vector2(0.09f, 0.6f), 0.5f,
                                 v => TouchThrottle = v * 2f - 1f);
            slider.direction = Slider.Direction.BottomToTop;

            YawButton(t, "⟲", new Vector2(0.82f, 0.18f), new Vector2(0.89f, 0.28f), -1f);
            YawButton(t, "⟳", new Vector2(0.9f, 0.18f), new Vector2(0.97f, 0.28f), 1f);
        }

        private void YawButton(Transform parent, string label, Vector2 min, Vector2 max, float dir)
        {
            var rt = Panel_(parent, "Yaw" + dir, PanelAlt, min, max);
            Label(rt, label, 30, Vector2.zero, Vector2.one, TextMain, TMPro.TextAlignmentOptions.Center);
            var trigger = rt.gameObject.AddComponent<EventTrigger>();
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ => TouchYaw = dir);
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => TouchYaw = 0f);
            trigger.triggers.Add(down); trigger.triggers.Add(up);
        }
    }
}
