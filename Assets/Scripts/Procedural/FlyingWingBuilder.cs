using UnityEngine;
using static AeroTerra.Procedural.DroneMeshBuilder;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// AT-W7 "Manta" — hand-launched tailless flying-wing mapping/survey drone in
    /// the eBee X mold: a fat teardrop center body blended straight into two large
    /// swept wing panels, high-vis leading-edge strips, upturned wingtips, a rear
    /// pusher prop tucked behind the body, and a belly-mounted survey camera (it
    /// belly-lands, so there is no landing gear). Elevons on the trailing edge
    /// animate with flight input. Purely a game model.
    /// </summary>
    public static class FlyingWingBuilder
    {
        public static GameObject Build(Color body, Color accent, out Material bodyMat, out Material accentMat)
        {
            bodyMat = MakeMat(body, 0.2f, 0.35f);        // matte EPP foam finish
            accentMat = MakeMat(accent, 0.4f, 0.55f);    // high-vis trim
            var dark = MakeMat(new Color(0.10f, 0.10f, 0.11f), 0.3f, 0.4f);
            var glass = MakeMat(new Color(0.14f, 0.19f, 0.24f), 0.9f, 0.95f);

            var root = new GameObject("AT-W7_Manta");
            var t = root.transform;

            // ---- Teardrop center body (front = +Z) ----
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0.01f, 0.05f), new Vector3(0.17f, 0.13f, 0.46f), bodyMat, name: "Body");
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0.0f, 0.26f), new Vector3(0.13f, 0.10f, 0.22f), bodyMat, name: "Nose");
            // Top hatch + status LED
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.065f, 0.06f), new Vector3(0.08f, 0.004f, 0.16f), accentMat, name: "AccessPanel");
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0.068f, -0.06f), Vector3.one * 0.02f, dark, name: "StatusLed");

            // ---- Belly survey camera (the whole point of a mapping wing) ----
            Part(PrimitiveType.Cylinder, t, new Vector3(0, -0.062f, 0.10f), new Vector3(0.05f, 0.015f, 0.05f), dark, name: "CameraRing");
            Part(PrimitiveType.Cylinder, t, new Vector3(0, -0.072f, 0.10f), new Vector3(0.04f, 0.008f, 0.04f), glass, name: "SurveyLens");
            // Pitot on the nose
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.01f, 0.40f), new Vector3(0.005f, 0.045f, 0.005f), dark,
                 new Vector3(90f, 0, 0), "PitotProbe");

            // ---- Two big swept wing panels per side, blended into the body ----
            foreach (int s in new[] { -1, 1 })
            {
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.17f, 0, -0.02f),
                     new Vector3(0.34f, 0.026f, 0.34f), bodyMat, new Vector3(0, s * -28f, 0), "WingInner");
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.43f, 0.006f, -0.17f),
                     new Vector3(0.32f, 0.018f, 0.24f), bodyMat, new Vector3(0, s * -32f, s * -2f), "WingOuter");

                // High-vis leading-edge strip (the eBee's yellow chevron)
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.28f, 0.010f, 0.075f),
                     new Vector3(0.30f, 0.022f, 0.030f), accentMat, new Vector3(0, s * -30f, 0), "LeadingEdge");

                // Trailing-edge elevon (animated by ControlSurfaceAnimator)
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.33f, 0, -0.245f),
                     new Vector3(0.28f, 0.010f, 0.06f), accentMat, new Vector3(0, s * -30f, 0),
                     s < 0 ? "ElevonL" : "ElevonR");

                // Upturned wingtip + nav light
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.575f, 0.035f, -0.26f),
                     new Vector3(0.014f, 0.07f, 0.12f), accentMat, new Vector3(0, 0, s * -18f), "Winglet");
                NavLight(t, new Vector3(s * 0.585f, 0.075f, -0.25f), s < 0 ? Color.red : Color.green);
            }
            NavLight(t, new Vector3(0, 0.05f, -0.30f), Color.white); // tail strobe

            // ---- Rear pusher prop tucked behind the body ----
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.01f, -0.24f), new Vector3(0.05f, 0.04f, 0.05f), dark,
                 new Vector3(90f, 0, 0), "MotorHousing");
            var prop = Rotor(t, new Vector3(0, 0.01f, -0.32f), 0.13f, dark, dark, 1);
            prop.transform.localRotation = Quaternion.Euler(90f, 0, 0);

            // No payload: a survey wing carries sensors, not stores.
            return root;
        }
    }
}
