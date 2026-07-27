using UnityEngine;
using AeroTerra.Drone;
using static AeroTerra.Procedural.DroneMeshBuilder;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// AT-P10 "Pixel" — consumer folding camera quadcopter in the pocket-drone
    /// mold: angular two-tier body with a beveled top shell, front 2-axis gimbal
    /// camera, four slim fold-style arms with compact two-tier motors and props,
    /// belly vision sensors and an LED status bar. No payload — it carries a
    /// camera, not cargo. Purely a game model.
    /// </summary>
    public static class FoldQuadBuilder
    {
        public static GameObject Build(Color body, Color accent, out Material bodyMat, out Material accentMat)
        {
            bodyMat = MakeMat(body, 0.35f, 0.5f);        // matte consumer plastic
            accentMat = MakeMat(accent, 0.5f, 0.65f);
            var dark = MakeMat(new Color(0.09f, 0.09f, 0.10f), 0.35f, 0.45f);
            var glass = MakeMat(new Color(0.13f, 0.17f, 0.22f), 0.9f, 0.95f);

            var root = new GameObject("AT-P10_Pixel");
            var t = root.transform;

            // ---- Two-tier angular body with beveled top shell ----
            Part(PrimitiveType.Cube, t, Vector3.zero, new Vector3(0.16f, 0.05f, 0.26f), bodyMat, name: "LowerShell");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.042f, -0.01f), new Vector3(0.13f, 0.045f, 0.21f), bodyMat, name: "UpperShell");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.068f, -0.01f), new Vector3(0.10f, 0.012f, 0.16f), accentMat, name: "TopPlate");
            // Power button + LED status bar on the tail
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.076f, -0.06f), new Vector3(0.018f, 0.004f, 0.018f), dark, name: "PowerButton");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.02f, -0.132f), new Vector3(0.08f, 0.018f, 0.006f), accentMat, name: "LedBar");

            // ---- Front gimbal camera on an unscaled pivot so it can scan ----
            var gimbal = new GameObject("GimbalPivot");
            gimbal.transform.SetParent(t, false);
            gimbal.transform.localPosition = new Vector3(0, -0.018f, 0.135f);
            Part(PrimitiveType.Cube, gimbal.transform, new Vector3(0, 0.02f, -0.01f),
                 new Vector3(0.045f, 0.02f, 0.03f), dark, name: "GimbalMount");
            Part(PrimitiveType.Sphere, gimbal.transform, Vector3.zero, Vector3.one * 0.055f, dark, name: "GimbalBall");
            Part(PrimitiveType.Cylinder, gimbal.transform, new Vector3(0, 0, 0.026f),
                 new Vector3(0.028f, 0.008f, 0.028f), glass, new Vector3(90f, 0, 0), "CameraLens");
            gimbal.AddComponent<GimbalScanner>();

            // ---- Belly vision sensors ----
            foreach (float z in new[] { 0.05f, -0.05f })
                Part(PrimitiveType.Cylinder, t, new Vector3(0.03f, -0.028f, z), new Vector3(0.016f, 0.004f, 0.016f), glass, name: "VisionSensor");

            // ---- Four slim fold-style arms + compact motors, under a RotorRig ----
            var rig = new GameObject("RotorRig");
            rig.transform.SetParent(t, false);
            rig.AddComponent<RotorTiltAnimator>();

            for (int i = 0; i < 4; i++)
            {
                float ang = 45f + i * 90f;
                Vector3 dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                bool front = dir.z > 0f;

                // Front arms sweep forward-flat, rear arms drop slightly — the classic
                // folded-arm consumer silhouette.
                Part(PrimitiveType.Cube, rig.transform, dir * 0.14f + Vector3.up * (front ? 0.012f : -0.004f),
                     new Vector3(0.024f, 0.014f, 0.20f), bodyMat, new Vector3(0, ang, 0), $"Arm{i}");

                Vector3 tip = dir * 0.24f + Vector3.up * (front ? 0.018f : 0.002f);
                Part(PrimitiveType.Cylinder, rig.transform, tip, new Vector3(0.026f, 0.014f, 0.026f), dark, name: "Motor");
                Rotor(rig.transform, tip + Vector3.up * 0.024f, 0.10f, dark, dark, i % 2 == 0 ? 1 : -1);

                // Tiny landing nub under each motor
                Part(PrimitiveType.Cylinder, rig.transform, tip - Vector3.up * 0.03f,
                     new Vector3(0.009f, 0.024f, 0.009f), dark, name: "LandingNub");

                NavLight(rig.transform, tip - Vector3.up * 0.008f, front ? Color.green : Color.red);
            }

            // No payload: it carries a camera, not cargo.
            return root;
        }
    }
}
