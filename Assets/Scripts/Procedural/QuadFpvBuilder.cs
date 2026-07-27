using UnityEngine;
using AeroTerra.Drone;
using static AeroTerra.Procedural.DroneMeshBuilder;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// AT-R4 "Hornet" — long-range FPV strike quadcopter in the style of
    /// improvised carbon-fiber bomber quads: flat plate frame, four slim arms
    /// with large 3-blade props, strapped-on battery pack, FPV camera, whip
    /// antenna, and a belly-slung drop munition (the "PayloadVisual" group,
    /// toggleable in the Workshop). Purely a game model.
    /// </summary>
    public static class QuadFpvBuilder
    {
        public static GameObject Build(Color body, Color accent, out Material bodyMat, out Material accentMat)
        {
            bodyMat = MakeMat(body, 0.55f, 0.5f);        // carbon plates + arms
            accentMat = MakeMat(accent, 0.15f, 0.4f);    // battery shrink-wrap
            var dark = MakeMat(new Color(0.09f, 0.09f, 0.10f), 0.4f, 0.45f);
            var glass = MakeMat(new Color(0.15f, 0.2f, 0.25f), 0.9f, 0.95f);
            var olive = MakeMat(new Color(0.33f, 0.34f, 0.26f), 0.2f, 0.3f);

            var root = new GameObject("AT-R4_Hornet");
            var t = root.transform;

            // ---- Frame: two carbon plates with standoffs ----
            Part(PrimitiveType.Cube, t, Vector3.zero, new Vector3(0.15f, 0.012f, 0.20f), bodyMat, name: "BottomPlate");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.045f, 0), new Vector3(0.15f, 0.012f, 0.20f), bodyMat, name: "TopPlate");
            foreach (float x in new[] { -0.055f, 0.055f })
                foreach (float z in new[] { -0.075f, 0.075f })
                    Part(PrimitiveType.Cylinder, t, new Vector3(x, 0.022f, z),
                         new Vector3(0.008f, 0.017f, 0.008f), dark, name: "Standoff");

            // ---- Battery pack strapped on top ----
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.085f, 0), new Vector3(0.095f, 0.055f, 0.175f), accentMat, name: "Battery");
            foreach (float z in new[] { -0.05f, 0.05f })
                Part(PrimitiveType.Cube, t, new Vector3(0, 0.085f, z), new Vector3(0.105f, 0.062f, 0.018f), dark, name: "Strap");
            Part(PrimitiveType.Cube, t, new Vector3(0.055f, 0.075f, 0.095f), new Vector3(0.025f, 0.02f, 0.02f), dark, name: "XT60");

            // ---- FPV camera + whip antenna ----
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.03f, 0.105f), new Vector3(0.038f, 0.038f, 0.022f), dark,
                 new Vector3(-20f, 0, 0), "FpvCam");
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.036f, 0.118f), new Vector3(0.02f, 0.006f, 0.02f), glass,
                 new Vector3(70f, 0, 0), "FpvLens");
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.115f, -0.09f), new Vector3(0.005f, 0.05f, 0.005f), dark,
                 new Vector3(-30f, 0, 0), "VtxAntenna");
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0.16f, -0.115f), Vector3.one * 0.018f, accentMat, name: "AntennaTip");

            // ---- Surface greeble: frame screws, zip-tie, wire loom ----
            foreach (float x in new[] { -0.06f, 0.06f })
                foreach (float z in new[] { -0.085f, 0.085f })
                    Part(PrimitiveType.Cylinder, t, new Vector3(x, 0.051f, z), new Vector3(0.004f, 0.002f, 0.004f), dark, name: "FrameScrew");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.085f, -0.09f), new Vector3(0.03f, 0.008f, 0.008f), dark, name: "ZipTie");
            Part(PrimitiveType.Cylinder, t, new Vector3(0.09f, 0.03f, 0.03f), new Vector3(0.004f, 0.09f, 0.004f), dark,
                 new Vector3(0, 0, 60f), "WireLoom");

            // ---- Arms, motors, 3-blade props ----
            // Grouped under RotorRig so RotorTiltAnimator can lean the whole rotor
            // assembly with stick input, on top of the real physics roll/pitch.
            var rig = new GameObject("RotorRig");
            rig.transform.SetParent(t, false);
            rig.AddComponent<RotorTiltAnimator>();

            for (int i = 0; i < 4; i++)
            {
                float ang = 45f + i * 90f;
                Vector3 dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                Part(PrimitiveType.Cube, rig.transform, dir * 0.20f + Vector3.up * 0.022f,
                     new Vector3(0.030f, 0.014f, 0.40f), bodyMat, new Vector3(0, ang, 0), $"Arm{i}");

                Vector3 tip = dir * 0.38f;
                Part(PrimitiveType.Cylinder, rig.transform, tip + Vector3.up * 0.038f,
                     new Vector3(0.030f, 0.018f, 0.030f), dark, name: "Motor");
                Rotor(rig.transform, tip + Vector3.up * 0.062f, 0.15f, dark, dark, i % 2 == 0 ? 1 : -1);

                // Short landing post under each motor
                Part(PrimitiveType.Cylinder, rig.transform, tip - Vector3.up * 0.035f,
                     new Vector3(0.010f, 0.045f, 0.010f), dark, name: "LandingPost");

                NavLight(rig.transform, tip + Vector3.up * 0.005f, i < 2 ? Color.green : Color.red);
            }

            // ---- Belly-slung drop bomb (Workshop-toggleable payload) ----
            // Deliberately oversized for the airframe — the improvised-bomber look.
            // The release clamp stays on the frame; the "Store0" group is the actual
            // bomb that falls away on a drop.
            var payload = new GameObject("PayloadVisual");
            payload.transform.SetParent(t, false);
            payload.transform.localPosition = new Vector3(0, -0.095f, 0);
            Part(PrimitiveType.Cube, payload.transform, new Vector3(0, 0.06f, 0),
                 new Vector3(0.06f, 0.025f, 0.07f), dark, name: "ReleaseClamp");
            var store = new GameObject("Store0");
            store.transform.SetParent(payload.transform, false);
            Part(PrimitiveType.Capsule, store.transform, Vector3.zero,
                 new Vector3(0.058f, 0.145f, 0.058f), olive, new Vector3(90f, 0, 0), "Munition");
            Part(PrimitiveType.Sphere, store.transform, new Vector3(0, 0, 0.148f),
                 Vector3.one * 0.045f, dark, name: "NoseFuze");
            for (int f = 0; f < 4; f++)
                Part(PrimitiveType.Cube, store.transform, new Vector3(0, 0, -0.15f),
                     new Vector3(0.008f, 0.07f, 0.05f), olive, new Vector3(0, 0, 45f + f * 90f), "MunitionFin");

            return root;
        }
    }
}
