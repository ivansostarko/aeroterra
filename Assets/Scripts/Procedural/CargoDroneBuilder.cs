using UnityEngine;
using AeroTerra.Drone;
using static AeroTerra.Procedural.DroneMeshBuilder;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// AT-C1 "Pelican" — heavy-lift octocopter cargo delivery drone.
    /// Coaxial X8 layout, layered hull with battery deck and camera gimbal,
    /// braced arms with motor mounts, cargo pod with straps/buckles/feet,
    /// tube landing skids, GPS mast, antennas and nav/strobe lights.
    /// Body/accent materials are exposed for the Workshop color customizer.
    /// </summary>
    public static class CargoDroneBuilder
    {
        public static GameObject Build(Color body, Color accent, out Material bodyMat, out Material accentMat)
        {
            bodyMat = MakeMat(body, 0.4f, 0.55f);
            accentMat = MakeMat(accent, 0.7f, 0.7f);
            var dark = MakeMat(new Color(0.12f, 0.12f, 0.13f), 0.3f, 0.4f);
            var strap = MakeMat(new Color(0.85f, 0.55f, 0.1f), 0.1f, 0.3f);
            var glass = MakeMat(new Color(0.13f, 0.17f, 0.22f), 0.9f, 0.95f);
            var plate = MakeMat(new Color(0.8f, 0.8f, 0.78f), 0.2f, 0.35f);

            var root = new GameObject("AT-C1_Pelican");
            var t = root.transform;

            // ---- Layered central hull ----
            Part(PrimitiveType.Cube, t, Vector3.zero, new Vector3(0.60f, 0.14f, 0.72f), bodyMat, name: "LowerHull");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.02f, 0), new Vector3(0.64f, 0.035f, 0.76f), accentMat, name: "TrimBand");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.11f, 0), new Vector3(0.52f, 0.10f, 0.60f), bodyMat, name: "UpperDeck");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.185f, 0.10f), new Vector3(0.40f, 0.05f, 0.34f), accentMat, name: "TopCover");

            // Battery slab on the rear deck with cooling vents
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.20f, -0.16f), new Vector3(0.30f, 0.075f, 0.30f), dark, name: "BatteryPack");
            for (int v = -1; v <= 1; v++)
                Part(PrimitiveType.Cube, t, new Vector3(v * 0.09f, 0.242f, -0.16f),
                     new Vector3(0.05f, 0.008f, 0.26f), accentMat, name: "Vent");

            // ---- Surface greeble: rivets, panel seam, access panel, cable conduit ----
            foreach (float x in new[] { -0.25f, 0.25f })
                foreach (float z in new[] { -0.28f, 0.28f })
                    Part(PrimitiveType.Sphere, t, new Vector3(x, 0.16f, z), Vector3.one * 0.012f, dark, name: "Rivet");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.155f, -0.02f), new Vector3(0.42f, 0.006f, 0.006f), dark, name: "PanelSeam");
            Part(PrimitiveType.Cylinder, t, new Vector3(0.12f, 0.16f, -0.05f), new Vector3(0.008f, 0.10f, 0.008f), dark,
                 new Vector3(0, 0, 35f), "CableConduit");
            Part(PrimitiveType.Cube, t, new Vector3(-0.10f, 0.16f, 0.20f), new Vector3(0.10f, 0.004f, 0.14f), accentMat, name: "AccessPanel");
            foreach (float px in new[] { -0.14f, -0.06f })
                Part(PrimitiveType.Cylinder, t, new Vector3(px, 0.163f, 0.145f), new Vector3(0.006f, 0.003f, 0.006f), dark, name: "PanelScrew");

            // ---- Nose camera gimbal (front = +Z), on a pivot so it can scan ----
            Part(PrimitiveType.Cylinder, t, new Vector3(0, -0.09f, 0.34f), new Vector3(0.05f, 0.035f, 0.05f), dark, name: "GimbalMount");
            var gimbal = new GameObject("GimbalPivot");
            gimbal.transform.SetParent(t, false);
            gimbal.transform.localPosition = new Vector3(0, -0.145f, 0.35f);
            Part(PrimitiveType.Sphere, gimbal.transform, Vector3.zero, Vector3.one * 0.13f, dark, name: "GimbalBall");
            Part(PrimitiveType.Cylinder, gimbal.transform, new Vector3(0, 0, 0.075f), new Vector3(0.055f, 0.02f, 0.055f), glass,
                 new Vector3(90f, 0, 0), "CameraLens");
            gimbal.AddComponent<GimbalScanner>();

            // ---- Masts & antennas ----
            Part(PrimitiveType.Cylinder, t, new Vector3(0.10f, 0.28f, -0.24f), new Vector3(0.022f, 0.09f, 0.022f), dark, name: "GpsMast");
            Part(PrimitiveType.Sphere, t, new Vector3(0.10f, 0.375f, -0.24f), Vector3.one * 0.075f, accentMat, name: "GpsDome");
            Part(PrimitiveType.Cube, t, new Vector3(-0.14f, 0.27f, -0.20f), new Vector3(0.012f, 0.08f, 0.05f), dark, name: "BladeAntenna");
            NavLight(t, new Vector3(0, 0.235f, 0.10f), Color.white);   // top strobe

            // ---- Four arms with coaxial rotors (X8), motor mounts and braces ----
            // Grouped under RotorRig so RotorTiltAnimator can lean the whole rotor
            // assembly with stick input, on top of the real physics roll/pitch.
            var rig = new GameObject("RotorRig");
            rig.transform.SetParent(t, false);
            rig.AddComponent<RotorTiltAnimator>();

            const float armLen = 0.68f;
            for (int i = 0; i < 4; i++)
            {
                float ang = 45f + i * 90f;
                Vector3 dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;

                Part(PrimitiveType.Cylinder, rig.transform, dir * armLen * 0.5f + Vector3.up * 0.05f,
                     new Vector3(0.055f, armLen * 0.5f, 0.055f), bodyMat, new Vector3(90f, ang, 0), $"Arm{i}");
                // Diagonal support strut from the lower hull to mid-arm
                Part(PrimitiveType.Cylinder, rig.transform, dir * 0.30f - Vector3.up * 0.02f,
                     new Vector3(0.02f, 0.17f, 0.02f), dark, new Vector3(72f, ang, 0), "Strut");

                Vector3 tip = dir * armLen + Vector3.up * 0.05f;
                Part(PrimitiveType.Cylinder, rig.transform, tip, new Vector3(0.085f, 0.055f, 0.085f), dark, name: "MotorMount");

                Rotor(rig.transform, tip + Vector3.up * 0.075f, 0.26f, accentMat, dark, i % 2 == 0 ? 1 : -1);
                var lower = Rotor(rig.transform, tip - Vector3.up * 0.075f, 0.26f, accentMat, dark, i % 2 == 0 ? -1 : 1);
                lower.transform.localRotation = Quaternion.Euler(180f, 0, 0);

                NavLight(rig.transform, tip + Vector3.up * 0.17f, i < 2 ? Color.green : Color.red);
            }

            // ---- Cargo pod slung underneath (Workshop-toggleable payload) ----
            var pod = new GameObject("PayloadVisual");
            pod.transform.SetParent(t, false);
            pod.transform.localPosition = new Vector3(0, -0.32f, 0);
            Part(PrimitiveType.Cube, pod.transform, Vector3.zero, new Vector3(0.44f, 0.30f, 0.60f), bodyMat, name: "PodBody");
            Part(PrimitiveType.Cube, pod.transform, new Vector3(0, 0.155f, 0), new Vector3(0.46f, 0.03f, 0.62f), accentMat, name: "PodLip");
            Part(PrimitiveType.Cube, pod.transform, new Vector3(0, -0.16f, 0), new Vector3(0.42f, 0.02f, 0.58f), accentMat, name: "Hatch");
            Part(PrimitiveType.Cube, pod.transform, new Vector3(0.226f, 0.02f, 0), new Vector3(0.012f, 0.12f, 0.28f), plate, name: "LabelPlate");
            foreach (float z in new[] { -0.20f, 0.20f })
            {
                Part(PrimitiveType.Cube, pod.transform, new Vector3(0, 0, z), new Vector3(0.47f, 0.33f, 0.045f), strap, name: "Strap");
                Part(PrimitiveType.Cube, pod.transform, new Vector3(0, -0.175f, z), new Vector3(0.07f, 0.02f, 0.06f), dark, name: "Buckle");
            }
            foreach (float x in new[] { -0.17f, 0.17f })
                foreach (float z in new[] { -0.24f, 0.24f })
                    Part(PrimitiveType.Sphere, pod.transform, new Vector3(x, -0.17f, z), Vector3.one * 0.05f, dark, name: "PodFoot");

            // ---- Tube landing skids ----
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * 0.36f;
                // Slanted legs, canted slightly outward
                Part(PrimitiveType.Cylinder, t, new Vector3(x * 0.85f, -0.30f, 0.22f),
                     new Vector3(0.025f, 0.17f, 0.025f), dark, new Vector3(12f, 0, side * -16f), "LegFront");
                Part(PrimitiveType.Cylinder, t, new Vector3(x * 0.85f, -0.30f, -0.22f),
                     new Vector3(0.025f, 0.17f, 0.025f), dark, new Vector3(-12f, 0, side * -16f), "LegRear");
                // Skid tube with rounded ends
                Part(PrimitiveType.Capsule, t, new Vector3(x, -0.47f, 0), new Vector3(0.05f, 0.32f, 0.05f), dark,
                     new Vector3(90f, 0, 0), "Skid");
            }
            return root;
        }
    }
}
