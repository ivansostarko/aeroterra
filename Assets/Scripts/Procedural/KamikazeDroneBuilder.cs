using UnityEngine;
using static AeroTerra.Procedural.DroneMeshBuilder;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// AT-K2 "Vespid" — fictional military kamikaze (loitering-munition style)
    /// drone for the simulator. Sleek tapered fuselage with nose cone, seeker
    /// dome and dorsal canopy; two-segment swept delta wings with leading-edge
    /// strips, elevons and winglets; canted X tail fins, ventral fin and a
    /// coned pusher prop. Purely a game model.
    /// </summary>
    public static class KamikazeDroneBuilder
    {
        public static GameObject Build(Color body, Color accent, out Material bodyMat, out Material accentMat)
        {
            bodyMat = MakeMat(body, 0.5f, 0.45f);
            accentMat = MakeMat(accent, 0.75f, 0.65f);
            var dark = MakeMat(new Color(0.1f, 0.1f, 0.11f), 0.3f, 0.35f);
            var glass = MakeMat(new Color(0.15f, 0.2f, 0.25f), 0.9f, 0.95f);

            var root = new GameObject("AT-K2_Vespid");
            var t = root.transform;

            // ---- Fuselage: tapered tube, nose cone, seeker dome (front = +Z) ----
            Part(PrimitiveType.Capsule, t, Vector3.zero, new Vector3(0.17f, 0.55f, 0.17f), bodyMat,
                 new Vector3(90f, 0, 0), "Fuselage");
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0, 0.52f), new Vector3(0.19f, 0.19f, 0.34f), bodyMat, name: "NoseCone");
            Part(PrimitiveType.Sphere, t, new Vector3(0, -0.02f, 0.64f), Vector3.one * 0.14f, glass, name: "SeekerDome");
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.055f, 0.72f), new Vector3(0.008f, 0.06f, 0.008f), dark,
                 new Vector3(90f, 0, 0), "PitotTube");

            // Dorsal canopy + avionics spine
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0.10f, 0.16f), new Vector3(0.11f, 0.075f, 0.28f), glass, name: "Canopy");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.095f, -0.15f), new Vector3(0.015f, 0.055f, 0.35f), dark, name: "Strake");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.13f, -0.28f), new Vector3(0.012f, 0.06f, 0.05f), dark, name: "BladeAntenna");

            // Chin intake + ventral sensor blister
            Part(PrimitiveType.Cube, t, new Vector3(0, -0.10f, 0.30f), new Vector3(0.10f, 0.05f, 0.13f), dark, name: "ChinIntake");
            Part(PrimitiveType.Cylinder, t, new Vector3(0, -0.115f, 0.02f), new Vector3(0.095f, 0.075f, 0.095f), dark,
                 new Vector3(90f, 0, 0), "SensorFairing");
            Part(PrimitiveType.Sphere, t, new Vector3(0, -0.165f, 0.09f), Vector3.one * 0.075f, glass, name: "SensorBall");

            // ---- Surface greeble: panel seam, access panel, rivets, tail cable run ----
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.02f, 0.30f), new Vector3(0.003f, 0.16f, 0.003f), dark, name: "PanelSeam");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.06f, -0.05f), new Vector3(0.09f, 0.003f, 0.16f), accentMat, name: "AccessPanel");
            foreach (float px in new[] { -0.03f, 0.03f })
                Part(PrimitiveType.Cylinder, t, new Vector3(px, 0.062f, 0.02f), new Vector3(0.006f, 0.002f, 0.006f), dark, name: "PanelScrew");
            foreach (int s in new[] { -1, 1 })
                foreach (float rx in new[] { 0.20f, 0.40f })
                    Part(PrimitiveType.Sphere, t, new Vector3(s * rx, 0.01f, -0.02f), Vector3.one * 0.010f, dark, name: "Rivet");
            Part(PrimitiveType.Cylinder, t, new Vector3(0, -0.03f, -0.40f), new Vector3(0.006f, 0.10f, 0.006f), dark,
                 new Vector3(90f, 0, 0), "TailCable");

            // ---- Two-segment swept delta wings ----
            foreach (int s in new[] { -1, 1 })
            {
                float yaw = s * -24f;
                // Inner panel (thicker) and outer panel (thinner, more sweep)
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.30f, 0, -0.03f),
                     new Vector3(0.52f, 0.028f, 0.32f), bodyMat, new Vector3(0, yaw, 0), "WingInner");
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.62f, 0.004f, -0.15f),
                     new Vector3(0.42f, 0.018f, 0.22f), bodyMat, new Vector3(0, s * -28f, 0), "WingOuter");
                // Leading-edge strip and elevon
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.32f, 0.006f, 0.125f),
                     new Vector3(0.42f, 0.024f, 0.035f), accentMat, new Vector3(0, yaw, 0), "LeadingEdge");
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.46f, 0, -0.27f),
                     new Vector3(0.30f, 0.012f, 0.07f), accentMat, new Vector3(0, s * -26f, 0),
                     s < 0 ? "ElevonL" : "ElevonR");
                // Winglet + wingtip nav light
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.80f, 0.05f, -0.22f),
                     new Vector3(0.018f, 0.11f, 0.14f), accentMat, new Vector3(0, 0, s * -12f), "Winglet");
                NavLight(t, new Vector3(s * 0.80f, 0.115f, -0.20f), s < 0 ? Color.red : Color.green);
            }

            // ---- Tail: boom taper, canted X fins, ventral fin, pusher prop ----
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0, -0.50f), new Vector3(0.10f, 0.10f, 0.10f), bodyMat,
                 new Vector3(90f, 0, 0), "TailBoom");
            for (int i = 0; i < 4; i++)
            {
                float roll = 45f + i * 90f;
                Part(PrimitiveType.Cube, t, new Vector3(0, 0, -0.48f),
                     new Vector3(0.02f, 0.28f, 0.16f), accentMat, new Vector3(0, 0, roll), "Fin");
            }
            Part(PrimitiveType.Cube, t, new Vector3(0, -0.13f, -0.36f), new Vector3(0.015f, 0.10f, 0.11f), dark, name: "VentralFin");

            var rotor = Rotor(t, new Vector3(0, 0, -0.62f), 0.21f, dark, accentMat, 1);
            rotor.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0, -0.73f), new Vector3(0.06f, 0.06f, 0.11f), dark, name: "SpinnerCone");

            // No slung payload: the warhead is integral to the airframe (the whole
            // drone IS the munition — it detonates on impact, nothing is released).
            // See DroneFlightController.Detonate for the one-way attack profile.

            return root;
        }
    }
}
