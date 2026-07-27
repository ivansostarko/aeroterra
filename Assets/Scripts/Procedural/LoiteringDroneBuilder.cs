using UnityEngine;
using static AeroTerra.Procedural.DroneMeshBuilder;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// AT-L3 "Locust" — long-range delta-wing loitering munition, modeled on
    /// the Shahed-136 silhouette: one-piece triangular delta blended into a
    /// slim fuselage, nose cone hanging below the wing plane, up/down wingtip
    /// plates, and a top-rear engine cowling driving a pusher prop. Elevons
    /// on the trailing edge animate with flight input. Purely a game model.
    /// </summary>
    public static class LoiteringDroneBuilder
    {
        public static GameObject Build(Color body, Color accent, out Material bodyMat, out Material accentMat)
        {
            bodyMat = MakeMat(body, 0.25f, 0.35f);      // matte composite finish
            accentMat = MakeMat(accent, 0.4f, 0.5f);
            var dark = MakeMat(new Color(0.12f, 0.12f, 0.12f), 0.3f, 0.35f);

            var root = new GameObject("AT-L3_Locust");
            var t = root.transform;

            // ---- Fuselage: slim spine + drooped nose cone (front = +Z) ----
            Part(PrimitiveType.Cube, t, new Vector3(0, 0, 0.05f), new Vector3(0.22f, 0.13f, 1.30f), bodyMat, name: "Spine");
            Part(PrimitiveType.Sphere, t, new Vector3(0, -0.025f, 0.78f), new Vector3(0.17f, 0.15f, 0.50f), bodyMat, name: "NoseCone");
            Part(PrimitiveType.Cube, t, new Vector3(0, -0.075f, 0.55f), new Vector3(0.06f, 0.02f, 0.10f), dark, name: "SensorPlate");

            // ---- Surface greeble: access panel, rivets, panel seam ----
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.045f, 0.35f), new Vector3(0.10f, 0.003f, 0.12f), accentMat, name: "AccessPanel");
            foreach (float px in new[] { -0.03f, 0.03f })
                Part(PrimitiveType.Cylinder, t, new Vector3(px, 0.047f, 0.30f), new Vector3(0.005f, 0.002f, 0.005f), dark, name: "PanelScrew");
            foreach (int s in new[] { -1, 1 })
                foreach (float rx in new[] { 0.35f, 0.70f })
                    Part(PrimitiveType.Sphere, t, new Vector3(s * rx, 0.012f, -0.15f), Vector3.one * 0.010f, dark, name: "Rivet");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.066f, 0.10f), new Vector3(0.003f, 0.10f, 0.003f), dark, name: "PanelSeam");

            // ---- Delta wing: three swept, overlapping panels per side ----
            foreach (int s in new[] { -1, 1 })
            {
                float yaw = s * -20f;
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.28f, 0, -0.10f),
                     new Vector3(0.60f, 0.028f, 0.92f), bodyMat, new Vector3(0, yaw, 0), "WingInner");
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.65f, 0, -0.34f),
                     new Vector3(0.55f, 0.022f, 0.58f), bodyMat, new Vector3(0, yaw, 0), "WingMid");
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.95f, 0, -0.52f),
                     new Vector3(0.42f, 0.018f, 0.30f), bodyMat, new Vector3(0, yaw, 0), "WingOuter");

                // Trailing-edge elevon (animated by ControlSurfaceAnimator)
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.52f, 0, -0.64f),
                     new Vector3(0.45f, 0.012f, 0.10f), accentMat, null, s < 0 ? "ElevonL" : "ElevonR");

                // Wingtip plate extending above and below (Shahed-style)
                Part(PrimitiveType.Cube, t, new Vector3(s * 1.10f, 0, -0.55f),
                     new Vector3(0.014f, 0.30f, 0.26f), accentMat, name: "TipFin");

                NavLight(t, new Vector3(s * 1.10f, 0.17f, -0.55f), s < 0 ? Color.red : Color.green);
            }

            // ---- Top-rear engine cowling + pusher prop ----
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.095f, -0.50f), new Vector3(0.16f, 0.11f, 0.28f), dark, name: "EngineCowl");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.155f, -0.40f), new Vector3(0.10f, 0.035f, 0.08f), accentMat, name: "IntakeScoop");
            var prop = Rotor(t, new Vector3(0, 0.095f, -0.70f), 0.20f, dark, dark, 1);
            prop.transform.localRotation = Quaternion.Euler(90f, 0, 0);

            // Belly comms antennas
            Part(PrimitiveType.Cube, t, new Vector3(0.05f, -0.085f, -0.10f), new Vector3(0.010f, 0.05f, 0.06f), dark, name: "Antenna");
            Part(PrimitiveType.Cube, t, new Vector3(-0.05f, -0.085f, -0.30f), new Vector3(0.010f, 0.05f, 0.06f), dark, name: "Antenna");

            // No slung payload: the warhead is integral to the airframe (the whole
            // drone IS the munition — it detonates on impact, nothing is released).
            // See DroneFlightController.Detonate for the one-way attack profile.

            return root;
        }
    }
}
