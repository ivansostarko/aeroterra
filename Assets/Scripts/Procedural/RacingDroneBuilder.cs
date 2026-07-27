using UnityEngine;
using AeroTerra.Drone;
using static AeroTerra.Procedural.DroneMeshBuilder;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// AT-V6 "Velocity" — tiny 5"-class FPV racing quad: minimal X-frame, sleek
    /// aero canopy over the stack, small high-KV motors and props, bright racing
    /// livery with a contrast chevron. No landing gear (races belly-land), no
    /// payload — pure speed. Purely a game model.
    /// </summary>
    public static class RacingDroneBuilder
    {
        public static GameObject Build(Color body, Color accent, out Material bodyMat, out Material accentMat)
        {
            bodyMat = MakeMat(body, 0.35f, 0.55f);        // matte race-frame carbon/composite
            accentMat = MakeMat(accent, 0.15f, 0.75f);    // glossy racing stripe
            var dark = MakeMat(new Color(0.08f, 0.08f, 0.09f), 0.4f, 0.5f);
            var glass = MakeMat(new Color(0.15f, 0.2f, 0.25f), 0.9f, 0.95f);

            var root = new GameObject("AT-V6_Velocity");
            var t = root.transform;

            // ---- Frame: two slim plates with standoffs ----
            Part(PrimitiveType.Cube, t, Vector3.zero, new Vector3(0.085f, 0.010f, 0.11f), bodyMat, name: "BottomPlate");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.032f, 0), new Vector3(0.085f, 0.010f, 0.11f), bodyMat, name: "TopPlate");
            foreach (float x in new[] { -0.032f, 0.032f })
                foreach (float z in new[] { -0.042f, 0.042f })
                    Part(PrimitiveType.Cylinder, t, new Vector3(x, 0.016f, z),
                         new Vector3(0.006f, 0.012f, 0.006f), dark, name: "Standoff");

            // ---- Sleek aero canopy over the FC/VTX stack ----
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0.05f, -0.01f), new Vector3(0.055f, 0.03f, 0.075f), bodyMat, name: "Canopy");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.052f, -0.01f), new Vector3(0.006f, 0.026f, 0.07f), accentMat, name: "CanopyStripe");

            // ---- Small race battery, strapped low and tight ----
            Part(PrimitiveType.Cube, t, new Vector3(0, -0.018f, 0), new Vector3(0.06f, 0.024f, 0.10f), dark, name: "Battery");
            Part(PrimitiveType.Cube, t, new Vector3(0, -0.018f, 0.03f), new Vector3(0.065f, 0.028f, 0.012f), accentMat, name: "BattStrap");

            // ---- FPV camera, raked hard forward for racing, + whip antenna ----
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.03f, 0.058f), new Vector3(0.022f, 0.022f, 0.016f), dark,
                 new Vector3(-32f, 0, 0), "FpvCam");
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.037f, 0.066f), new Vector3(0.011f, 0.004f, 0.011f), glass,
                 new Vector3(58f, 0, 0), "FpvLens");
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.07f, -0.05f), new Vector3(0.003f, 0.032f, 0.003f), dark,
                 new Vector3(-25f, 0, 0), "VtxAntenna");

            // ---- Arms, tiny high-KV motors, 3-blade props — grouped under RotorRig ----
            // so RotorTiltAnimator can lean the whole rotor assembly with stick input,
            // on top of the real physics roll/pitch (extra "twitchy" racing feel).
            var rig = new GameObject("RotorRig");
            rig.transform.SetParent(t, false);
            rig.AddComponent<RotorTiltAnimator>();

            for (int i = 0; i < 4; i++)
            {
                float ang = 45f + i * 90f;
                Vector3 dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                Part(PrimitiveType.Cube, rig.transform, dir * 0.085f,
                     new Vector3(0.016f, 0.008f, 0.19f), bodyMat, new Vector3(0, ang, 0), $"Arm{i}");

                Vector3 tip = dir * 0.16f;
                Part(PrimitiveType.Cylinder, rig.transform, tip + Vector3.up * 0.014f,
                     new Vector3(0.017f, 0.011f, 0.017f), dark, name: "Motor");
                Rotor(rig.transform, tip + Vector3.up * 0.026f, 0.09f, dark, dark, i % 2 == 0 ? 1 : -1);

                NavLight(rig.transform, tip + Vector3.up * 0.005f, i < 2 ? Color.green : Color.red);
            }

            return root;
        }
    }
}
