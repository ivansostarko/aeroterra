using UnityEngine;
using static AeroTerra.Procedural.DroneMeshBuilder;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// AT-J9 "Wraith" — fictional jet-powered one-way strike drone in the mold of
    /// sleek swept-wing loitering munitions (Shadow-25 silhouette): long slender
    /// fuselage with a pointed nose and low canopy hump, dorsal intake feeding a
    /// rear jet nozzle, mid-set swept wings, swept tailplanes with a single fin,
    /// and a long tail probe. No prop, no landing gear, no droppable payload —
    /// the warhead is integral and the whole airframe detonates on impact.
    /// Purely a game model.
    /// </summary>
    public static class JetStrikeBuilder
    {
        public static GameObject Build(Color body, Color accent, out Material bodyMat, out Material accentMat)
        {
            bodyMat = MakeMat(body, 0.55f, 0.6f);        // smooth gunmetal composite
            accentMat = MakeMat(accent, 0.6f, 0.65f);
            var dark = MakeMat(new Color(0.09f, 0.09f, 0.10f), 0.35f, 0.4f);
            var glass = MakeMat(new Color(0.13f, 0.17f, 0.22f), 0.9f, 0.95f);
            var nozzleGlow = MakeMat(new Color(0.55f, 0.25f, 0.12f), 0.2f, 0.3f);

            var root = new GameObject("AT-J9_Wraith");
            var t = root.transform;

            // ---- Long slender fuselage, pointed nose, low canopy hump (front = +Z) ----
            Part(PrimitiveType.Capsule, t, Vector3.zero, new Vector3(0.13f, 0.75f, 0.13f), bodyMat,
                 new Vector3(90f, 0, 0), "Fuselage");
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0, 0.78f), new Vector3(0.10f, 0.09f, 0.34f), bodyMat, name: "NoseCone");
            Part(PrimitiveType.Sphere, t, new Vector3(0, -0.015f, 0.92f), Vector3.one * 0.09f, glass, name: "SeekerDome");
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0.075f, 0.42f), new Vector3(0.085f, 0.055f, 0.26f), glass, name: "Canopy");

            // Dorsal intake hump feeding the jet
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.095f, -0.05f), new Vector3(0.09f, 0.055f, 0.24f), dark, name: "DorsalIntake");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.115f, 0.08f), new Vector3(0.07f, 0.02f, 0.05f), accentMat, name: "IntakeLip");

            // Surface greeble: panel seam, rivet line, squadron stripe
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.03f, 0.25f), new Vector3(0.003f, 0.12f, 0.003f), dark, name: "PanelSeam");
            foreach (int s in new[] { -1, 1 })
                foreach (float rz in new[] { 0.10f, -0.15f })
                    Part(PrimitiveType.Sphere, t, new Vector3(s * 0.115f, 0.01f, rz), Vector3.one * 0.009f, dark, name: "Rivet");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.02f, -0.32f), new Vector3(0.135f, 0.05f, 0.045f), accentMat, name: "Stripe");

            // ---- Mid-set swept wings with leading edges + elevons ----
            foreach (int s in new[] { -1, 1 })
            {
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.42f, 0, -0.12f),
                     new Vector3(0.58f, 0.020f, 0.40f), bodyMat, new Vector3(0, s * -35f, 0), "Wing");
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.42f, 0.006f, 0.055f),
                     new Vector3(0.50f, 0.018f, 0.03f), accentMat, new Vector3(0, s * -35f, 0), "LeadingEdge");
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.50f, 0, -0.335f),
                     new Vector3(0.34f, 0.010f, 0.07f), accentMat, new Vector3(0, s * -32f, 0),
                     s < 0 ? "ElevonL" : "ElevonR");
                NavLight(t, new Vector3(s * 0.83f, 0.015f, -0.33f), s < 0 ? Color.red : Color.green);
            }

            // ---- Swept tailplanes + single vertical fin ----
            foreach (int s in new[] { -1, 1 })
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.20f, 0.01f, -0.66f),
                     new Vector3(0.30f, 0.014f, 0.18f), bodyMat, new Vector3(0, s * -35f, 0), "TailPlane");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.155f, -0.66f),
                 new Vector3(0.016f, 0.26f, 0.20f), bodyMat, new Vector3(-18f, 0, 0), "Rudder");
            NavLight(t, new Vector3(0, 0.29f, -0.70f), Color.white); // tail strobe

            // ---- Jet nozzle + long tail probe (no prop — it's a jet) ----
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0, -0.82f), new Vector3(0.075f, 0.05f, 0.075f), dark,
                 new Vector3(90f, 0, 0), "JetNozzle");
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0, -0.86f), new Vector3(0.05f, 0.012f, 0.05f), nozzleGlow,
                 new Vector3(90f, 0, 0), "NozzleGlow");
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0, -1.00f), new Vector3(0.006f, 0.12f, 0.006f), dark,
                 new Vector3(90f, 0, 0), "TailProbe");

            // No PayloadVisual: the warhead is integral — the airframe IS the munition.
            return root;
        }
    }
}
