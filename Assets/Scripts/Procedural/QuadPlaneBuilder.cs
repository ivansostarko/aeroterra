using UnityEngine;
using static AeroTerra.Procedural.DroneMeshBuilder;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// AT-V8 "Osprey" — civilian VTOL hybrid "quad-plane" cargo drone: slim tube
    /// fuselage with a nose tractor prop, long high-aspect straight wing, two
    /// under-wing booms each carrying a pair of vertical lift rotors (hover), twin
    /// tail fins bridged by a horizontal stabilizer, and a strapped cargo pod slung
    /// under the belly. Hovers on the lift rotors, cruises on the wing — see the
    /// VtolHybrid flight model. Purely a game model.
    /// </summary>
    public static class QuadPlaneBuilder
    {
        public static GameObject Build(Color body, Color accent, out Material bodyMat, out Material accentMat)
        {
            bodyMat = MakeMat(body, 0.35f, 0.55f);
            accentMat = MakeMat(accent, 0.5f, 0.6f);
            var dark = MakeMat(new Color(0.12f, 0.12f, 0.13f), 0.3f, 0.4f);
            var glass = MakeMat(new Color(0.14f, 0.19f, 0.24f), 0.9f, 0.95f);
            var strap = MakeMat(new Color(0.85f, 0.55f, 0.1f), 0.1f, 0.3f);

            var root = new GameObject("AT-V8_Osprey");
            var t = root.transform;

            // ---- Fuselage: slim tube, rounded nose, tapering tail (front = +Z) ----
            Part(PrimitiveType.Capsule, t, new Vector3(0, 0, 0.10f), new Vector3(0.15f, 0.55f, 0.15f), bodyMat,
                 new Vector3(90f, 0, 0), "Fuselage");
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0, 0.68f), new Vector3(0.13f, 0.12f, 0.24f), bodyMat, name: "Nose");
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.01f, -0.62f), new Vector3(0.06f, 0.28f, 0.06f), bodyMat,
                 new Vector3(90f, 0, 0), "TailCone");
            // Nose sensor dome + panel greeble
            Part(PrimitiveType.Sphere, t, new Vector3(0, -0.055f, 0.62f), Vector3.one * 0.075f, glass, name: "SensorBall");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.075f, 0.20f), new Vector3(0.08f, 0.004f, 0.14f), accentMat, name: "AccessPanel");

            // ---- Nose tractor prop ----
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0, 0.80f), new Vector3(0.05f, 0.05f, 0.09f), dark, name: "SpinnerCone");
            var noseProp = Rotor(t, new Vector3(0, 0, 0.84f), 0.22f, dark, dark, 1);
            noseProp.transform.localRotation = Quaternion.Euler(-90f, 0, 0);

            // ---- Long high-aspect straight wing on top ----
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.11f, 0.10f), new Vector3(1.00f, 0.032f, 0.30f), bodyMat, name: "WingCenter");
            foreach (int s in new[] { -1, 1 })
            {
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.95f, 0.115f, 0.10f),
                     new Vector3(1.00f, 0.026f, 0.27f), bodyMat, new Vector3(0, 0, s * -1.5f), "WingOuter");
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.70f, 0.125f, 0.245f),
                     new Vector3(1.35f, 0.022f, 0.03f), accentMat, name: "LeadingEdge");
                // Trailing-edge elevon (aileron/elevator mix in the animator)
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.95f, 0.108f, -0.055f),
                     new Vector3(0.75f, 0.012f, 0.07f), accentMat, null, s < 0 ? "ElevonL" : "ElevonR");
                // Upturned wingtip + nav light
                Part(PrimitiveType.Cube, t, new Vector3(s * 1.475f, 0.15f, 0.10f),
                     new Vector3(0.014f, 0.09f, 0.22f), accentMat, new Vector3(0, 0, s * -14f), "Winglet");
                NavLight(t, new Vector3(s * 1.49f, 0.20f, 0.10f), s < 0 ? Color.red : Color.green);
            }

            // ---- Two under-wing booms, each with fore+aft vertical lift rotors ----
            foreach (int s in new[] { -1, 1 })
            {
                float bx = s * 0.55f;
                Part(PrimitiveType.Cylinder, t, new Vector3(bx, 0.045f, -0.05f),
                     new Vector3(0.055f, 0.75f, 0.055f), bodyMat, new Vector3(90f, 0, 0), "LiftBoom");
                foreach (float bz in new[] { 0.58f, -0.68f })
                {
                    Part(PrimitiveType.Cylinder, t, new Vector3(bx, 0.075f, bz),
                         new Vector3(0.055f, 0.025f, 0.055f), dark, name: "LiftMotor");
                    Rotor(t, new Vector3(bx, 0.105f, bz), 0.24f, dark, dark, bz > 0f == (s > 0) ? 1 : -1);
                }
                // Boom skid feet — the Osprey lands vertically on its booms
                foreach (float bz in new[] { 0.50f, -0.60f })
                    Part(PrimitiveType.Cylinder, t, new Vector3(bx, -0.055f, bz),
                         new Vector3(0.018f, 0.06f, 0.018f), dark, name: "SkidFoot");
            }

            // ---- Twin tail fins bridged by a horizontal stabilizer ----
            foreach (int s in new[] { -1, 1 })
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.28f, 0.10f, -0.72f),
                     new Vector3(0.016f, 0.22f, 0.18f), bodyMat, null, "Rudder");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.215f, -0.72f), new Vector3(0.62f, 0.018f, 0.16f), bodyMat, name: "TailPlane");
            NavLight(t, new Vector3(0, 0.24f, -0.78f), Color.white); // tail strobe

            // ---- Strapped cargo pod slung under the belly (droppable payload) ----
            var pod = new GameObject("PayloadVisual");
            pod.transform.SetParent(t, false);
            pod.transform.localPosition = new Vector3(0, -0.20f, 0.08f);
            Part(PrimitiveType.Cube, pod.transform, Vector3.zero, new Vector3(0.24f, 0.18f, 0.42f), bodyMat, name: "PodBody");
            Part(PrimitiveType.Cube, pod.transform, new Vector3(0, 0.095f, 0), new Vector3(0.26f, 0.02f, 0.44f), accentMat, name: "PodLip");
            foreach (float z in new[] { -0.13f, 0.13f })
                Part(PrimitiveType.Cube, pod.transform, new Vector3(0, 0, z), new Vector3(0.27f, 0.20f, 0.035f), strap, name: "Strap");

            return root;
        }
    }
}
